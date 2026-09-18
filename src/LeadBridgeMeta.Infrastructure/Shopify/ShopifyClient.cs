using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LeadBridgeMeta.Application.Shopify;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeadBridgeMeta.Infrastructure.Shopify;

public class ShopifyClient : IShopifyClient
{
    private readonly HttpClient _http;
    private readonly ShopifyOptions _options;
    private readonly ILogger<ShopifyClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ShopifyClient(HttpClient http, IOptions<ShopifyOptions> options, ILogger<ShopifyClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public static string NormalizeShopDomain(string shop)
    {
        var cleaned = shop.Trim().ToLowerInvariant()
            .Replace("https://", "")
            .Replace("http://", "")
            .TrimEnd('/');

        if (!cleaned.Contains('.'))
        {
            cleaned += ".myshopify.com";
        }

        return cleaned;
    }

    public string BuildAuthorizeUrl(string shop, string state, string redirectUri)
    {
        var normalizedShop = NormalizeShopDomain(shop);
        var scopes = Uri.EscapeDataString(_options.Scopes);
        var encodedRedirect = Uri.EscapeDataString(redirectUri);
        var encodedState = Uri.EscapeDataString(state);

        return $"https://{normalizedShop}/admin/oauth/authorize?client_id={_options.ClientId}&scope={scopes}&redirect_uri={encodedRedirect}&state={encodedState}";
    }

    public async Task<ShopifyTokenResult> ExchangeCodeForAccessTokenAsync(string shop, string code, string redirectUri, CancellationToken ct = default)
    {
        var normalizedShop = NormalizeShopDomain(shop);
        var url = $"https://{normalizedShop}/admin/oauth/access_token";

        var payload = new
        {
            client_id = _options.ClientId,
            client_secret = _options.ClientSecret,
            code
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Shopify token exchange failed for shop {Shop}: {Status} - {Body}", normalizedShop, response.StatusCode, body);
            throw new InvalidOperationException($"Shopify token exchange failed: {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var token = doc.RootElement.GetProperty("access_token").GetString()
                    ?? throw new InvalidOperationException("Missing access_token in Shopify response.");
        var scope = doc.RootElement.TryGetProperty("scope", out var s) ? s.GetString() ?? "" : "";

        return new ShopifyTokenResult(token, scope);
    }

    public async Task<ShopifyShopDto> GetShopInfoAsync(string shop, string accessToken, CancellationToken ct = default)
    {
        var normalizedShop = NormalizeShopDomain(shop);
        var url = $"https://{normalizedShop}/admin/api/{_options.ApiVersion}/shop.json";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("X-Shopify-Access-Token", accessToken);

        var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to fetch shop info for {Shop}: {Status} - {Body}", normalizedShop, response.StatusCode, body);
            throw new InvalidOperationException($"Failed to fetch shop details from Shopify: {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var shopElem = doc.RootElement.GetProperty("shop");

        var id = shopElem.GetProperty("id").GetInt64();
        var name = shopElem.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        var email = shopElem.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "";
        var domain = shopElem.TryGetProperty("domain", out var d) ? d.GetString() ?? "" : "";
        var myshopifyDomain = shopElem.TryGetProperty("myshopify_domain", out var md) ? md.GetString() ?? normalizedShop : normalizedShop;

        return new ShopifyShopDto(id, name, email, domain, myshopifyDomain);
    }

    public async Task<ShopifyCustomerResult> CreateOrUpdateCustomerAsync(string shop, string accessToken, ShopifyCustomerUpsertRequest customer, CancellationToken ct = default)
    {
        var normalizedShop = NormalizeShopDomain(shop);

        // Check if customer already exists by email
        long? existingCustomerId = null;
        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            existingCustomerId = await FindCustomerByEmailAsync(normalizedShop, accessToken, customer.Email, ct);
        }

        var customerPayload = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(customer.FirstName)) customerPayload["first_name"] = customer.FirstName;
        if (!string.IsNullOrWhiteSpace(customer.LastName)) customerPayload["last_name"] = customer.LastName;
        if (!string.IsNullOrWhiteSpace(customer.Email)) customerPayload["email"] = customer.Email;
        if (!string.IsNullOrWhiteSpace(customer.Phone)) customerPayload["phone"] = customer.Phone;
        if (!string.IsNullOrWhiteSpace(customer.Tags)) customerPayload["tags"] = customer.Tags;
        if (!string.IsNullOrWhiteSpace(customer.Note)) customerPayload["note"] = customer.Note;

        if (customer.Address is { } addr)
        {
            customerPayload["addresses"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["address1"] = addr.Address1,
                    ["city"] = addr.City,
                    ["province"] = addr.Province,
                    ["zip"] = addr.Zip,
                    ["country"] = addr.Country,
                    ["company"] = addr.Company
                }
            };
        }

        if (customer.CustomFields is { Count: > 0 } cfs)
        {
            var metafieldsList = new List<Dictionary<string, object?>>();
            foreach (var (key, val) in cfs)
            {
                if (string.IsNullOrWhiteSpace(val)) continue;

                var ns = "custom";
                var fieldKey = key;
                if (key.Contains('.'))
                {
                    var parts = key.Split('.', 2);
                    ns = parts[0];
                    fieldKey = parts[1];
                }

                metafieldsList.Add(new Dictionary<string, object?>
                {
                    ["namespace"] = ns,
                    ["key"] = fieldKey,
                    ["value"] = val,
                    ["type"] = "single_line_text_field"
                });
            }

            if (metafieldsList.Count > 0)
            {
                customerPayload["metafields"] = metafieldsList;
            }
        }

        var wrapper = new { customer = customerPayload };
        var json = JsonSerializer.Serialize(wrapper, JsonOptions);

        HttpResponseMessage response;
        if (existingCustomerId.HasValue)
        {
            var updateUrl = $"https://{normalizedShop}/admin/api/{_options.ApiVersion}/customers/{existingCustomerId.Value}.json";
            using var request = new HttpRequestMessage(HttpMethod.Put, updateUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Shopify-Access-Token", accessToken);
            response = await _http.SendAsync(request, ct);
        }
        else
        {
            var createUrl = $"https://{normalizedShop}/admin/api/{_options.ApiVersion}/customers.json";
            using var request = new HttpRequestMessage(HttpMethod.Post, createUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Shopify-Access-Token", accessToken);
            response = await _http.SendAsync(request, ct);
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Shopify customer upsert failed on {Shop}: {Status} - {Body}", normalizedShop, response.StatusCode, body);
            throw new InvalidOperationException($"Shopify customer creation/update failed: {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var custElem = doc.RootElement.GetProperty("customer");
        var id = custElem.GetProperty("id").GetInt64();
        var email = custElem.TryGetProperty("email", out var ce) ? ce.GetString() : null;
        var firstName = custElem.TryGetProperty("first_name", out var cf) ? cf.GetString() : null;
        var lastName = custElem.TryGetProperty("last_name", out var cl) ? cl.GetString() : null;

        return new ShopifyCustomerResult(id, email, firstName, lastName);
    }

    public async Task<IReadOnlyList<ShopifyFieldOptionDto>> GetCustomerCustomFieldsAsync(string shop, string accessToken, CancellationToken ct = default)
    {
        var normalizedShop = NormalizeShopDomain(shop);
        var graphqlUrl = $"https://{normalizedShop}/admin/api/{_options.ApiVersion}/graphql.json";

        var query = @"
        query {
          metafieldDefinitions(ownerType: CUSTOMER, first: 100) {
            edges {
              node {
                id
                name
                namespace
                key
                type {
                  name
                }
              }
            }
          }
        }";

        var requestBody = new { query };
        var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, graphqlUrl)
        {
            Content = jsonContent
        };
        request.Headers.Add("X-Shopify-Access-Token", accessToken);

        var list = new List<ShopifyFieldOptionDto>();
        try
        {
            var response = await _http.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Shopify GraphQL metafield definitions query returned {Status} for {Shop}: {Body}", response.StatusCode, normalizedShop, content);
                return list;
            }

            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("errors", out var errors))
            {
                _logger.LogWarning("Shopify GraphQL metafield definitions errors for {Shop}: {Errors}", normalizedShop, errors.ToString());
            }

            if (doc.RootElement.TryGetProperty("data", out var data) &&
                data.TryGetProperty("metafieldDefinitions", out var definitions))
            {
                if (definitions.TryGetProperty("edges", out var edges) && edges.ValueKind == JsonValueKind.Array)
                {
                    foreach (var edge in edges.EnumerateArray())
                    {
                        if (edge.TryGetProperty("node", out var node))
                        {
                            ParseDefinition(node, list);
                        }
                    }
                }
                else if (definitions.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array)
                {
                    foreach (var node in nodes.EnumerateArray())
                    {
                        ParseDefinition(node, list);
                    }
                }
            }

            _logger.LogInformation("Fetched {Count} customer metafield definitions from Shopify store {Shop}", list.Count, normalizedShop);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error fetching Shopify customer metafield definitions for {Shop}", normalizedShop);
        }

        return list;
    }

    private static void ParseDefinition(JsonElement node, List<ShopifyFieldOptionDto> list)
    {
        var name = node.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        var ns = node.TryGetProperty("namespace", out var nsEl) ? nsEl.GetString() ?? "" : "";
        var key = node.TryGetProperty("key", out var k) ? k.GetString() ?? "" : "";
        var typeName = node.TryGetProperty("type", out var t) && t.TryGetProperty("name", out var tn) ? tn.GetString() ?? "Single line text" : "Single line text";

        var fullKey = !string.IsNullOrWhiteSpace(ns) ? $"{ns}.{key}" : key;
        if (!string.IsNullOrWhiteSpace(fullKey))
        {
            list.Add(new ShopifyFieldOptionDto(
                Key: fullKey,
                Label: !string.IsNullOrWhiteSpace(name) ? name : fullKey,
                Type: typeName,
                IsStandard: false,
                Category: "Custom Field"
            ));
        }
    }

    private async Task<long?> FindCustomerByEmailAsync(string normalizedShop, string accessToken, string email, CancellationToken ct)
    {
        try
        {
            var searchUrl = $"https://{normalizedShop}/admin/api/{_options.ApiVersion}/customers/search.json?query=email:{Uri.EscapeDataString(email)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            request.Headers.Add("X-Shopify-Access-Token", accessToken);

            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("customers", out var customers) && customers.GetArrayLength() > 0)
            {
                return customers[0].GetProperty("id").GetInt64();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to search customer by email on Shopify store {Shop}", normalizedShop);
        }

        return null;
    }
}

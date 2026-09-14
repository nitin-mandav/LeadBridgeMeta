using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using LeadBridgeMeta.Application.Ghl;
using Microsoft.Extensions.Options;

namespace LeadBridgeMeta.Infrastructure.Ghl;

public class GhlClient : IGhlClient
{
    private readonly HttpClient _http;
    private readonly GhlOptions _options;

    public GhlClient(HttpClient http, IOptions<GhlOptions> options)
    {
        _http = http;
        _options = options.Value;
        _http.BaseAddress = new Uri(_options.ApiBaseUrl);
        _http.DefaultRequestHeaders.Add("Version", _options.ApiVersion);
    }

    public string BuildAuthorizeUrl(string state, string redirectUri)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["response_type"] = "code";
        qs["client_id"] = _options.ClientId;
        qs["redirect_uri"] = redirectUri;
        qs["state"] = state;
        // Location-level scopes needed to read/write contacts for the installing sub-account.
        qs["scope"] = "contacts.readonly contacts.write locations.readonly";
        return $"https://marketplace.gohighlevel.com/oauth/chooselocation?{qs}";
    }

    public async Task<GhlTokenResult> ExchangeCodeForTokenAsync(string code, string redirectUri, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri
        };
        return await PostTokenRequestAsync(form, ct);
    }

    public async Task<GhlTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        };
        return await PostTokenRequestAsync(form, ct);
    }

    public async Task<GhlContactResult> UpsertContactAsync(string accessToken, GhlContactUpsertRequest request, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "contacts/upsert");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var body = new Dictionary<string, object?>
        {
            ["locationId"] = request.LocationId,
            ["firstName"] = request.FirstName,
            ["lastName"] = request.LastName,
            ["email"] = request.Email,
            ["phone"] = request.Phone,
            ["source"] = request.SourceLabel,
            ["tags"] = request.Tags,
        };

        if (request.CustomFields is { Count: > 0 })
        {
            body["customFields"] = request.CustomFields
                .Select(kv => new { id = kv.Key, field_value = kv.Value })
                .ToList();
        }

        message.Content = JsonContent.Create(body, options: JsonOpts);

        using var response = await _http.SendAsync(message, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new GhlApiException($"GHL contact upsert failed ({(int)response.StatusCode}): {responseBody}");

        var parsed = JsonSerializer.Deserialize<UpsertContactResponse>(responseBody, JsonOpts)
                     ?? throw new GhlApiException("GHL returned an empty/invalid contact upsert response.");

        return new GhlContactResult(parsed.Contact.Id);
    }

    private async Task<GhlTokenResult> PostTokenRequestAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var response = await _http.PostAsync("oauth/token", new FormUrlEncodedContent(form), ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new GhlApiException($"GHL OAuth token request failed ({(int)response.StatusCode}): {body}");

        var token = JsonSerializer.Deserialize<TokenResponse>(body, JsonOpts)
                    ?? throw new GhlApiException("GHL returned an empty/invalid token response.");

        return new GhlTokenResult(
            token.AccessToken,
            token.RefreshToken,
            DateTime.UtcNow.AddSeconds(token.ExpiresIn),
            token.LocationId ?? string.Empty,
            token.CompanyId ?? string.Empty);
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] long ExpiresIn,
        [property: JsonPropertyName("locationId")] string? LocationId,
        [property: JsonPropertyName("companyId")] string? CompanyId);

    private record UpsertContactResponse(ContactResponse Contact);

    private record ContactResponse(string Id);
}

public class GhlApiException(string message) : Exception(message);

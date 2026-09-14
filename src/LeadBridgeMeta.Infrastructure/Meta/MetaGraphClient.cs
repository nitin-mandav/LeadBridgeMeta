using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using LeadBridgeMeta.Application.Meta;
using Microsoft.Extensions.Options;

namespace LeadBridgeMeta.Infrastructure.Meta;

public class MetaGraphClient : IMetaGraphClient
{
    private static readonly string[] RequiredScopes =
    [
        "pages_show_list",
        "pages_manage_metadata",
        "pages_read_engagement",
        "leads_retrieval",
        "business_management"
    ];

    private readonly HttpClient _http;
    private readonly MetaOptions _options;

    public MetaGraphClient(HttpClient http, IOptions<MetaOptions> options)
    {
        _http = http;
        _options = options.Value;
        _http.BaseAddress = new Uri($"https://graph.facebook.com/{_options.GraphApiVersion}/");
    }

    public string BuildLoginDialogUrl(string state, string redirectUri)
    {
        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["client_id"] = _options.AppId;
        qs["redirect_uri"] = redirectUri;
        qs["state"] = state;
        qs["scope"] = string.Join(',', RequiredScopes);
        qs["response_type"] = "code";
        return $"https://www.facebook.com/{_options.GraphApiVersion}/dialog/oauth?{qs}";
    }

    public async Task<MetaTokenResult> ExchangeCodeForUserTokenAsync(string code, string redirectUri, CancellationToken ct = default)
    {
        var url = $"oauth/access_token?client_id={_options.AppId}&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                  $"&client_secret={_options.AppSecret}&code={Uri.EscapeDataString(code)}";
        var response = await GetAsync<TokenResponse>(url, ct);
        var expiresAt = response.ExpiresIn.HasValue
            ? DateTime.UtcNow.AddSeconds(response.ExpiresIn.Value)
            : DateTime.UtcNow.AddHours(1);
        return new MetaTokenResult(response.AccessToken, expiresAt);
    }

    public async Task<MetaTokenResult> GetLongLivedUserTokenAsync(string shortLivedToken, CancellationToken ct = default)
    {
        var url = $"oauth/access_token?grant_type=fb_exchange_token&client_id={_options.AppId}" +
                  $"&client_secret={_options.AppSecret}&fb_exchange_token={Uri.EscapeDataString(shortLivedToken)}";
        var response = await GetAsync<TokenResponse>(url, ct);
        var expiresAt = DateTime.UtcNow.AddSeconds(response.ExpiresIn ?? 60 * 24 * 60 * 60);
        return new MetaTokenResult(response.AccessToken, expiresAt);
    }

    public async Task<(string FacebookUserId, string Name)> GetMeAsync(string userAccessToken, CancellationToken ct = default)
    {
        var me = await GetAsync<MeResponse>($"me?fields=id,name&access_token={Uri.EscapeDataString(userAccessToken)}", ct);
        return (me.Id, me.Name);
    }

    public async Task<IReadOnlyList<MetaPageDto>> GetManagedPagesAsync(string userAccessToken, CancellationToken ct = default)
    {
        var result = new List<MetaPageDto>();
        var url = $"me/accounts?fields=id,name,access_token&limit=100&access_token={Uri.EscapeDataString(userAccessToken)}";

        while (!string.IsNullOrEmpty(url))
        {
            var page = await GetAsync<PagedResponse<PageResponse>>(url, ct, absoluteUrl: url.StartsWith("http"));
            result.AddRange(page.Data.Select(p => new MetaPageDto(p.Id, p.Name, p.AccessToken)));
            url = page.Paging?.Next ?? string.Empty;
        }

        return result;
    }

    public async Task<IReadOnlyList<MetaLeadFormDto>> GetLeadFormsAsync(string pageId, string pageAccessToken, CancellationToken ct = default)
    {
        var result = new List<MetaLeadFormDto>();
        var url = $"{pageId}/leadgen_forms?fields=id,name,status&limit=100&access_token={Uri.EscapeDataString(pageAccessToken)}";

        while (!string.IsNullOrEmpty(url))
        {
            var page = await GetAsync<PagedResponse<LeadFormResponse>>(url, ct, absoluteUrl: url.StartsWith("http"));
            result.AddRange(page.Data.Select(f => new MetaLeadFormDto(f.Id, f.Name)));
            url = page.Paging?.Next ?? string.Empty;
        }

        return result;
    }

    public async Task SubscribePageToLeadgenAsync(string pageId, string pageAccessToken, CancellationToken ct = default)
    {
        var url = $"{pageId}/subscribed_apps?subscribed_fields=leadgen&access_token={Uri.EscapeDataString(pageAccessToken)}";
        using var response = await _http.PostAsync(url, content: null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<MetaLeadDataDto> GetLeadDataAsync(string leadgenId, string pageAccessToken, CancellationToken ct = default)
    {
        var url = $"{leadgenId}?fields=id,form_id,created_time,field_data,ad_id&access_token={Uri.EscapeDataString(pageAccessToken)}";
        var lead = await GetAsync<LeadResponse>(url, ct);

        return new MetaLeadDataDto(
            lead.Id,
            lead.FormId,
            PageId: string.Empty,
            lead.CreatedTime,
            lead.FieldData.Select(f => new MetaLeadFieldData(f.Name, f.Values)).ToList());
    }

    private async Task<T> GetAsync<T>(string urlOrPath, CancellationToken ct, bool absoluteUrl = false)
    {
        using var response = absoluteUrl
            ? await _http.GetAsync(urlOrPath, ct)
            : await _http.GetAsync(urlOrPath, ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new MetaGraphApiException($"Meta Graph API call failed ({(int)response.StatusCode}): {body}");

        return JsonSerializer.Deserialize<T>(body, JsonOpts)
               ?? throw new MetaGraphApiException("Meta Graph API returned an empty/invalid response.");
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] long? ExpiresIn);

    private record MeResponse(string Id, string Name);

    private record PageResponse(string Id, string Name, [property: JsonPropertyName("access_token")] string AccessToken);

    private record LeadFormResponse(string Id, string Name, string? Status);

    private record LeadFieldDataResponse(string Name, List<string> Values);

    private record LeadResponse(
        string Id,
        [property: JsonPropertyName("form_id")] string FormId,
        [property: JsonPropertyName("created_time")] DateTime CreatedTime,
        [property: JsonPropertyName("field_data")] List<LeadFieldDataResponse> FieldData);

    private record PagingResponse([property: JsonPropertyName("next")] string? Next);

    private record PagedResponse<T>(List<T> Data, PagingResponse? Paging);
}

public class MetaGraphApiException(string message) : Exception(message);

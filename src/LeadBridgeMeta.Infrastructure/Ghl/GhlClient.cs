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
        _http.BaseAddress = new Uri(_options.ApiBaseUrl.TrimEnd('/') + "/");
        _http.DefaultRequestHeaders.Add("Version", _options.ApiVersion);
    }

    public string BuildAuthorizeUrl(string state, string redirectUri)
    {
        if (!string.IsNullOrWhiteSpace(_options.DirectInstallUrl))
        {
            return _options.DirectInstallUrl;
        }

        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["response_type"] = "code";
        qs["client_id"] = _options.ClientId;
        qs["redirect_uri"] = redirectUri;
        qs["state"] = state;
        // Scopes needed to read/write contacts and custom fields for the installing sub-account.
        qs["scope"] = string.IsNullOrWhiteSpace(_options.Scopes)
            ? "contacts.readonly contacts.write locations.readonly locations/customFields.readonly"
            : _options.Scopes;

        var baseUrl = string.IsNullOrWhiteSpace(_options.MarketplaceBaseUrl)
            ? "https://marketplace.gohighlevel.com"
            : _options.MarketplaceBaseUrl.TrimEnd('/');
        return $"{baseUrl}/oauth/chooselocation?{qs}";
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
            ["name"] = request.Name,
            ["email"] = request.Email,
            ["phone"] = request.Phone,
            ["companyName"] = request.CompanyName,
            ["address1"] = request.Address1,
            ["city"] = request.City,
            ["state"] = request.State,
            ["postalCode"] = request.PostalCode,
            ["country"] = request.Country,
            ["website"] = request.Website,
            ["dateOfBirth"] = request.DateOfBirth,
            ["source"] = request.SourceLabel,
            ["tags"] = request.Tags,
        };

        if (request.CustomFields is { Count: > 0 })
        {
            IReadOnlyList<GhlCustomFieldDto>? locationFields = null;
            try
            {
                locationFields = await GetCustomFieldsAsync(accessToken, request.LocationId, ct);
            }
            catch
            {
                // Fallback: proceed with raw keys if lookup fails
            }

            var customFieldList = new List<object>();
            foreach (var kv in request.CustomFields)
            {
                var cleanKey = kv.Key.Trim('{', '}').Trim();
                var matched = locationFields?.FirstOrDefault(cf =>
                    string.Equals(cf.Id, kv.Key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cf.FieldKey, cleanKey, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cf.Name, kv.Key, StringComparison.OrdinalIgnoreCase));

                var idToSend = matched?.Id ?? kv.Key;
                var keyToSend = matched?.FieldKey ?? cleanKey;

                customFieldList.Add(new
                {
                    id = idToSend,
                    key = keyToSend,
                    field_value = kv.Value
                });
            }

            body["customFields"] = customFieldList;
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

    public async Task<IReadOnlyList<GhlCustomFieldDto>> GetCustomFieldsAsync(string accessToken, string locationId, CancellationToken ct = default)
    {
        var allFields = new List<GhlCustomFieldDto>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Fetch contact and opportunity custom fields directly from GHL
        foreach (var model in new[] { "contact", "opportunity", "all" })
        {
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Get, $"locations/{locationId}/customFields?model={model}");
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                using var response = await _http.SendAsync(message, ct);
                if (!response.IsSuccessStatusCode) continue;

                var responseBody = await response.Content.ReadAsStringAsync(ct);
                var parsed = JsonSerializer.Deserialize<CustomFieldsResponse>(responseBody, JsonOpts);
                if (parsed?.CustomFields is null || parsed.CustomFields.Count == 0) continue;

                foreach (var f in parsed.CustomFields)
                {
                    if (seenIds.Add(f.Id))
                    {
                        var inferredModel = !string.IsNullOrWhiteSpace(f.Model)
                            ? f.Model
                            : (string.Equals(model, "opportunity", StringComparison.OrdinalIgnoreCase) ? "opportunity" : "contact");

                        allFields.Add(new GhlCustomFieldDto(f.Id, f.Name, f.FieldKey, f.DataType, inferredModel));
                    }
                }
            }
            catch
            {
                // Continue to next model
            }
        }

        return allFields;
    }

    public async Task<IReadOnlyList<GhlCustomValueDto>> GetCustomValuesAsync(string accessToken, string locationId, CancellationToken ct = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, $"locations/{locationId}/customValues");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _http.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return [];

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<CustomValuesResponse>(responseBody, JsonOpts);
            if (parsed?.CustomValues is null || parsed.CustomValues.Count == 0) return [];

            return parsed.CustomValues
                .Select(v => new GhlCustomValueDto(v.Id, v.Name, v.FieldKey, v.Value))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<GhlPipelineDto>> GetPipelinesAsync(string accessToken, string locationId, CancellationToken ct = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, $"opportunities/pipelines?locationId={locationId}");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _http.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return [];

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<PipelinesResponse>(responseBody, JsonOpts);
            if (parsed?.Pipelines is null || parsed.Pipelines.Count == 0) return [];

            return parsed.Pipelines
                .Select(p => new GhlPipelineDto(
                    p.Id,
                    p.Name,
                    p.Stages?.Select(s => new GhlPipelineStageDto(s.Id, s.Name)).ToList() ?? []))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<GhlCalendarDto>> GetCalendarsAsync(string accessToken, string locationId, CancellationToken ct = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, $"calendars/?locationId={locationId}");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _http.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return [];

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<CalendarsResponse>(responseBody, JsonOpts);
            if (parsed?.Calendars is null || parsed.Calendars.Count == 0) return [];

            return parsed.Calendars
                .Select(c => new GhlCalendarDto(c.Id, c.Name))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<GhlUserDto>> GetUsersAsync(string accessToken, string locationId, CancellationToken ct = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, $"users/?locationId={locationId}");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _http.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return [];

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<UsersResponse>(responseBody, JsonOpts);
            if (parsed?.Users is null || parsed.Users.Count == 0) return [];

            return parsed.Users
                .Select(u => new GhlUserDto(u.Id, !string.IsNullOrWhiteSpace(u.Name) ? u.Name : $"{u.FirstName} {u.LastName}".Trim(), u.Email))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<GhlTagDto>> GetTagsAsync(string accessToken, string locationId, CancellationToken ct = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, $"locations/{locationId}/tags");
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _http.SendAsync(message, ct);
            if (!response.IsSuccessStatusCode) return [];

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<TagsResponse>(responseBody, JsonOpts);
            if (parsed?.Tags is null || parsed.Tags.Count == 0) return [];

            return parsed.Tags
                .Select(t => new GhlTagDto(t.Id, t.Name))
                .ToList();
        }
        catch
        {
            return [];
        }
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

    private record CustomFieldItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("fieldKey")] string? FieldKey,
        [property: JsonPropertyName("dataType")] string? DataType,
        [property: JsonPropertyName("model")] string? Model);

    private record CustomFieldsResponse(
        [property: JsonPropertyName("customFields")] List<CustomFieldItem>? CustomFields);

    private record CustomValueItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("fieldKey")] string? FieldKey,
        [property: JsonPropertyName("value")] string? Value);

    private record CustomValuesResponse(
        [property: JsonPropertyName("customValues")] List<CustomValueItem>? CustomValues);

    private record PipelineStageItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name);

    private record PipelineItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("stages")] List<PipelineStageItem>? Stages);

    private record PipelinesResponse(
        [property: JsonPropertyName("pipelines")] List<PipelineItem>? Pipelines);

    private record CalendarItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name);

    private record CalendarsResponse(
        [property: JsonPropertyName("calendars")] List<CalendarItem>? Calendars);

    private record UserItem(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("firstName")] string? FirstName,
        [property: JsonPropertyName("lastName")] string? LastName,
        [property: JsonPropertyName("email")] string? Email);

    private record UsersResponse(
        [property: JsonPropertyName("users")] List<UserItem>? Users);

    private record TagItem(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string Name);

    private record TagsResponse(
        [property: JsonPropertyName("tags")] List<TagItem>? Tags);
}

public class GhlApiException(string message) : Exception(message);

namespace LeadBridgeMeta.Application.Ghl;

public record GhlTokenResult(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    string LocationId,
    string CompanyId);

public record GhlContactUpsertRequest(
    string LocationId,
    string? FirstName = null,
    string? LastName = null,
    string? Name = null,
    string? Email = null,
    string? Phone = null,
    string? CompanyName = null,
    string? Address1 = null,
    string? City = null,
    string? State = null,
    string? PostalCode = null,
    string? Country = null,
    string? Website = null,
    string? DateOfBirth = null,
    IReadOnlyDictionary<string, string>? CustomFields = null,
    IReadOnlyList<string>? Tags = null,
    string SourceLabel = "Meta Lead Ads");

public record GhlContactResult(string ContactId);

public record GhlCustomFieldDto(string Id, string Name, string? FieldKey, string? DataType, string? Model = null);

public record GhlCustomValueDto(string Id, string Name, string? FieldKey, string? Value = null);

public record GhlPipelineStageDto(string Id, string Name);

public record GhlPipelineDto(string Id, string Name, IReadOnlyList<GhlPipelineStageDto> Stages);

public record GhlCalendarDto(string Id, string Name);

public record GhlUserDto(string Id, string Name, string? Email);

public record GhlTagDto(string? Id, string Name);

/// <summary>Thin wrapper over the GoHighLevel Marketplace OAuth + Contacts APIs.</summary>
public interface IGhlClient
{
    string BuildAuthorizeUrl(string state, string redirectUri);

    Task<GhlTokenResult> ExchangeCodeForTokenAsync(string code, string redirectUri, CancellationToken ct = default);

    Task<GhlTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Creates the contact if it doesn't exist for that email/phone in the location, otherwise updates it (GHL upserts by duplicate check).</summary>
    Task<GhlContactResult> UpsertContactAsync(string accessToken, GhlContactUpsertRequest request, CancellationToken ct = default);

    /// <summary>Retrieves custom fields configured for the specified GHL location.</summary>
    Task<IReadOnlyList<GhlCustomFieldDto>> GetCustomFieldsAsync(string accessToken, string locationId, CancellationToken ct = default);

    /// <summary>Retrieves custom values configured for the specified GHL location.</summary>
    Task<IReadOnlyList<GhlCustomValueDto>> GetCustomValuesAsync(string accessToken, string locationId, CancellationToken ct = default);

    /// <summary>Retrieves pipelines and their stages configured for the specified GHL location.</summary>
    Task<IReadOnlyList<GhlPipelineDto>> GetPipelinesAsync(string accessToken, string locationId, CancellationToken ct = default);

    /// <summary>Retrieves calendars configured for the specified GHL location.</summary>
    Task<IReadOnlyList<GhlCalendarDto>> GetCalendarsAsync(string accessToken, string locationId, CancellationToken ct = default);

    /// <summary>Retrieves users/team members configured for the specified GHL location.</summary>
    Task<IReadOnlyList<GhlUserDto>> GetUsersAsync(string accessToken, string locationId, CancellationToken ct = default);

    /// <summary>Retrieves tags configured for the specified GHL location.</summary>
    Task<IReadOnlyList<GhlTagDto>> GetTagsAsync(string accessToken, string locationId, CancellationToken ct = default);
}

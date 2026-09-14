namespace LeadBridgeMeta.Application.Ghl;

public record GhlTokenResult(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    string LocationId,
    string CompanyId);

public record GhlContactUpsertRequest(
    string LocationId,
    string? FirstName,
    string? LastName,
    string? Email,
    string? Phone,
    IReadOnlyDictionary<string, string>? CustomFields,
    IReadOnlyList<string>? Tags,
    string SourceLabel);

public record GhlContactResult(string ContactId);

/// <summary>Thin wrapper over the GoHighLevel Marketplace OAuth + Contacts APIs.</summary>
public interface IGhlClient
{
    string BuildAuthorizeUrl(string state, string redirectUri);

    Task<GhlTokenResult> ExchangeCodeForTokenAsync(string code, string redirectUri, CancellationToken ct = default);

    Task<GhlTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Creates the contact if it doesn't exist for that email/phone in the location, otherwise updates it (GHL upserts by duplicate check).</summary>
    Task<GhlContactResult> UpsertContactAsync(string accessToken, GhlContactUpsertRequest request, CancellationToken ct = default);
}

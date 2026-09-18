namespace LeadBridgeMeta.Application.Ghl;

public record GhlConnectionDto(Guid Id, string LocationId, string LocationName, DateTime AccessTokenExpiresAtUtc);

public record GhlOAuthCallbackResult(bool Success, string? LocationId, string? ErrorMessage);

public record GhlFieldOptionDto(string Key, string Label, string? DataType, bool IsStandard, string Category = "Contact");

public interface IGhlConnectionService
{
    string GetConnectUrl(Guid tenantId, string callbackRedirectUri);

    Task<GhlOAuthCallbackResult> ProcessOAuthCallbackAsync(string code, string? state, string callbackRedirectUri, CancellationToken ct = default);

    Task<List<GhlConnectionDto>> GetConnectionsAsync(Guid tenantId, CancellationToken ct = default);

    Task<List<GhlFieldOptionDto>> GetLocationFieldsAsync(Guid connectionId, Guid tenantId, CancellationToken ct = default);
    Task<bool> DisconnectAsync(Guid connectionId, Guid tenantId, CancellationToken ct = default);
}

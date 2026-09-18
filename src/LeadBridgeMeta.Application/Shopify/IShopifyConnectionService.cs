namespace LeadBridgeMeta.Application.Shopify;

public record ShopifyConnectionDto(Guid Id, string ShopDomain, string ShopName, DateTime CreatedAtUtc);

public record ShopifyOAuthResult(bool Success, string? ShopDomain = null, string? ErrorMessage = null);

public record ShopifyFieldOptionDto(string Key, string Label, string Type, bool IsStandard = true, string Category = "Standard");

public interface IShopifyConnectionService
{
    string GetConnectUrl(string shop, Guid tenantId, string redirectUri);

    Task<ShopifyOAuthResult> ProcessOAuthCallbackAsync(string code, string shop, string? state, string redirectUri, CancellationToken ct = default);

    Task<List<ShopifyConnectionDto>> GetConnectionsAsync(Guid tenantId, CancellationToken ct = default);

    Task<bool> DisconnectAsync(Guid connectionId, Guid tenantId, CancellationToken ct = default);

    Task<List<ShopifyFieldOptionDto>> GetCustomerFieldsAsync(Guid? connectionId = null, Guid? tenantId = null, CancellationToken ct = default);

    List<ShopifyFieldOptionDto> GetCustomerFields();
}

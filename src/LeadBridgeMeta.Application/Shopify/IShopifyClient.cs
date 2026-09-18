namespace LeadBridgeMeta.Application.Shopify;

public record ShopifyTokenResult(string AccessToken, string Scope);

public record ShopifyShopDto(long Id, string Name, string Email, string Domain, string MyshopifyDomain);

public record ShopifyAddressDto(
    string? Address1 = null,
    string? City = null,
    string? Province = null,
    string? Zip = null,
    string? Country = null,
    string? Company = null);

public record ShopifyCustomerUpsertRequest(
    string? Email = null,
    string? FirstName = null,
    string? LastName = null,
    string? Phone = null,
    string? Tags = null,
    string? Note = null,
    ShopifyAddressDto? Address = null,
    Dictionary<string, string>? CustomFields = null);

public record ShopifyCustomerResult(long Id, string? Email, string? FirstName, string? LastName);

public interface IShopifyClient
{
    string BuildAuthorizeUrl(string shop, string state, string redirectUri);

    Task<ShopifyTokenResult> ExchangeCodeForAccessTokenAsync(string shop, string code, string redirectUri, CancellationToken ct = default);

    Task<ShopifyShopDto> GetShopInfoAsync(string shop, string accessToken, CancellationToken ct = default);

    Task<ShopifyCustomerResult> CreateOrUpdateCustomerAsync(string shop, string accessToken, ShopifyCustomerUpsertRequest customer, CancellationToken ct = default);

    Task<IReadOnlyList<ShopifyFieldOptionDto>> GetCustomerCustomFieldsAsync(string shop, string accessToken, CancellationToken ct = default);
}

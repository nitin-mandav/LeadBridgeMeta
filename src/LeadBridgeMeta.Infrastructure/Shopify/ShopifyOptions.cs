namespace LeadBridgeMeta.Infrastructure.Shopify;

public class ShopifyOptions
{
    public const string SectionName = "Shopify";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scopes { get; set; } = "read_customers,write_customers,read_orders";
    public string ApiVersion { get; set; } = "2024-01";
    public string? RedirectUri { get; set; }
}

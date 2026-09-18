namespace LeadBridgeMeta.Domain.Entities;

/// <summary>A Shopify store authorized via Shopify OAuth app.</summary>
public class ShopifyConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>The shop domain, e.g. "my-brand.myshopify.com".</summary>
    public string ShopDomain { get; set; } = string.Empty;

    public string ShopName { get; set; } = string.Empty;

    public string EncryptedAccessToken { get; set; } = string.Empty;

    public string Scopes { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<MetaLeadForm> MappedForms { get; set; } = new List<MetaLeadForm>();
}

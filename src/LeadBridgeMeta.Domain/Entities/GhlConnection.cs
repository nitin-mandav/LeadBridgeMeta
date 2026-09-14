namespace LeadBridgeMeta.Domain.Entities;

/// <summary>A GoHighLevel sub-account (Location) authorized via the GHL Marketplace OAuth app.</summary>
public class GhlConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string LocationId { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string CompanyId { get; set; } = string.Empty;

    public string EncryptedAccessToken { get; set; } = string.Empty;
    public string EncryptedRefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<MetaLeadForm> MappedForms { get; set; } = new List<MetaLeadForm>();
}

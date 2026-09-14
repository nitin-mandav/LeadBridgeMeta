namespace LeadBridgeMeta.Domain.Entities;

/// <summary>A Facebook user's OAuth grant to this app, on behalf of a Tenant. One tenant may have more than one (e.g. multiple ad managers).</summary>
public class MetaConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public string FacebookUserId { get; set; } = string.Empty;
    public string FacebookUserName { get; set; } = string.Empty;

    /// <summary>Long-lived user access token, encrypted at rest via IDataProtector.</summary>
    public string EncryptedUserAccessToken { get; set; } = string.Empty;
    public DateTime TokenExpiresAtUtc { get; set; }
    public string GrantedScopes { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<MetaPage> Pages { get; set; } = new List<MetaPage>();
}

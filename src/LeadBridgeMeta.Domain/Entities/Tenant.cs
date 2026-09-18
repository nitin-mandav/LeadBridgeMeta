namespace LeadBridgeMeta.Domain.Entities;

public class Tenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<MetaConnection> MetaConnections { get; set; } = new List<MetaConnection>();
    public ICollection<GhlConnection> GhlConnections { get; set; } = new List<GhlConnection>();
    public ICollection<ShopifyConnection> ShopifyConnections { get; set; } = new List<ShopifyConnection>();
    public ICollection<FieldMapping> FieldMappings { get; set; } = new List<FieldMapping>();
    public ICollection<ShopifyFieldMapping> ShopifyFieldMappings { get; set; } = new List<ShopifyFieldMapping>();
}

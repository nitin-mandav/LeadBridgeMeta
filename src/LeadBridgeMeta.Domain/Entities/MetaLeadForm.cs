namespace LeadBridgeMeta.Domain.Entities;

public class MetaLeadForm
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MetaPageId { get; set; }
    public MetaPage? MetaPage { get; set; }

    public string FormId { get; set; } = string.Empty;
    public string FormName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    /// <summary>Which GHL location new leads from this form should be sent to. Null until mapped.</summary>
    public Guid? GhlConnectionId { get; set; }
    public GhlConnection? GhlConnection { get; set; }

    /// <summary>Which Shopify store new leads from this form should be sent to. Null until mapped.</summary>
    public Guid? ShopifyConnectionId { get; set; }
    public ShopifyConnection? ShopifyConnection { get; set; }

    public ICollection<FieldMapping> FieldMappings { get; set; } = new List<FieldMapping>();
    public ICollection<ShopifyFieldMapping> ShopifyFieldMappings { get; set; } = new List<ShopifyFieldMapping>();
    public ICollection<LeadEvent> LeadEvents { get; set; } = new List<LeadEvent>();
}

namespace LeadBridgeMeta.Domain.Entities;

public enum ShopifyTargetFieldType
{
    StandardCustomerField = 0,
    Tag = 1,
    Note = 2,
    CustomField = 3
}

/// <summary>Maps one Meta lead form question key to a Shopify customer field.</summary>
public class ShopifyFieldMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid? MetaLeadFormId { get; set; }
    public MetaLeadForm? MetaLeadForm { get; set; }

    /// <summary>The Meta lead question key, e.g. "email", "full_name", "phone_number".</summary>
    public string MetaFieldKey { get; set; } = string.Empty;

    public ShopifyTargetFieldType TargetType { get; set; }

    /// <summary>Standard customer attribute name (e.g. "firstName", "lastName", "email", "phone", "tags", "note").</summary>
    public string ShopifyFieldKey { get; set; } = string.Empty;
}

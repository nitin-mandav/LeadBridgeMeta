namespace LeadBridgeMeta.Domain.Entities;

public enum GhlTargetFieldType
{
    StandardContactField = 0,
    CustomField = 1,
    Tag = 2
}

/// <summary>Maps one Meta lead form question key to a GHL contact field. Scoped to a specific form when MetaLeadFormId is set, otherwise acts as the tenant's default mapping for that Meta field key.</summary>
public class FieldMapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public Guid? MetaLeadFormId { get; set; }
    public MetaLeadForm? MetaLeadForm { get; set; }

    /// <summary>The Meta lead question key, e.g. "full_name", "email", "phone_number", or a custom question key.</summary>
    public string MetaFieldKey { get; set; } = string.Empty;

    public GhlTargetFieldType TargetType { get; set; }

    /// <summary>Standard field name (e.g. "firstName", "email", "phone") or GHL custom field id, depending on TargetType.</summary>
    public string GhlFieldKey { get; set; } = string.Empty;
}

namespace LeadBridgeMeta.Domain.Entities;

/// <summary>An email address connected to receive automated lead notifications.</summary>
public class EmailConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>The recipient email address (e.g. office@mandavconsultancy.com).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Whether leads are currently being dispatched to this email.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

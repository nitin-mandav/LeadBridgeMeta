namespace LeadBridgeMeta.Domain.Entities;

public enum LeadEventStatus
{
    Received = 0,
    Fetched = 1,
    Sent = 2,
    Failed = 3,
    Skipped = 4
}

/// <summary>One Meta leadgen webhook notification and its processing outcome. Kept as an audit trail and for retrying failures.</summary>
public class LeadEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }

    public Guid MetaLeadFormId { get; set; }
    public MetaLeadForm? MetaLeadForm { get; set; }

    public string LeadgenId { get; set; } = string.Empty;
    public string? RawWebhookPayload { get; set; }
    public string? RawLeadDataJson { get; set; }

    public LeadEventStatus Status { get; set; } = LeadEventStatus.Received;
    public string? GhlContactId { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }

    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
}

using LeadBridgeMeta.Domain.Entities;

namespace LeadBridgeMeta.Application.Leads;

public record LeadEventDto(
    Guid Id,
    string LeadgenId,
    string FormName,
    LeadEventStatus Status,
    string? GhlContactId,
    string? ShopifyCustomerId,
    string? ErrorMessage,
    int RetryCount,
    DateTime ReceivedAtUtc,
    DateTime? ProcessedAtUtc,
    string? RawLeadDataJson = null);

/// <summary>Handles one Meta leadgen webhook notification end-to-end: fetch the lead's answers from the Graph API,
/// apply the tenant's field mappings, and push the resulting contact into the mapped GHL location.</summary>
public interface ILeadProcessingService
{
    Task ProcessLeadgenNotificationAsync(string pageId, string formId, string leadgenId, string rawWebhookPayload, CancellationToken ct = default);

    /// <summary>Re-runs processing for a LeadEvent that previously failed (e.g. after fixing a mapping or a token).</summary>
    Task RetryAsync(Guid leadEventId, CancellationToken ct = default);

    /// <summary>Re-runs processing for a LeadEvent after verifying tenant ownership.</summary>
    Task<bool> RetryAsync(Guid leadEventId, Guid tenantId, CancellationToken ct = default);

    /// <summary>Retrieves paginated lead events for a tenant with optional status filter.</summary>
    Task<List<LeadEventDto>> GetLeadsAsync(Guid tenantId, LeadEventStatus? status, int page = 1, int pageSize = 50, CancellationToken ct = default);

    /// <summary>Retrieves a single lead event by ID and tenant, fetching missing lead details from Meta if necessary.</summary>
    Task<LeadEventDto?> GetLeadByIdAsync(Guid id, Guid tenantId, CancellationToken ct = default);
}

namespace LeadBridgeMeta.Application.Leads;

/// <summary>Handles one Meta leadgen webhook notification end-to-end: fetch the lead's answers from the Graph API,
/// apply the tenant's field mappings, and push the resulting contact into the mapped GHL location.</summary>
public interface ILeadProcessingService
{
    Task ProcessLeadgenNotificationAsync(string pageId, string formId, string leadgenId, string rawWebhookPayload, CancellationToken ct = default);

    /// <summary>Re-runs processing for a LeadEvent that previously failed (e.g. after fixing a mapping or a token).</summary>
    Task RetryAsync(Guid leadEventId, CancellationToken ct = default);
}

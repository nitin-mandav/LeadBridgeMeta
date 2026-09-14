namespace LeadBridgeMeta.Domain.Entities;

/// <summary>A Facebook Page connected via a MetaConnection, with its own Page access token used for leadgen webhook subscription and Graph API reads.</summary>
public class MetaPage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MetaConnectionId { get; set; }
    public MetaConnection? MetaConnection { get; set; }

    public string PageId { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;

    /// <summary>Page access token, encrypted at rest. Does not expire while the user token/grant remains valid.</summary>
    public string EncryptedPageAccessToken { get; set; } = string.Empty;

    public bool IsLeadgenWebhookSubscribed { get; set; }
    public DateTime? SubscribedAtUtc { get; set; }

    public ICollection<MetaLeadForm> LeadForms { get; set; } = new List<MetaLeadForm>();
}

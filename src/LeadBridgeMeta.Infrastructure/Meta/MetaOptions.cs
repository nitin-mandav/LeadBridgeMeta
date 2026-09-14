namespace LeadBridgeMeta.Infrastructure.Meta;

public class MetaOptions
{
    public const string SectionName = "Meta";

    public string AppId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;

    /// <summary>Verify token you choose and enter in the Meta App's Webhooks product config; used to validate the GET challenge.</summary>
    public string WebhookVerifyToken { get; set; } = string.Empty;

    public string GraphApiVersion { get; set; } = "v21.0";
}

namespace LeadBridgeMeta.Application.Meta;

public interface IMetaWebhookService
{
    /// <summary>Validates the webhook verification token sent by Meta App Dashboard.</summary>
    bool VerifyToken(string mode, string verifyToken);

    /// <summary>Validates HMAC-SHA256 signature, parses leadgen notifications, and enqueues them for background processing.</summary>
    Task<bool> ProcessWebhookPayloadAsync(byte[] rawBytes, string? signatureHeader, CancellationToken ct = default);
}

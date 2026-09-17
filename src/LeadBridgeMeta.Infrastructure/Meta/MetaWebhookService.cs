using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeadBridgeMeta.Application.Meta;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LeadBridgeMeta.Infrastructure.Meta;

public class MetaWebhookService : IMetaWebhookService
{
    private readonly MetaOptions _options;
    private readonly IMetaWebhookQueue _queue;
    private readonly ILogger<MetaWebhookService> _logger;

    public MetaWebhookService(
        IOptions<MetaOptions> options,
        IMetaWebhookQueue queue,
        ILogger<MetaWebhookService> logger)
    {
        _options = options.Value;
        _queue = queue;
        _logger = logger;
    }

    public bool VerifyToken(string mode, string verifyToken)
    {
        _logger.LogInformation("Meta webhook verification attempt: mode={Mode}, token={Token}", mode, verifyToken);

        if (mode == "subscribe" && verifyToken == _options.WebhookVerifyToken)
        {
            _logger.LogInformation("Meta webhook verification succeeded.");
            return true;
        }

        _logger.LogWarning("Meta webhook verification failed. Expected verify token '{Expected}', but received '{Received}'",
            _options.WebhookVerifyToken, verifyToken);
        return false;
    }

    public async Task<bool> ProcessWebhookPayloadAsync(byte[] rawBytes, string? signatureHeader, CancellationToken ct = default)
    {
        var rawBody = Encoding.UTF8.GetString(rawBytes);
        _logger.LogInformation("Received Meta webhook ({Bytes} bytes): {Payload}", rawBytes.Length, rawBody);

        if (!IsValidSignature(rawBytes, signatureHeader))
        {
            _logger.LogWarning("Rejected Meta webhook call with an invalid X-Hub-Signature-256.");
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBytes);
            if (!doc.RootElement.TryGetProperty("entry", out var entries))
            {
                _logger.LogInformation("Meta webhook payload has no 'entry' property.");
                return true;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                var entryId = GetStringOrNumber(entry, "id");
                if (!entry.TryGetProperty("changes", out var changes)) continue;

                foreach (var change in changes.EnumerateArray())
                {
                    var field = change.TryGetProperty("field", out var f) ? f.GetString() : null;
                    if (field != "leadgen") continue;

                    if (!change.TryGetProperty("value", out var value)) continue;

                    var formId = GetStringOrNumber(value, "form_id");
                    var leadgenId = GetStringOrNumber(value, "leadgen_id");
                    var pageId = GetStringOrNumber(value, "page_id");
                    if (string.IsNullOrWhiteSpace(pageId)) pageId = entryId;

                    _logger.LogInformation("Enqueuing leadgen notification: PageId={PageId}, FormId={FormId}, LeadgenId={LeadgenId}", pageId, formId, leadgenId);
                    await _queue.EnqueueAsync(new LeadgenNotification(pageId, formId, leadgenId, rawBody), ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse incoming Meta webhook JSON payload.");
        }

        return true;
    }

    private bool IsValidSignature(byte[] rawBytes, string? signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            _logger.LogWarning("Missing X-Hub-Signature-256 header in Meta webhook call.");
            return false;
        }

        var expected = ComputeHmacSha256(rawBytes, _options.AppSecret).Trim().ToLowerInvariant();
        var provided = signatureHeader.Replace("sha256=", string.Empty, StringComparison.OrdinalIgnoreCase).Trim().ToLowerInvariant();

        var isValid = CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided));

        if (!isValid)
        {
            _logger.LogWarning("Invalid X-Hub-Signature-256. Expected: {Expected}, Provided: {Provided}", expected, provided);
        }

        return isValid;
    }

    private static string ComputeHmacSha256(byte[] payloadBytes, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string GetStringOrNumber(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            return string.Empty;

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? string.Empty,
            JsonValueKind.Number => prop.GetRawText(),
            _ => prop.ToString() ?? string.Empty
        };
    }
}

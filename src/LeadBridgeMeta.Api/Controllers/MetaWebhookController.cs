using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LeadBridgeMeta.Infrastructure.Meta;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LeadBridgeMeta.Api.Controllers;

[ApiController]
[Route("api/webhooks/meta")]
public class MetaWebhookController : ControllerBase
{
    private readonly MetaOptions _options;
    private readonly IMetaWebhookQueue _queue;
    private readonly ILogger<MetaWebhookController> _logger;

    public MetaWebhookController(IOptions<MetaOptions> options, IMetaWebhookQueue queue, ILogger<MetaWebhookController> logger)
    {
        _options = options.Value;
        _queue = queue;
        _logger = logger;
    }

    /// <summary>Meta calls this once, synchronously, when you save the Webhooks subscription in the App Dashboard.</summary>
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.verify_token")] string verifyToken,
        [FromQuery(Name = "hub.challenge")] string challenge)
    {
        if (mode == "subscribe" && verifyToken == _options.WebhookVerifyToken)
            return Content(challenge, "text/plain");

        return Forbid();
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);
        Request.Body.Position = 0;

        if (!IsValidSignature(rawBody))
        {
            _logger.LogWarning("Rejected Meta webhook call with an invalid X-Hub-Signature-256.");
            return Unauthorized();
        }

        using var doc = JsonDocument.Parse(rawBody);
        if (!doc.RootElement.TryGetProperty("entry", out var entries))
            return Ok();

        foreach (var entry in entries.EnumerateArray())
        {
            var pageId = entry.GetProperty("id").GetString() ?? string.Empty;
            if (!entry.TryGetProperty("changes", out var changes)) continue;

            foreach (var change in changes.EnumerateArray())
            {
                if (change.GetProperty("field").GetString() != "leadgen") continue;

                var value = change.GetProperty("value");
                var formId = value.GetProperty("form_id").GetString() ?? string.Empty;
                var leadgenId = value.GetProperty("leadgen_id").GetString() ?? string.Empty;

                await _queue.EnqueueAsync(new LeadgenNotification(pageId, formId, leadgenId, rawBody), ct);
            }
        }

        // Always ack quickly with 200 - Meta retries/backs off aggressively on non-2xx responses.
        return Ok();
    }

    private bool IsValidSignature(string rawBody)
    {
        if (!Request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureHeader))
            return false;

        var expected = ComputeHmacSha256(rawBody, _options.AppSecret);
        var provided = signatureHeader.ToString().Replace("sha256=", string.Empty);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided));
    }

    private static string ComputeHmacSha256(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

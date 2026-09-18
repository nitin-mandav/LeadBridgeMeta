using LeadBridgeMeta.Application.Meta;
using Microsoft.AspNetCore.Mvc;

namespace LeadBridgeMeta.Api.Controllers;

[ApiController]
[Route("api/webhooks/meta")]
public class MetaWebhookController : ControllerBase
{
    private readonly IMetaWebhookService _webhookService;
    private readonly ILogger<MetaWebhookController> _logger;

    public MetaWebhookController(IMetaWebhookService webhookService, ILogger<MetaWebhookController> logger)
    {
        _webhookService = webhookService;
        _logger = logger;
    }

    /// <summary>Meta calls this once, synchronously, when you save the Webhooks subscription in the App Dashboard.</summary>
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string mode,
        [FromQuery(Name = "hub.verify_token")] string verifyToken,
        [FromQuery(Name = "hub.challenge")] string challenge)
    {
        if (_webhookService.VerifyToken(mode, verifyToken))
        {
            return Content(challenge, "text/plain");
        }

        return Forbid();
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, ct);
        var rawBytes = ms.ToArray();
        Request.Body.Position = 0;

        Request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureHeader);

        var success = await _webhookService.ProcessWebhookPayloadAsync(rawBytes, signatureHeader.ToString(), ct);
        if (!success)
        {
            return Unauthorized();
        }

        // Always ack quickly with 200 - Meta retries/backs off aggressively on non-2xx responses.
        return Ok();
    }
}

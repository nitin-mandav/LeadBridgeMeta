using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Application.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeadBridgeMeta.Api.Controllers;

[ApiController]
[Route("api/email")]
[Authorize]
public class EmailController : ControllerBase
{
    private readonly IEmailConnectionService _emailService;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<EmailController> _logger;

    public EmailController(
        IEmailConnectionService emailService,
        ICurrentUserContext currentUser,
        ILogger<EmailController> logger)
    {
        _emailService = emailService;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet("connections")]
    public async Task<ActionResult<List<EmailConnectionDto>>> GetConnections(CancellationToken ct)
    {
        var connections = await _emailService.GetConnectionsAsync(_currentUser.TenantId, ct);
        return Ok(connections);
    }

    [HttpPost("connections")]
    public async Task<ActionResult<EmailConnectionDto>> AddConnection(
        [FromBody] AddEmailConnectionRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { error = "Email address is required." });
        }

        try
        {
            var created = await _emailService.AddConnectionAsync(_currentUser.TenantId, request.Email, ct);
            return Ok(created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("connections/{id:guid}")]
    public async Task<IActionResult> Disconnect(Guid id, CancellationToken ct)
    {
        var removed = await _emailService.DisconnectAsync(_currentUser.TenantId, id, ct);
        if (!removed)
        {
            return NotFound(new { error = "Email connection not found." });
        }

        return Ok(new { success = true, message = "Email connection removed successfully." });
    }

    [HttpPatch("connections/{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(
        Guid id,
        [FromBody] ToggleEmailConnectionRequest request,
        CancellationToken ct)
    {
        var updated = await _emailService.ToggleActiveAsync(_currentUser.TenantId, id, request.IsActive, ct);
        if (!updated)
        {
            return NotFound(new { error = "Email connection not found." });
        }

        return Ok(new { success = true });
    }

    [HttpPost("test")]
    public async Task<IActionResult> SendTestEmail(
        [FromBody] SendTestEmailRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { error = "Target email address is required for test." });
        }

        try
        {
            await _emailService.SendTestEmailAsync(_currentUser.TenantId, request.Email, ct);
            return Ok(new { success = true, message = $"Test lead email sent successfully to {request.Email}." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send test email to {Email}.", request.Email);
            return BadRequest(new { error = $"Failed to send test email: {ex.Message}" });
        }
    }

    [HttpGet("status"), AllowAnonymous]
    public ActionResult<object> GetStatus()
    {
        var sender = _emailService.GetSenderEmail();
        return Ok(new
        {
            isConfigured = !string.IsNullOrWhiteSpace(sender),
            senderEmail = sender
        });
    }
}

public record AddEmailConnectionRequest(string Email);
public record ToggleEmailConnectionRequest(bool IsActive);
public record SendTestEmailRequest(string Email);

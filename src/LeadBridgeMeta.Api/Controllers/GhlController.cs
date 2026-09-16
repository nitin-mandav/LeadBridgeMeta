using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Application.Ghl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace LeadBridgeMeta.Api.Controllers;

[ApiController]
[Route("api/ghl")]
public class GhlController : ControllerBase
{
    private readonly IGhlConnectionService _ghlConnectionService;
    private readonly IConfiguration _config;

    public GhlController(IGhlConnectionService ghlConnectionService, IConfiguration config)
    {
        _ghlConnectionService = ghlConnectionService;
        _config = config;
    }

    private string CallbackRedirectUri => $"{_config["App:ApiBaseUrl"]}/api/oauth/callback";
    private string FrontendConnectionsUrl => $"{_config["App:FrontendBaseUrl"]}/connections";

    /// <summary>Direct-install link (also used from the GHL Marketplace "Install" button, which appends its own params).</summary>
    [HttpGet("connect-url"), Authorize]
    public ActionResult<object> GetConnectUrl([FromServices] ICurrentUserContext currentUser)
    {
        var url = _ghlConnectionService.GetConnectUrl(currentUser.TenantId, CallbackRedirectUri);
        return Ok(new { url });
    }

    [HttpGet("/api/oauth/callback")]
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string code,
        [FromQuery] string? state = null,
        [FromQuery] bool? json = null,
        CancellationToken ct = default)
    {
        var wantsJson = json == true || Request.Headers.Accept.ToString().Contains("application/json");

        var result = await _ghlConnectionService.ProcessOAuthCallbackAsync(code, state, CallbackRedirectUri, ct);

        if (result.Success)
        {
            if (wantsJson)
                return Ok(new { success = true, locationId = result.LocationId });

            return Redirect($"{FrontendConnectionsUrl}?ghl_connected=1");
        }

        if (wantsJson)
            return BadRequest(new { error = result.ErrorMessage });

        return Redirect($"{FrontendConnectionsUrl}?ghl_error={Uri.EscapeDataString(result.ErrorMessage ?? "Connection failed.")}");
    }

    [HttpGet("connections"), Authorize]
    public async Task<ActionResult<List<GhlConnectionDto>>> GetConnections([FromServices] ICurrentUserContext currentUser, CancellationToken ct)
    {
        var connections = await _ghlConnectionService.GetConnectionsAsync(currentUser.TenantId, ct);
        return Ok(connections);
    }
}

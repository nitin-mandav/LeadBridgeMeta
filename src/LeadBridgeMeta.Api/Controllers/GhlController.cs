using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Application.Ghl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace LeadBridgeMeta.Api.Controllers;

public record GhlConnectionResponse(Guid Id, string LocationId, string LocationName, DateTime AccessTokenExpiresAtUtc);

[ApiController]
[Route("api/ghl")]
public class GhlController : ControllerBase
{
    private readonly IGhlConnectionService _ghlService;
    private readonly IConfiguration _config;

    public GhlController(IGhlConnectionService ghlService, IConfiguration config)
    {
        _ghlService = ghlService;
        _config = config;
    }

    private string CallbackRedirectUri => $"{_config["App:ApiBaseUrl"]}/api/ghl/callback";
    private string FrontendConnectionsUrl => $"{_config["App:FrontendBaseUrl"]}/connections";

    /// <summary>Direct-install link (also used from the GHL Marketplace "Install" button, which appends its own params).</summary>
    [HttpGet("connect-url"), Authorize]
    public ActionResult<object> GetConnectUrl([FromServices] ICurrentUserContext currentUser)
    {
        var url = _ghlService.GetConnectUrl(currentUser.TenantId, CallbackRedirectUri);
        return Ok(new { url });
    }

    [HttpGet("callback")]
    [HttpGet("/api/oauth/callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string? state, [FromQuery] bool? json, CancellationToken ct)
    {
        var wantsJson = json == true || Request.Headers.Accept.ToString().Contains("application/json");

        var result = await _ghlService.ProcessOAuthCallbackAsync(code, state, CallbackRedirectUri, ct);

        if (!result.Success)
        {
            if (wantsJson)
                return BadRequest(new { error = result.ErrorMessage });

            return Redirect($"{FrontendConnectionsUrl}?ghl_error={Uri.EscapeDataString(result.ErrorMessage ?? "OAuth callback failed")}");
        }

        if (wantsJson)
            return Ok(new { success = true, locationId = result.LocationId });

        return Redirect($"{FrontendConnectionsUrl}?ghl_connected=1");
    }

    [HttpGet("connections"), Authorize]
    public async Task<ActionResult<List<GhlConnectionResponse>>> GetConnections([FromServices] ICurrentUserContext currentUser, CancellationToken ct)
    {
        var connections = await _ghlService.GetConnectionsAsync(currentUser.TenantId, ct);
        var response = connections.Select(c => new GhlConnectionResponse(c.Id, c.LocationId, c.LocationName, c.AccessTokenExpiresAtUtc)).ToList();
        return Ok(response);
    }
}

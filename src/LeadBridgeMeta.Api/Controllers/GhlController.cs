using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Application.Ghl;
using LeadBridgeMeta.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LeadBridgeMeta.Api.Controllers;

public record GhlConnectionResponse(Guid Id, string LocationId, string LocationName, DateTime AccessTokenExpiresAtUtc);

[ApiController]
[Route("api/ghl")]
public class GhlController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IGhlClient _ghl;
    private readonly ITokenProtector _protector;
    private readonly IOAuthStateService _state;
    private readonly IConfiguration _config;

    public GhlController(IAppDbContext db, IGhlClient ghl, ITokenProtector protector, IOAuthStateService state, IConfiguration config)
    {
        _db = db;
        _ghl = ghl;
        _protector = protector;
        _state = state;
        _config = config;
    }

    private string CallbackRedirectUri => $"{_config["App:ApiBaseUrl"]}/api/ghl/callback";
    private string FrontendConnectionsUrl => $"{_config["App:FrontendBaseUrl"]}/connections";

    /// <summary>Direct-install link (also used from the GHL Marketplace "Install" button, which appends its own params).</summary>
    [HttpGet("connect-url"), Authorize]
    public ActionResult<object> GetConnectUrl([FromServices] ICurrentUserContext currentUser)
    {
        var state = _state.CreateState(currentUser.TenantId, "ghl-connect");
        var url = _ghl.BuildAuthorizeUrl(state, CallbackRedirectUri);
        return Ok(new { url });
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state, CancellationToken ct)
    {
        Guid tenantId;
        try
        {
            (tenantId, _) = _state.ValidateState(state);
        }
        catch (Exception ex)
        {
            return Redirect($"{FrontendConnectionsUrl}?ghl_error={Uri.EscapeDataString(ex.Message)}");
        }

        try
        {
            var token = await _ghl.ExchangeCodeForTokenAsync(code, CallbackRedirectUri, ct);

            var connection = await _db.GhlConnections
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.LocationId == token.LocationId, ct);

            if (connection is null)
            {
                connection = new GhlConnection { TenantId = tenantId, LocationId = token.LocationId };
                _db.GhlConnections.Add(connection);
            }

            connection.CompanyId = token.CompanyId;
            connection.LocationName = token.LocationId; // Refined later via a GET /locations/{id} call if a friendly name is needed.
            connection.EncryptedAccessToken = _protector.Protect(token.AccessToken);
            connection.EncryptedRefreshToken = _protector.Protect(token.RefreshToken);
            connection.AccessTokenExpiresAtUtc = token.ExpiresAtUtc;

            await _db.SaveChangesAsync(ct);

            return Redirect($"{FrontendConnectionsUrl}?ghl_connected=1");
        }
        catch (Exception ex)
        {
            return Redirect($"{FrontendConnectionsUrl}?ghl_error={Uri.EscapeDataString(ex.Message)}");
        }
    }

    [HttpGet("connections"), Authorize]
    public async Task<ActionResult<List<GhlConnectionResponse>>> GetConnections([FromServices] ICurrentUserContext currentUser, CancellationToken ct)
    {
        var connections = await _db.GhlConnections
            .Where(c => c.TenantId == currentUser.TenantId)
            .Select(c => new GhlConnectionResponse(c.Id, c.LocationId, c.LocationName, c.AccessTokenExpiresAtUtc))
            .ToListAsync(ct);

        return Ok(connections);
    }
}

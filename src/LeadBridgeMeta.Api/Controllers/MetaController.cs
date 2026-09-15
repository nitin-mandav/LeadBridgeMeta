using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Application.Meta;
using LeadBridgeMeta.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LeadBridgeMeta.Api.Controllers;

public record MetaLeadFormResponse(Guid Id, string FormId, string FormName, bool IsActive, Guid? GhlConnectionId);
public record MetaPageResponse(Guid Id, string PageId, string PageName, bool IsLeadgenWebhookSubscribed, List<MetaLeadFormResponse> Forms);
public record MetaConnectionResponse(Guid Id, string FacebookUserName, DateTime TokenExpiresAtUtc, List<MetaPageResponse> Pages);

[ApiController]
[Route("api/meta")]
public class MetaController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly IMetaGraphClient _meta;
    private readonly ITokenProtector _protector;
    private readonly IOAuthStateService _state;
    private readonly IConfiguration _config;

    public MetaController(IAppDbContext db, IMetaGraphClient meta, ITokenProtector protector, IOAuthStateService state, IConfiguration config)
    {
        _db = db;
        _meta = meta;
        _protector = protector;
        _state = state;
        _config = config;
    }

    private string CallbackRedirectUri => $"{_config["App:ApiBaseUrl"]}/api/meta/callback";
    private string FrontendConnectionsUrl => $"{_config["App:FrontendBaseUrl"]}/connections";

    [HttpGet("connect-url"), Authorize]
    public ActionResult<object> GetConnectUrl([FromServices] ICurrentUserContext currentUser)
    {
        var state = _state.CreateState(currentUser.TenantId, "meta-connect");
        var url = _meta.BuildLoginDialogUrl(state, CallbackRedirectUri);
        return Ok(new { url });
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state, [FromQuery] bool? json, CancellationToken ct)
    {
        var wantsJson = json == true || Request.Headers.Accept.ToString().Contains("application/json");

        Guid tenantId;
        try
        {
            (tenantId, _) = _state.ValidateState(state);
        }
        catch (Exception ex)
        {
            if (wantsJson)
                return BadRequest(new { error = ex.Message });
            return Redirect($"{FrontendConnectionsUrl}?meta_error={Uri.EscapeDataString(ex.Message)}");
        }

        try
        {
            var shortLived = await _meta.ExchangeCodeForUserTokenAsync(code, CallbackRedirectUri, ct);
            var longLived = await _meta.GetLongLivedUserTokenAsync(shortLived.AccessToken, ct);
            var (fbUserId, fbName) = await _meta.GetMeAsync(longLived.AccessToken, ct);

            var connection = await _db.MetaConnections
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.FacebookUserId == fbUserId, ct);

            if (connection is null)
            {
                connection = new MetaConnection { TenantId = tenantId, FacebookUserId = fbUserId };
                _db.MetaConnections.Add(connection);
            }

            connection.FacebookUserName = fbName;
            connection.EncryptedUserAccessToken = _protector.Protect(longLived.AccessToken);
            connection.TokenExpiresAtUtc = longLived.ExpiresAtUtc;
            connection.GrantedScopes = "pages_show_list,pages_manage_metadata,pages_read_engagement,leads_retrieval,business_management";
            await _db.SaveChangesAsync(ct);

            var pages = await _meta.GetManagedPagesAsync(longLived.AccessToken, ct);
            foreach (var p in pages)
            {
                var existingPage = await _db.MetaPages.FirstOrDefaultAsync(mp => mp.PageId == p.PageId, ct);
                if (existingPage is null)
                {
                    existingPage = new MetaPage { MetaConnectionId = connection.Id, PageId = p.PageId };
                    _db.MetaPages.Add(existingPage);
                }
                existingPage.PageName = p.PageName;
                existingPage.EncryptedPageAccessToken = _protector.Protect(p.PageAccessToken);
            }
            await _db.SaveChangesAsync(ct);

            if (wantsJson)
                return Ok(new { success = true, userName = fbName });

            return Redirect($"{FrontendConnectionsUrl}?meta_connected=1");
        }
        catch (Exception ex)
        {
            if (wantsJson)
                return StatusCode(500, new { error = ex.Message });

            return Redirect($"{FrontendConnectionsUrl}?meta_error={Uri.EscapeDataString(ex.Message)}");
        }
    }

    [HttpGet("connections"), Authorize]
    public async Task<ActionResult<List<MetaConnectionResponse>>> GetConnections([FromServices] ICurrentUserContext currentUser, CancellationToken ct)
    {
        var connections = await _db.MetaConnections
            .Where(c => c.TenantId == currentUser.TenantId)
            .Include(c => c.Pages).ThenInclude(p => p.LeadForms)
            .ToListAsync(ct);

        var result = connections.Select(c => new MetaConnectionResponse(
            c.Id, c.FacebookUserName, c.TokenExpiresAtUtc,
            c.Pages.Select(p => new MetaPageResponse(
                p.Id, p.PageId, p.PageName, p.IsLeadgenWebhookSubscribed,
                p.LeadForms.Select(f => new MetaLeadFormResponse(f.Id, f.FormId, f.FormName, f.IsActive, f.GhlConnectionId)).ToList()
            )).ToList()
        )).ToList();

        return Ok(result);
    }

    [HttpPost("pages/{pageId:guid}/subscribe"), Authorize]
    public async Task<IActionResult> SubscribePage(Guid pageId, [FromServices] ICurrentUserContext currentUser, CancellationToken ct)
    {
        var page = await _db.MetaPages
            .Include(p => p.MetaConnection)
            .FirstOrDefaultAsync(p => p.Id == pageId && p.MetaConnection!.TenantId == currentUser.TenantId, ct);
        if (page is null) return NotFound();

        var pageToken = _protector.Unprotect(page.EncryptedPageAccessToken);
        await _meta.SubscribePageToLeadgenAsync(page.PageId, pageToken, ct);
        page.IsLeadgenWebhookSubscribed = true;
        page.SubscribedAtUtc = DateTime.UtcNow;

        var forms = await _meta.GetLeadFormsAsync(page.PageId, pageToken, ct);
        foreach (var f in forms)
        {
            var existingForm = await _db.MetaLeadForms.FirstOrDefaultAsync(mf => mf.FormId == f.FormId, ct);
            if (existingForm is null)
            {
                existingForm = new MetaLeadForm { MetaPageId = page.Id, FormId = f.FormId };
                _db.MetaLeadForms.Add(existingForm);
            }
            existingForm.FormName = f.FormName;
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

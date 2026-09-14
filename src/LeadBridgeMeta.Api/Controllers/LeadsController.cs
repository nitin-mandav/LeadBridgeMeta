using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Application.Leads;
using LeadBridgeMeta.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadBridgeMeta.Api.Controllers;

public record LeadEventResponse(
    Guid Id, string LeadgenId, string FormName, LeadEventStatus Status,
    string? GhlContactId, string? ErrorMessage, int RetryCount, DateTime ReceivedAtUtc, DateTime? ProcessedAtUtc);

[ApiController]
[Route("api/leads")]
[Authorize]
public class LeadsController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILeadProcessingService _leadProcessing;

    public LeadsController(IAppDbContext db, ICurrentUserContext currentUser, ILeadProcessingService leadProcessing)
    {
        _db = db;
        _currentUser = currentUser;
        _leadProcessing = leadProcessing;
    }

    [HttpGet]
    public async Task<ActionResult<List<LeadEventResponse>>> GetLeads(
        [FromQuery] LeadEventStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var query = _db.LeadEvents
            .Include(l => l.MetaLeadForm)
            .Where(l => l.TenantId == _currentUser.TenantId);

        if (status.HasValue)
            query = query.Where(l => l.Status == status.Value);

        var results = await query
            .OrderByDescending(l => l.ReceivedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LeadEventResponse(
                l.Id, l.LeadgenId, l.MetaLeadForm!.FormName, l.Status,
                l.GhlContactId, l.ErrorMessage, l.RetryCount, l.ReceivedAtUtc, l.ProcessedAtUtc))
            .ToListAsync(ct);

        return Ok(results);
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var owns = await _db.LeadEvents.AnyAsync(l => l.Id == id && l.TenantId == _currentUser.TenantId, ct);
        if (!owns) return NotFound();

        await _leadProcessing.RetryAsync(id, ct);
        return NoContent();
    }
}

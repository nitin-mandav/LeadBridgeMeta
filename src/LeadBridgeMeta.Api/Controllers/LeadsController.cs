using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Application.Leads;
using LeadBridgeMeta.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeadBridgeMeta.Api.Controllers;

public record LeadEventResponse(
    Guid Id, string LeadgenId, string FormName, LeadEventStatus Status,
    string? GhlContactId, string? ErrorMessage, int RetryCount, DateTime ReceivedAtUtc, DateTime? ProcessedAtUtc,
    string? RawLeadDataJson = null);

[ApiController]
[Route("api/leads")]
[Authorize]
public class LeadsController : ControllerBase
{
    private readonly ILeadProcessingService _leadProcessing;
    private readonly ICurrentUserContext _currentUser;

    public LeadsController(
        ILeadProcessingService leadProcessing,
        ICurrentUserContext currentUser)
    {
        _leadProcessing = leadProcessing;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<List<LeadEventResponse>>> GetLeads(
        [FromQuery] LeadEventStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var leads = await _leadProcessing.GetLeadsAsync(_currentUser.TenantId, status, page, pageSize, ct);
        var results = leads.Select(MapToResponse).ToList();
        return Ok(results);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LeadEventResponse>> GetLeadById(Guid id, CancellationToken ct)
    {
        var lead = await _leadProcessing.GetLeadByIdAsync(id, _currentUser.TenantId, ct);
        if (lead is null) return NotFound();

        return Ok(MapToResponse(lead));
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var success = await _leadProcessing.RetryAsync(id, _currentUser.TenantId, ct);
        if (!success) return NotFound();

        return NoContent();
    }

    private static LeadEventResponse MapToResponse(LeadEventDto dto) =>
        new(dto.Id, dto.LeadgenId, dto.FormName, dto.Status,
            dto.GhlContactId, dto.ErrorMessage, dto.RetryCount, dto.ReceivedAtUtc, dto.ProcessedAtUtc,
            dto.RawLeadDataJson);
}

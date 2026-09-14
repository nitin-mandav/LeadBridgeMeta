using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadBridgeMeta.Api.Controllers;

public record SetFormGhlConnectionRequest(Guid GhlConnectionId);
public record FieldMappingRequest(Guid? MetaLeadFormId, string MetaFieldKey, GhlTargetFieldType TargetType, string GhlFieldKey);
public record FieldMappingResponse(Guid Id, Guid? MetaLeadFormId, string MetaFieldKey, GhlTargetFieldType TargetType, string GhlFieldKey);

[ApiController]
[Route("api/mappings")]
[Authorize]
public class MappingsController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserContext _currentUser;

    public MappingsController(IAppDbContext db, ICurrentUserContext currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpPut("forms/{formId:guid}/ghl-connection")]
    public async Task<IActionResult> SetFormGhlConnection(Guid formId, SetFormGhlConnectionRequest request, CancellationToken ct)
    {
        var form = await _db.MetaLeadForms
            .Include(f => f.MetaPage).ThenInclude(p => p!.MetaConnection)
            .FirstOrDefaultAsync(f => f.Id == formId, ct);
        if (form is null || form.MetaPage!.MetaConnection!.TenantId != _currentUser.TenantId)
            return NotFound();

        var ghlConnection = await _db.GhlConnections
            .FirstOrDefaultAsync(g => g.Id == request.GhlConnectionId && g.TenantId == _currentUser.TenantId, ct);
        if (ghlConnection is null)
            return BadRequest("GHL connection not found for this tenant.");

        form.GhlConnectionId = ghlConnection.Id;
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("fields")]
    public async Task<ActionResult<List<FieldMappingResponse>>> GetFieldMappings([FromQuery] Guid? formId, CancellationToken ct)
    {
        var mappings = await _db.FieldMappings
            .Where(m => m.TenantId == _currentUser.TenantId && m.MetaLeadFormId == formId)
            .Select(m => new FieldMappingResponse(m.Id, m.MetaLeadFormId, m.MetaFieldKey, m.TargetType, m.GhlFieldKey))
            .ToListAsync(ct);

        return Ok(mappings);
    }

    [HttpPost("fields")]
    public async Task<ActionResult<FieldMappingResponse>> UpsertFieldMapping(FieldMappingRequest request, CancellationToken ct)
    {
        var existing = await _db.FieldMappings.FirstOrDefaultAsync(m =>
            m.TenantId == _currentUser.TenantId &&
            m.MetaLeadFormId == request.MetaLeadFormId &&
            m.MetaFieldKey == request.MetaFieldKey, ct);

        if (existing is null)
        {
            existing = new FieldMapping
            {
                TenantId = _currentUser.TenantId,
                MetaLeadFormId = request.MetaLeadFormId,
                MetaFieldKey = request.MetaFieldKey,
            };
            _db.FieldMappings.Add(existing);
        }

        existing.TargetType = request.TargetType;
        existing.GhlFieldKey = request.GhlFieldKey;
        await _db.SaveChangesAsync(ct);

        return Ok(new FieldMappingResponse(existing.Id, existing.MetaLeadFormId, existing.MetaFieldKey, existing.TargetType, existing.GhlFieldKey));
    }

    [HttpDelete("fields/{id:guid}")]
    public async Task<IActionResult> DeleteFieldMapping(Guid id, CancellationToken ct)
    {
        var mapping = await _db.FieldMappings.FirstOrDefaultAsync(m => m.Id == id && m.TenantId == _currentUser.TenantId, ct);
        if (mapping is null) return NotFound();

        _db.FieldMappings.Remove(mapping);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}

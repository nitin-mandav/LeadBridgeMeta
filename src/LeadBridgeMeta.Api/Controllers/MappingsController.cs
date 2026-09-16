using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Application.Meta;
using LeadBridgeMeta.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadBridgeMeta.Api.Controllers;

public record SetFormGhlConnectionRequest(Guid GhlConnectionId);
public record FieldMappingRequest(Guid? MetaLeadFormId, string MetaFieldKey, GhlTargetFieldType TargetType, string GhlFieldKey);
public record FieldMappingResponse(Guid Id, Guid? MetaLeadFormId, string MetaFieldKey, GhlTargetFieldType TargetType, string GhlFieldKey);
public record MetaFormQuestionResponse(string Key, string Label, string? Type);

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

    [HttpGet("forms/{formId:guid}/fields")]
    public async Task<ActionResult<List<MetaFormQuestionResponse>>> GetFormFields(
        Guid formId,
        [FromServices] IMetaGraphClient meta,
        [FromServices] ITokenProtector protector,
        CancellationToken ct)
    {
        var form = await _db.MetaLeadForms
            .Include(f => f.MetaPage).ThenInclude(p => p!.MetaConnection)
            .FirstOrDefaultAsync(f => f.Id == formId, ct);

        if (form is null || form.MetaPage?.MetaConnection?.TenantId != _currentUser.TenantId)
            return NotFound();

        var fields = new List<MetaFormQuestionResponse>();
        if (!string.IsNullOrEmpty(form.MetaPage?.EncryptedPageAccessToken))
        {
            try
            {
                var pageToken = protector.Unprotect(form.MetaPage.EncryptedPageAccessToken);
                var questions = await meta.GetLeadFormQuestionsAsync(form.FormId, pageToken, ct);
                fields.AddRange(questions.Select(q => new MetaFormQuestionResponse(q.Key, q.Label, q.Type)));
            }
            catch
            {
                // Fall back gracefully if Meta API call fails or token expired
            }
        }

        if (fields.Count == 0)
        {
            fields.AddRange([
                new("full_name", "Full Name", "FULL_NAME"),
                new("first_name", "First Name", "FIRST_NAME"),
                new("last_name", "Last Name", "LAST_NAME"),
                new("email", "Email", "EMAIL"),
                new("phone_number", "Phone Number", "PHONE"),
                new("city", "City", "CITY"),
                new("street_address", "Street Address", "STREET_ADDRESS"),
                new("state", "State", "STATE"),
                new("zip_code", "Zip Code", "ZIP_CODE"),
                new("country", "Country", "COUNTRY"),
                new("company_name", "Company Name", "COMPANY_NAME"),
                new("job_title", "Job Title", "JOB_TITLE"),
            ]);
        }

        var existingMappedKeys = await _db.FieldMappings
            .Where(m => m.MetaLeadFormId == formId)
            .Select(m => m.MetaFieldKey)
            .ToListAsync(ct);

        foreach (var k in existingMappedKeys)
        {
            if (!fields.Any(f => f.Key.Equals(k, StringComparison.OrdinalIgnoreCase)))
            {
                fields.Add(new(k, k, "CUSTOM"));
            }
        }

        return Ok(fields);
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

using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Application.Mappings;
using LeadBridgeMeta.Application.Meta;
using LeadBridgeMeta.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeadBridgeMeta.Infrastructure.Mappings;

public class MappingService : IMappingService
{
    private readonly IAppDbContext _db;
    private readonly IMetaGraphClient _meta;
    private readonly ITokenProtector _protector;
    private readonly ILogger<MappingService> _logger;

    public MappingService(
        IAppDbContext db,
        IMetaGraphClient meta,
        ITokenProtector protector,
        ILogger<MappingService> logger)
    {
        _db = db;
        _meta = meta;
        _protector = protector;
        _logger = logger;
    }

    public async Task<bool?> SetFormGhlConnectionAsync(Guid formId, Guid ghlConnectionId, Guid tenantId, CancellationToken ct = default)
    {
        var form = await _db.MetaLeadForms
            .Include(f => f.MetaPage).ThenInclude(p => p!.MetaConnection)
            .FirstOrDefaultAsync(f => f.Id == formId, ct);

        if (form is null || form.MetaPage?.MetaConnection?.TenantId != tenantId)
            return null; // Not found

        var ghlConnection = await _db.GhlConnections
            .FirstOrDefaultAsync(g => g.Id == ghlConnectionId && g.TenantId == tenantId, ct);

        if (ghlConnection is null)
            return false; // Invalid GHL connection

        form.GhlConnectionId = ghlConnection.Id;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<FormQuestionDto>?> GetFormQuestionsAsync(Guid formId, Guid tenantId, CancellationToken ct = default)
    {
        var form = await _db.MetaLeadForms
            .Include(f => f.MetaPage).ThenInclude(p => p!.MetaConnection)
            .FirstOrDefaultAsync(f => f.Id == formId, ct);

        if (form is null || form.MetaPage?.MetaConnection?.TenantId != tenantId)
            return null;

        var questionsList = new List<FormQuestionDto>();

        if (!string.IsNullOrEmpty(form.MetaPage?.EncryptedPageAccessToken))
        {
            try
            {
                var pageToken = _protector.Unprotect(form.MetaPage.EncryptedPageAccessToken);
                var metaQuestions = await _meta.GetLeadFormQuestionsAsync(form.FormId, pageToken, ct);
                questionsList.AddRange(metaQuestions.Select(q => new FormQuestionDto(q.Key, q.Label, q.Type)));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve questions from Meta Graph API for form {FormId}", form.FormId);
            }
        }

        // Include any custom field mappings previously configured for this form
        var existingMappedKeys = await _db.FieldMappings
            .Where(m => m.MetaLeadFormId == formId && m.TenantId == tenantId)
            .Select(m => m.MetaFieldKey)
            .ToListAsync(ct);

        foreach (var k in existingMappedKeys)
        {
            if (!questionsList.Any(f => f.Key.Equals(k, StringComparison.OrdinalIgnoreCase)))
            {
                questionsList.Add(new FormQuestionDto(k, k, "CUSTOM"));
            }
        }

        return questionsList;
    }

    public async Task<List<FieldMappingDto>> GetFieldMappingsAsync(Guid? formId, Guid tenantId, CancellationToken ct = default)
    {
        return await _db.FieldMappings
            .Where(m => m.TenantId == tenantId && m.MetaLeadFormId == formId)
            .Select(m => new FieldMappingDto(m.Id, m.MetaLeadFormId, m.MetaFieldKey, m.TargetType, m.GhlFieldKey))
            .ToListAsync(ct);
    }

    public async Task<FieldMappingDto> UpsertFieldMappingAsync(
        Guid? formId,
        string metaFieldKey,
        GhlTargetFieldType targetType,
        string ghlFieldKey,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var existing = await _db.FieldMappings.FirstOrDefaultAsync(m =>
            m.TenantId == tenantId &&
            m.MetaLeadFormId == formId &&
            m.MetaFieldKey == metaFieldKey, ct);

        if (existing is null)
        {
            existing = new FieldMapping
            {
                TenantId = tenantId,
                MetaLeadFormId = formId,
                MetaFieldKey = metaFieldKey,
            };
            _db.FieldMappings.Add(existing);
        }

        existing.TargetType = targetType;
        existing.GhlFieldKey = ghlFieldKey;
        await _db.SaveChangesAsync(ct);

        return new FieldMappingDto(existing.Id, existing.MetaLeadFormId, existing.MetaFieldKey, existing.TargetType, existing.GhlFieldKey);
    }

    public async Task<bool> DeleteFieldMappingAsync(Guid mappingId, Guid tenantId, CancellationToken ct = default)
    {
        var mapping = await _db.FieldMappings.FirstOrDefaultAsync(m => m.Id == mappingId && m.TenantId == tenantId, ct);
        if (mapping is null) return false;

        _db.FieldMappings.Remove(mapping);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

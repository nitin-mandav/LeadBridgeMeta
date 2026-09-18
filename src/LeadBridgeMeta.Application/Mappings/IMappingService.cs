using LeadBridgeMeta.Domain.Entities;

namespace LeadBridgeMeta.Application.Mappings;

public record FormQuestionDto(string Key, string Label, string? Type);
public record FieldMappingDto(Guid Id, Guid? MetaLeadFormId, string MetaFieldKey, GhlTargetFieldType TargetType, string GhlFieldKey);

public interface IMappingService
{
    Task<bool?> SetFormGhlConnectionAsync(Guid formId, Guid ghlConnectionId, Guid tenantId, CancellationToken ct = default);

    Task<List<FormQuestionDto>?> GetFormQuestionsAsync(Guid formId, Guid tenantId, CancellationToken ct = default);

    Task<List<FieldMappingDto>> GetFieldMappingsAsync(Guid? formId, Guid tenantId, CancellationToken ct = default);

    Task<FieldMappingDto> UpsertFieldMappingAsync(Guid? formId, string metaFieldKey, GhlTargetFieldType targetType, string ghlFieldKey, Guid tenantId, CancellationToken ct = default);

    Task<bool> DeleteFieldMappingAsync(Guid mappingId, Guid tenantId, CancellationToken ct = default);
}

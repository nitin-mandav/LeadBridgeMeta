using LeadBridgeMeta.Domain.Entities;

namespace LeadBridgeMeta.Application.Mappings;

public record ShopifyFieldMappingDto(
    Guid Id,
    Guid? MetaLeadFormId,
    string MetaFieldKey,
    ShopifyTargetFieldType TargetType,
    string ShopifyFieldKey);

public interface IShopifyMappingService
{
    Task<bool?> SetFormShopifyConnectionAsync(Guid formId, Guid shopifyConnectionId, Guid tenantId, CancellationToken ct = default);

    Task<List<ShopifyFieldMappingDto>> GetFieldMappingsAsync(Guid? formId, Guid tenantId, CancellationToken ct = default);

    Task<ShopifyFieldMappingDto> UpsertFieldMappingAsync(
        Guid? formId,
        string metaFieldKey,
        ShopifyTargetFieldType targetType,
        string shopifyFieldKey,
        Guid tenantId,
        CancellationToken ct = default);

    Task<bool> DeleteFieldMappingAsync(Guid mappingId, Guid tenantId, CancellationToken ct = default);
}

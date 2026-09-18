using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Application.Mappings;
using LeadBridgeMeta.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeadBridgeMeta.Infrastructure.Mappings;

public class ShopifyMappingService : IShopifyMappingService
{
    private readonly IAppDbContext _db;
    private readonly ILogger<ShopifyMappingService> _logger;

    public ShopifyMappingService(IAppDbContext db, ILogger<ShopifyMappingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool?> SetFormShopifyConnectionAsync(Guid formId, Guid shopifyConnectionId, Guid tenantId, CancellationToken ct = default)
    {
        var form = await _db.MetaLeadForms
            .Include(f => f.MetaPage).ThenInclude(p => p!.MetaConnection)
            .FirstOrDefaultAsync(f => f.Id == formId, ct);

        if (form is null || form.MetaPage?.MetaConnection?.TenantId != tenantId)
            return null;

        var shopifyConnection = await _db.ShopifyConnections
            .FirstOrDefaultAsync(s => s.Id == shopifyConnectionId && s.TenantId == tenantId, ct);

        if (shopifyConnection is null)
            return false;

        form.ShopifyConnectionId = shopifyConnection.Id;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Assigned form {FormId} to Shopify store {ShopDomain}", form.FormId, shopifyConnection.ShopDomain);
        return true;
    }

    public async Task<List<ShopifyFieldMappingDto>> GetFieldMappingsAsync(Guid? formId, Guid tenantId, CancellationToken ct = default)
    {
        return await _db.ShopifyFieldMappings
            .Where(m => m.TenantId == tenantId && m.MetaLeadFormId == formId)
            .Select(m => new ShopifyFieldMappingDto(m.Id, m.MetaLeadFormId, m.MetaFieldKey, m.TargetType, m.ShopifyFieldKey))
            .ToListAsync(ct);
    }

    public async Task<ShopifyFieldMappingDto> UpsertFieldMappingAsync(
        Guid? formId,
        string metaFieldKey,
        ShopifyTargetFieldType targetType,
        string shopifyFieldKey,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var existing = await _db.ShopifyFieldMappings.FirstOrDefaultAsync(m =>
            m.TenantId == tenantId &&
            m.MetaLeadFormId == formId &&
            m.MetaFieldKey == metaFieldKey, ct);

        if (existing is null)
        {
            existing = new ShopifyFieldMapping
            {
                TenantId = tenantId,
                MetaLeadFormId = formId,
                MetaFieldKey = metaFieldKey,
            };
            _db.ShopifyFieldMappings.Add(existing);
        }

        existing.TargetType = targetType;
        existing.ShopifyFieldKey = shopifyFieldKey;
        await _db.SaveChangesAsync(ct);

        return new ShopifyFieldMappingDto(existing.Id, existing.MetaLeadFormId, existing.MetaFieldKey, existing.TargetType, existing.ShopifyFieldKey);
    }

    public async Task<bool> DeleteFieldMappingAsync(Guid mappingId, Guid tenantId, CancellationToken ct = default)
    {
        var mapping = await _db.ShopifyFieldMappings.FirstOrDefaultAsync(m => m.Id == mappingId && m.TenantId == tenantId, ct);
        if (mapping is null) return false;

        _db.ShopifyFieldMappings.Remove(mapping);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

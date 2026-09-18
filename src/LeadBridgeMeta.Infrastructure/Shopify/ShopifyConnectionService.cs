using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Application.Shopify;
using LeadBridgeMeta.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeadBridgeMeta.Infrastructure.Shopify;

public class ShopifyConnectionService : IShopifyConnectionService
{
    private static readonly List<ShopifyFieldOptionDto> StandardFields = new()
    {
        new("email", "Email Address", "String", true),
        new("firstName", "First Name", "String", true),
        new("lastName", "Last Name", "String", true),
        new("phone", "Phone Number", "String", true),
        new("company", "Company", "String", true),
        new("address1", "Street Address", "String", true),
        new("city", "City", "String", true),
        new("province", "State / Province", "String", true),
        new("zip", "ZIP / Postal Code", "String", true),
        new("country", "Country", "String", true),
        new("tags", "Customer Tags (comma-separated)", "Tag", true),
        new("note", "Customer Note", "Note", true),
    };

    private readonly IAppDbContext _db;
    private readonly IShopifyClient _shopify;
    private readonly ITokenProtector _protector;
    private readonly IOAuthStateService _state;
    private readonly ILogger<ShopifyConnectionService> _logger;

    public ShopifyConnectionService(
        IAppDbContext db,
        IShopifyClient shopify,
        ITokenProtector protector,
        IOAuthStateService state,
        ILogger<ShopifyConnectionService> logger)
    {
        _db = db;
        _shopify = shopify;
        _protector = protector;
        _state = state;
        _logger = logger;
    }

    public string GetConnectUrl(string shop, Guid tenantId, string redirectUri)
    {
        var normalizedShop = ShopifyClient.NormalizeShopDomain(shop);
        var state = _state.CreateState(tenantId, $"shopify-connect:{normalizedShop}");
        return _shopify.BuildAuthorizeUrl(normalizedShop, state, redirectUri);
    }

    public async Task<ShopifyOAuthResult> ProcessOAuthCallbackAsync(
        string code,
        string shop,
        string? state,
        string redirectUri,
        CancellationToken ct = default)
    {
        Guid tenantId;
        try
        {
            (tenantId, _) = _state.ValidateState(state ?? "");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid Shopify OAuth state token.");
            return new ShopifyOAuthResult(false, ErrorMessage: "Invalid or expired authorization state. Please try connecting again.");
        }

        try
        {
            var normalizedShop = ShopifyClient.NormalizeShopDomain(shop);
            var tokenResult = await _shopify.ExchangeCodeForAccessTokenAsync(normalizedShop, code, redirectUri, ct);
            var shopInfo = await _shopify.GetShopInfoAsync(normalizedShop, tokenResult.AccessToken, ct);

            var connection = await _db.ShopifyConnections
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ShopDomain == shopInfo.MyshopifyDomain, ct);

            if (connection is null)
            {
                connection = new ShopifyConnection
                {
                    TenantId = tenantId,
                    ShopDomain = shopInfo.MyshopifyDomain,
                };
                _db.ShopifyConnections.Add(connection);
            }

            connection.ShopName = !string.IsNullOrWhiteSpace(shopInfo.Name) ? shopInfo.Name : shopInfo.MyshopifyDomain;
            connection.EncryptedAccessToken = _protector.Protect(tokenResult.AccessToken);
            connection.Scopes = tokenResult.Scope;
            connection.CreatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully connected Shopify store {Shop} for tenant {TenantId}", shopInfo.MyshopifyDomain, tenantId);

            return new ShopifyOAuthResult(true, ShopDomain: shopInfo.MyshopifyDomain);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Shopify OAuth callback.");
            return new ShopifyOAuthResult(false, ErrorMessage: ex.Message);
        }
    }

    public async Task<List<ShopifyConnectionDto>> GetConnectionsAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.ShopifyConnections
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => new ShopifyConnectionDto(c.Id, c.ShopDomain, c.ShopName, c.CreatedAtUtc))
            .ToListAsync(ct);
    }

    public async Task<bool> DisconnectAsync(Guid connectionId, Guid tenantId, CancellationToken ct = default)
    {
        var connection = await _db.ShopifyConnections
            .Include(c => c.MappedForms)
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.TenantId == tenantId, ct);

        if (connection is null)
            return false;

        foreach (var form in connection.MappedForms)
        {
            form.ShopifyConnectionId = null;
        }

        _db.ShopifyConnections.Remove(connection);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Disconnected Shopify store {Shop} for tenant {TenantId}", connection.ShopDomain, tenantId);

        return true;
    }

    public List<ShopifyFieldOptionDto> GetCustomerFields() => StandardFields;

    public async Task<List<ShopifyFieldOptionDto>> GetCustomerFieldsAsync(Guid? connectionId, Guid? tenantId, CancellationToken ct = default)
    {
        var result = new List<ShopifyFieldOptionDto>(StandardFields);
        var seenKeys = new HashSet<string>(StandardFields.Select(f => f.Key), StringComparer.OrdinalIgnoreCase);

        // Fetch live customer metafield definitions from Shopify store
        if (tenantId.HasValue)
        {
            try
            {
                ShopifyConnection? connection = null;
                if (connectionId.HasValue)
                {
                    connection = await _db.ShopifyConnections
                        .FirstOrDefaultAsync(c => c.Id == connectionId.Value && c.TenantId == tenantId.Value, ct);
                }
                else
                {
                    connection = await _db.ShopifyConnections
                        .FirstOrDefaultAsync(c => c.TenantId == tenantId.Value, ct);
                }

                if (connection is not null)
                {
                    var token = _protector.Unprotect(connection.EncryptedAccessToken);
                    var customFields = await _shopify.GetCustomerCustomFieldsAsync(connection.ShopDomain, token, ct);
                    foreach (var cf in customFields)
                    {
                        if (seenKeys.Add(cf.Key))
                        {
                            result.Add(cf);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch custom metafields for Shopify connection {ConnectionId}", connectionId);
            }
        }

        // Include any custom field keys already configured in ShopifyFieldMappings
        if (tenantId.HasValue)
        {
            try
            {
                var existingCustomMappings = await _db.ShopifyFieldMappings
                    .Where(m => m.TenantId == tenantId.Value && !string.IsNullOrWhiteSpace(m.ShopifyFieldKey))
                    .Select(m => m.ShopifyFieldKey)
                    .Distinct()
                    .ToListAsync(ct);

                foreach (var k in existingCustomMappings)
                {
                    if (seenKeys.Add(k))
                    {
                        result.Add(new ShopifyFieldOptionDto(
                            Key: k,
                            Label: FormatLabel(k),
                            Type: "Custom",
                            IsStandard: false,
                            Category: "Custom Field"
                        ));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed loading mapped custom fields for tenant {TenantId}", tenantId);
            }
        }

        return result;
    }

    private static string FormatLabel(string key)
    {
        var clean = key.Replace("custom.", "").Replace("_", " ");
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(clean);
    }
}

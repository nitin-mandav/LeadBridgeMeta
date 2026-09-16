using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Application.Ghl;
using LeadBridgeMeta.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeadBridgeMeta.Infrastructure.Ghl;

public class GhlConnectionService : IGhlConnectionService
{
    private readonly IAppDbContext _db;
    private readonly IGhlClient _ghl;
    private readonly ITokenProtector _protector;
    private readonly IOAuthStateService _state;
    private readonly ILogger<GhlConnectionService> _logger;

    public GhlConnectionService(
        IAppDbContext db,
        IGhlClient ghl,
        ITokenProtector protector,
        IOAuthStateService state,
        ILogger<GhlConnectionService> logger)
    {
        _db = db;
        _ghl = ghl;
        _protector = protector;
        _state = state;
        _logger = logger;
    }

    public string GetConnectUrl(Guid tenantId, string callbackRedirectUri)
    {
        var state = _state.CreateState(tenantId, "ghl-connect");
        return _ghl.BuildAuthorizeUrl(state, callbackRedirectUri);
    }

    public async Task<GhlOAuthCallbackResult> ProcessOAuthCallbackAsync(
        string code,
        string? state,
        string callbackRedirectUri,
        CancellationToken ct = default)
    {
        Guid tenantId = Guid.Empty;
        if (!string.IsNullOrWhiteSpace(state))
        {
            try
            {
                (tenantId, _) = _state.ValidateState(state);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OAuth state validation threw an exception; proceeding to fallback tenant resolution.");
            }
        }

        try
        {
            var token = await _ghl.ExchangeCodeForTokenAsync(code, callbackRedirectUri, ct);

            // If tenantId was not provided in state (e.g. direct installation from GHL Marketplace),
            // resolve tenant from existing location connection or fall back to the active tenant.
            if (tenantId == Guid.Empty)
            {
                var existingForLocation = await _db.GhlConnections
                    .FirstOrDefaultAsync(c => c.LocationId == token.LocationId, ct);

                if (existingForLocation is not null)
                {
                    tenantId = existingForLocation.TenantId;
                }
                else
                {
                    var defaultTenant = await _db.Tenants
                        .Where(t => t.IsActive && t.Name != "Acme Ads")
                        .OrderByDescending(t => t.CreatedAtUtc)
                        .FirstOrDefaultAsync(ct)
                        ?? await _db.Tenants.OrderByDescending(t => t.CreatedAtUtc).FirstOrDefaultAsync(ct);

                    if (defaultTenant is not null)
                    {
                        tenantId = defaultTenant.Id;
                    }
                    else
                    {
                        const string msg = "No tenant found to associate GoHighLevel connection with. Please initiate connection from the dashboard.";
                        return new GhlOAuthCallbackResult(false, null, msg);
                    }
                }
            }

            var connection = await _db.GhlConnections
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.LocationId == token.LocationId, ct);

            if (connection is null)
            {
                connection = new GhlConnection { TenantId = tenantId, LocationId = token.LocationId };
                _db.GhlConnections.Add(connection);
            }

            connection.CompanyId = token.CompanyId;
            connection.LocationName = token.LocationId;
            connection.EncryptedAccessToken = _protector.Protect(token.AccessToken);
            connection.EncryptedRefreshToken = _protector.Protect(token.RefreshToken);
            connection.AccessTokenExpiresAtUtc = token.ExpiresAtUtc;

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Successfully connected GHL location {LocationId} for tenant {TenantId}.", token.LocationId, tenantId);

            return new GhlOAuthCallbackResult(true, token.LocationId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing GoHighLevel OAuth callback.");
            return new GhlOAuthCallbackResult(false, null, ex.Message);
        }
    }

    public async Task<List<GhlConnectionDto>> GetConnectionsAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.GhlConnections
            .Where(c => c.TenantId == tenantId)
            .Select(c => new GhlConnectionDto(c.Id, c.LocationId, c.LocationName, c.AccessTokenExpiresAtUtc))
            .ToListAsync(ct);
    }

    private static readonly List<GhlFieldOptionDto> GhlOpportunityFields = new()
    {
        new GhlFieldOptionDto("opportunity.name", "Opportunity name", "Single line", IsStandard: true, Category: "Opportunity"),
        new GhlFieldOptionDto("opportunity.pipeline_id", "Pipeline", "Dropdown", IsStandard: true, Category: "Opportunity"),
        new GhlFieldOptionDto("opportunity.pipeline_stage_id", "Stage", "Dropdown", IsStandard: true, Category: "Opportunity"),
        new GhlFieldOptionDto("opportunity.status", "Status", "Dropdown", IsStandard: true, Category: "Opportunity"),
        new GhlFieldOptionDto("opportunity.monetary_value", "Lead value", "Monetary", IsStandard: true, Category: "Opportunity"),
        new GhlFieldOptionDto("opportunity.assigned_to", "Owner", "Dropdown", IsStandard: true, Category: "Opportunity"),
        new GhlFieldOptionDto("opportunity.source", "Opportunity source", "Single line", IsStandard: true, Category: "Opportunity"),
        new GhlFieldOptionDto("opportunity.lost_reason", "Lost reason", "Dropdown", IsStandard: true, Category: "Opportunity")
    };

    private static readonly List<GhlFieldOptionDto> GhlContactSystemFields = new()
    {
        new GhlFieldOptionDto("contact.first_name", "First name", "Single line", IsStandard: true, Category: "Contact"),
        new GhlFieldOptionDto("contact.last_name", "Last name", "Single line", IsStandard: true, Category: "Contact"),
        new GhlFieldOptionDto("contact.name", "Full name", "Single line", IsStandard: true, Category: "Contact"),
        new GhlFieldOptionDto("contact.email", "Email", "Email", IsStandard: true, Category: "Contact"),
        new GhlFieldOptionDto("contact.phone", "Phone", "Phone", IsStandard: true, Category: "Contact"),
        new GhlFieldOptionDto("contact.company_name", "Business name", "Single line", IsStandard: true, Category: "Contact"),
        new GhlFieldOptionDto("contact.address1", "Street address", "Single line", IsStandard: true, Category: "Contact"),
        new GhlFieldOptionDto("contact.city", "City", "Single line", IsStandard: true, Category: "Contact"),
        new GhlFieldOptionDto("contact.state", "State", "Single line", IsStandard: true, Category: "Contact"),
        new GhlFieldOptionDto("contact.postal_code", "Postal code", "Single line", IsStandard: true, Category: "Contact"),
        new GhlFieldOptionDto("contact.country", "Country", "Single line", IsStandard: true, Category: "Contact"),
        new GhlFieldOptionDto("contact.website", "Website", "Single line", IsStandard: true, Category: "Contact"),
        new GhlFieldOptionDto("contact.date_of_birth", "Date of birth", "Date", IsStandard: true, Category: "Contact"),
        new GhlFieldOptionDto("contact.timezone", "Timezone", "Timezone", IsStandard: true, Category: "Contact"),
        new GhlFieldOptionDto("contact.source", "Source", "Single line", IsStandard: true, Category: "Contact")
    };

    public async Task<List<GhlFieldOptionDto>> GetLocationFieldsAsync(Guid connectionId, Guid tenantId, CancellationToken ct = default)
    {
        var result = new List<GhlFieldOptionDto>();
        var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var connection = await _db.GhlConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.TenantId == tenantId, ct);

        if (connection != null)
        {
            try
            {
                var accessToken = _protector.Unprotect(connection.EncryptedAccessToken);

                // If token is expiring soon or expired, attempt refresh
                if (connection.AccessTokenExpiresAtUtc <= DateTime.UtcNow.AddMinutes(5) && !string.IsNullOrEmpty(connection.EncryptedRefreshToken))
                {
                    try
                    {
                        var refreshToken = _protector.Unprotect(connection.EncryptedRefreshToken);
                        var refreshed = await _ghl.RefreshTokenAsync(refreshToken, ct);
                        connection.EncryptedAccessToken = _protector.Protect(refreshed.AccessToken);
                        connection.EncryptedRefreshToken = _protector.Protect(refreshed.RefreshToken);
                        connection.AccessTokenExpiresAtUtc = refreshed.ExpiresAtUtc;
                        await _db.SaveChangesAsync(ct);
                        accessToken = refreshed.AccessToken;
                    }
                    catch (Exception refreshEx)
                    {
                        _logger.LogWarning(refreshEx, "Failed to refresh GHL access token for connection {ConnectionId}", connectionId);
                    }
                }

                // 1. Fetch live custom fields directly from GHL API (e.g. asdfghjk, name1)
                try
                {
                    var customFields = await _ghl.GetCustomFieldsAsync(accessToken, connection.LocationId, ct);
                    foreach (var cf in customFields)
                    {
                        var cleanKey = !string.IsNullOrWhiteSpace(cf.FieldKey)
                            ? cf.FieldKey.Replace("{{", "").Replace("}}", "").Trim()
                            : cf.Id;

                        var isOpp = string.Equals(cf.Model, "opportunity", StringComparison.OrdinalIgnoreCase);

                        if (seenKeys.Add(cleanKey))
                        {
                            result.Add(new GhlFieldOptionDto(
                                Key: cleanKey,
                                Label: cf.Name,
                                DataType: !string.IsNullOrWhiteSpace(cf.DataType) ? cf.DataType : (!string.IsNullOrWhiteSpace(cf.Model) ? cf.Model : "CUSTOM"),
                                IsStandard: false,
                                Category: isOpp ? "Opportunity" : "Contact"
                            ));
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch custom fields for location {LocationId}", connection.LocationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to decrypt token for location {LocationId}", connection.LocationId);
            }
        }

        // 2. Add Opportunity system fields matching GHL's Opportunity fields
        foreach (var oppField in GhlOpportunityFields)
        {
            if (seenKeys.Add(oppField.Key))
            {
                result.Add(oppField);
            }
        }

        // 3. Add Contact system fields matching GHL's Contact fields
        foreach (var contactField in GhlContactSystemFields)
        {
            if (seenKeys.Add(contactField.Key))
            {
                result.Add(contactField);
            }
        }

        return result;
    }

    public async Task<bool> DisconnectAsync(Guid connectionId, Guid tenantId, CancellationToken ct = default)
    {
        var connection = await _db.GhlConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId && c.TenantId == tenantId, ct);

        if (connection is null)
            return false;

        // Unlink any Meta lead forms referencing this GHL connection
        var mappedForms = await _db.MetaLeadForms
            .Where(f => f.GhlConnectionId == connectionId)
            .ToListAsync(ct);

        foreach (var form in mappedForms)
        {
            form.GhlConnectionId = null;
        }

        _db.GhlConnections.Remove(connection);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("GHL Connection {ConnectionId} (Location {LocationId}) disconnected for tenant {TenantId}.",
            connectionId, connection.LocationId, tenantId);

        return true;
    }
}

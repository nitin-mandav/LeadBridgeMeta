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
}

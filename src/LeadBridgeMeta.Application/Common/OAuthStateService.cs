using System.Text.Json;

namespace LeadBridgeMeta.Application.Common;

public interface IOAuthStateService
{
    string CreateState(Guid tenantId, string purpose);
    (Guid TenantId, string Purpose) ValidateState(string state);
}

/// <summary>Encodes {tenantId, purpose, expiry} into an opaque, encrypted "state" value passed through the
/// third-party OAuth redirect. Lets the callback (which arrives with no auth header/session) recover which
/// tenant initiated the connection, without a server-side session store.</summary>
public class OAuthStateService : IOAuthStateService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    private readonly ITokenProtector _protector;

    public OAuthStateService(ITokenProtector protector)
    {
        _protector = protector;
    }

    public string CreateState(Guid tenantId, string purpose)
    {
        var payload = new StatePayload(tenantId, purpose, DateTime.UtcNow.Add(Lifetime));
        return _protector.Protect(JsonSerializer.Serialize(payload));
    }

    public (Guid TenantId, string Purpose) ValidateState(string state)
    {
        StatePayload? payload = null;
        try
        {
            var raw = _protector.Unprotect(state);
            payload = JsonSerializer.Deserialize<StatePayload>(raw);
        }
        catch
        {
            // If query string decoding replaced '+' with ' ', retry after restoring '+'
            if (state.Contains(' '))
            {
                try
                {
                    var raw = _protector.Unprotect(state.Replace(' ', '+'));
                    payload = JsonSerializer.Deserialize<StatePayload>(raw);
                }
                catch
                {
                    // ignored
                }
            }
        }

        if (payload is null)
            throw new InvalidOperationException("Invalid or tampered OAuth state.");

        if (payload.ExpiresAtUtc < DateTime.UtcNow)
            throw new InvalidOperationException("OAuth state has expired; please restart the connection.");

        return (payload.TenantId, payload.Purpose);
    }

    private record StatePayload(Guid TenantId, string Purpose, DateTime ExpiresAtUtc);
}

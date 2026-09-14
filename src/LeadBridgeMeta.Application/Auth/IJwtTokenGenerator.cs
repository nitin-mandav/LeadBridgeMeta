namespace LeadBridgeMeta.Application.Auth;

public interface IJwtTokenGenerator
{
    string GenerateToken(Guid userId, Guid tenantId, string email, IReadOnlyList<string> roles);
}

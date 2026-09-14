using LeadBridgeMeta.Application.Common;
using Microsoft.AspNetCore.Http;

namespace LeadBridgeMeta.Infrastructure.Auth;

public class HttpCurrentUserContext : ICurrentUserContext
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUserContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private System.Security.Claims.ClaimsPrincipal User =>
        _accessor.HttpContext?.User ?? throw new InvalidOperationException("No active HTTP context.");

    public Guid UserId => Guid.Parse(User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!.Value);

    public Guid TenantId => Guid.Parse(User.FindFirst("tenant_id")!.Value);

    public string Email => User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)!.Value;
}

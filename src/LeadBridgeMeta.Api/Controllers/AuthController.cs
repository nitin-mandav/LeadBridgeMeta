using LeadBridgeMeta.Application.Auth;
using LeadBridgeMeta.Domain.Entities;
using LeadBridgeMeta.Infrastructure.Identity;
using LeadBridgeMeta.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LeadBridgeMeta.Api.Controllers;

public record RegisterRequest(string CompanyName, string Email, string Password, string DisplayName);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string AccessToken, string Email, string DisplayName, Guid TenantId);

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IJwtTokenGenerator _jwt;

    public AuthController(UserManager<ApplicationUser> userManager, AppDbContext db, IJwtTokenGenerator jwt)
    {
        _userManager = userManager;
        _db = db;
        _jwt = jwt;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        if (await _userManager.FindByEmailAsync(request.Email) is not null)
            return Conflict("An account with that email already exists.");

        var tenant = new Tenant { Name = request.CompanyName };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            TenantId = tenant.Id,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        var token = _jwt.GenerateToken(user.Id, tenant.Id, user.Email!, roles: []);
        return Ok(new AuthResponse(token, user.Email!, user.DisplayName, tenant.Id));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized("Invalid email or password.");

        var roles = await _userManager.GetRolesAsync(user);
        var token = _jwt.GenerateToken(user.Id, user.TenantId, user.Email!, roles.ToList());
        return Ok(new AuthResponse(token, user.Email!, user.DisplayName, user.TenantId));
    }
}

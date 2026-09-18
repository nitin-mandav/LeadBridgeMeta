using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Application.Shopify;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace LeadBridgeMeta.Api.Controllers;

[ApiController]
[Route("api/shopify")]
public class ShopifyController : ControllerBase
{
    private readonly IShopifyConnectionService _shopifyService;
    private readonly IConfiguration _config;
    private readonly ILogger<ShopifyController> _logger;

    public ShopifyController(IShopifyConnectionService shopifyService, IConfiguration config, ILogger<ShopifyController> logger)
    {
        _shopifyService = shopifyService;
        _config = config;
        _logger = logger;
    }

    private string FrontendConnectionsUrl => $"{_config["App:FrontendBaseUrl"]}/connections";

    private string GetEffectiveRedirectUri()
    {
        // 1. Explicit override in Shopify:RedirectUri if specified
        var configured = _config["Shopify:RedirectUri"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();

        // 2. Check if request came through ngrok or reverse proxy
        var forwardedProto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var forwardedHost = Request.Headers["X-Forwarded-Host"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedProto) && !string.IsNullOrWhiteSpace(forwardedHost))
        {
            return $"{forwardedProto}://{forwardedHost}/api/shopify/callback";
        }

        // 3. Fallback to App:ApiBaseUrl if configured, otherwise current Request
        var baseUrl = _config["App:ApiBaseUrl"];
        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            return $"{baseUrl.TrimEnd('/')}/api/shopify/callback";
        }

        return $"{Request.Scheme}://{Request.Host}/api/shopify/callback";
    }

    /// <summary>Generate OAuth authorization URL for a specific Shopify store.</summary>
    [HttpGet("connect-url"), Authorize]
    public ActionResult<object> GetConnectUrl([FromQuery] string shop, [FromServices] ICurrentUserContext currentUser)
    {
        if (string.IsNullOrWhiteSpace(shop))
            return BadRequest(new { error = "Shop domain is required (e.g. your-store.myshopify.com)." });

        var redirectUri = GetEffectiveRedirectUri();
        _logger.LogInformation("Generating Shopify OAuth URL for shop '{Shop}' with redirect URI: '{RedirectUri}'", shop, redirectUri);

        var url = _shopifyService.GetConnectUrl(shop.Trim(), currentUser.TenantId, redirectUri);
        return Ok(new { url, redirectUri });
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string code,
        [FromQuery] string shop,
        [FromQuery] string? state,
        [FromQuery] bool? json,
        CancellationToken ct)
    {
        var wantsJson = json == true || Request.Headers.Accept.ToString().Contains("application/json");

        var effectiveRedirectUri = GetEffectiveRedirectUri();
        var result = await _shopifyService.ProcessOAuthCallbackAsync(code, shop, state, effectiveRedirectUri, ct);

        if (!result.Success)
        {
            var requestUri = $"{Request.Scheme}://{Request.Host}{Request.Path}";
            if (effectiveRedirectUri != requestUri)
            {
                result = await _shopifyService.ProcessOAuthCallbackAsync(code, shop, state, requestUri, ct);
            }
        }

        if (!result.Success)
        {
            if (wantsJson)
                return BadRequest(new { error = result.ErrorMessage });

            return Redirect($"{FrontendConnectionsUrl}?shopify_error={Uri.EscapeDataString(result.ErrorMessage ?? "OAuth callback failed")}");
        }

        if (wantsJson)
            return Ok(new { success = true, shopDomain = result.ShopDomain });

        return Redirect($"{FrontendConnectionsUrl}?shopify_connected=1");
    }

    [HttpGet("connections"), Authorize]
    public async Task<ActionResult<List<ShopifyConnectionDto>>> GetConnections([FromServices] ICurrentUserContext currentUser, CancellationToken ct)
    {
        var connections = await _shopifyService.GetConnectionsAsync(currentUser.TenantId, ct);
        return Ok(connections);
    }

    [HttpGet("fields"), Authorize]
    public async Task<ActionResult<List<ShopifyFieldOptionDto>>> GetFields(
        [FromQuery] Guid? connectionId,
        [FromServices] ICurrentUserContext currentUser,
        CancellationToken ct)
    {
        var fields = await _shopifyService.GetCustomerFieldsAsync(connectionId, currentUser.TenantId, ct);
        return Ok(fields);
    }

    [HttpGet("connections/{connectionId:guid}/fields"), Authorize]
    public async Task<ActionResult<List<ShopifyFieldOptionDto>>> GetConnectionFields(
        Guid connectionId,
        [FromServices] ICurrentUserContext currentUser,
        CancellationToken ct)
    {
        var fields = await _shopifyService.GetCustomerFieldsAsync(connectionId, currentUser.TenantId, ct);
        return Ok(fields);
    }

    [HttpDelete("connections/{connectionId:guid}"), Authorize]
    public async Task<IActionResult> Disconnect(
        Guid connectionId,
        [FromServices] ICurrentUserContext currentUser,
        CancellationToken ct)
    {
        var success = await _shopifyService.DisconnectAsync(connectionId, currentUser.TenantId, ct);
        if (!success)
            return NotFound(new { error = "Connection not found or already removed." });

        return Ok(new { success = true, message = "Shopify store disconnected successfully." });
    }
}

using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Application.Mappings;
using LeadBridgeMeta.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeadBridgeMeta.Api.Controllers;

public record SetFormGhlConnectionRequest(Guid GhlConnectionId);
public record SetFormShopifyConnectionRequest(Guid ShopifyConnectionId);
public record FieldMappingRequest(Guid? MetaLeadFormId, string MetaFieldKey, GhlTargetFieldType TargetType, string GhlFieldKey);
public record FieldMappingResponse(Guid Id, Guid? MetaLeadFormId, string MetaFieldKey, GhlTargetFieldType TargetType, string GhlFieldKey);
public record ShopifyFieldMappingRequest(Guid? MetaLeadFormId, string MetaFieldKey, ShopifyTargetFieldType TargetType, string ShopifyFieldKey);
public record MetaFormQuestionResponse(string Key, string Label, string? Type);

[ApiController]
[Route("api/mappings")]
[Authorize]
public class MappingsController : ControllerBase
{
    private readonly IMappingService _mappingService;
    private readonly IShopifyMappingService _shopifyMappingService;
    private readonly ICurrentUserContext _currentUser;

    public MappingsController(
        IMappingService mappingService,
        IShopifyMappingService shopifyMappingService,
        ICurrentUserContext currentUser)
    {
        _mappingService = mappingService;
        _shopifyMappingService = shopifyMappingService;
        _currentUser = currentUser;
    }

    [HttpPut("forms/{formId:guid}/ghl-connection")]
    public async Task<IActionResult> SetFormGhlConnection(Guid formId, SetFormGhlConnectionRequest request, CancellationToken ct)
    {
        var result = await _mappingService.SetFormGhlConnectionAsync(formId, request.GhlConnectionId, _currentUser.TenantId, ct);
        if (result is null)
            return NotFound();

        if (result == false)
            return BadRequest("GHL connection not found for this tenant.");

        return NoContent();
    }

    [HttpPut("forms/{formId:guid}/shopify-connection")]
    public async Task<IActionResult> SetFormShopifyConnection(Guid formId, SetFormShopifyConnectionRequest request, CancellationToken ct)
    {
        var result = await _shopifyMappingService.SetFormShopifyConnectionAsync(formId, request.ShopifyConnectionId, _currentUser.TenantId, ct);
        if (result is null)
            return NotFound();

        if (result == false)
            return BadRequest("Shopify connection not found for this tenant.");

        return NoContent();
    }

    [HttpGet("forms/{formId:guid}/fields")]
    public async Task<ActionResult<List<MetaFormQuestionResponse>>> GetFormFields(Guid formId, CancellationToken ct)
    {
        var questions = await _mappingService.GetFormQuestionsAsync(formId, _currentUser.TenantId, ct);
        if (questions is null)
            return NotFound();

        var response = questions.Select(q => new MetaFormQuestionResponse(q.Key, q.Label, q.Type)).ToList();
        return Ok(response);
    }

    [HttpGet("fields")]
    public async Task<ActionResult<List<FieldMappingResponse>>> GetFieldMappings([FromQuery] Guid? formId, CancellationToken ct)
    {
        var mappings = await _mappingService.GetFieldMappingsAsync(formId, _currentUser.TenantId, ct);
        var response = mappings.Select(m => new FieldMappingResponse(m.Id, m.MetaLeadFormId, m.MetaFieldKey, m.TargetType, m.GhlFieldKey)).ToList();
        return Ok(response);
    }

    [HttpPost("fields")]
    public async Task<ActionResult<FieldMappingResponse>> UpsertFieldMapping(FieldMappingRequest request, CancellationToken ct)
    {
        var result = await _mappingService.UpsertFieldMappingAsync(
            request.MetaLeadFormId,
            request.MetaFieldKey,
            request.TargetType,
            request.GhlFieldKey,
            _currentUser.TenantId,
            ct);

        return Ok(new FieldMappingResponse(result.Id, result.MetaLeadFormId, result.MetaFieldKey, result.TargetType, result.GhlFieldKey));
    }

    [HttpDelete("fields/{id:guid}")]
    public async Task<IActionResult> DeleteFieldMapping(Guid id, CancellationToken ct)
    {
        var deleted = await _mappingService.DeleteFieldMappingAsync(id, _currentUser.TenantId, ct);
        if (!deleted)
            return NotFound();

        return NoContent();
    }

    [HttpGet("shopify/fields")]
    public async Task<ActionResult<List<ShopifyFieldMappingDto>>> GetShopifyFieldMappings([FromQuery] Guid? formId, CancellationToken ct)
    {
        var mappings = await _shopifyMappingService.GetFieldMappingsAsync(formId, _currentUser.TenantId, ct);
        return Ok(mappings);
    }

    [HttpPost("shopify/fields")]
    public async Task<ActionResult<ShopifyFieldMappingDto>> UpsertShopifyFieldMapping(ShopifyFieldMappingRequest request, CancellationToken ct)
    {
        var result = await _shopifyMappingService.UpsertFieldMappingAsync(
            request.MetaLeadFormId,
            request.MetaFieldKey,
            request.TargetType,
            request.ShopifyFieldKey,
            _currentUser.TenantId,
            ct);

        return Ok(result);
    }

    [HttpDelete("shopify/fields/{id:guid}")]
    public async Task<IActionResult> DeleteShopifyFieldMapping(Guid id, CancellationToken ct)
    {
        var deleted = await _shopifyMappingService.DeleteFieldMappingAsync(id, _currentUser.TenantId, ct);
        if (!deleted)
            return NotFound();

        return NoContent();
    }
}

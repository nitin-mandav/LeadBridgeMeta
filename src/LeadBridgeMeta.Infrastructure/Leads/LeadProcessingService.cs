using System.Text.Json;
using LeadBridgeMeta.Application.Common;
using LeadBridgeMeta.Application.Ghl;
using LeadBridgeMeta.Application.Leads;
using LeadBridgeMeta.Application.Meta;
using LeadBridgeMeta.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeadBridgeMeta.Infrastructure.Leads;

public class LeadProcessingService : ILeadProcessingService
{
    // Best-effort defaults applied when the tenant hasn't defined an explicit mapping for a Meta field key.
    private static readonly Dictionary<string, string> DefaultStandardFieldMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["email"] = "email",
        ["first_name"] = "firstName",
        ["last_name"] = "lastName",
        ["phone_number"] = "phone",
        ["city"] = "city",
        ["state"] = "state",
        ["zip_code"] = "postalCode",
        ["post_code"] = "postalCode",
        ["country"] = "country",
    };

    private readonly IAppDbContext _db;
    private readonly IMetaGraphClient _meta;
    private readonly IGhlClient _ghl;
    private readonly ITokenProtector _protector;
    private readonly ILogger<LeadProcessingService> _logger;

    public LeadProcessingService(
        IAppDbContext db,
        IMetaGraphClient meta,
        IGhlClient ghl,
        ITokenProtector protector,
        ILogger<LeadProcessingService> logger)
    {
        _db = db;
        _meta = meta;
        _ghl = ghl;
        _protector = protector;
        _logger = logger;
    }

    public async Task ProcessLeadgenNotificationAsync(string pageId, string formId, string leadgenId, string rawWebhookPayload, CancellationToken ct = default)
    {
        if (await _db.LeadEvents.AnyAsync(l => l.LeadgenId == leadgenId, ct))
        {
            _logger.LogInformation("Leadgen {LeadgenId} already recorded, skipping duplicate webhook delivery.", leadgenId);
            return;
        }

        var page = await _db.MetaPages
            .Include(p => p.MetaConnection)
            .FirstOrDefaultAsync(p => p.PageId == pageId, ct);

        if (page is null)
        {
            _logger.LogWarning("Received leadgen webhook for untracked page {PageId}.", pageId);
            return;
        }

        var form = await _db.MetaLeadForms
            .Include(f => f.FieldMappings)
            .Include(f => f.GhlConnection)
            .Include(f => f.MetaPage)
            .FirstOrDefaultAsync(f => f.FormId == formId, ct);

        if (form is null)
        {
            // New form Meta hasn't been discovered/mapped in our UI yet - track it so it shows up as "needs mapping".
            form = new MetaLeadForm { MetaPageId = page.Id, FormId = formId, FormName = $"(new form {formId})", IsActive = true };
            _db.MetaLeadForms.Add(form);
            await _db.SaveChangesAsync(ct);
        }

        var tenantId = page.MetaConnection!.TenantId;
        var leadEvent = new LeadEvent
        {
            TenantId = tenantId,
            MetaLeadFormId = form.Id,
            LeadgenId = leadgenId,
            RawWebhookPayload = rawWebhookPayload,
            Status = LeadEventStatus.Received,
        };
        _db.LeadEvents.Add(leadEvent);
        await _db.SaveChangesAsync(ct);

        await ProcessAsync(leadEvent, form, page, ct);
    }

    public async Task RetryAsync(Guid leadEventId, CancellationToken ct = default)
    {
        var leadEvent = await _db.LeadEvents.FirstOrDefaultAsync(l => l.Id == leadEventId, ct)
                         ?? throw new InvalidOperationException($"Lead event {leadEventId} not found.");

        var form = await _db.MetaLeadForms
            .Include(f => f.FieldMappings)
            .Include(f => f.GhlConnection)
            .Include(f => f.MetaPage).ThenInclude(p => p!.MetaConnection)
            .FirstOrDefaultAsync(f => f.Id == leadEvent.MetaLeadFormId, ct)
                   ?? throw new InvalidOperationException($"Meta lead form {leadEvent.MetaLeadFormId} not found.");

        leadEvent.RetryCount++;
        await ProcessAsync(leadEvent, form, form.MetaPage!, ct);
    }

    private async Task ProcessAsync(LeadEvent leadEvent, MetaLeadForm form, MetaPage page, CancellationToken ct)
    {
        try
        {
            if (form.GhlConnectionId is null || form.GhlConnection is null)
            {
                leadEvent.Status = LeadEventStatus.Skipped;
                leadEvent.ErrorMessage = "This Meta lead form is not mapped to a GHL location yet.";
                await _db.SaveChangesAsync(ct);
                return;
            }

            var pageAccessToken = _protector.Unprotect(page.EncryptedPageAccessToken);
            var leadData = await _meta.GetLeadDataAsync(leadEvent.LeadgenId, pageAccessToken, ct);

            leadEvent.RawLeadDataJson = JsonSerializer.Serialize(leadData);
            leadEvent.Status = LeadEventStatus.Fetched;
            await _db.SaveChangesAsync(ct);

            var mappings = await ResolveMappingsAsync(form, ct);
            var upsertRequest = BuildGhlRequest(form.GhlConnection.LocationId, leadData, mappings);

            var accessToken = await GetValidGhlAccessTokenAsync(form.GhlConnection, ct);
            var contact = await _ghl.UpsertContactAsync(accessToken, upsertRequest, ct);

            leadEvent.GhlContactId = contact.ContactId;
            leadEvent.Status = LeadEventStatus.Sent;
            leadEvent.ProcessedAtUtc = DateTime.UtcNow;
            leadEvent.ErrorMessage = null;
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Lead {LeadgenId} sent to GHL contact {ContactId}.", leadEvent.LeadgenId, contact.ContactId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process lead {LeadgenId}.", leadEvent.LeadgenId);
            leadEvent.Status = LeadEventStatus.Failed;
            leadEvent.ErrorMessage = ex.Message;
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<List<FieldMapping>> ResolveMappingsAsync(MetaLeadForm form, CancellationToken ct)
    {
        var formSpecific = form.FieldMappings.ToList();
        var tenantDefaults = await _db.FieldMappings
            .Where(m => m.TenantId == GetTenantId(form) && m.MetaLeadFormId == null)
            .ToListAsync(ct);

        // Form-specific mappings win over tenant-wide defaults for the same Meta field key.
        var byKey = tenantDefaults.ToDictionary(m => m.MetaFieldKey, StringComparer.OrdinalIgnoreCase);
        foreach (var m in formSpecific)
            byKey[m.MetaFieldKey] = m;

        return byKey.Values.ToList();
    }

    private static Guid GetTenantId(MetaLeadForm form) => form.MetaPage!.MetaConnection!.TenantId;

    private static GhlContactUpsertRequest BuildGhlRequest(string locationId, MetaLeadDataDto lead, List<FieldMapping> mappings)
    {
        string? firstName = null, lastName = null, name = null, email = null, phone = null;
        string? companyName = null, address1 = null, city = null, state = null, postalCode = null;
        string? country = null, website = null, dateOfBirth = null;
        var customFields = new Dictionary<string, string>();
        var tags = new List<string>();

        foreach (var field in lead.FieldData)
        {
            var value = field.Values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(value)) continue;

            var explicitMapping = mappings.FirstOrDefault(m => string.Equals(m.MetaFieldKey, field.Name, StringComparison.OrdinalIgnoreCase));

            if (explicitMapping is not null)
            {
                switch (explicitMapping.TargetType)
                {
                    case GhlTargetFieldType.StandardContactField:
                        AssignStandard(explicitMapping.GhlFieldKey, value, ref firstName, ref lastName, ref name, ref email, ref phone, ref companyName, ref address1, ref city, ref state, ref postalCode, ref country, ref website, ref dateOfBirth);
                        break;
                    case GhlTargetFieldType.CustomField:
                        customFields[explicitMapping.GhlFieldKey] = value;
                        break;
                    case GhlTargetFieldType.Tag:
                        tags.Add(value);
                        break;
                }
                continue;
            }

            if (DefaultStandardFieldMap.TryGetValue(field.Name, out var standardKey))
            {
                AssignStandard(standardKey, value, ref firstName, ref lastName, ref name, ref email, ref phone, ref companyName, ref address1, ref city, ref state, ref postalCode, ref country, ref website, ref dateOfBirth);
                continue;
            }

            if (string.Equals(field.Name, "full_name", StringComparison.OrdinalIgnoreCase) && firstName is null)
            {
                var parts = value.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                firstName = parts.ElementAtOrDefault(0);
                lastName = parts.ElementAtOrDefault(1);
                name = value;
                continue;
            }

            // Unmapped custom question: keep the raw answer as a custom field keyed by the question id, so nothing is silently dropped.
            customFields[field.Name] = value;
        }

        return new GhlContactUpsertRequest(
            LocationId: locationId,
            FirstName: firstName,
            LastName: lastName,
            Name: name,
            Email: email,
            Phone: phone,
            CompanyName: companyName,
            Address1: address1,
            City: city,
            State: state,
            PostalCode: postalCode,
            Country: country,
            Website: website,
            DateOfBirth: dateOfBirth,
            CustomFields: customFields,
            Tags: tags,
            SourceLabel: "Meta Lead Ads");
    }

    private static void AssignStandard(
        string standardKey,
        string value,
        ref string? firstName,
        ref string? lastName,
        ref string? name,
        ref string? email,
        ref string? phone,
        ref string? companyName,
        ref string? address1,
        ref string? city,
        ref string? state,
        ref string? postalCode,
        ref string? country,
        ref string? website,
        ref string? dateOfBirth)
    {
        var normalized = standardKey.ToLowerInvariant().Replace("contact.", "").Replace("_", "");
        switch (normalized)
        {
            case "firstname": firstName = value; break;
            case "lastname": lastName = value; break;
            case "name":
            case "fullname": name = value; break;
            case "email": email = value; break;
            case "phone": phone = value; break;
            case "companyname": companyName = value; break;
            case "address1":
            case "address":
            case "street": address1 = value; break;
            case "city": city = value; break;
            case "state": state = value; break;
            case "postalcode":
            case "zip":
            case "zipcode": postalCode = value; break;
            case "country": country = value; break;
            case "website": website = value; break;
            case "dateofbirth":
            case "dob": dateOfBirth = value; break;
        }
    }

    private async Task<string> GetValidGhlAccessTokenAsync(GhlConnection connection, CancellationToken ct)
    {
        if (connection.AccessTokenExpiresAtUtc > DateTime.UtcNow.AddMinutes(2))
            return _protector.Unprotect(connection.EncryptedAccessToken);

        var refreshToken = _protector.Unprotect(connection.EncryptedRefreshToken);
        var refreshed = await _ghl.RefreshTokenAsync(refreshToken, ct);

        connection.EncryptedAccessToken = _protector.Protect(refreshed.AccessToken);
        connection.EncryptedRefreshToken = _protector.Protect(refreshed.RefreshToken);
        connection.AccessTokenExpiresAtUtc = refreshed.ExpiresAtUtc;
        await _db.SaveChangesAsync(ct);

        return refreshed.AccessToken;
    }
}

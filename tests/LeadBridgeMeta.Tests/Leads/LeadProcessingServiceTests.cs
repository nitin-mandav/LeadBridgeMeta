using LeadBridgeMeta.Application.Ghl;
using LeadBridgeMeta.Application.Meta;
using LeadBridgeMeta.Domain.Entities;
using LeadBridgeMeta.Infrastructure.Ghl;
using LeadBridgeMeta.Infrastructure.Leads;
using LeadBridgeMeta.Infrastructure.Persistence;
using LeadBridgeMeta.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace LeadBridgeMeta.Tests.Leads;

public class LeadProcessingServiceTests
{
    private static (AppDbContext Db, Tenant Tenant, MetaPage Page, MetaLeadForm Form, GhlConnection? Ghl) SeedForm(
        bool mapToGhl, AppDbContext? db = null)
    {
        db ??= InMemoryDbContextFactory.Create();

        var tenant = new Tenant { Name = "Acme Ads" };
        var connection = new MetaConnection { TenantId = tenant.Id, FacebookUserId = "fb-user-1", FacebookUserName = "Jane" };
        var page = new MetaPage { MetaConnectionId = connection.Id, PageId = "page-1", PageName = "Acme Page", EncryptedPageAccessToken = "page-token" };
        var form = new MetaLeadForm { MetaPageId = page.Id, FormId = "form-1", FormName = "Contact Us" };

        GhlConnection? ghl = null;
        if (mapToGhl)
        {
            ghl = new GhlConnection
            {
                TenantId = tenant.Id,
                LocationId = "loc-1",
                LocationName = "loc-1",
                EncryptedAccessToken = "ghl-access",
                EncryptedRefreshToken = "ghl-refresh",
                AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
            };
            form.GhlConnectionId = ghl.Id;
        }

        connection.Pages.Add(page);
        page.LeadForms.Add(form);

        db.Tenants.Add(tenant);
        db.MetaConnections.Add(connection);
        db.MetaPages.Add(page);
        db.MetaLeadForms.Add(form);
        if (ghl is not null) db.GhlConnections.Add(ghl);
        db.SaveChanges();

        return (db, tenant, page, form, ghl);
    }

    private static MetaLeadDataDto SampleLead(string leadgenId, params (string Name, string Value)[] fields) =>
        new(leadgenId, "form-1", "page-1", DateTime.UtcNow,
            fields.Select(f => new MetaLeadFieldData(f.Name, new[] { f.Value })).ToList());

    [Fact]
    public async Task ProcessLeadgenNotification_UnmappedForm_MarksSkippedWithoutCallingGhl()
    {
        var (db, _, page, form, _) = SeedForm(mapToGhl: false);
        var meta = new Mock<IMetaGraphClient>();
        var ghl = new Mock<IGhlClient>();

        var sut = new LeadProcessingService(db, meta.Object, ghl.Object, new PassThroughTokenProtector(), NullLogger<LeadProcessingService>.Instance);

        await sut.ProcessLeadgenNotificationAsync(page.PageId, form.FormId, "lead-1", "{}");

        var leadEvent = Assert.Single(db.LeadEvents);
        Assert.Equal(LeadEventStatus.Skipped, leadEvent.Status);
        Assert.Contains("not mapped", leadEvent.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        ghl.Verify(g => g.UpsertContactAsync(It.IsAny<string>(), It.IsAny<GhlContactUpsertRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ProcessLeadgenNotification_DuplicateLeadgenId_DoesNotCreateSecondEvent()
    {
        var (db, _, page, form, _) = SeedForm(mapToGhl: false);
        var meta = new Mock<IMetaGraphClient>();
        var ghl = new Mock<IGhlClient>();
        var sut = new LeadProcessingService(db, meta.Object, ghl.Object, new PassThroughTokenProtector(), NullLogger<LeadProcessingService>.Instance);

        await sut.ProcessLeadgenNotificationAsync(page.PageId, form.FormId, "lead-1", "{}");
        await sut.ProcessLeadgenNotificationAsync(page.PageId, form.FormId, "lead-1", "{}");

        Assert.Single(db.LeadEvents);
    }

    [Fact]
    public async Task ProcessLeadgenNotification_MappedForm_FetchesLeadAndUpsertsGhlContact()
    {
        var (db, _, page, form, ghlConn) = SeedForm(mapToGhl: true);
        var meta = new Mock<IMetaGraphClient>();
        var ghl = new Mock<IGhlClient>();

        meta.Setup(m => m.GetLeadDataAsync("lead-1", "page-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleLead("lead-1", ("email", "jane@example.com"), ("first_name", "Jane"), ("phone_number", "+15551234567")));

        GhlContactUpsertRequest? captured = null;
        ghl.Setup(g => g.UpsertContactAsync("ghl-access", It.IsAny<GhlContactUpsertRequest>(), It.IsAny<CancellationToken>()))
            .Callback<string, GhlContactUpsertRequest, CancellationToken>((_, req, _) => captured = req)
            .ReturnsAsync(new GhlContactResult("contact-123"));

        var sut = new LeadProcessingService(db, meta.Object, ghl.Object, new PassThroughTokenProtector(), NullLogger<LeadProcessingService>.Instance);

        await sut.ProcessLeadgenNotificationAsync(page.PageId, form.FormId, "lead-1", "{}");

        var leadEvent = Assert.Single(db.LeadEvents);
        Assert.Equal(LeadEventStatus.Sent, leadEvent.Status);
        Assert.Equal("contact-123", leadEvent.GhlContactId);
        Assert.NotNull(leadEvent.ProcessedAtUtc);

        Assert.NotNull(captured);
        Assert.Equal(ghlConn!.LocationId, captured!.LocationId);
        Assert.Equal("jane@example.com", captured.Email);
        Assert.Equal("Jane", captured.FirstName);
        Assert.Equal("+15551234567", captured.Phone);
    }

    [Fact]
    public async Task ProcessLeadgenNotification_UnknownCustomQuestion_IsKeptAsCustomField()
    {
        var (db, _, page, form, _) = SeedForm(mapToGhl: true);
        var meta = new Mock<IMetaGraphClient>();
        var ghl = new Mock<IGhlClient>();

        meta.Setup(m => m.GetLeadDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleLead("lead-2", ("email", "a@b.com"), ("what_city_are_you_in", "Austin")));

        GhlContactUpsertRequest? captured = null;
        ghl.Setup(g => g.UpsertContactAsync(It.IsAny<string>(), It.IsAny<GhlContactUpsertRequest>(), It.IsAny<CancellationToken>()))
            .Callback<string, GhlContactUpsertRequest, CancellationToken>((_, req, _) => captured = req)
            .ReturnsAsync(new GhlContactResult("contact-1"));

        var sut = new LeadProcessingService(db, meta.Object, ghl.Object, new PassThroughTokenProtector(), NullLogger<LeadProcessingService>.Instance);
        await sut.ProcessLeadgenNotificationAsync(page.PageId, form.FormId, "lead-2", "{}");

        Assert.NotNull(captured!.CustomFields);
        Assert.Equal("Austin", captured.CustomFields!["what_city_are_you_in"]);
    }

    [Fact]
    public async Task ProcessLeadgenNotification_ExplicitFieldMapping_OverridesDefaultAndBuiltHeuristic()
    {
        var (db, tenant, page, form, _) = SeedForm(mapToGhl: true);
        db.FieldMappings.Add(new FieldMapping
        {
            TenantId = tenant.Id,
            MetaLeadFormId = form.Id,
            MetaFieldKey = "what_city_are_you_in",
            TargetType = GhlTargetFieldType.CustomField,
            GhlFieldKey = "city_custom_field_id",
        });
        db.SaveChanges();

        var meta = new Mock<IMetaGraphClient>();
        var ghl = new Mock<IGhlClient>();
        meta.Setup(m => m.GetLeadDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleLead("lead-3", ("what_city_are_you_in", "Austin")));

        GhlContactUpsertRequest? captured = null;
        ghl.Setup(g => g.UpsertContactAsync(It.IsAny<string>(), It.IsAny<GhlContactUpsertRequest>(), It.IsAny<CancellationToken>()))
            .Callback<string, GhlContactUpsertRequest, CancellationToken>((_, req, _) => captured = req)
            .ReturnsAsync(new GhlContactResult("contact-1"));

        var sut = new LeadProcessingService(db, meta.Object, ghl.Object, new PassThroughTokenProtector(), NullLogger<LeadProcessingService>.Instance);
        await sut.ProcessLeadgenNotificationAsync(page.PageId, form.FormId, "lead-3", "{}");

        Assert.Equal("Austin", captured!.CustomFields!["city_custom_field_id"]);
        Assert.False(captured.CustomFields!.ContainsKey("what_city_are_you_in"));
    }

    [Fact]
    public async Task ProcessLeadgenNotification_GhlThrows_MarksFailedWithErrorMessage()
    {
        var (db, _, page, form, _) = SeedForm(mapToGhl: true);
        var meta = new Mock<IMetaGraphClient>();
        var ghl = new Mock<IGhlClient>();

        meta.Setup(m => m.GetLeadDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleLead("lead-4", ("email", "a@b.com")));
        ghl.Setup(g => g.UpsertContactAsync(It.IsAny<string>(), It.IsAny<GhlContactUpsertRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GhlApiException("GHL is down"));

        var sut = new LeadProcessingService(db, meta.Object, ghl.Object, new PassThroughTokenProtector(), NullLogger<LeadProcessingService>.Instance);
        await sut.ProcessLeadgenNotificationAsync(page.PageId, form.FormId, "lead-4", "{}");

        var leadEvent = Assert.Single(db.LeadEvents);
        Assert.Equal(LeadEventStatus.Failed, leadEvent.Status);
        Assert.Equal("GHL is down", leadEvent.ErrorMessage);
    }

    [Fact]
    public async Task RetryAsync_IncrementsRetryCountAndCanSucceedAfterFixingTheGhlMapping()
    {
        var (db, _, page, form, _) = SeedForm(mapToGhl: false);
        var meta = new Mock<IMetaGraphClient>();
        var ghl = new Mock<IGhlClient>();

        var sut = new LeadProcessingService(db, meta.Object, ghl.Object, new PassThroughTokenProtector(), NullLogger<LeadProcessingService>.Instance);
        await sut.ProcessLeadgenNotificationAsync(page.PageId, form.FormId, "lead-5", "{}");

        var leadEvent = Assert.Single(db.LeadEvents);
        Assert.Equal(LeadEventStatus.Skipped, leadEvent.Status);

        // Simulate the user mapping the form to a GHL location after the fact, then retrying.
        var ghlConn = new GhlConnection
        {
            TenantId = form.MetaPage!.MetaConnection!.TenantId,
            LocationId = "loc-9",
            LocationName = "loc-9",
            EncryptedAccessToken = "token",
            EncryptedRefreshToken = "refresh",
            AccessTokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
        };
        db.GhlConnections.Add(ghlConn);
        form.GhlConnectionId = ghlConn.Id;
        db.SaveChanges();

        meta.Setup(m => m.GetLeadDataAsync("lead-5", "page-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleLead("lead-5", ("email", "a@b.com")));
        ghl.Setup(g => g.UpsertContactAsync("token", It.IsAny<GhlContactUpsertRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GhlContactResult("contact-9"));

        await sut.RetryAsync(leadEvent.Id);

        Assert.Equal(1, leadEvent.RetryCount);
        Assert.Equal(LeadEventStatus.Sent, leadEvent.Status);
        Assert.Equal("contact-9", leadEvent.GhlContactId);
    }

    [Fact]
    public async Task ProcessLeadgenNotification_GhlTokenNearExpiry_RefreshesBeforeUpserting()
    {
        var (db, _, page, form, ghlConn) = SeedForm(mapToGhl: true);
        ghlConn!.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(30);
        db.SaveChanges();

        var meta = new Mock<IMetaGraphClient>();
        var ghl = new Mock<IGhlClient>();

        meta.Setup(m => m.GetLeadDataAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleLead("lead-6", ("email", "a@b.com")));
        ghl.Setup(g => g.RefreshTokenAsync("ghl-refresh", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GhlTokenResult("new-access", "new-refresh", DateTime.UtcNow.AddHours(1), ghlConn.LocationId, ""));
        ghl.Setup(g => g.UpsertContactAsync("new-access", It.IsAny<GhlContactUpsertRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GhlContactResult("contact-10"));

        var sut = new LeadProcessingService(db, meta.Object, ghl.Object, new PassThroughTokenProtector(), NullLogger<LeadProcessingService>.Instance);
        await sut.ProcessLeadgenNotificationAsync(page.PageId, form.FormId, "lead-6", "{}");

        var leadEvent = Assert.Single(db.LeadEvents);
        Assert.Equal(LeadEventStatus.Sent, leadEvent.Status);
        ghl.Verify(g => g.RefreshTokenAsync("ghl-refresh", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal("new-access", ghlConn.EncryptedAccessToken);
    }
}

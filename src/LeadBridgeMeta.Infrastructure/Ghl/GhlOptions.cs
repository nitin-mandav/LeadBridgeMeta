namespace LeadBridgeMeta.Infrastructure.Ghl;

public class GhlOptions
{
    public const string SectionName = "Ghl";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://services.leadconnectorhq.com";
    public string MarketplaceBaseUrl { get; set; } = "https://marketplace.gohighlevel.com";
    public string ApiVersion { get; set; } = "2021-07-28";
    public string Scopes { get; set; } = "contacts.readonly contacts.write locations.readonly locations/customFields.readonly locations/customValues.readonly opportunities.readonly opportunities.write calendars.readonly users.readonly locations/tags.readonly";
    public string? DirectInstallUrl { get; set; }
}

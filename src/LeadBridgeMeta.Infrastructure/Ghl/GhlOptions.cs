namespace LeadBridgeMeta.Infrastructure.Ghl;

public class GhlOptions
{
    public const string SectionName = "Ghl";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://services.leadconnectorhq.com";
    public string MarketplaceBaseUrl { get; set; } = "https://marketplace.leadconnectorhq.com";
    public string ApiVersion { get; set; } = "2021-07-28";
}

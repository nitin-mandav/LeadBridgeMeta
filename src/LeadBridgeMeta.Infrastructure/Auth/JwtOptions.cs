namespace LeadBridgeMeta.Infrastructure.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "LeadBridgeMeta";
    public string Audience { get; set; } = "LeadBridgeMeta.Clients";
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60 * 8;
}

namespace Archiving.Infrastructure.Security;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Archiving.Api";
    public string Audience { get; set; } = "Archiving.Client";
    public string Key { get; set; } = string.Empty;       // HMAC-SHA256 signing key (>= 32 chars)
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 7;
}

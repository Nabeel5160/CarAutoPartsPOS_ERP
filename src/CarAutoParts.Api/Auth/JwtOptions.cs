namespace CarAutoParts.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "CarAutoParts.Api";
    public string Audience { get; set; } = "CarAutoParts.Clients";
    public string Key { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 480;
}

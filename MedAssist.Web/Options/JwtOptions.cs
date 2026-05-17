namespace MedAssist.Web.Options;

public sealed class JwtOptions
{
    public string SecretKey { get; init; } = string.Empty;
    public string Issuer { get; init; } = "medassist";
    public string Audience { get; init; } = "medassist-api";
    public int ExpiryMinutes { get; init; } = 480;
}

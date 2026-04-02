namespace Jobsite.Modules.Auth.Application.Configuration;

/// <summary>
/// JWT configuration settings for the Auth module.
/// Bound from the <c>App</c> section in appsettings.json.
/// </summary>
public sealed class JwtSettings
{
    public string JwtSecret { get; set; } = string.Empty;
    public string JwtIssuer { get; set; } = "djobsite-iconnect";
    public string JwtAudience { get; set; } = "djobsite-iconnect";
    public int JwtExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 30;
}

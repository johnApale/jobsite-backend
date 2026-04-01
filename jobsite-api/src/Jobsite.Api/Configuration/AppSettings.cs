namespace Jobsite.Api.Configuration;

/// <summary>
/// Strongly-typed configuration for the application.
/// Bound from the <c>App</c> section in appsettings.json.
/// </summary>
public sealed class AppSettings
{
    public const string SectionName = "App";

    /// <summary>JWT signing key. Must be at least 32 characters.</summary>
    public string JwtSecret { get; set; } = string.Empty;

    /// <summary>JWT issuer claim.</summary>
    public string JwtIssuer { get; set; } = "djobsite-iconnect";

    /// <summary>JWT audience claim.</summary>
    public string JwtAudience { get; set; } = "djobsite-iconnect";

    /// <summary>JWT access token lifetime in minutes.</summary>
    public int JwtExpirationMinutes { get; set; } = 60;

    /// <summary>Refresh token lifetime in days.</summary>
    public int RefreshTokenExpirationDays { get; set; } = 30;
}

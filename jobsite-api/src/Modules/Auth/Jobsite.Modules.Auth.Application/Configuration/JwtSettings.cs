namespace Jobsite.Modules.Auth.Application.Configuration;

/// <summary>
/// JWT configuration settings for the Auth module.
/// Bound from the <c>App</c> section in appsettings.json.
/// </summary>
public sealed class JwtSettings
{
    public string JwtSecret { get; set; } = string.Empty;
    public string JwtIssuer { get; set; } = "jobsite-iconnect";
    public string JwtAudience { get; set; } = "jobsite-iconnect";
    public int JwtExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 30;
    public int MaxFailedLoginAttempts { get; set; } = 5;
    public int LockoutDurationMinutes { get; set; } = 15;
    public int EmailVerificationTokenExpirationHours { get; set; } = 24;
    public int PasswordResetTokenExpirationHours { get; set; } = 1;
}

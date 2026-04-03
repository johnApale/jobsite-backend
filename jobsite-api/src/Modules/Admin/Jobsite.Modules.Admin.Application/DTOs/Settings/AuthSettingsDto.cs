namespace Jobsite.Modules.Admin.Application.DTOs.Settings;

/// <summary>Auth configuration settings (JSONB shape for <c>company_settings.auth_settings</c>).</summary>
public sealed class AuthSettingsDto
{
    public List<string> EnabledOauthProviders { get; set; } = ["Google", "Apple", "Facebook"];
    public bool AllowSelfRegistration { get; set; } = true;
    public bool RequireEmailVerification { get; set; } = true;
    public int PasswordMinLength { get; set; } = 8;
}

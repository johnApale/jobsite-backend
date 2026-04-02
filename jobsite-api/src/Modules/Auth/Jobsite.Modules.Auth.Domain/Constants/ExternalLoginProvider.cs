namespace Jobsite.Modules.Auth.Domain.Constants;

/// <summary>
/// OAuth provider constants for the <c>auth.user_external_logins.provider</c> column.
/// Values must match the CHECK constraint <c>chk_external_logins_provider</c> exactly.
/// </summary>
public static class ExternalLoginProvider
{
    public const string Google = "Google";
    public const string Apple = "Apple";
    public const string Facebook = "Facebook";

    public static bool IsValid(string provider) =>
        provider is Google or Apple or Facebook;
}

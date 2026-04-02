namespace Jobsite.Modules.Auth.Domain.Constants;

/// <summary>
/// Lifecycle status constants for the <c>auth.users.status</c> column.
/// Values must match the CHECK constraint <c>chk_users_status</c> exactly.
/// </summary>
public static class UserStatus
{
    public const string Active = "Active";
    public const string Invited = "Invited";
    public const string Deactivated = "Deactivated";

    public static bool IsValid(string status) =>
        status is Active or Invited or Deactivated;
}

namespace Jobsite.Modules.Auth.Domain.Constants;

/// <summary>
/// Role constants for the <c>auth.users.role</c> column.
/// Values must match the CHECK constraint <c>chk_users_role</c> exactly.
/// </summary>
public static class UserRole
{
    public const string Applicant = "Applicant";
    public const string Recruiter = "Recruiter";
    public const string HiringManager = "HiringManager";
    public const string Interviewer = "Interviewer";
    public const string AgencyAdmin = "AgencyAdmin";
    public const string PlatformAdmin = "PlatformAdmin";

    public static bool IsValid(string role) =>
        role is Applicant or Recruiter or HiringManager or Interviewer or AgencyAdmin or PlatformAdmin;
}

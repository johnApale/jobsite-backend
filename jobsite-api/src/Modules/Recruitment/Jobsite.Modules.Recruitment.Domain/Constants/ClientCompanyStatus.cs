namespace Jobsite.Modules.Recruitment.Domain.Constants;

/// <summary>
/// Status of a client company (agency model).
/// Values must match the CHECK constraint <c>chk_client_companies_status</c> exactly.
/// </summary>
public static class ClientCompanyStatus
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";

    public static bool IsValid(string status) =>
        status is Active or Inactive;
}

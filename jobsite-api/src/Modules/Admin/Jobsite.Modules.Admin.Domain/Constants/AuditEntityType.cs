namespace Jobsite.Modules.Admin.Domain.Constants;

/// <summary>
/// Known entity types for audit log entries.
/// </summary>
public static class AuditEntityType
{
    public const string User = "User";
    public const string CompanySettings = "CompanySettings";
    public const string Application = "Application";
    public const string ScreeningResult = "ScreeningResult";
    public const string FinalInterview = "FinalInterview";
    public const string JobOffer = "JobOffer";
    public const string Tenant = "Tenant";

    public static bool IsValid(string entityType) =>
        entityType is User or CompanySettings or Application
            or ScreeningResult or FinalInterview or JobOffer or Tenant;
}

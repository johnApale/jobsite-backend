namespace Jobsite.Modules.Admin.Domain.Constants;

/// <summary>
/// Known audit action types. Matches values written to <c>admin.audit_logs.action</c>.
/// </summary>
public static class AuditAction
{
    public const string UserRegistered = "UserRegistered";
    public const string SettingsUpdated = "SettingsUpdated";
    public const string ApplicationSubmitted = "ApplicationSubmitted";
    public const string CvScreeningCompleted = "CvScreeningCompleted";
    public const string AssessmentCompleted = "AssessmentCompleted";
    public const string CandidateShortlisted = "CandidateShortlisted";
    public const string FinalInterviewScheduled = "FinalInterviewScheduled";
    public const string OfferExtended = "OfferExtended";
    public const string TenantProvisioned = "TenantProvisioned";

    public static bool IsValid(string action) =>
        action is UserRegistered or SettingsUpdated or ApplicationSubmitted
            or CvScreeningCompleted or AssessmentCompleted or CandidateShortlisted
            or FinalInterviewScheduled or OfferExtended or TenantProvisioned;
}

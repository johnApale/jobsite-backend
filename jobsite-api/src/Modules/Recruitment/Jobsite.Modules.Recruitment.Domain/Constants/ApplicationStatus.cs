namespace Jobsite.Modules.Recruitment.Domain.Constants;

/// <summary>
/// Application pipeline stages — the spine of the hiring process.
/// Values must match the CHECK constraint <c>chk_applications_status</c> exactly.
/// </summary>
public static class ApplicationStatus
{
    public const string Submitted = "Submitted";
    public const string Screening = "Screening";
    public const string Assessment = "Assessment";
    public const string Shortlisted = "Shortlisted";
    public const string FinalInterview = "FinalInterview";
    public const string Offered = "Offered";
    public const string Hired = "Hired";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";

    public static bool IsValid(string status) =>
        status is Submitted or Screening or Assessment or Shortlisted
            or FinalInterview or Offered or Hired or Rejected or Withdrawn;
}

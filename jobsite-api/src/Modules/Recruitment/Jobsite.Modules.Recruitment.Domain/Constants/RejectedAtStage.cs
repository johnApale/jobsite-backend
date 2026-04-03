namespace Jobsite.Modules.Recruitment.Domain.Constants;

/// <summary>
/// Pipeline stage at which an application was rejected.
/// Values must match the CHECK constraint <c>chk_applications_rejected_at_stage</c> exactly.
/// </summary>
public static class RejectedAtStage
{
    public const string Screening = "Screening";
    public const string Assessment = "Assessment";
    public const string Shortlisted = "Shortlisted";
    public const string FinalInterview = "FinalInterview";
    public const string Offered = "Offered";

    public static bool IsValid(string stage) =>
        stage is Screening or Assessment or Shortlisted or FinalInterview or Offered;
}

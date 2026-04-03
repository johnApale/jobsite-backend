namespace Jobsite.Modules.Recruitment.Domain.Constants;

/// <summary>
/// Job posting lifecycle states.
/// Values must match the CHECK constraint <c>chk_job_postings_status</c> exactly.
/// </summary>
public static class JobPostingStatus
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Closed = "Closed";

    public static bool IsValid(string status) =>
        status is Draft or Published or Closed;
}

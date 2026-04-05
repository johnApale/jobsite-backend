namespace Jobsite.Modules.Recruitment.Domain.Constants;

/// <summary>
/// Work location arrangement for a job posting.
/// Values must match the CHECK constraint <c>chk_job_postings_location_type</c> exactly.
/// </summary>
public static class LocationType
{
    public const string OnSite = "OnSite";
    public const string Remote = "Remote";
    public const string Hybrid = "Hybrid";

    public static bool IsValid(string? locationType) =>
        locationType is OnSite or Remote or Hybrid;
}

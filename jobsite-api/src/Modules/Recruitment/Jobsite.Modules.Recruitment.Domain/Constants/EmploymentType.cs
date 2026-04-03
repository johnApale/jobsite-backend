namespace Jobsite.Modules.Recruitment.Domain.Constants;

/// <summary>
/// Employment arrangement for a job posting.
/// Values must match the CHECK constraint <c>chk_job_postings_employment_type</c> exactly.
/// </summary>
public static class EmploymentType
{
    public const string FullTime = "FullTime";
    public const string PartTime = "PartTime";
    public const string Contract = "Contract";
    public const string Temporary = "Temporary";
    public const string Internship = "Internship";

    public static bool IsValid(string employmentType) =>
        employmentType is FullTime or PartTime or Contract or Temporary or Internship;
}

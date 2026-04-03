namespace Jobsite.Modules.Recruitment.Domain.Constants;

/// <summary>
/// Category for a job evaluation criterion, determining the <c>configuration</c> JSONB shape.
/// Values must match the CHECK constraint <c>chk_criteria_category</c> exactly.
/// </summary>
public static class CriteriaCategory
{
    public const string Skill = "Skill";
    public const string Experience = "Experience";
    public const string Certification = "Certification";
    public const string Education = "Education";
    public const string Location = "Location";
    public const string Custom = "Custom";

    public static bool IsValid(string category) =>
        category is Skill or Experience or Certification or Education or Location or Custom;
}

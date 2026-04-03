namespace Jobsite.Modules.Profiles.Domain.Constants;

/// <summary>
/// Self-reported skill proficiency levels used in the applicant profile's skills JSONB.
/// Not enforced at the database level — informational only for matching.
/// </summary>
public static class SkillLevel
{
    public const string Beginner = "Beginner";
    public const string Intermediate = "Intermediate";
    public const string Advanced = "Advanced";
    public const string Expert = "Expert";

    public static bool IsValid(string level) =>
        level is Beginner or Intermediate or Advanced or Expert;
}

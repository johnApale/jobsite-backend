namespace Jobsite.Modules.Recruitment.Domain.Constants;

/// <summary>
/// When a screening question is presented to the candidate.
/// Values must match the CHECK constraint <c>chk_questions_timing</c> exactly.
/// </summary>
public static class QuestionTiming
{
    public const string AtApplication = "AtApplication";
    public const string AfterScreening = "AfterScreening";

    public static bool IsValid(string timing) =>
        timing is AtApplication or AfterScreening;
}

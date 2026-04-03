namespace Jobsite.Modules.Recruitment.Domain.Constants;

/// <summary>
/// How the Screening module scores a criterion against the applicant's profile/resume.
/// Values must match the CHECK constraint <c>chk_criteria_evaluation_method</c> exactly.
/// </summary>
public static class EvaluationMethod
{
    public const string ExactMatch = "ExactMatch";
    public const string RangeMatch = "RangeMatch";
    public const string SemanticSimilarity = "SemanticSimilarity";

    public static bool IsValid(string method) =>
        method is ExactMatch or RangeMatch or SemanticSimilarity;
}

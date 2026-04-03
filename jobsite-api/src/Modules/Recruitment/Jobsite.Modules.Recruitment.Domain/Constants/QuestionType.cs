namespace Jobsite.Modules.Recruitment.Domain.Constants;

/// <summary>
/// Answer format for a screening question.
/// Values must match the CHECK constraint <c>chk_questions_type</c> exactly.
/// </summary>
public static class QuestionType
{
    public const string FreeText = "FreeText";
    public const string MultipleChoice = "MultipleChoice";
    public const string YesNo = "YesNo";

    public static bool IsValid(string type) =>
        type is FreeText or MultipleChoice or YesNo;
}

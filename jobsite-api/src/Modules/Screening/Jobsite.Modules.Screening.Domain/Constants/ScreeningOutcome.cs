namespace Jobsite.Modules.Screening.Domain.Constants;

/// <summary>Routing outcome after screening scoring completes.</summary>
public static class ScreeningOutcome
{
    public const string AutoAdvanced = "AutoAdvanced";
    public const string AutoRejected = "AutoRejected";
    public const string ManualReview = "ManualReview";
    public const string ManuallyAdvanced = "ManuallyAdvanced";
    public const string ManuallyRejected = "ManuallyRejected";

    public static bool IsValid(string outcome) =>
        outcome is AutoAdvanced or AutoRejected or ManualReview or ManuallyAdvanced or ManuallyRejected;
}

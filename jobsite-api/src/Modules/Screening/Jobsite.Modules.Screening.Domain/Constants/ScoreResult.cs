namespace Jobsite.Modules.Screening.Domain.Constants;

/// <summary>Quick classification for individual criterion or question scores.</summary>
public static class ScoreResult
{
    public const string MeetsRequirement = "MeetsRequirement";
    public const string PartialMatch = "PartialMatch";
    public const string Missing = "Missing";

    public static bool IsValid(string result) =>
        result is MeetsRequirement or PartialMatch or Missing;

    /// <summary>Derives a score result label from a numeric score (0–100).</summary>
    public static string FromScore(decimal score) => score switch
    {
        >= 80m => MeetsRequirement,
        >= 40m => PartialMatch,
        _ => Missing
    };
}

namespace Jobsite.Modules.Matching.Domain.Constants;

/// <summary>
/// How a candidate was added to a shortlist.
/// Values match CHECK constraint <c>chk_shortlist_candidates_source</c>.
/// </summary>
public static class ShortlistCandidateSource
{
    public const string Algorithm = "Algorithm";
    public const string Manual = "Manual";

    public static bool IsValid(string source) =>
        source is Algorithm or Manual;
}

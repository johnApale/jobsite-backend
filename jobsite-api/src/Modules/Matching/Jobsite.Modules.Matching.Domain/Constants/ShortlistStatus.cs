namespace Jobsite.Modules.Matching.Domain.Constants;

/// <summary>
/// Shortlist lifecycle status. Values match CHECK constraint <c>chk_shortlists_status</c>.
/// </summary>
public static class ShortlistStatus
{
    public const string Draft = "Draft";
    public const string Finalized = "Finalized";

    public static bool IsValid(string status) =>
        status is Draft or Finalized;
}

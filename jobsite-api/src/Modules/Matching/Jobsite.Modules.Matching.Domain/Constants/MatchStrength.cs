namespace Jobsite.Modules.Matching.Domain.Constants;

/// <summary>
/// Human-readable match strength label derived from composite score ranges.
/// Stored as a denormalized label for quick filtering and display.
/// Values match CHECK constraint <c>chk_matches_match_strength</c>.
/// </summary>
public static class MatchStrength
{
    public const string Strong = "Strong";
    public const string Good = "Good";
    public const string Moderate = "Moderate";
    public const string Weak = "Weak";

    public static bool IsValid(string strength) =>
        strength is Strong or Good or Moderate or Weak;

    /// <summary>
    /// Derives match strength from a composite score.
    /// Strong: 80–100, Good: 60–79, Moderate: 40–59, Weak: 0–39.
    /// </summary>
    public static string FromScore(decimal score) => score switch
    {
        >= 80m => Strong,
        >= 60m => Good,
        >= 40m => Moderate,
        _ => Weak
    };
}

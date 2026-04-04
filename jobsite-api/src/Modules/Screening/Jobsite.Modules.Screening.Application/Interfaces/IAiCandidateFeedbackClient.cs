namespace Jobsite.Modules.Screening.Application.Interfaces;

/// <summary>
/// AI candidate feedback client — calls the AI Service to generate
/// candidate-facing transparency feedback.
/// Returns null when AI Service is unavailable or feature is disabled.
/// </summary>
public interface IAiCandidateFeedbackClient
{
    /// <summary>
    /// Generates candidate-facing feedback based on criteria breakdown and scores.
    /// Returns the feedback JSONB string, or null on failure.
    /// </summary>
    Task<string?> GenerateFeedbackAsync(
        string criteriaScoreBreakdown,
        decimal overallScore,
        string transparencyLevel,
        CancellationToken ct = default);
}

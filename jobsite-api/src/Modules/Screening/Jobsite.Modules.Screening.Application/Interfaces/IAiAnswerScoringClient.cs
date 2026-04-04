using Jobsite.Modules.Screening.Application.DTOs;

namespace Jobsite.Modules.Screening.Application.Interfaces;

/// <summary>
/// AI answer scoring client — calls the AI Service to score free-text answers.
/// Returns null when AI Service is unavailable or returns an error.
/// FreeText question scoring always calls AI (independent of AI scoring flag).
/// </summary>
public interface IAiAnswerScoringClient
{
    /// <summary>
    /// Scores a list of free-text question answers via the AI Service.
    /// Returns null on failure.
    /// </summary>
    Task<List<AnswerScore>?> ScoreAnswersAsync(
        List<AnswerScoringRequest> answers,
        CancellationToken ct = default);
}

/// <summary>Request payload for a single answer to be AI-scored.</summary>
public sealed class AnswerScoringRequest
{
    public required Guid QuestionId { get; init; }
    public required string QuestionText { get; init; }
    public required string ResponseText { get; init; }
    public string? ScoringGuidance { get; init; }
    public List<string>? KeyTopics { get; init; }
}

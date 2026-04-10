namespace Jobsite.Modules.Screening.Application.DTOs;

/// <summary>Request payload for a single free-text answer pending AI scoring.</summary>
public sealed class AnswerScoringRequest
{
    public required Guid QuestionId { get; init; }
    public required string QuestionText { get; init; }
    public required string ResponseText { get; init; }
    public string? ScoringGuidance { get; init; }
    public List<string>? KeyTopics { get; init; }
}

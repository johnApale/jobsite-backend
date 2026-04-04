namespace Jobsite.Modules.Screening.Application.DTOs;

/// <summary>Candidate-facing feedback response.</summary>
public sealed class CandidateFeedbackResponse
{
    public required Guid ApplicationId { get; init; }
    public string? Feedback { get; init; }
}

namespace Jobsite.Modules.Screening.Application.DTOs;

/// <summary>Request to submit AfterScreening assessment answers.</summary>
public sealed class SubmitAssessmentRequest
{
    public required Guid JobPostingId { get; init; }
    public required List<AssessmentAnswerDto> Answers { get; init; }
}

/// <summary>A single answer to an AfterScreening question.</summary>
public sealed class AssessmentAnswerDto
{
    public required Guid QuestionId { get; init; }
    public string? ResponseText { get; init; }
    public string? ResponseData { get; init; }
}

/// <summary>Assessment status response — questions, answers, and scores.</summary>
public sealed class AssessmentStatusResponse
{
    public required Guid ApplicationId { get; init; }
    public required bool IsSubmitted { get; init; }
    public decimal? AssessmentScore { get; init; }
    public required List<AssessmentQuestionDto> Questions { get; init; }
}

/// <summary>A question with its answer status for assessment display.</summary>
public sealed class AssessmentQuestionDto
{
    public required Guid QuestionId { get; init; }
    public required string QuestionText { get; init; }
    public required string QuestionType { get; init; }
    public string? Options { get; init; }
    public bool IsRequired { get; init; }
    public bool IsAnswered { get; init; }
    public decimal? Score { get; init; }
    public string? ScoreResult { get; init; }
}

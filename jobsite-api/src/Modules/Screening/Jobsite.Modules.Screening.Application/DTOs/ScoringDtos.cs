namespace Jobsite.Modules.Screening.Application.DTOs;

/// <summary>Result from the deterministic scoring engine.</summary>
public sealed class ScoringResult
{
    public required List<CriterionScoreDto> Breakdown { get; init; }
    public required decimal OverallScore { get; init; }
}

/// <summary>Per-criterion score detail (used in both deterministic and AI breakdowns).</summary>
public sealed class CriterionScoreDto
{
    public required Guid CriterionId { get; init; }
    public required string CriterionName { get; init; }
    public required string Category { get; init; }
    public required decimal Weight { get; init; }
    public required decimal Score { get; init; }
    public required string Result { get; init; }
    public required string Reasoning { get; init; }
}

/// <summary>Result from the AI scoring engine.</summary>
public sealed class AiScoringResult
{
    public required List<CriterionScoreDto> Breakdown { get; init; }
    public required decimal OverallScore { get; init; }
}

/// <summary>Score for a single question answer.</summary>
public sealed class AnswerScore
{
    public required Guid QuestionId { get; init; }
    public required decimal Score { get; init; }
    public required string Result { get; init; }
    public required string Reasoning { get; init; }
}

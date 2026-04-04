using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Screening.Domain.Entities;

/// <summary>
/// Candidate answer to a screening question — maps to <c>screening.screening_question_responses</c>.
/// Handles answers for both AtApplication and AfterScreening questions.
/// Scoring is applied separately after submission.
/// </summary>
public sealed class ScreeningQuestionResponse : Entity
{
    /// <summary>FK to <c>recruitment.applications.id</c> (cross-schema).</summary>
    public Guid ApplicationId { get; set; }

    /// <summary>FK to <c>recruitment.job_screening_questions.id</c> (cross-schema).</summary>
    public Guid QuestionId { get; set; }

    /// <summary>Free-text answer for FreeText questions. Null for MultipleChoice/YesNo.</summary>
    public string? ResponseText { get; set; }

    /// <summary>Structured answer data as JSONB. For MultipleChoice/YesNo.</summary>
    public string? ResponseData { get; set; }

    /// <summary>Score for this answer (0.00–100.00). Null until scored.</summary>
    public decimal? Score { get; set; }

    /// <summary>Quick label: MeetsRequirement, PartialMatch, Missing. Null until scored.</summary>
    public string? ScoreResult { get; set; }

    /// <summary>Explanation of the score. Null until scored.</summary>
    public string? ScoreReasoning { get; set; }

    /// <summary>When the candidate submitted this answer.</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>When this answer was scored. Null until scored.</summary>
    public DateTime? ScoredAt { get; set; }
}

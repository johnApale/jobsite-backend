using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Screening.Domain.Entities;

/// <summary>
/// One screening result per application — maps to <c>screening.screening_results</c>.
/// Uses shared primary key with <c>recruitment.applications</c> (ApplicationId is both PK and FK).
/// Contains deterministic and AI scoring breakdowns, routing outcome, and candidate feedback.
/// </summary>
public sealed class ScreeningResult : Entity
{
    /// <summary>Shared PK with <c>recruitment.applications.id</c>.</summary>
    public Guid ApplicationId { get; set; }

    /// <summary>Processing status: Pending, InProgress, Completed, Failed.</summary>
    public string Status { get; set; } = Constants.ScreeningStatus.Pending;

    /// <summary>Final weighted deterministic score (0.00–100.00). Drives all routing.</summary>
    public decimal? OverallScore { get; set; }

    /// <summary>Human-readable label: Strong, Good, Moderate, Weak.</summary>
    public string? MatchStrength { get; set; }

    /// <summary>Routing outcome: AutoAdvanced, AutoRejected, ManualReview, ManuallyAdvanced, ManuallyRejected.</summary>
    public string? Outcome { get; set; }

    /// <summary>Per-criterion deterministic score details (JSONB).</summary>
    public string? CriteriaScoreBreakdown { get; set; }

    /// <summary>Per-criterion AI analysis score details (JSONB). Null if AI scoring disabled/unavailable.</summary>
    public string? AiCriteriaScoreBreakdown { get; set; }

    /// <summary>AI's overall score (0.00–100.00). Null if AI scoring disabled/unavailable.</summary>
    public decimal? AiOverallScore { get; set; }

    /// <summary>Per-question scoring (JSONB). Null if no AtApplication questions.</summary>
    public string? QuestionScoreBreakdown { get; set; }

    /// <summary>Weighted score from AfterScreening question answers (0.00–100.00).</summary>
    public decimal? AssessmentScore { get; set; }

    /// <summary>Candidate-facing transparency data (JSONB). Only populated when enabled.</summary>
    public string? CandidateFeedback { get; set; }

    /// <summary>Tenant's auto-advance threshold captured at evaluation time.</summary>
    public decimal AutoAdvanceThreshold { get; set; }

    /// <summary>Tenant's auto-reject threshold captured at evaluation time.</summary>
    public decimal AutoRejectThreshold { get; set; }

    /// <summary>Recruiter who manually reviewed. Null for auto outcomes.</summary>
    public Guid? ReviewedBy { get; set; }

    /// <summary>When the manual review decision was made.</summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Optional notes from the reviewer.</summary>
    public string? ReviewNotes { get; set; }

    /// <summary>Processing error description if status = Failed.</summary>
    public string? FailureReason { get; set; }

    /// <summary>When screening began processing.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>When screening finished scoring.</summary>
    public DateTime? CompletedAt { get; set; }
}

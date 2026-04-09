using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Matching.Domain.Entities;

/// <summary>
/// One candidate match per application — maps to <c>matching.candidate_matches</c>.
/// Uses shared primary key with <c>recruitment.applications</c> (ApplicationId is PK, cross-module reference).
/// Aggregates screening and assessment scores into a weighted composite score.
/// </summary>
public sealed class CandidateMatch : Entity
{
    /// <summary>Shared PK with <c>recruitment.applications.id</c>.</summary>
    public Guid ApplicationId { get; set; }

    /// <summary>Ref to <c>recruitment.job_postings.id</c> (cross-module, no navigation).</summary>
    public Guid JobPostingId { get; set; }

    /// <summary>Ref to <c>auth.users.id</c> (cross-module, no navigation).</summary>
    public Guid ApplicantUserId { get; set; }

    /// <summary>Deterministic screening overall score (0.00–100.00).</summary>
    public decimal ScreeningScore { get; set; }

    /// <summary>Assessment score (0.00–100.00). Null until assessment completes.</summary>
    public decimal? AssessmentScore { get; set; }

    /// <summary>Weighted composite of screening + assessment using tenant weights (0.00–100.00).</summary>
    public decimal CompositeScore { get; set; }

    /// <summary>Human-readable label: Strong, Good, Moderate, Weak.</summary>
    public string MatchStrength { get; set; } = null!;

    /// <summary>Candidate rank within the job posting (1-based). Null until ranked.</summary>
    public int? Rank { get; set; }

    /// <summary>When screening completed for this application.</summary>
    public DateTime ScreeningCompletedAt { get; set; }

    /// <summary>When assessment completed. Null if no assessment or not yet completed.</summary>
    public DateTime? AssessmentCompletedAt { get; set; }
}

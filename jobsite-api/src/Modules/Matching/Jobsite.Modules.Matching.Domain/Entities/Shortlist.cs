using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Matching.Domain.Entities;

/// <summary>
/// Per-job-posting shortlist of top candidates — maps to <c>matching.shortlists</c>.
/// Aggregate root that publishes <see cref="SharedKernel.Events.CandidateShortlistedEvent"/> on finalization.
/// </summary>
public sealed class Shortlist : AggregateRoot
{
    /// <summary>FK to <c>recruitment.job_postings.id</c> (cross-schema, no navigation).</summary>
    public Guid JobPostingId { get; set; }

    /// <summary>Lifecycle status: Draft, Finalized.</summary>
    public string Status { get; set; } = Constants.ShortlistStatus.Draft;

    /// <summary>How the shortlist was generated — "Algorithm" or a user ID string.</summary>
    public string GeneratedBy { get; set; } = null!;

    /// <summary>Count of active (non-removed) candidates.</summary>
    public int TotalCandidates { get; set; }

    /// <summary>When the shortlist was finalized. Null while in Draft.</summary>
    public DateTime? FinalizedAt { get; set; }

    /// <summary>User who finalized the shortlist. Null while in Draft.</summary>
    public Guid? FinalizedBy { get; set; }

    /// <summary>Candidates on this shortlist.</summary>
    public ICollection<ShortlistCandidate> Candidates { get; set; } = new List<ShortlistCandidate>();
}

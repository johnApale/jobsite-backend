using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Matching.Domain.Entities;

/// <summary>
/// A candidate on a shortlist — maps to <c>matching.shortlist_candidates</c>.
/// Unique constraint on <c>(shortlist_id, application_id)</c>.
/// </summary>
public sealed class ShortlistCandidate : Entity
{
    /// <summary>FK to <c>matching.shortlists.id</c>.</summary>
    public Guid ShortlistId { get; set; }

    /// <summary>FK to <c>recruitment.applications.id</c> (cross-schema, no navigation).</summary>
    public Guid ApplicationId { get; set; }

    /// <summary>FK to <c>auth.users.id</c> (cross-schema, no navigation).</summary>
    public Guid ApplicantUserId { get; set; }

    /// <summary>Composite score at time of shortlisting (0.00–100.00).</summary>
    public decimal CompositeScore { get; set; }

    /// <summary>Position in the shortlist (1-based, ordered by composite score desc).</summary>
    public int Rank { get; set; }

    /// <summary>How the candidate was added: Algorithm or Manual.</summary>
    public string Source { get; set; } = null!;

    /// <summary>When the candidate was added to the shortlist.</summary>
    public DateTime AddedAt { get; set; }

    /// <summary>When the candidate was removed. Null if still active (soft removal).</summary>
    public DateTime? RemovedAt { get; set; }

    /// <summary>Navigation property to the parent shortlist.</summary>
    public Shortlist Shortlist { get; set; } = null!;
}

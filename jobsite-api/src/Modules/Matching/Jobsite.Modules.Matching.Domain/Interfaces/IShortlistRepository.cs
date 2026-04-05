using Jobsite.Modules.Matching.Domain.Entities;

namespace Jobsite.Modules.Matching.Domain.Interfaces;

/// <summary>Repository for <c>matching.shortlists</c> and <c>matching.shortlist_candidates</c>.</summary>
public interface IShortlistRepository
{
    Task<Shortlist?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Shortlist?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);

    Task<Shortlist?> GetDraftByJobPostingIdAsync(Guid jobPostingId, CancellationToken ct = default);

    Task<List<Shortlist>> GetByJobPostingIdAsync(Guid jobPostingId, CancellationToken ct = default);

    void Add(Shortlist shortlist);
}

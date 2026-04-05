using Jobsite.Modules.Matching.Domain.Entities;

namespace Jobsite.Modules.Matching.Domain.Interfaces;

/// <summary>Repository for <c>matching.candidate_matches</c>.</summary>
public interface ICandidateMatchRepository
{
    Task<CandidateMatch?> GetByApplicationIdAsync(Guid applicationId, CancellationToken ct = default);

    Task<CandidateMatch?> GetByApplicationIdForUpdateAsync(Guid applicationId, CancellationToken ct = default);

    Task<List<CandidateMatch>> GetByJobPostingIdAsync(Guid jobPostingId, CancellationToken ct = default);

    void Add(CandidateMatch match);
}

using Jobsite.Modules.Matching.Application.DTOs;

namespace Jobsite.Modules.Matching.Application.Services;

/// <summary>Read-only service for candidate match queries.</summary>
public interface IMatchingService
{
    Task<CandidateMatchResponse> GetMatchAsync(Guid applicationId, CancellationToken ct = default);

    Task<CandidateMatchListResponse> ListMatchesAsync(
        CandidateMatchQueryParameters parameters, CancellationToken ct = default);
}

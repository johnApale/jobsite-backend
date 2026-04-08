using Jobsite.Modules.Matching.Application.DTOs;

namespace Jobsite.Modules.Matching.Application.Services;

/// <summary>Manages shortlist generation, candidate management, and finalization.</summary>
public interface IShortlistService
{
    Task<ShortlistResponse> GenerateShortlistAsync(Guid jobPostingId, CancellationToken ct = default);

    Task<ShortlistResponse> GetShortlistAsync(Guid shortlistId, CancellationToken ct = default);

    Task<ShortlistListResponse> ListShortlistsAsync(
        ShortlistQueryParameters parameters, CancellationToken ct = default);

    Task<ShortlistResponse> AddCandidateAsync(
        Guid shortlistId, Guid applicationId, CancellationToken ct = default);

    Task RemoveCandidateAsync(
        Guid shortlistId, Guid applicationId, CancellationToken ct = default);

    Task<ShortlistResponse> ApproveCandidateAsync(
        Guid shortlistId, Guid candidateId, CancellationToken ct = default);

    Task<ShortlistResponse> RejectCandidateAsync(
        Guid shortlistId, Guid candidateId, CancellationToken ct = default);

    Task<ShortlistResponse> FinalizeShortlistAsync(
        Guid shortlistId, Guid userId, CancellationToken ct = default);
}

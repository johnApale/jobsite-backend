using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Domain.Entities;

namespace Jobsite.Modules.Screening.Application.Interfaces;

/// <summary>Repository for <c>screening.screening_results</c>.</summary>
public interface IScreeningResultRepository
{
    Task<ScreeningResult?> GetByApplicationIdAsync(Guid applicationId, CancellationToken ct = default);

    Task<ScreeningResult?> GetByApplicationIdForUpdateAsync(Guid applicationId, CancellationToken ct = default);

    Task<ScreeningResultListResponse> ListAsync(
        ScreeningResultQueryParameters parameters, CancellationToken ct = default);

    Task<List<ScreeningResult>> GetPendingForRescoringAsync(
        Guid jobPostingId, CancellationToken ct = default);

    void Add(ScreeningResult result);
}

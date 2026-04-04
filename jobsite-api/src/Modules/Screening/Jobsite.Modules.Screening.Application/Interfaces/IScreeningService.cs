using Jobsite.Modules.Screening.Application.DTOs;

namespace Jobsite.Modules.Screening.Application.Interfaces;

/// <summary>Orchestrates the screening scoring pipeline and manual review.</summary>
public interface IScreeningService
{
    Task ProcessScreeningAsync(Guid applicationId, Guid jobPostingId,
        Guid applicantUserId, Guid? resumeId, CancellationToken ct = default);

    Task<ScreeningResultResponse> GetResultAsync(Guid applicationId, CancellationToken ct = default);

    Task<ScreeningResultListResponse> ListResultsAsync(
        ScreeningResultQueryParameters parameters, CancellationToken ct = default);

    Task<ScreeningResultResponse> ManualReviewAsync(Guid applicationId,
        ManualReviewRequest request, Guid reviewerId, CancellationToken ct = default);

    Task RescoreApplicationAsync(Guid applicationId, Guid jobPostingId,
        Guid applicantUserId, Guid? resumeId, CancellationToken ct = default);
}

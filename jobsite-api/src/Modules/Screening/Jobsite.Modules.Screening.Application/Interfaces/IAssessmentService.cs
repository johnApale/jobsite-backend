using Jobsite.Modules.Screening.Application.DTOs;

namespace Jobsite.Modules.Screening.Application.Interfaces;

/// <summary>Manages the assessment flow — AfterScreening question submission and scoring.</summary>
public interface IAssessmentService
{
    Task SubmitAssessmentAsync(
        Guid applicationId, Guid jobPostingId, Guid applicantUserId,
        SubmitAssessmentRequest request, CancellationToken ct = default);

    Task<AssessmentStatusResponse> GetAssessmentStatusAsync(
        Guid applicationId, Guid jobPostingId, CancellationToken ct = default);
}

using Jobsite.Modules.HRWorkflows.Application.DTOs;

namespace Jobsite.Modules.HRWorkflows.Application.Services;

public interface IInterviewService
{
    Task<FinalInterviewResponse> ScheduleInterviewAsync(ScheduleInterviewRequest request, Guid scheduledByUserId, CancellationToken ct = default);
    Task<FinalInterviewResponse> GetInterviewAsync(Guid applicationId, CancellationToken ct = default);
    Task<InterviewListResponse> ListInterviewsAsync(InterviewQueryParameters parameters, CancellationToken ct = default);
    Task<FinalInterviewResponse> UpdateInterviewAsync(Guid applicationId, UpdateInterviewRequest request, CancellationToken ct = default);
    Task<FinalInterviewResponse> SubmitPanelistFeedbackAsync(Guid applicationId, Guid interviewerId, SubmitFeedbackRequest request, CancellationToken ct = default);
    Task<FinalInterviewResponse> RecordDecisionAsync(Guid applicationId, RecordDecisionRequest request, Guid decidedByUserId, CancellationToken ct = default);
    Task CancelInterviewAsync(Guid applicationId, string reason, CancellationToken ct = default);
}

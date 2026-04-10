using Jobsite.Modules.HRWorkflows.Application.DTOs;
using Jobsite.Modules.HRWorkflows.Domain.Constants;
using Jobsite.Modules.HRWorkflows.Domain.Entities;
using Jobsite.Modules.HRWorkflows.Domain.Interfaces;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.HRWorkflows.Application.Services;

public sealed class InterviewService : IInterviewService
{
    private readonly IFinalInterviewRepository _interviewRepository;
    private readonly IApplicationStatusUpdater _statusUpdater;
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly IFeedbackAggregationService _feedbackAggregation;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InterviewService> _logger;

    public InterviewService(
        IFinalInterviewRepository interviewRepository,
        IApplicationStatusUpdater statusUpdater,
        IDomainEventDispatcher dispatcher,
        IFeedbackAggregationService feedbackAggregation,
        [FromKeyedServices("hr_workflows")] IUnitOfWork unitOfWork,
        ILogger<InterviewService> logger)
    {
        _interviewRepository = interviewRepository;
        _statusUpdater = statusUpdater;
        _dispatcher = dispatcher;
        _feedbackAggregation = feedbackAggregation;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<FinalInterviewResponse> ScheduleInterviewAsync(
        ScheduleInterviewRequest request, Guid scheduledByUserId, CancellationToken ct = default)
    {
        FinalInterview? existing = await _interviewRepository.GetByApplicationIdAsync(request.ApplicationId, ct);
        if (existing is not null)
            throw AppErrors.InterviewAlreadyExists;

        DateTime now = DateTime.UtcNow;
        FinalInterview interview = new()
        {
            ApplicationId = request.ApplicationId,
            Status = InterviewStatus.Scheduled,
            InterviewType = request.InterviewType,
            ScheduledAt = request.ScheduledAt,
            DurationMinutes = request.DurationMinutes,
            Location = request.Location,
            ScheduledBy = scheduledByUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        foreach (Guid panelistUserId in request.PanelistUserIds)
        {
            InterviewPanelist panelist = new()
            {
                InterviewId = interview.ApplicationId,
                InterviewerId = panelistUserId,
                CreatedAt = now
            };
            interview.Panelists.Add(panelist);
        }

        _interviewRepository.Add(interview);

        await _statusUpdater.UpdateStatusAsync(
            request.ApplicationId,
            "FinalInterview",
            rejectionReason: null,
            rejectedAtStage: null,
            ct);

        await _unitOfWork.SaveChangesAsync(ct);

        await _dispatcher.DispatchAsync(new FinalInterviewScheduledEvent
        {
            ApplicationId = interview.ApplicationId,
            InterviewId = interview.ApplicationId,
            ScheduledAt = interview.ScheduledAt
        }, ct);

        _logger.LogInformation(
            "Scheduled final interview for application {ApplicationId} with {PanelistCount} panelists",
            interview.ApplicationId, request.PanelistUserIds.Count);

        return MapToResponse(interview);
    }

    public async Task<FinalInterviewResponse> GetInterviewAsync(
        Guid applicationId, CancellationToken ct = default)
    {
        FinalInterview? interview = await _interviewRepository.GetByApplicationIdAsync(applicationId, ct);
        if (interview is null)
            throw AppErrors.InterviewNotFound;

        return MapToResponse(interview);
    }

    public async Task<InterviewListResponse> ListInterviewsAsync(
        InterviewQueryParameters parameters, CancellationToken ct = default)
    {
        List<FinalInterview> interviews = await _interviewRepository.GetUpcomingAsync(ct);

        if (!string.IsNullOrEmpty(parameters.Status))
        {
            interviews = interviews.Where(i => i.Status == parameters.Status).ToList();
        }

        interviews = interviews.OrderByDescending(i => i.ScheduledAt).ToList();

        List<FinalInterviewResponse> items = interviews.Select(MapToResponse).ToList();

        return new InterviewListResponse
        {
            Items = items,
            NextCursor = null,
            HasMore = false
        };
    }

    public async Task<FinalInterviewResponse> UpdateInterviewAsync(
        Guid applicationId, UpdateInterviewRequest request, CancellationToken ct = default)
    {
        FinalInterview? interview = await _interviewRepository.GetByApplicationIdForUpdateAsync(applicationId, ct);
        if (interview is null)
            throw AppErrors.InterviewNotFound;

        if (interview.Status is InterviewStatus.Completed or InterviewStatus.Cancelled)
            throw AppErrors.InterviewAlreadyCompleted;

        if (request.InterviewType is not null)
            interview.InterviewType = request.InterviewType;
        if (request.ScheduledAt.HasValue)
            interview.ScheduledAt = request.ScheduledAt.Value;
        if (request.DurationMinutes.HasValue)
            interview.DurationMinutes = request.DurationMinutes.Value;
        if (request.Location is not null)
            interview.Location = request.Location;

        interview.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Updated interview schedule for application {ApplicationId}", applicationId);

        return MapToResponse(interview);
    }

    public async Task<FinalInterviewResponse> SubmitPanelistFeedbackAsync(
        Guid applicationId, Guid interviewerId, SubmitFeedbackRequest request, CancellationToken ct = default)
    {
        FinalInterview? interview = await _interviewRepository.GetByApplicationIdForUpdateAsync(applicationId, ct);
        if (interview is null)
            throw AppErrors.InterviewNotFound;

        InterviewPanelist? panelist = interview.Panelists
            .FirstOrDefault(p => p.InterviewerId == interviewerId);
        if (panelist is null)
            throw AppErrors.PanelistNotFound;

        if (panelist.FeedbackSubmittedAt is not null)
            throw AppErrors.FeedbackAlreadySubmitted;

        panelist.Rating = request.Rating;
        panelist.Recommendation = request.Recommendation;
        panelist.Strengths = request.Strengths;
        panelist.Concerns = request.Concerns;
        panelist.Notes = request.Notes;
        panelist.FeedbackSubmittedAt = DateTime.UtcNow;

        // Auto-transition to Completed when all panelists have submitted feedback
        bool allSubmitted = interview.Panelists.All(p => p.FeedbackSubmittedAt is not null);
        if (allSubmitted && interview.Status == InterviewStatus.Scheduled)
        {
            interview.Status = InterviewStatus.Completed;
            interview.CompletedAt = DateTime.UtcNow;
        }

        interview.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Panelist {InterviewerId} submitted feedback for interview {ApplicationId}",
            interviewerId, applicationId);

        return MapToResponse(interview);
    }

    public async Task<FinalInterviewResponse> RecordDecisionAsync(
        Guid applicationId, RecordDecisionRequest request, Guid decidedByUserId, CancellationToken ct = default)
    {
        FinalInterview? interview = await _interviewRepository.GetByApplicationIdForUpdateAsync(applicationId, ct);
        if (interview is null)
            throw AppErrors.InterviewNotFound;

        if (interview.Status is not (InterviewStatus.Completed or InterviewStatus.InProgress))
            throw AppErrors.InterviewAlreadyCompleted
                .WithMessage("Interview must be InProgress or Completed to record a decision");

        DateTime now = DateTime.UtcNow;
        interview.OverallRecommendation = request.OverallRecommendation;
        interview.DecisionNotes = request.DecisionNotes;
        interview.DecidedBy = decidedByUserId;
        interview.DecidedAt = now;

        if (interview.Status != InterviewStatus.Completed)
        {
            interview.Status = InterviewStatus.Completed;
            interview.CompletedAt = now;
        }

        interview.UpdatedAt = now;

        // Negative recommendation → reject application at FinalInterview stage
        if (!InterviewRecommendation.IsPositive(request.OverallRecommendation))
        {
            await _statusUpdater.UpdateStatusAsync(
                applicationId,
                "Rejected",
                rejectionReason: request.DecisionNotes,
                rejectedAtStage: "FinalInterview",
                ct);
        }

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Recorded decision {Recommendation} for interview {ApplicationId} by {DecidedBy}",
            request.OverallRecommendation, applicationId, decidedByUserId);

        return MapToResponse(interview);
    }

    public async Task CancelInterviewAsync(
        Guid applicationId, string reason, CancellationToken ct = default)
    {
        FinalInterview? interview = await _interviewRepository.GetByApplicationIdForUpdateAsync(applicationId, ct);
        if (interview is null)
            throw AppErrors.InterviewNotFound;

        if (interview.Status is InterviewStatus.Completed)
            throw AppErrors.InterviewAlreadyCompleted;

        if (interview.Status is InterviewStatus.Cancelled)
            throw AppErrors.InterviewAlreadyCancelled;

        DateTime now = DateTime.UtcNow;
        interview.Status = InterviewStatus.Cancelled;
        interview.CancelledAt = now;
        interview.CancellationReason = reason;
        interview.UpdatedAt = now;

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Cancelled interview for application {ApplicationId}", applicationId);
    }

    internal FinalInterviewResponse MapToResponse(FinalInterview interview)
    {
        List<InterviewPanelist> panelists = interview.Panelists?.ToList() ?? [];

        string? aggregatedRecommendation = _feedbackAggregation.AggregateRecommendation(panelists);

        return new FinalInterviewResponse
        {
            ApplicationId = interview.ApplicationId,
            Status = interview.Status,
            InterviewType = interview.InterviewType,
            ScheduledAt = interview.ScheduledAt,
            DurationMinutes = interview.DurationMinutes,
            Location = interview.Location,
            ScheduledBy = interview.ScheduledBy,
            OverallRecommendation = interview.OverallRecommendation,
            DecisionNotes = interview.DecisionNotes,
            DecidedBy = interview.DecidedBy,
            DecidedAt = interview.DecidedAt,
            CompletedAt = interview.CompletedAt,
            CancelledAt = interview.CancelledAt,
            CancellationReason = interview.CancellationReason,
            AggregatedRecommendation = aggregatedRecommendation,
            Panelists = panelists
                .Select(p => new PanelistResponse
                {
                    Id = p.Id,
                    InterviewerId = p.InterviewerId,
                    Rating = p.Rating,
                    Recommendation = p.Recommendation,
                    Strengths = p.Strengths,
                    Concerns = p.Concerns,
                    Notes = p.Notes,
                    FeedbackSubmittedAt = p.FeedbackSubmittedAt,
                    CreatedAt = p.CreatedAt
                })
                .ToList(),
            CreatedAt = interview.CreatedAt,
            UpdatedAt = interview.UpdatedAt
        };
    }
}

using Jobsite.Modules.HRWorkflows.Domain.Constants;
using Jobsite.Modules.HRWorkflows.Domain.Entities;
using Jobsite.Modules.HRWorkflows.Domain.Interfaces;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.HRWorkflows.Application.EventHandlers;

/// <summary>
/// Handles <see cref="SharedKernel.Events.CandidateShortlistedEvent"/> from the Matching module.
/// Auto-creates a placeholder <see cref="FinalInterview"/> for the shortlisted candidate.
/// </summary>
public sealed class CandidateShortlistedHRWorkflowsHandler : IDomainEventHandler<SharedKernel.Events.CandidateShortlistedEvent>
{
    private readonly IFinalInterviewRepository _interviewRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CandidateShortlistedHRWorkflowsHandler> _logger;

    public CandidateShortlistedHRWorkflowsHandler(
        IFinalInterviewRepository interviewRepository,
        [FromKeyedServices("hr_workflows")] IUnitOfWork unitOfWork,
        ILogger<CandidateShortlistedHRWorkflowsHandler> logger)
    {
        _interviewRepository = interviewRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task HandleAsync(SharedKernel.Events.CandidateShortlistedEvent domainEvent, CancellationToken ct)
    {
        _logger.LogInformation(
            "Handling CandidateShortlistedEvent for application {ApplicationId}",
            domainEvent.ApplicationId);

        // Idempotency — already has a final interview record?
        FinalInterview? existing = await _interviewRepository.GetByApplicationIdAsync(domainEvent.ApplicationId, ct);
        if (existing is not null)
        {
            _logger.LogWarning(
                "FinalInterview already exists for application {ApplicationId} — skipping placeholder creation",
                domainEvent.ApplicationId);
            return;
        }

        // Create placeholder interview — recruiter completes scheduling via PATCH
        DateTime now = DateTime.UtcNow;
        FinalInterview placeholder = new()
        {
            ApplicationId = domainEvent.ApplicationId,
            Status = InterviewStatus.Scheduled,
            InterviewType = InterviewType.Video,
            ScheduledAt = now.AddDays(7),
            DurationMinutes = 60,
            ScheduledBy = Guid.Empty,
            CreatedAt = now,
            UpdatedAt = now
        };

        _interviewRepository.Add(placeholder);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created placeholder final interview for application {ApplicationId}",
            domainEvent.ApplicationId);
    }
}

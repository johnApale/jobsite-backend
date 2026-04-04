using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Events;

namespace Jobsite.Modules.Admin.Application.EventHandlers;

public sealed class AssessmentCompletedAuditHandler : IDomainEventHandler<AssessmentCompletedEvent>
{
    private readonly IAuditLogService _auditLogService;

    public AssessmentCompletedAuditHandler(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task HandleAsync(AssessmentCompletedEvent domainEvent, CancellationToken ct)
    {
        await _auditLogService.LogAsync(
            actorId: domainEvent.ApplicantUserId,
            actorEmail: "system",
            actorRole: "Applicant",
            action: AuditAction.AssessmentCompleted,
            entityType: AuditEntityType.ScreeningResult,
            entityId: domainEvent.ApplicationId,
            details: new
            {
                job_posting_id = domainEvent.JobPostingId,
                assessment_score = domainEvent.AssessmentScore,
                completed_at = domainEvent.CompletedAt
            },
            ipAddress: null,
            userAgent: null,
            ct);
    }
}

using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.SharedKernel.Events;
using MediatR;

namespace Jobsite.Modules.Admin.Application.EventHandlers;

public sealed class AssessmentCompletedAuditHandler : INotificationHandler<AssessmentCompletedEvent>
{
    private readonly IAuditLogService _auditLogService;

    public AssessmentCompletedAuditHandler(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task Handle(AssessmentCompletedEvent notification, CancellationToken ct)
    {
        await _auditLogService.LogAsync(
            actorId: notification.ApplicantUserId,
            actorEmail: "system",
            actorRole: "Applicant",
            action: AuditAction.AssessmentCompleted,
            entityType: AuditEntityType.ScreeningResult,
            entityId: notification.ApplicationId,
            details: new
            {
                job_posting_id = notification.JobPostingId,
                assessment_score = notification.AssessmentScore,
                completed_at = notification.CompletedAt
            },
            ipAddress: null,
            userAgent: null,
            ct);
    }
}

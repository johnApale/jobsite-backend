using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.SharedKernel.Events;
using MediatR;

namespace Jobsite.Modules.Admin.Application.EventHandlers;

/// <summary>
/// Records an audit log entry when an application is submitted.
/// </summary>
public sealed class ApplicationSubmittedAuditHandler : INotificationHandler<ApplicationSubmittedEvent>
{
    private readonly IAuditLogService _auditLogService;

    public ApplicationSubmittedAuditHandler(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task Handle(ApplicationSubmittedEvent notification, CancellationToken ct)
    {
        await _auditLogService.LogAsync(
            actorId: notification.ApplicantUserId,
            actorEmail: "system",
            actorRole: "Applicant",
            action: AuditAction.ApplicationSubmitted,
            entityType: AuditEntityType.Application,
            entityId: notification.ApplicationId,
            details: new
            {
                job_posting_id = notification.JobPostingId,
                submitted_at = notification.SubmittedAt
            },
            ipAddress: null,
            userAgent: null,
            ct);
    }
}

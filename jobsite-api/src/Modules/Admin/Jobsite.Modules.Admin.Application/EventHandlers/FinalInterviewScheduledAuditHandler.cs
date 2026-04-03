using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.SharedKernel.Events;
using MediatR;

namespace Jobsite.Modules.Admin.Application.EventHandlers;

/// <summary>
/// Records an audit log entry when a final interview is scheduled.
/// </summary>
public sealed class FinalInterviewScheduledAuditHandler : INotificationHandler<FinalInterviewScheduledEvent>
{
    private readonly IAuditLogService _auditLogService;

    public FinalInterviewScheduledAuditHandler(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task Handle(FinalInterviewScheduledEvent notification, CancellationToken ct)
    {
        await _auditLogService.LogAsync(
            actorId: Guid.Empty,
            actorEmail: "system",
            actorRole: "System",
            action: AuditAction.FinalInterviewScheduled,
            entityType: AuditEntityType.FinalInterview,
            entityId: notification.InterviewId,
            details: new
            {
                application_id = notification.ApplicationId,
                scheduled_at = notification.ScheduledAt
            },
            ipAddress: null,
            userAgent: null,
            ct);
    }
}

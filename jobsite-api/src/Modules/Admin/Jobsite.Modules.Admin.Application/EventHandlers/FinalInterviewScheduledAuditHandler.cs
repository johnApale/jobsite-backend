using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Events;

namespace Jobsite.Modules.Admin.Application.EventHandlers;

/// <summary>
/// Records an audit log entry when a final interview is scheduled.
/// </summary>
public sealed class FinalInterviewScheduledAuditHandler : IDomainEventHandler<FinalInterviewScheduledEvent>
{
    private readonly IAuditLogService _auditLogService;

    public FinalInterviewScheduledAuditHandler(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task HandleAsync(FinalInterviewScheduledEvent domainEvent, CancellationToken ct)
    {
        await _auditLogService.LogAsync(
            actorId: Guid.Empty,
            actorEmail: "system",
            actorRole: "System",
            action: AuditAction.FinalInterviewScheduled,
            entityType: AuditEntityType.FinalInterview,
            entityId: domainEvent.InterviewId,
            details: new
            {
                application_id = domainEvent.ApplicationId,
                scheduled_at = domainEvent.ScheduledAt
            },
            ipAddress: null,
            userAgent: null,
            ct);
    }
}

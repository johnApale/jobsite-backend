using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.SharedKernel.Events;
using MediatR;

namespace Jobsite.Modules.Admin.Application.EventHandlers;

/// <summary>
/// Records an audit log entry when CV screening completes.
/// </summary>
public sealed class CvScreeningCompletedAuditHandler : INotificationHandler<CvScreeningCompletedEvent>
{
    private readonly IAuditLogService _auditLogService;

    public CvScreeningCompletedAuditHandler(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task Handle(CvScreeningCompletedEvent notification, CancellationToken ct)
    {
        await _auditLogService.LogAsync(
            actorId: Guid.Empty,
            actorEmail: "system",
            actorRole: "System",
            action: AuditAction.CvScreeningCompleted,
            entityType: AuditEntityType.ScreeningResult,
            entityId: notification.ScreeningResultId,
            details: new
            {
                application_id = notification.ApplicationId,
                passed_screening = notification.PassedScreening,
                completed_at = notification.CompletedAt
            },
            ipAddress: null,
            userAgent: null,
            ct);
    }
}

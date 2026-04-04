using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Events;

namespace Jobsite.Modules.Admin.Application.EventHandlers;

/// <summary>
/// Records an audit log entry when CV screening completes.
/// </summary>
public sealed class CvScreeningCompletedAuditHandler : IDomainEventHandler<CvScreeningCompletedEvent>
{
    private readonly IAuditLogService _auditLogService;

    public CvScreeningCompletedAuditHandler(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task HandleAsync(CvScreeningCompletedEvent domainEvent, CancellationToken ct)
    {
        await _auditLogService.LogAsync(
            actorId: Guid.Empty,
            actorEmail: "system",
            actorRole: "System",
            action: AuditAction.CvScreeningCompleted,
            entityType: AuditEntityType.ScreeningResult,
            entityId: domainEvent.ScreeningResultId,
            details: new
            {
                application_id = domainEvent.ApplicationId,
                passed_screening = domainEvent.PassedScreening,
                completed_at = domainEvent.CompletedAt
            },
            ipAddress: null,
            userAgent: null,
            ct);
    }
}

using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.SharedKernel.Events;
using MediatR;

namespace Jobsite.Modules.Admin.Application.EventHandlers;

/// <summary>
/// Records an audit log entry when a job offer is extended.
/// </summary>
public sealed class OfferExtendedAuditHandler : INotificationHandler<OfferExtendedEvent>
{
    private readonly IAuditLogService _auditLogService;

    public OfferExtendedAuditHandler(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task Handle(OfferExtendedEvent notification, CancellationToken ct)
    {
        await _auditLogService.LogAsync(
            actorId: Guid.Empty,
            actorEmail: "system",
            actorRole: "System",
            action: AuditAction.OfferExtended,
            entityType: AuditEntityType.JobOffer,
            entityId: notification.OfferId,
            details: new
            {
                application_id = notification.ApplicationId,
                offered_at = notification.OfferedAt
            },
            ipAddress: null,
            userAgent: null,
            ct);
    }
}

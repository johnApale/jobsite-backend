using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Events;

namespace Jobsite.Modules.Admin.Application.EventHandlers;

/// <summary>
/// Records an audit log entry when a job offer is extended.
/// </summary>
public sealed class OfferExtendedAuditHandler : IDomainEventHandler<OfferExtendedEvent>
{
    private readonly IAuditLogService _auditLogService;

    public OfferExtendedAuditHandler(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task HandleAsync(OfferExtendedEvent domainEvent, CancellationToken ct)
    {
        await _auditLogService.LogAsync(
            actorId: Guid.Empty,
            actorEmail: "system",
            actorRole: "System",
            action: AuditAction.OfferExtended,
            entityType: AuditEntityType.JobOffer,
            entityId: domainEvent.OfferId,
            details: new
            {
                application_id = domainEvent.ApplicationId,
                offered_at = domainEvent.OfferedAt
            },
            ipAddress: null,
            userAgent: null,
            ct);
    }
}

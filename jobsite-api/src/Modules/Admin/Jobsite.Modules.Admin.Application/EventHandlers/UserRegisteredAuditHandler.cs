using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Events;

namespace Jobsite.Modules.Admin.Application.EventHandlers;

/// <summary>
/// Records an audit log entry when a new user registers.
/// </summary>
public sealed class UserRegisteredAuditHandler : IDomainEventHandler<UserRegisteredEvent>
{
    private readonly IAuditLogService _auditLogService;

    public UserRegisteredAuditHandler(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task HandleAsync(UserRegisteredEvent domainEvent, CancellationToken ct)
    {
        await _auditLogService.LogAsync(
            actorId: domainEvent.UserId,
            actorEmail: domainEvent.Email,
            actorRole: domainEvent.Role,
            action: AuditAction.UserRegistered,
            entityType: AuditEntityType.User,
            entityId: domainEvent.UserId,
            details: new { registered_at = domainEvent.RegisteredAt },
            ipAddress: null,
            userAgent: null,
            ct);
    }
}

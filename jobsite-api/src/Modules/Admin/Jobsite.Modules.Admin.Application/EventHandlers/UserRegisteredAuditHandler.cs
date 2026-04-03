using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.SharedKernel.Events;
using MediatR;

namespace Jobsite.Modules.Admin.Application.EventHandlers;

/// <summary>
/// Records an audit log entry when a new user registers.
/// </summary>
public sealed class UserRegisteredAuditHandler : INotificationHandler<UserRegisteredEvent>
{
    private readonly IAuditLogService _auditLogService;

    public UserRegisteredAuditHandler(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    public async Task Handle(UserRegisteredEvent notification, CancellationToken ct)
    {
        await _auditLogService.LogAsync(
            actorId: notification.UserId,
            actorEmail: notification.Email,
            actorRole: notification.Role,
            action: AuditAction.UserRegistered,
            entityType: AuditEntityType.User,
            entityId: notification.UserId,
            details: new { registered_at = notification.RegisteredAt },
            ipAddress: null,
            userAgent: null,
            ct);
    }
}

using Jobsite.Modules.Admin.Application.DTOs;

namespace Jobsite.Modules.Admin.Application.Interfaces;

/// <summary>Application service for audit trail management.</summary>
public interface IAuditLogService
{
    /// <summary>Record an audit log entry.</summary>
    Task LogAsync(
        Guid actorId,
        string actorEmail,
        string actorRole,
        string action,
        string entityType,
        Guid? entityId,
        object? details,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default);

    /// <summary>Query audit logs with pagination and filters.</summary>
    Task<AuditLogPageResponse> QueryAsync(AuditLogQueryParameters parameters, CancellationToken ct = default);
}

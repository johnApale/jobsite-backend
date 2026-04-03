using Jobsite.Modules.Admin.Application.DTOs;
using Jobsite.Modules.Admin.Domain.Entities;

namespace Jobsite.Modules.Admin.Application.Interfaces;

/// <summary>Repository for append-only <c>admin.audit_logs</c>.</summary>
public interface IAuditLogRepository
{
    /// <summary>Query audit logs with cursor-based pagination and optional filters.</summary>
    Task<AuditLogPageResponse> GetPageAsync(AuditLogQueryParameters parameters, CancellationToken ct = default);

    /// <summary>Append a new audit log entry.</summary>
    void Add(AuditLog log);
}

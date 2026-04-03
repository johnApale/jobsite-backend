namespace Jobsite.Modules.Admin.Application.DTOs;

/// <summary>Paginated response for audit log queries.</summary>
public sealed class AuditLogPageResponse
{
    public required List<AuditLogResponse> Items { get; init; }

    /// <summary>Cursor for the next page. Null if no more results.</summary>
    public string? NextCursor { get; init; }
}

namespace Jobsite.Modules.Admin.Application.DTOs;

/// <summary>Query parameters for <c>GET /api/v1/admin/audit-logs</c>.</summary>
public sealed class AuditLogQueryParameters
{
    /// <summary>Filter by action type (e.g., UserRegistered).</summary>
    public string? Action { get; init; }

    /// <summary>Filter by actor user ID.</summary>
    public Guid? ActorId { get; init; }

    /// <summary>Filter by entity type (e.g., User, CompanySettings).</summary>
    public string? EntityType { get; init; }

    /// <summary>Filter by start date (inclusive).</summary>
    public DateTime? DateFrom { get; init; }

    /// <summary>Filter by end date (inclusive).</summary>
    public DateTime? DateTo { get; init; }

    /// <summary>Cursor for pagination (opaque string from previous response).</summary>
    public string? Cursor { get; init; }

    /// <summary>Number of results per page. Default 20, max 100.</summary>
    public int PageSize { get; init; } = 20;
}

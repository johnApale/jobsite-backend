namespace Jobsite.Modules.Tenancy.Application.DTOs;

/// <summary>Paginated list of tenants for platform admin.</summary>
public sealed class TenantListResponse
{
    public required List<TenantResponse> Items { get; init; }
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
}

/// <summary>Query parameters for <c>GET /api/v1/platform/tenants</c>.</summary>
public sealed class TenantQueryParameters
{
    public string? Status { get; init; }
    public string? Search { get; init; }
    public string? Cursor { get; init; }
    public int PageSize { get; init; } = 20;
}

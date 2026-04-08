using Jobsite.Modules.Tenancy.Application.DTOs;
using Jobsite.Modules.Tenancy.Application.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Jobsite.Modules.Tenancy.Api;

/// <summary>
/// Minimal API endpoint definitions for platform-wide tenant administration.
/// Route prefix: <c>/api/v1/platform/tenants</c>.
/// Requires <c>RequirePlatformAdmin</c> authorization policy.
/// </summary>
public static class PlatformAdminEndpoints
{
    public static void MapPlatformAdminEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/platform/tenants")
            .WithTags("PlatformAdmin")
            .RequireAuthorization("RequirePlatformAdmin")
            .RequireRateLimiting("global");

        group.MapGet("/", async (
                string? status,
                string? search,
                string? cursor,
                int? pageSize,
                IPlatformAdminService service,
                CancellationToken ct) =>
            {
                TenantQueryParameters parameters = new()
                {
                    Status = status,
                    Search = search,
                    Cursor = cursor,
                    PageSize = pageSize ?? 20
                };

                TenantListResponse response = await service.GetTenantsAsync(parameters, ct);
                return Results.Ok(response);
            })
            .WithName("ListTenants")
            .WithSummary("List all tenants")
            .WithDescription("Returns a paginated list of all tenants with optional filters by status and search term. Uses cursor-based pagination.")
            .Produces<TenantListResponse>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (Guid id, IPlatformAdminService service, CancellationToken ct) =>
            {
                TenantResponse response = await service.GetTenantByIdAsync(id, ct);
                return Results.Ok(response);
            })
            .WithName("GetTenantByIdPlatform")
            .WithSummary("Get tenant by ID (platform admin)")
            .WithDescription("Retrieves full tenant metadata and branding by tenant ID for platform administration.")
            .Produces<TenantResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/suspend", async (Guid id, IPlatformAdminService service, CancellationToken ct) =>
            {
                TenantResponse response = await service.SuspendTenantAsync(id, ct);
                return Results.Ok(response);
            })
            .WithName("SuspendTenant")
            .WithSummary("Suspend a tenant")
            .WithDescription("Suspends an active tenant, preventing all access. Only active tenants can be suspended.")
            .Produces<TenantResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/reactivate", async (Guid id, IPlatformAdminService service, CancellationToken ct) =>
            {
                TenantResponse response = await service.ReactivateTenantAsync(id, ct);
                return Results.Ok(response);
            })
            .WithName("ReactivateTenant")
            .WithSummary("Reactivate a suspended tenant")
            .WithDescription("Reactivates a previously suspended tenant. Only suspended tenants can be reactivated.")
            .Produces<TenantResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }
}

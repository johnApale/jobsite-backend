using Jobsite.Modules.Tenancy.Application.DTOs;
using Jobsite.Modules.Tenancy.Application.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Jobsite.Modules.Tenancy.Api;

/// <summary>
/// Minimal API endpoint definitions for the Tenancy module.
/// Route prefix: <c>/api/v1/tenants</c>.
/// </summary>
public static class TenantEndpoints
{
    public static void MapTenancyEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/tenants")
            .WithTags("Tenants");

        group.MapGet("/{id:guid}", async (Guid id, ITenantService service, CancellationToken ct) =>
            {
                TenantResponse response = await service.GetByIdAsync(id, ct);
                return Results.Ok(response);
            })
            .WithName("GetTenantById")
            .WithSummary("Get tenant by ID")
            .WithDescription("Retrieves tenant metadata and branding by tenant ID.")
            .Produces<TenantResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/register", async (RegisterTenantRequest request, ITenantService service, CancellationToken ct) =>
            {
                TenantResponse response = await service.RegisterAsync(request, ct);
                return Results.Created($"/api/v1/tenants/{response.Id}", response);
            })
            .WithName("RegisterTenant")
            .WithSummary("Register a new tenant")
            .WithDescription("Creates a new tenant in Provisioning status. Triggers async database provisioning.")
            .Produces<TenantResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);
    }
}

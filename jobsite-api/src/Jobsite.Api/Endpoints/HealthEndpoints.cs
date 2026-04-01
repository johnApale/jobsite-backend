using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Jobsite.Api.Endpoints;

/// <summary>
/// Health check endpoints for infrastructure probes.
/// </summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
            .WithTags("Health")
            .WithName("Health")
            .WithSummary("Health check")
            .ExcludeFromDescription();

        app.MapGet("/ready", () => Results.Ok(new { status = "ready" }))
            .WithTags("Health")
            .WithName("Ready")
            .WithSummary("Readiness check")
            .ExcludeFromDescription();
    }
}

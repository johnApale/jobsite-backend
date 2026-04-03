using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Jobsite.Modules.Profiles.Api;

/// <summary>
/// Minimal API endpoint definitions for the Profiles module.
/// Route prefix: <c>/api/v1/profiles</c>.
/// </summary>
public static class ProfileEndpoints
{
    public static void MapProfilesEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/profiles")
            .WithTags("Profiles")
            .RequireAuthorization();

        // Profile CRUD and resume endpoints will be added in Part B and Part C.
    }
}

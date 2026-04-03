using System.Security.Claims;
using Jobsite.Modules.Profiles.Application.DTOs;
using Jobsite.Modules.Profiles.Application.Interfaces;
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

        group.MapGet("/me", async (IProfileService service, HttpContext http, CancellationToken ct) =>
            {
                Guid userId = GetUserId(http);
                ProfileResponse response = await service.GetByUserIdAsync(userId, ct);
                return Results.Ok(response);
            })
            .WithName("GetMyProfile")
            .WithSummary("Get current user's profile")
            .WithDescription("Returns the applicant profile for the authenticated user.")
            .Produces<ProfileResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/me", async (CreateProfileRequest request, IProfileService service, HttpContext http, CancellationToken ct) =>
            {
                Guid userId = GetUserId(http);
                ProfileResponse response = await service.CreateAsync(request, userId, ct);
                return Results.Created("/api/v1/profiles/me", response);
            })
            .WithName("CreateMyProfile")
            .WithSummary("Create current user's profile")
            .WithDescription("Creates a new applicant profile for the authenticated user. Fails if a profile already exists.")
            .Produces<ProfileResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPatch("/me", async (UpdateProfileRequest request, IProfileService service, HttpContext http, CancellationToken ct) =>
            {
                Guid userId = GetUserId(http);
                ProfileResponse response = await service.UpdateAsync(request, userId, ct);
                return Results.Ok(response);
            })
            .WithName("UpdateMyProfile")
            .WithSummary("Update current user's profile")
            .WithDescription("Applies a JSON merge patch to the authenticated user's profile. Only provided fields are updated.")
            .Produces<ProfileResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        // Resume endpoints will be added in Part C.
    }

    private static Guid GetUserId(HttpContext http)
    {
        string? sub = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? http.User.FindFirstValue("sub");

        if (sub is not null && Guid.TryParse(sub, out Guid userId))
            return userId;

        throw new InvalidOperationException("User ID not found in JWT claims.");
    }
}

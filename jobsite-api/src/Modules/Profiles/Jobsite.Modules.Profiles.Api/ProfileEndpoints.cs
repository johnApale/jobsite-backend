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
            .RequireAuthorization()
            .RequireRateLimiting("global");

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

        // ── Resume endpoints ──────────────────────────────────────────────

        group.MapPost("/me/resumes", async (IResumeService resumeService, HttpContext http, CancellationToken ct) =>
            {
                IFormFile? formFile = http.Request.Form.Files.GetFile("file");
                if (formFile is null || formFile.Length == 0)
                    return Results.BadRequest(new { error = "File is required" });

                Guid userId = GetUserId(http);
                Guid tenantId = GetTenantId(http);
                await using Stream stream = formFile.OpenReadStream();
                ResumeResponse response = await resumeService.UploadResumeAsync(
                    userId, tenantId, stream, formFile.FileName, formFile.Length, ct);
                return Results.Created($"/api/v1/profiles/me/resumes/{response.Id}", response);
            })
            .WithName("UploadResume")
            .WithSummary("Upload a resume")
            .WithDescription("Uploads a PDF or DOCX resume. Marks previous resumes as not latest. Triggers async parsing.")
            .DisableAntiforgery()
            .Produces<ResumeResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/me/resumes", async (IResumeService resumeService, HttpContext http, CancellationToken ct) =>
            {
                Guid userId = GetUserId(http);
                List<ResumeResponse> response = await resumeService.GetResumesAsync(userId, ct);
                return Results.Ok(response);
            })
            .WithName("GetMyResumes")
            .WithSummary("List all resumes")
            .WithDescription("Returns all resumes uploaded by the authenticated user, ordered by most recent first.")
            .Produces<List<ResumeResponse>>(StatusCodes.Status200OK);

        group.MapGet("/me/resumes/{id:guid}", async (Guid id, IResumeService resumeService, HttpContext http, CancellationToken ct) =>
            {
                Guid userId = GetUserId(http);
                ResumeResponse response = await resumeService.GetResumeByIdAsync(id, userId, ct);
                return Results.Ok(response);
            })
            .WithName("GetResumeById")
            .WithSummary("Get a specific resume")
            .WithDescription("Returns a specific resume by ID, if it belongs to the authenticated user.")
            .Produces<ResumeResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static Guid GetTenantId(HttpContext http)
    {
        object? tenantId = http.Items["TenantId"];
        if (tenantId is Guid id)
            return id;

        throw new InvalidOperationException("TenantId not found in request context. Ensure TenantResolutionMiddleware is configured.");
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

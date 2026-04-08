using System.Security.Claims;
using Jobsite.Modules.Matching.Application.DTOs;
using Jobsite.Modules.Matching.Application.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Jobsite.Modules.Matching.Api;

public static class MatchingEndpoints
{
    public static void MapMatchingEndpoints(this IEndpointRouteBuilder app)
    {
        MapCandidateMatchEndpoints(app);
        MapShortlistEndpoints(app);
    }

    // ── Candidate Match endpoints ────────────────────────────────────────

    private static void MapCandidateMatchEndpoints(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/matching/matches")
            .WithTags("Matching - Candidate Matches")
            .RequireAuthorization()
            .RequireRateLimiting("global");

        group.MapGet("/{applicationId:guid}", async (
                Guid applicationId,
                IMatchingService service,
                CancellationToken ct) =>
            {
                CandidateMatchResponse response = await service.GetMatchAsync(applicationId, ct);
                return Results.Ok(response);
            })
            .WithName("GetCandidateMatch")
            .WithSummary("Get candidate match for an application")
            .WithDescription("Returns the full candidate match including screening score, assessment score, composite score, and match strength.")
            .Produces<CandidateMatchResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", async (
                Guid? jobPostingId,
                string? matchStrength,
                string? cursor,
                int? pageSize,
                IMatchingService service,
                CancellationToken ct) =>
            {
                CandidateMatchQueryParameters parameters = new()
                {
                    JobPostingId = jobPostingId,
                    MatchStrength = matchStrength,
                    Cursor = cursor,
                    PageSize = pageSize ?? 20
                };

                CandidateMatchListResponse response = await service.ListMatchesAsync(parameters, ct);
                return Results.Ok(response);
            })
            .WithName("ListCandidateMatches")
            .WithSummary("List candidate matches")
            .WithDescription("Returns paginated candidate matches for a job posting with optional match strength filter.")
            .Produces<CandidateMatchListResponse>(StatusCodes.Status200OK);
    }

    // ── Shortlist endpoints ──────────────────────────────────────────────

    private static void MapShortlistEndpoints(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/matching/shortlists")
            .WithTags("Matching - Shortlists")
            .RequireAuthorization()
            .RequireRateLimiting("global");

        group.MapPost("/", async (
                GenerateShortlistRequest request,
                IShortlistService service,
                CancellationToken ct) =>
            {
                ShortlistResponse response =
                    await service.GenerateShortlistAsync(request.JobPostingId, ct);
                return Results.Created($"/api/v1/matching/shortlists/{response.Id}", response);
            })
            .WithName("GenerateShortlist")
            .WithSummary("Generate a shortlist for a job posting")
            .WithDescription("Creates a new shortlist with the top-N candidates based on composite scores. Uses tenant-configured shortlist size.")
            .RequireAuthorization("RequireRecruiterOrAdmin")
            .Produces<ShortlistResponse>(StatusCodes.Status201Created);

        group.MapGet("/{shortlistId:guid}", async (
                Guid shortlistId,
                IShortlistService service,
                CancellationToken ct) =>
            {
                ShortlistResponse response = await service.GetShortlistAsync(shortlistId, ct);
                return Results.Ok(response);
            })
            .WithName("GetShortlist")
            .WithSummary("Get a shortlist by ID")
            .WithDescription("Returns the full shortlist with embedded candidate details.")
            .Produces<ShortlistResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", async (
                Guid? jobPostingId,
                string? status,
                string? cursor,
                int? pageSize,
                IShortlistService service,
                CancellationToken ct) =>
            {
                ShortlistQueryParameters parameters = new()
                {
                    JobPostingId = jobPostingId,
                    Status = status,
                    Cursor = cursor,
                    PageSize = pageSize ?? 20
                };

                ShortlistListResponse response = await service.ListShortlistsAsync(parameters, ct);
                return Results.Ok(response);
            })
            .WithName("ListShortlists")
            .WithSummary("List shortlists")
            .WithDescription("Returns paginated shortlists for a job posting with optional status filter.")
            .Produces<ShortlistListResponse>(StatusCodes.Status200OK);

        group.MapPost("/{shortlistId:guid}/candidates", async (
                Guid shortlistId,
                AddCandidateToShortlistRequest request,
                IShortlistService service,
                CancellationToken ct) =>
            {
                ShortlistResponse response =
                    await service.AddCandidateAsync(shortlistId, request.ApplicationId, ct);
                return Results.Ok(response);
            })
            .WithName("AddCandidateToShortlist")
            .WithSummary("Add a candidate to a shortlist")
            .WithDescription("Manually adds a candidate to an existing draft shortlist.")
            .RequireAuthorization("RequireRecruiterOrAdmin")
            .Produces<ShortlistResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapDelete("/{shortlistId:guid}/candidates/{applicationId:guid}", async (
                Guid shortlistId,
                Guid applicationId,
                IShortlistService service,
                CancellationToken ct) =>
            {
                await service.RemoveCandidateAsync(shortlistId, applicationId, ct);
                return Results.NoContent();
            })
            .WithName("RemoveCandidateFromShortlist")
            .WithSummary("Remove a candidate from a shortlist")
            .WithDescription("Soft-removes a candidate from a draft shortlist.")
            .RequireAuthorization("RequireRecruiterOrAdmin")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{shortlistId:guid}/finalize", async (
                Guid shortlistId,
                HttpContext http,
                IShortlistService service,
                CancellationToken ct) =>
            {
                Guid userId = GetUserId(http);
                ShortlistResponse response =
                    await service.FinalizeShortlistAsync(shortlistId, userId, ct);
                return Results.Ok(response);
            })
            .WithName("FinalizeShortlist")
            .WithSummary("Finalize a shortlist")
            .WithDescription("Locks the shortlist, updates application statuses to 'Shortlisted', and publishes CandidateShortlistedEvent for each candidate.")
            .RequireAuthorization("RequireRecruiterOrAdmin")
            .Produces<ShortlistResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }

    // ── Helper methods ───────────────────────────────────────────────────

    private static Guid GetUserId(HttpContext http)
    {
        string? sub = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? http.User.FindFirstValue("sub");

        if (sub is not null && Guid.TryParse(sub, out Guid userId))
            return userId;

        throw new InvalidOperationException("User ID not found in JWT claims.");
    }
}

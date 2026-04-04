using System.Security.Claims;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Application.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Jobsite.Modules.Screening.Api;

public static class ScreeningEndpoints
{
    public static void MapScreeningEndpoints(this IEndpointRouteBuilder app)
    {
        MapScreeningResultEndpoints(app);
        MapAssessmentEndpoints(app);
    }

    // ── Screening Result endpoints ───────────────────────────────────────

    private static void MapScreeningResultEndpoints(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/screening/results")
            .WithTags("Screening - Results")
            .RequireAuthorization();

        group.MapGet("/{applicationId:guid}", async (
                Guid applicationId,
                IScreeningService service,
                CancellationToken ct) =>
            {
                ScreeningResultResponse response = await service.GetResultAsync(applicationId, ct);
                return Results.Ok(response);
            })
            .WithName("GetScreeningResult")
            .WithSummary("Get screening result for an application")
            .WithDescription("Returns the full screening result including scores, breakdowns, and routing outcome.")
            .Produces<ScreeningResultResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", async (
                string? status,
                string? matchStrength,
                string? outcome,
                string? cursor,
                int? pageSize,
                IScreeningService service,
                CancellationToken ct) =>
            {
                ScreeningResultQueryParameters parameters = new()
                {
                    Status = status,
                    MatchStrength = matchStrength,
                    Outcome = outcome,
                    Cursor = cursor,
                    PageSize = pageSize ?? 20
                };

                ScreeningResultListResponse response = await service.ListResultsAsync(parameters, ct);
                return Results.Ok(response);
            })
            .WithName("ListScreeningResults")
            .WithSummary("List screening results")
            .WithDescription("Returns paginated screening results with optional filters. Uses cursor-based pagination.")
            .Produces<ScreeningResultListResponse>(StatusCodes.Status200OK);

        group.MapPost("/{applicationId:guid}/review", async (
                Guid applicationId,
                ManualReviewRequest request,
                HttpContext http,
                IScreeningService service,
                CancellationToken ct) =>
            {
                Guid reviewerId = GetUserId(http);
                ScreeningResultResponse response = await service.ManualReviewAsync(
                    applicationId, request, reviewerId, ct);
                return Results.Ok(response);
            })
            .WithName("ManualReviewScreeningResult")
            .WithSummary("Submit a manual review decision")
            .WithDescription("Allows a recruiter to manually advance or reject an application that was queued for manual review.")
            .RequireAuthorization("RequireRecruiterOrAdmin")
            .Produces<ScreeningResultResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{applicationId:guid}/feedback", async (
                Guid applicationId,
                IScreeningService service,
                CancellationToken ct) =>
            {
                ScreeningResultResponse result = await service.GetResultAsync(applicationId, ct);
                CandidateFeedbackResponse response = new()
                {
                    ApplicationId = applicationId,
                    Feedback = result.CandidateFeedback
                };
                return Results.Ok(response);
            })
            .WithName("GetCandidateFeedback")
            .WithSummary("Get candidate feedback for an application")
            .WithDescription("Returns the candidate-facing transparency feedback if available.")
            .Produces<CandidateFeedbackResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    // ── Assessment endpoints ─────────────────────────────────────────────

    private static void MapAssessmentEndpoints(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/screening/assessments")
            .WithTags("Screening - Assessments")
            .RequireAuthorization();

        group.MapPost("/{applicationId:guid}", async (
                Guid applicationId,
                SubmitAssessmentRequest request,
                HttpContext http,
                IAssessmentService service,
                CancellationToken ct) =>
            {
                Guid applicantUserId = GetUserId(http);
                // JobPostingId needs to be resolved — include it in the request or resolve from context.
                // For now, it's expected in the request body or resolved by the service.
                await service.SubmitAssessmentAsync(
                    applicationId, request.JobPostingId, applicantUserId, request, ct);
                return Results.NoContent();
            })
            .WithName("SubmitAssessment")
            .WithSummary("Submit assessment answers")
            .WithDescription("Submits answers to AfterScreening questions for an application. Scores are calculated and the application is routed based on the tenant's completion policy.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapGet("/{applicationId:guid}", async (
                Guid applicationId,
                Guid jobPostingId,
                IAssessmentService service,
                CancellationToken ct) =>
            {
                AssessmentStatusResponse response = await service.GetAssessmentStatusAsync(
                    applicationId, jobPostingId, ct);
                return Results.Ok(response);
            })
            .WithName("GetAssessmentStatus")
            .WithSummary("Get assessment status and questions")
            .WithDescription("Returns the assessment status for an application. If not yet submitted, includes the AfterScreening questions to answer.")
            .Produces<AssessmentStatusResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status422UnprocessableEntity);
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

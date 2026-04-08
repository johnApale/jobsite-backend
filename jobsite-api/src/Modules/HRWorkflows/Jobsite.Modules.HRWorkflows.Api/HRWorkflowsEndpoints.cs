using System.Security.Claims;
using Jobsite.Modules.HRWorkflows.Application.DTOs;
using Jobsite.Modules.HRWorkflows.Application.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Jobsite.Modules.HRWorkflows.Api;

public static class HRWorkflowsEndpoints
{
    public static void MapHRWorkflowsEndpoints(this IEndpointRouteBuilder app)
    {
        MapInterviewEndpoints(app);
        MapOfferEndpoints(app);
    }

    // ── Final Interview endpoints ────────────────────────────────────────

    private static void MapInterviewEndpoints(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/hr-workflows/interviews")
            .WithTags("HR Workflows - Final Interviews")
            .RequireAuthorization()
            .RequireRateLimiting("global");

        group.MapPost("/", async (
                ScheduleInterviewRequest request,
                HttpContext http,
                IInterviewService service,
                CancellationToken ct) =>
            {
                Guid userId = GetUserId(http);
                FinalInterviewResponse response =
                    await service.ScheduleInterviewAsync(request, userId, ct);
                return Results.Created(
                    $"/api/v1/hr-workflows/interviews/{response.ApplicationId}", response);
            })
            .WithName("ScheduleInterview")
            .WithSummary("Schedule a final interview")
            .WithDescription("Creates a new final interview for a shortlisted candidate with assigned panelists. Publishes FinalInterviewScheduledEvent and updates application status to FinalInterview.")
            .RequireAuthorization("RequireRecruiterOrAdmin")
            .Produces<FinalInterviewResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict);

        group.MapGet("/{applicationId:guid}", async (
                Guid applicationId,
                IInterviewService service,
                CancellationToken ct) =>
            {
                FinalInterviewResponse response =
                    await service.GetInterviewAsync(applicationId, ct);
                return Results.Ok(response);
            })
            .WithName("GetInterview")
            .WithSummary("Get a final interview by application ID")
            .WithDescription("Returns the full interview details including panelist feedback and aggregated recommendation.")
            .Produces<FinalInterviewResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", async (
                string? status,
                string? cursor,
                int? pageSize,
                IInterviewService service,
                CancellationToken ct) =>
            {
                InterviewQueryParameters parameters = new()
                {
                    Status = status,
                    Cursor = cursor,
                    PageSize = pageSize ?? 20
                };

                InterviewListResponse response =
                    await service.ListInterviewsAsync(parameters, ct);
                return Results.Ok(response);
            })
            .WithName("ListInterviews")
            .WithSummary("List final interviews")
            .WithDescription("Returns paginated final interviews with optional status filter.")
            .Produces<InterviewListResponse>(StatusCodes.Status200OK);

        group.MapPatch("/{applicationId:guid}", async (
                Guid applicationId,
                UpdateInterviewRequest request,
                IInterviewService service,
                CancellationToken ct) =>
            {
                FinalInterviewResponse response =
                    await service.UpdateInterviewAsync(applicationId, request, ct);
                return Results.Ok(response);
            })
            .WithName("UpdateInterview")
            .WithSummary("Update interview schedule")
            .WithDescription("Updates interview type, scheduled time, duration, or location. Only allowed for Scheduled or InProgress interviews.")
            .RequireAuthorization("RequireRecruiterOrAdmin")
            .Produces<FinalInterviewResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{applicationId:guid}/feedback", async (
                Guid applicationId,
                SubmitFeedbackRequest request,
                HttpContext http,
                IInterviewService service,
                CancellationToken ct) =>
            {
                Guid interviewerId = GetUserId(http);
                FinalInterviewResponse response =
                    await service.SubmitPanelistFeedbackAsync(
                        applicationId, interviewerId, request, ct);
                return Results.Ok(response);
            })
            .WithName("SubmitPanelistFeedback")
            .WithSummary("Submit panelist feedback")
            .WithDescription("Allows an assigned panelist to submit their rating, recommendation, and notes. Auto-completes the interview when all panelists have submitted.")
            .Produces<FinalInterviewResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{applicationId:guid}/decision", async (
                Guid applicationId,
                RecordDecisionRequest request,
                HttpContext http,
                IInterviewService service,
                CancellationToken ct) =>
            {
                Guid decidedBy = GetUserId(http);
                FinalInterviewResponse response =
                    await service.RecordDecisionAsync(applicationId, request, decidedBy, ct);
                return Results.Ok(response);
            })
            .WithName("RecordInterviewDecision")
            .WithSummary("Record interview decision")
            .WithDescription("Hiring manager records the overall recommendation after reviewing panelist feedback. Negative recommendations reject the application at the FinalInterview stage.")
            .RequireAuthorization("RequireRecruiterOrAdmin")
            .Produces<FinalInterviewResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{applicationId:guid}/cancel", async (
                Guid applicationId,
                CancelInterviewRequest request,
                IInterviewService service,
                CancellationToken ct) =>
            {
                await service.CancelInterviewAsync(applicationId, request.Reason, ct);
                return Results.NoContent();
            })
            .WithName("CancelInterview")
            .WithSummary("Cancel an interview")
            .WithDescription("Cancels a scheduled or in-progress interview. Cannot cancel completed or already cancelled interviews.")
            .RequireAuthorization("RequireRecruiterOrAdmin")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }

    // ── Job Offer endpoints ──────────────────────────────────────────────

    private static void MapOfferEndpoints(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/hr-workflows/offers")
            .WithTags("HR Workflows - Job Offers")
            .RequireAuthorization()
            .RequireRateLimiting("global");

        group.MapPost("/", async (
                CreateOfferRequest request,
                HttpContext http,
                IOfferService service,
                CancellationToken ct) =>
            {
                Guid userId = GetUserId(http);
                JobOfferResponse response =
                    await service.CreateOfferAsync(request, userId, ct);
                return Results.Created(
                    $"/api/v1/hr-workflows/offers/{response.ApplicationId}", response);
            })
            .WithName("CreateOffer")
            .WithSummary("Create a draft job offer")
            .WithDescription("Creates a new job offer in Draft status for a candidate. Must be extended separately to send to the candidate.")
            .RequireAuthorization("RequireRecruiterOrAdmin")
            .Produces<JobOfferResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict);

        group.MapGet("/{applicationId:guid}", async (
                Guid applicationId,
                IOfferService service,
                CancellationToken ct) =>
            {
                JobOfferResponse response =
                    await service.GetOfferAsync(applicationId, ct);
                return Results.Ok(response);
            })
            .WithName("GetOffer")
            .WithSummary("Get a job offer by application ID")
            .WithDescription("Returns the full offer details including terms, status, and response information.")
            .Produces<JobOfferResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/", async (
                string? status,
                string? cursor,
                int? pageSize,
                IOfferService service,
                CancellationToken ct) =>
            {
                OfferQueryParameters parameters = new()
                {
                    Status = status,
                    Cursor = cursor,
                    PageSize = pageSize ?? 20
                };

                OfferListResponse response =
                    await service.ListOffersAsync(parameters, ct);
                return Results.Ok(response);
            })
            .WithName("ListOffers")
            .WithSummary("List job offers")
            .WithDescription("Returns paginated job offers with optional status filter.")
            .Produces<OfferListResponse>(StatusCodes.Status200OK);

        group.MapPatch("/{applicationId:guid}", async (
                Guid applicationId,
                UpdateOfferRequest request,
                IOfferService service,
                CancellationToken ct) =>
            {
                JobOfferResponse response =
                    await service.UpdateOfferAsync(applicationId, request, ct);
                return Results.Ok(response);
            })
            .WithName("UpdateOffer")
            .WithSummary("Update offer terms")
            .WithDescription("Updates salary, benefits, terms, or other offer details. Only allowed while the offer is in Draft status.")
            .RequireAuthorization("RequireRecruiterOrAdmin")
            .Produces<JobOfferResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{applicationId:guid}/extend", async (
                Guid applicationId,
                IOfferService service,
                CancellationToken ct) =>
            {
                JobOfferResponse response =
                    await service.ExtendOfferAsync(applicationId, ct);
                return Results.Ok(response);
            })
            .WithName("ExtendOffer")
            .WithSummary("Extend an offer to the candidate")
            .WithDescription("Moves a Draft offer to Pending, publishes OfferExtendedEvent, and updates application status to Offered.")
            .RequireAuthorization("RequireRecruiterOrAdmin")
            .Produces<JobOfferResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{applicationId:guid}/respond", async (
                Guid applicationId,
                RespondToOfferRequest request,
                IOfferService service,
                CancellationToken ct) =>
            {
                JobOfferResponse response =
                    await service.RespondToOfferAsync(applicationId, request, ct);
                return Results.Ok(response);
            })
            .WithName("RespondToOffer")
            .WithSummary("Accept or decline an offer")
            .WithDescription("Records the candidate's response. Accepting moves application status to Hired; declining moves it to Rejected at the Offered stage.")
            .Produces<JobOfferResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{applicationId:guid}/withdraw", async (
                Guid applicationId,
                WithdrawOfferRequest request,
                IOfferService service,
                CancellationToken ct) =>
            {
                await service.WithdrawOfferAsync(applicationId, request, ct);
                return Results.NoContent();
            })
            .WithName("WithdrawOffer")
            .WithSummary("Withdraw an offer")
            .WithDescription("Withdraws a Draft or Pending offer. Cannot withdraw offers that have already been responded to.")
            .RequireAuthorization("RequireRecruiterOrAdmin")
            .Produces(StatusCodes.Status204NoContent)
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

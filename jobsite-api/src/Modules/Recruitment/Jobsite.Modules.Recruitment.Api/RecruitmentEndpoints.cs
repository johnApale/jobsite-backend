using System.Security.Claims;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Jobsite.Modules.Recruitment.Api;

/// <summary>
/// Minimal API endpoint definitions for the Recruitment module.
/// Route prefix: <c>/api/v1/recruitment</c>.
/// </summary>
public static class RecruitmentEndpoints
{
    public static void MapRecruitmentEndpoints(this IEndpointRouteBuilder app)
    {
        MapClientCompanyEndpoints(app);
        MapJobPostingEndpoints(app);
        MapApplicationEndpoints(app);
    }

    // ── Client Company endpoints (AgencyAdmin only) ──────────────────────

    private static void MapClientCompanyEndpoints(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/recruitment/client-companies")
            .WithTags("Recruitment - Client Companies")
            .RequireAuthorization("RequireAgencyAdmin");

        group.MapPost("/", async (
                CreateClientCompanyRequest request,
                IClientCompanyService service,
                CancellationToken ct) =>
            {
                ClientCompanyResponse response = await service.CreateAsync(request, ct);
                return Results.Created($"/api/v1/recruitment/client-companies/{response.Id}", response);
            })
            .WithName("CreateClientCompany")
            .WithSummary("Create a new client company")
            .WithDescription("Creates a new client company for agency tenants. Only agency administrators can manage client companies.")
            .Produces<ClientCompanyResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/", async (
                string? status,
                string? cursor,
                int? pageSize,
                IClientCompanyService service,
                CancellationToken ct) =>
            {
                ClientCompanyQueryParameters parameters = new()
                {
                    Status = status,
                    Cursor = cursor,
                    PageSize = pageSize ?? 20
                };

                ClientCompanyListResponse response = await service.ListAsync(parameters, ct);
                return Results.Ok(response);
            })
            .WithName("ListClientCompanies")
            .WithSummary("List client companies")
            .WithDescription("Returns paginated client companies with optional status filter. Uses cursor-based pagination.")
            .Produces<ClientCompanyListResponse>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IClientCompanyService service,
                CancellationToken ct) =>
            {
                ClientCompanyResponse response = await service.GetByIdAsync(id, ct);
                return Results.Ok(response);
            })
            .WithName("GetClientCompanyById")
            .WithSummary("Get a specific client company")
            .WithDescription("Returns details for a single client company by ID.")
            .Produces<ClientCompanyResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}", async (
                Guid id,
                UpdateClientCompanyRequest request,
                IClientCompanyService service,
                CancellationToken ct) =>
            {
                ClientCompanyResponse response = await service.UpdateAsync(id, request, ct);
                return Results.Ok(response);
            })
            .WithName("UpdateClientCompany")
            .WithSummary("Update a client company")
            .WithDescription("Applies a JSON merge patch to an existing client company. Only provided fields are updated.")
            .Produces<ClientCompanyResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }

    // ── Job Posting endpoints ────────────────────────────────────────────

    private static void MapJobPostingEndpoints(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/recruitment/job-postings")
            .WithTags("Recruitment - Job Postings")
            .RequireAuthorization();

        group.MapPost("/", async (
                CreateJobPostingRequest request,
                IRecruitmentService service,
                HttpContext http,
                CancellationToken ct) =>
            {
                Guid postedBy = GetUserId(http);
                JobPostingResponse response = await service.CreateAsync(request, postedBy, ct);
                return Results.Created($"/api/v1/recruitment/job-postings/{response.Id}", response);
            })
            .WithName("CreateJobPosting")
            .WithSummary("Create a new job posting")
            .WithDescription("Creates a new job posting in Draft status. The authenticated user is recorded as the poster.")
            .Produces<JobPostingResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/", async (
                string? status,
                Guid? clientCompanyId,
                string? cursor,
                int? pageSize,
                IRecruitmentService service,
                CancellationToken ct) =>
            {
                JobPostingQueryParameters parameters = new()
                {
                    Status = status,
                    ClientCompanyId = clientCompanyId,
                    Cursor = cursor,
                    PageSize = pageSize ?? 20
                };

                JobPostingListResponse response = await service.ListAsync(parameters, ct);
                return Results.Ok(response);
            })
            .WithName("ListJobPostings")
            .WithSummary("List job postings")
            .WithDescription("Returns paginated job postings with optional status and client company filters. Uses cursor-based pagination.")
            .Produces<JobPostingListResponse>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IRecruitmentService service,
                CancellationToken ct) =>
            {
                JobPostingResponse response = await service.GetByIdAsync(id, ct);
                return Results.Ok(response);
            })
            .WithName("GetJobPostingById")
            .WithSummary("Get a specific job posting")
            .WithDescription("Returns full job posting details including evaluation criteria and screening questions.")
            .Produces<JobPostingResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{id:guid}", async (
                Guid id,
                UpdateJobPostingRequest request,
                IRecruitmentService service,
                CancellationToken ct) =>
            {
                JobPostingResponse response = await service.UpdateAsync(id, request, ct);
                return Results.Ok(response);
            })
            .WithName("UpdateJobPosting")
            .WithSummary("Update a job posting")
            .WithDescription("Applies a JSON merge patch to a job posting. Only Draft postings can be updated.")
            .Produces<JobPostingResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/publish", async (
                Guid id,
                IRecruitmentService service,
                CancellationToken ct) =>
            {
                JobPostingResponse response = await service.PublishAsync(id, ct);
                return Results.Ok(response);
            })
            .WithName("PublishJobPosting")
            .WithSummary("Publish a job posting")
            .WithDescription("Transitions a Draft job posting to Published status, making it visible to applicants.")
            .Produces<JobPostingResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/close", async (
                Guid id,
                IRecruitmentService service,
                CancellationToken ct) =>
            {
                JobPostingResponse response = await service.CloseAsync(id, ct);
                return Results.Ok(response);
            })
            .WithName("CloseJobPosting")
            .WithSummary("Close a job posting")
            .WithDescription("Transitions a Published job posting to Closed status, preventing new applications.")
            .Produces<JobPostingResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // ── Criteria sub-resource ────────────────────────────────────────

        group.MapPost("/{jobPostingId:guid}/criteria", async (
                Guid jobPostingId,
                CreateCriteriaRequest request,
                ICriteriaService service,
                CancellationToken ct) =>
            {
                CriteriaResponse response = await service.AddAsync(jobPostingId, request, ct);
                return Results.Created($"/api/v1/recruitment/job-postings/{jobPostingId}/criteria/{response.Id}", response);
            })
            .WithName("AddCriteria")
            .WithSummary("Add an evaluation criterion")
            .WithDescription("Adds a new evaluation criterion to a job posting for candidate screening.")
            .Produces<CriteriaResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{jobPostingId:guid}/criteria", async (
                Guid jobPostingId,
                ICriteriaService service,
                CancellationToken ct) =>
            {
                List<CriteriaResponse> response = await service.ListByJobPostingAsync(jobPostingId, ct);
                return Results.Ok(response);
            })
            .WithName("ListCriteria")
            .WithSummary("List evaluation criteria")
            .WithDescription("Returns all evaluation criteria for a job posting, ordered by display order.")
            .Produces<List<CriteriaResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{jobPostingId:guid}/criteria/{criteriaId:guid}", async (
                Guid jobPostingId,
                Guid criteriaId,
                UpdateCriteriaRequest request,
                ICriteriaService service,
                CancellationToken ct) =>
            {
                CriteriaResponse response = await service.UpdateAsync(jobPostingId, criteriaId, request, ct);
                return Results.Ok(response);
            })
            .WithName("UpdateCriteria")
            .WithSummary("Update an evaluation criterion")
            .WithDescription("Applies a JSON merge patch to an existing evaluation criterion. Only provided fields are updated.")
            .Produces<CriteriaResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{jobPostingId:guid}/criteria/{criteriaId:guid}", async (
                Guid jobPostingId,
                Guid criteriaId,
                ICriteriaService service,
                CancellationToken ct) =>
            {
                await service.DeleteAsync(jobPostingId, criteriaId, ct);
                return Results.NoContent();
            })
            .WithName("DeleteCriteria")
            .WithSummary("Delete an evaluation criterion")
            .WithDescription("Permanently removes an evaluation criterion from a job posting.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{jobPostingId:guid}/criteria/suggest", async (
                Guid jobPostingId,
                ICriteriaService service,
                CancellationToken ct) =>
            {
                List<AiCriteriaSuggestion>? suggestions = await service.SuggestAsync(jobPostingId, ct);
                return suggestions is not null ? Results.Ok(suggestions) : Results.NoContent();
            })
            .WithName("SuggestCriteria")
            .WithSummary("Get AI-suggested criteria")
            .WithDescription("Returns AI-generated evaluation criteria suggestions based on the job posting details. Returns 204 if the AI service is unavailable.")
            .Produces<List<AiCriteriaSuggestion>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        // ── Screening Questions sub-resource ─────────────────────────────

        group.MapPost("/{jobPostingId:guid}/questions", async (
                Guid jobPostingId,
                CreateQuestionRequest request,
                IScreeningQuestionService service,
                CancellationToken ct) =>
            {
                QuestionResponse response = await service.AddAsync(jobPostingId, request, ct);
                return Results.Created($"/api/v1/recruitment/job-postings/{jobPostingId}/questions/{response.Id}", response);
            })
            .WithName("AddScreeningQuestion")
            .WithSummary("Add a screening question")
            .WithDescription("Adds a new screening question to a job posting.")
            .Produces<QuestionResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{jobPostingId:guid}/questions", async (
                Guid jobPostingId,
                IScreeningQuestionService service,
                CancellationToken ct) =>
            {
                List<QuestionResponse> response = await service.ListByJobPostingAsync(jobPostingId, ct);
                return Results.Ok(response);
            })
            .WithName("ListScreeningQuestions")
            .WithSummary("List screening questions")
            .WithDescription("Returns all screening questions for a job posting, ordered by display order.")
            .Produces<List<QuestionResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/{jobPostingId:guid}/questions/{questionId:guid}", async (
                Guid jobPostingId,
                Guid questionId,
                UpdateQuestionRequest request,
                IScreeningQuestionService service,
                CancellationToken ct) =>
            {
                QuestionResponse response = await service.UpdateAsync(jobPostingId, questionId, request, ct);
                return Results.Ok(response);
            })
            .WithName("UpdateScreeningQuestion")
            .WithSummary("Update a screening question")
            .WithDescription("Applies a JSON merge patch to an existing screening question. Only provided fields are updated.")
            .Produces<QuestionResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{jobPostingId:guid}/questions/{questionId:guid}", async (
                Guid jobPostingId,
                Guid questionId,
                IScreeningQuestionService service,
                CancellationToken ct) =>
            {
                await service.DeleteAsync(jobPostingId, questionId, ct);
                return Results.NoContent();
            })
            .WithName("DeleteScreeningQuestion")
            .WithSummary("Delete a screening question")
            .WithDescription("Permanently removes a screening question from a job posting.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{jobPostingId:guid}/questions/suggest", async (
                Guid jobPostingId,
                IScreeningQuestionService service,
                CancellationToken ct) =>
            {
                List<AiQuestionSuggestion>? suggestions = await service.SuggestAsync(jobPostingId, ct);
                return suggestions is not null ? Results.Ok(suggestions) : Results.NoContent();
            })
            .WithName("SuggestScreeningQuestions")
            .WithSummary("Get AI-suggested screening questions")
            .WithDescription("Returns AI-generated screening question suggestions based on the job posting details. Returns 204 if the AI service is unavailable or feature is disabled.")
            .Produces<List<AiQuestionSuggestion>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    // ── Application endpoints ────────────────────────────────────────────

    private static void MapApplicationEndpoints(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/recruitment/applications")
            .WithTags("Recruitment - Applications")
            .RequireAuthorization();

        group.MapPost("/job-postings/{jobPostingId:guid}", async (
                Guid jobPostingId,
                SubmitApplicationRequest request,
                IApplicationService service,
                HttpContext http,
                CancellationToken ct) =>
            {
                Guid applicantId = GetUserId(http);
                ApplicationResponse response = await service.SubmitAsync(jobPostingId, request, applicantId, ct);
                return Results.Created($"/api/v1/recruitment/applications/{response.Id}", response);
            })
            .WithName("SubmitApplication")
            .WithSummary("Submit an application")
            .WithDescription("Submits the authenticated user's application to a published job posting. Validates resume ownership and prevents duplicate applications.")
            .Produces<ApplicationResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapGet("/", async (
                Guid? jobPostingId,
                string? status,
                Guid? applicantId,
                string? cursor,
                int? pageSize,
                IApplicationService service,
                CancellationToken ct) =>
            {
                ApplicationQueryParameters parameters = new()
                {
                    JobPostingId = jobPostingId,
                    Status = status,
                    ApplicantId = applicantId,
                    Cursor = cursor,
                    PageSize = pageSize ?? 20
                };

                ApplicationListResponse response = await service.ListAsync(parameters, ct);
                return Results.Ok(response);
            })
            .WithName("ListApplications")
            .WithSummary("List applications")
            .WithDescription("Returns paginated applications with optional filters by job posting, status, and applicant. Uses cursor-based pagination.")
            .Produces<ApplicationListResponse>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", async (
                Guid id,
                IApplicationService service,
                CancellationToken ct) =>
            {
                ApplicationResponse response = await service.GetByIdAsync(id, ct);
                return Results.Ok(response);
            })
            .WithName("GetApplicationById")
            .WithSummary("Get a specific application")
            .WithDescription("Returns full application details by ID.")
            .Produces<ApplicationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/withdraw", async (
                Guid id,
                IApplicationService service,
                HttpContext http,
                CancellationToken ct) =>
            {
                Guid applicantId = GetUserId(http);
                ApplicationResponse response = await service.WithdrawAsync(id, applicantId, ct);
                return Results.Ok(response);
            })
            .WithName("WithdrawApplication")
            .WithSummary("Withdraw an application")
            .WithDescription("Withdraws the authenticated user's application. Only the original applicant can withdraw their own application.")
            .Produces<ApplicationResponse>(StatusCodes.Status200OK)
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

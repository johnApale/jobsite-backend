namespace Jobsite.Modules.Recruitment.Application.DTOs;

/// <summary>Request body for submitting an application to a job posting.</summary>
public sealed class SubmitApplicationRequest
{
    /// <summary>The specific resume version to submit with this application.</summary>
    public required Guid ResumeId { get; init; }

    /// <summary>Optional cover letter URL.</summary>
    public string? CoverLetterUrl { get; init; }

    /// <summary>Answers to AtApplication screening questions.</summary>
    public List<QuestionAnswerDto>? QuestionAnswers { get; init; }
}

/// <summary>Answer to a screening question submitted with the application.</summary>
public sealed class QuestionAnswerDto
{
    /// <summary>The screening question being answered.</summary>
    public required Guid QuestionId { get; init; }

    /// <summary>Free-text response (for FreeText and YesNo types).</summary>
    public string? ResponseText { get; init; }

    /// <summary>Structured response data as JSON (for MultipleChoice selected indices).</summary>
    public string? ResponseData { get; init; }
}

/// <summary>Response body for application endpoints.</summary>
public sealed class ApplicationResponse
{
    public required Guid Id { get; init; }
    public required Guid JobPostingId { get; init; }
    public required Guid ApplicantId { get; init; }
    public required string Status { get; init; }
    public required Guid ResumeId { get; init; }
    public string? CoverLetterUrl { get; init; }
    public string? RejectionReason { get; init; }
    public string? RejectedAtStage { get; init; }
    public DateTime? WithdrawnAt { get; init; }
    public required DateTime SubmittedAt { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>Query parameters for listing applications with cursor-based pagination.</summary>
public sealed class ApplicationQueryParameters
{
    /// <summary>Filter by job posting.</summary>
    public Guid? JobPostingId { get; init; }

    /// <summary>Filter by status (Submitted, Screening, Assessment, etc.).</summary>
    public string? Status { get; init; }

    /// <summary>Filter by applicant.</summary>
    public Guid? ApplicantId { get; init; }

    /// <summary>Cursor for pagination (opaque string from previous response).</summary>
    public string? Cursor { get; init; }

    /// <summary>Number of results per page. Default 20, max 100.</summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>Paginated response for application queries.</summary>
public sealed class ApplicationListResponse
{
    public required List<ApplicationResponse> Items { get; init; }

    /// <summary>Cursor for the next page. Null if no more results.</summary>
    public string? NextCursor { get; init; }
}

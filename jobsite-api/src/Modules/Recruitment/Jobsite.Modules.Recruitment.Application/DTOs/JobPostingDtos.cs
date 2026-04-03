namespace Jobsite.Modules.Recruitment.Application.DTOs;

/// <summary>Request body for creating a job posting.</summary>
public sealed class CreateJobPostingRequest
{
    /// <summary>Job title (e.g., "Senior .NET Developer").</summary>
    public required string Title { get; init; }

    /// <summary>Full job description.</summary>
    public required string Description { get; init; }

    /// <summary>Work location arrangement: OnSite, Remote, Hybrid.</summary>
    public required string LocationType { get; init; }

    /// <summary>City. Required for OnSite and Hybrid.</summary>
    public string? City { get; init; }

    /// <summary>Country. Required for OnSite and Hybrid.</summary>
    public string? Country { get; init; }

    /// <summary>Employment type: FullTime, PartTime, Contract, Temporary, Internship.</summary>
    public required string EmploymentType { get; init; }

    /// <summary>Minimum salary.</summary>
    public decimal? SalaryMin { get; init; }

    /// <summary>Maximum salary.</summary>
    public decimal? SalaryMax { get; init; }

    /// <summary>ISO 4217 currency code (e.g., USD, EUR). Required if salary is provided.</summary>
    public string? SalaryCurrency { get; init; }

    /// <summary>Organizational department (e.g., "Engineering").</summary>
    public string? Department { get; init; }

    /// <summary>FK to client company. NULL for non-agency tenants.</summary>
    public Guid? ClientCompanyId { get; init; }

    /// <summary>Optional auto-close date.</summary>
    public DateTime? ClosesAt { get; init; }
}

/// <summary>
/// Request body for updating a job posting (JSON merge patch).
/// All fields are nullable — only non-null values are applied.
/// </summary>
public sealed class UpdateJobPostingRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? LocationType { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public string? EmploymentType { get; init; }
    public decimal? SalaryMin { get; init; }
    public decimal? SalaryMax { get; init; }
    public string? SalaryCurrency { get; init; }
    public string? Department { get; init; }
    public Guid? ClientCompanyId { get; init; }
    public DateTime? ClosesAt { get; init; }
}

/// <summary>Response body for job posting endpoints.</summary>
public sealed class JobPostingResponse
{
    public required Guid Id { get; init; }
    public Guid? ClientCompanyId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string LocationType { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public required string EmploymentType { get; init; }
    public decimal? SalaryMin { get; init; }
    public decimal? SalaryMax { get; init; }
    public string? SalaryCurrency { get; init; }
    public string? Department { get; init; }
    public required string Status { get; init; }
    public required Guid PostedBy { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime? ClosesAt { get; init; }
    public DateTime? ClosedAt { get; init; }
    public List<CriteriaResponse>? Criteria { get; init; }
    public List<QuestionResponse>? Questions { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>Query parameters for listing job postings with cursor-based pagination.</summary>
public sealed class JobPostingQueryParameters
{
    /// <summary>Filter by status (Draft, Published, Closed).</summary>
    public string? Status { get; init; }

    /// <summary>Filter by client company.</summary>
    public Guid? ClientCompanyId { get; init; }

    /// <summary>Cursor for pagination (opaque string from previous response).</summary>
    public string? Cursor { get; init; }

    /// <summary>Number of results per page. Default 20, max 100.</summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>Paginated response for job posting queries.</summary>
public sealed class JobPostingListResponse
{
    public required List<JobPostingResponse> Items { get; init; }

    /// <summary>Cursor for the next page. Null if no more results.</summary>
    public string? NextCursor { get; init; }
}

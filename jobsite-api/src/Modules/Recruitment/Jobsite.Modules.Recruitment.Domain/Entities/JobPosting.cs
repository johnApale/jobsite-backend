using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Recruitment.Domain.Entities;

/// <summary>
/// A job that applicants can apply to — maps to <c>recruitment.job_postings</c>.
/// Created by Recruiters or HiringManagers, goes through Draft → Published → Closed lifecycle.
/// Structured evaluation criteria and screening questions are stored in separate entities.
/// </summary>
public sealed class JobPosting : AggregateRoot
{
    /// <summary>FK to <c>recruitment.client_companies</c>. NULL for non-agency tenants.</summary>
    public Guid? ClientCompanyId { get; set; }

    /// <summary>Job title (e.g., "Senior .NET Developer").</summary>
    public string Title { get; set; } = null!;

    /// <summary>Full job description. Free-form rich text for the applicant-facing listing.</summary>
    public string Description { get; set; } = null!;

    /// <summary>Free-form text describing job requirements. Used by AI to suggest evaluation criteria.</summary>
    public string? Requirements { get; set; }

    /// <summary>Work location arrangement: OnSite, Remote, Hybrid. Constrained by CHECK.</summary>
    public string LocationType { get; set; } = null!;

    /// <summary>City. Required for OnSite and Hybrid. NULL for fully remote.</summary>
    public string? City { get; set; }

    /// <summary>Country. Required for OnSite and Hybrid. NULL for fully remote.</summary>
    public string? Country { get; set; }

    /// <summary>Employment type: FullTime, PartTime, Contract, Temporary, Internship.</summary>
    public string EmploymentType { get; set; } = null!;

    /// <summary>Minimum salary. Nullable because not all postings disclose salary.</summary>
    public decimal? SalaryMin { get; set; }

    /// <summary>Maximum salary.</summary>
    public decimal? SalaryMax { get; set; }

    /// <summary>ISO 4217 currency code (e.g., USD, EUR). Required if salary is provided.</summary>
    public string? SalaryCurrency { get; set; }

    /// <summary>Organizational department (e.g., "Engineering", "Marketing").</summary>
    public string? Department { get; set; }

    /// <summary>Lifecycle status: Draft, Published, Closed. Constrained by CHECK.</summary>
    public string Status { get; set; } = Constants.JobPostingStatus.Draft;

    /// <summary>Ref to <c>auth.users</c> (cross-module). The Recruiter or HiringManager who created this posting.</summary>
    public Guid PostedBy { get; set; }

    /// <summary>Set when status moves to Published. Used for "newest jobs" sorting.</summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>Optional auto-close date. Job stops accepting applications after this.</summary>
    public DateTime? ClosesAt { get; set; }

    /// <summary>Set when status moves to Closed (manually or via auto-close).</summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>Navigation to the optional client company.</summary>
    public ClientCompany? ClientCompany { get; set; }

    /// <summary>Evaluation criteria for this job.</summary>
    public List<JobEvaluationCriteria> Criteria { get; set; } = [];

    /// <summary>Screening questions for this job.</summary>
    public List<JobScreeningQuestion> Questions { get; set; } = [];

    /// <summary>Applications submitted to this job.</summary>
    public List<Application> Applications { get; set; } = [];

    /// <summary>Transition from Draft to Published. Sets <see cref="PublishedAt"/>.</summary>
    public void Publish()
    {
        Status = Constants.JobPostingStatus.Published;
        PublishedAt = DateTime.UtcNow;
    }

    /// <summary>Transition from Published to Closed. Sets <see cref="ClosedAt"/>.</summary>
    public void Close()
    {
        Status = Constants.JobPostingStatus.Closed;
        ClosedAt = DateTime.UtcNow;
    }
}

using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Recruitment.Domain.Entities;

/// <summary>
/// External company that an agency tenant recruits on behalf of.
/// Maps to <c>recruitment.client_companies</c>.
/// Non-agency tenants hiring for themselves leave this table empty.
/// </summary>
public sealed class ClientCompany : AggregateRoot
{
    /// <summary>Client company name (e.g., "Google", "Meta").</summary>
    public string Name { get; set; } = null!;

    /// <summary>Public-facing name shown on job listings. NULL = use <see cref="Name"/>.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Whether to hide the real company name on job listings.</summary>
    public bool IsAnonymous { get; set; }

    /// <summary>Client's industry (e.g., "Technology", "Healthcare"). Constrained by CHECK.</summary>
    public string? Industry { get; set; }

    /// <summary>Client company's website.</summary>
    public string? Website { get; set; }

    /// <summary>Primary contact person at the client company.</summary>
    public string? ContactName { get; set; }

    /// <summary>Contact email for the client.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>Contact phone for the client.</summary>
    public string? ContactPhone { get; set; }

    /// <summary>Internal notes about the client relationship (not shown to applicants).</summary>
    public string? Notes { get; set; }

    /// <summary>Status: Active or Inactive. Inactive clients can't have new jobs posted.</summary>
    public string Status { get; set; } = Constants.ClientCompanyStatus.Active;

    /// <summary>Job postings linked to this client company.</summary>
    public List<JobPosting> JobPostings { get; set; } = [];
}

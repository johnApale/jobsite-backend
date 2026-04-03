using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Recruitment.Domain.Entities;

/// <summary>
/// Structured evaluation criterion for a job posting — maps to <c>recruitment.job_evaluation_criteria</c>.
/// The Screening module evaluates candidates against these criteria.
/// Recruiters configure them manually or via AI-assisted suggestions.
/// </summary>
public sealed class JobEvaluationCriteria : Entity
{
    /// <summary>FK to <c>recruitment.job_postings</c>.</summary>
    public Guid JobPostingId { get; set; }

    /// <summary>Human-readable criterion name (e.g., "C# Proficiency").</summary>
    public string Name { get; set; } = null!;

    /// <summary>Category: Skill, Experience, Certification, Education, Location, Custom.</summary>
    public string Category { get; set; } = null!;

    /// <summary>How the Screening module scores: ExactMatch, RangeMatch, SemanticSimilarity.</summary>
    public string EvaluationMethod { get; set; } = null!;

    /// <summary>Whether this is a hard requirement (pass/fail) or a nice-to-have.</summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>Contribution to the overall screening score (0.00–100.00).</summary>
    public decimal Weight { get; set; }

    /// <summary>
    /// Category-specific configuration as JSONB. Shape depends on <see cref="Category"/>.
    /// See RECRUITMENT_DB_DESIGN.md "Criteria Configuration Formats" for shapes.
    /// </summary>
    public string Configuration { get; set; } = null!;

    /// <summary>Ordering for display in the recruiter UI.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Navigation to the owning job posting.</summary>
    public JobPosting JobPosting { get; set; } = null!;
}

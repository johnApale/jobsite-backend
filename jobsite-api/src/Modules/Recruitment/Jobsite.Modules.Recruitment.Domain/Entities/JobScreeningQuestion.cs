using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Recruitment.Domain.Entities;

/// <summary>
/// Screening question attached to a job posting — maps to <c>recruitment.job_screening_questions</c>.
/// Presented to candidates either at application time or after passing the screening stage.
/// </summary>
public sealed class JobScreeningQuestion : Entity
{
    /// <summary>FK to <c>recruitment.job_postings</c>.</summary>
    public Guid JobPostingId { get; set; }

    /// <summary>The question presented to the candidate.</summary>
    public string QuestionText { get; set; } = null!;

    /// <summary>Answer format: FreeText, MultipleChoice, YesNo. Constrained by CHECK.</summary>
    public string QuestionType { get; set; } = null!;

    /// <summary>When the candidate sees this question: AtApplication or AfterScreening.</summary>
    public string Timing { get; set; } = null!;

    /// <summary>Whether the candidate must answer this question.</summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>Contribution to the question score component (0.00–100.00).</summary>
    public decimal Weight { get; set; }

    /// <summary>
    /// Rubric or expected response for scoring as JSONB.
    /// For YesNo: <c>{ "correct": true }</c>.
    /// For MultipleChoice: <c>{ "correct_options": [0, 2], "partial_credit": true }</c>.
    /// For FreeText: <c>{ "key_topics": [...], "scoring_guidance": "..." }</c>.
    /// </summary>
    public string? ExpectedAnswer { get; set; }

    /// <summary>
    /// For MultipleChoice only. Array of option strings as JSONB.
    /// Format: <c>["Option A", "Option B", "Option C"]</c>.
    /// </summary>
    public string? Options { get; set; }

    /// <summary>Ordering within the question set.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Navigation to the owning job posting.</summary>
    public JobPosting JobPosting { get; set; } = null!;
}

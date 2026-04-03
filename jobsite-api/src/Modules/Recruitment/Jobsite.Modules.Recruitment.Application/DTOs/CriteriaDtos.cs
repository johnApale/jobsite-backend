namespace Jobsite.Modules.Recruitment.Application.DTOs;

/// <summary>Request body for adding an evaluation criterion to a job posting.</summary>
public sealed class CreateCriteriaRequest
{
    /// <summary>Human-readable criterion name (e.g., "C# Proficiency").</summary>
    public required string Name { get; init; }

    /// <summary>Category: Skill, Experience, Certification, Education, Location, Custom.</summary>
    public required string Category { get; init; }

    /// <summary>How the Screening module scores: ExactMatch, RangeMatch, SemanticSimilarity.</summary>
    public required string EvaluationMethod { get; init; }

    /// <summary>Whether this is a hard requirement (pass/fail) or a nice-to-have.</summary>
    public bool IsRequired { get; init; } = true;

    /// <summary>Contribution to the overall screening score (0.00–100.00).</summary>
    public required decimal Weight { get; init; }

    /// <summary>Category-specific configuration as JSON string.</summary>
    public required string Configuration { get; init; }

    /// <summary>Ordering for display in the recruiter UI.</summary>
    public int DisplayOrder { get; init; }
}

/// <summary>
/// Request body for updating an evaluation criterion (JSON merge patch).
/// All fields are nullable — only non-null values are applied.
/// </summary>
public sealed class UpdateCriteriaRequest
{
    public string? Name { get; init; }
    public string? Category { get; init; }
    public string? EvaluationMethod { get; init; }
    public bool? IsRequired { get; init; }
    public decimal? Weight { get; init; }
    public string? Configuration { get; init; }
    public int? DisplayOrder { get; init; }
}

/// <summary>Response body for evaluation criteria endpoints.</summary>
public sealed class CriteriaResponse
{
    public required Guid Id { get; init; }
    public required Guid JobPostingId { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string EvaluationMethod { get; init; }
    public required bool IsRequired { get; init; }
    public required decimal Weight { get; init; }
    public required string Configuration { get; init; }
    public required int DisplayOrder { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>AI-suggested evaluation criterion (not yet persisted).</summary>
public sealed class AiCriteriaSuggestion
{
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string EvaluationMethod { get; init; }
    public required bool IsRequired { get; init; }
    public required decimal Weight { get; init; }
    public required string Configuration { get; init; }
}

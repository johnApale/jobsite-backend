namespace Jobsite.Modules.Recruitment.Application.DTOs;

/// <summary>Request body for adding a screening question to a job posting.</summary>
public sealed class CreateQuestionRequest
{
    /// <summary>The question presented to the candidate.</summary>
    public required string QuestionText { get; init; }

    /// <summary>Answer format: FreeText, MultipleChoice, YesNo.</summary>
    public required string QuestionType { get; init; }

    /// <summary>When the candidate sees this question: AtApplication or AfterScreening.</summary>
    public required string Timing { get; init; }

    /// <summary>Whether the candidate must answer this question.</summary>
    public bool IsRequired { get; init; } = true;

    /// <summary>Contribution to the question score component (0.00–100.00).</summary>
    public required decimal Weight { get; init; }

    /// <summary>Rubric or expected response for scoring as JSON string.</summary>
    public string? ExpectedAnswer { get; init; }

    /// <summary>For MultipleChoice only. Array of option strings as JSON.</summary>
    public string? Options { get; init; }

    /// <summary>Ordering within the question set.</summary>
    public int DisplayOrder { get; init; }
}

/// <summary>
/// Request body for updating a screening question (JSON merge patch).
/// All fields are nullable — only non-null values are applied.
/// </summary>
public sealed class UpdateQuestionRequest
{
    public string? QuestionText { get; init; }
    public string? QuestionType { get; init; }
    public string? Timing { get; init; }
    public bool? IsRequired { get; init; }
    public decimal? Weight { get; init; }
    public string? ExpectedAnswer { get; init; }
    public string? Options { get; init; }
    public int? DisplayOrder { get; init; }
}

/// <summary>Response body for screening question endpoints.</summary>
public sealed class QuestionResponse
{
    public required Guid Id { get; init; }
    public required Guid JobPostingId { get; init; }
    public required string QuestionText { get; init; }
    public required string QuestionType { get; init; }
    public required string Timing { get; init; }
    public required bool IsRequired { get; init; }
    public required decimal Weight { get; init; }
    public string? ExpectedAnswer { get; init; }
    public string? Options { get; init; }
    public required int DisplayOrder { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>AI-suggested screening question (not yet persisted).</summary>
public sealed class AiQuestionSuggestion
{
    public required string QuestionText { get; init; }
    public required string QuestionType { get; init; }
    public required string Timing { get; init; }
    public required bool IsRequired { get; init; }
    public required decimal Weight { get; init; }
    public string? ExpectedAnswer { get; init; }
    public string? Options { get; init; }
}

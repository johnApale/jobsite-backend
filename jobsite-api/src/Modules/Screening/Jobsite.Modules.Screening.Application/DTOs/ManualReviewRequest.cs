namespace Jobsite.Modules.Screening.Application.DTOs;

/// <summary>Manual review decision by a recruiter.</summary>
public sealed class ManualReviewRequest
{
    /// <summary>Must be ManuallyAdvanced or ManuallyRejected.</summary>
    public required string Outcome { get; init; }

    /// <summary>Optional notes explaining the decision.</summary>
    public string? ReviewNotes { get; init; }
}

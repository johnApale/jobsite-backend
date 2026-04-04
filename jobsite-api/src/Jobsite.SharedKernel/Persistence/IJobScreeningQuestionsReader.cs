namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Reads screening questions from the Recruitment module.
/// Implemented by Recruitment.Infrastructure to avoid cross-module project references.
/// Consumed by the Screening module during assessment and question scoring.
/// </summary>
public interface IJobScreeningQuestionsReader
{
    /// <summary>Returns all screening questions for the given job posting, ordered by display order.</summary>
    Task<List<QuestionSnapshot>> GetQuestionsForJobAsync(Guid jobPostingId, CancellationToken ct = default);

    /// <summary>Returns <c>true</c> if the job posting has any <c>AfterScreening</c> questions.</summary>
    Task<bool> HasAfterScreeningQuestionsAsync(Guid jobPostingId, CancellationToken ct = default);
}

/// <summary>
/// Snapshot of a screening question — carried across module boundaries
/// without referencing Recruitment domain entities.
/// </summary>
public sealed class QuestionSnapshot
{
    public required Guid Id { get; init; }
    public required string QuestionText { get; init; }
    public required string QuestionType { get; init; }
    public required string Timing { get; init; }
    public required bool IsRequired { get; init; }
    public required decimal Weight { get; init; }
    public string? ExpectedAnswer { get; init; }
    public string? Options { get; init; }
}

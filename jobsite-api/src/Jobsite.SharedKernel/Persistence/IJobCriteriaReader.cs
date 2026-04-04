namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Reads job evaluation criteria from the Recruitment module.
/// Implemented by Recruitment.Infrastructure to avoid cross-module project references.
/// Consumed by the Screening module during candidate evaluation.
/// </summary>
public interface IJobCriteriaReader
{
    /// <summary>Returns all evaluation criteria for the given job posting, ordered by display order.</summary>
    Task<List<CriteriaSnapshot>> GetCriteriaForJobAsync(Guid jobPostingId, CancellationToken ct = default);
}

/// <summary>
/// Snapshot of a job evaluation criterion — carried across module boundaries
/// without referencing Recruitment domain entities.
/// </summary>
public sealed class CriteriaSnapshot
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string EvaluationMethod { get; init; }
    public required bool IsRequired { get; init; }
    public required decimal Weight { get; init; }
    public required string Configuration { get; init; }
}

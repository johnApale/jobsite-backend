using Jobsite.Modules.Recruitment.Domain.Entities;

namespace Jobsite.Modules.Recruitment.Application.Interfaces;

/// <summary>Repository for job screening question lookups and persistence.</summary>
public interface IScreeningQuestionRepository
{
    /// <summary>Get all questions for a job posting, ordered by display order (read-only).</summary>
    Task<List<JobScreeningQuestion>> GetByJobPostingIdAsync(Guid jobPostingId, CancellationToken ct = default);

    /// <summary>Get a question by ID (read-only).</summary>
    Task<JobScreeningQuestion?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Get a question by ID with tracking enabled (for updates).</summary>
    Task<JobScreeningQuestion?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Persist a new question.</summary>
    void Add(JobScreeningQuestion question);

    /// <summary>Remove a question.</summary>
    void Remove(JobScreeningQuestion question);
}

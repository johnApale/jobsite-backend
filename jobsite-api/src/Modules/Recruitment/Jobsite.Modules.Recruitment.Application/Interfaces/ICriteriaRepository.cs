using Jobsite.Modules.Recruitment.Domain.Entities;

namespace Jobsite.Modules.Recruitment.Application.Interfaces;

/// <summary>Repository for job evaluation criteria lookups and persistence.</summary>
public interface ICriteriaRepository
{
    /// <summary>Get all criteria for a job posting, ordered by display order (read-only).</summary>
    Task<List<JobEvaluationCriteria>> GetByJobPostingIdAsync(Guid jobPostingId, CancellationToken ct = default);

    /// <summary>Get a criterion by ID (read-only).</summary>
    Task<JobEvaluationCriteria?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Get a criterion by ID with tracking enabled (for updates).</summary>
    Task<JobEvaluationCriteria?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Persist a new criterion.</summary>
    void Add(JobEvaluationCriteria criteria);

    /// <summary>Remove a criterion.</summary>
    void Remove(JobEvaluationCriteria criteria);
}

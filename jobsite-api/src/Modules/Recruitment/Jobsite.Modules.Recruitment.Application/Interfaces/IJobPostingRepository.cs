using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Domain.Entities;

namespace Jobsite.Modules.Recruitment.Application.Interfaces;

/// <summary>Repository for job posting lookups and persistence.</summary>
public interface IJobPostingRepository
{
    /// <summary>Get a job posting by ID (read-only).</summary>
    Task<JobPosting?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Get a job posting by ID with tracking enabled (for updates).</summary>
    Task<JobPosting?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Get a job posting by ID with criteria and questions included (read-only).</summary>
    Task<JobPosting?> GetByIdWithDetailsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Query job postings with cursor-based pagination and optional filters.</summary>
    Task<JobPostingListResponse> ListAsync(JobPostingQueryParameters parameters, CancellationToken ct = default);

    /// <summary>Check if a job posting exists.</summary>
    Task<bool> ExistsByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Persist a new job posting.</summary>
    void Add(JobPosting jobPosting);
}

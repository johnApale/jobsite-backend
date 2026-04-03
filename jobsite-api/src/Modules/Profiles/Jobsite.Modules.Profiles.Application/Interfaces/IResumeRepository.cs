using Jobsite.Modules.Profiles.Domain.Entities;

namespace Jobsite.Modules.Profiles.Application.Interfaces;

/// <summary>
/// Repository for resume lookups and persistence in the tenant database.
/// </summary>
public interface IResumeRepository
{
    /// <summary>Get a resume by ID (read-only).</summary>
    Task<Resume?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Get a resume by ID with tracking enabled (for updates).</summary>
    Task<Resume?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Get all resumes for a user, ordered by most recent first (read-only).</summary>
    Task<List<Resume>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Get the latest resume for a user (read-only).</summary>
    Task<Resume?> GetLatestByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Mark all existing resumes for the user as not latest.</summary>
    Task MarkPreviousAsNotLatestAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Persist a new resume.</summary>
    void Add(Resume resume);
}

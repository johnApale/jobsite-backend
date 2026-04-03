using Jobsite.Modules.Profiles.Domain.Entities;

namespace Jobsite.Modules.Profiles.Application.Interfaces;

/// <summary>
/// Repository for applicant profile lookups and persistence in the tenant database.
/// </summary>
public interface IApplicantProfileRepository
{
    /// <summary>Get a profile by user ID (read-only).</summary>
    Task<ApplicantProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Get a profile by user ID with tracking enabled (for updates).</summary>
    Task<ApplicantProfile?> GetByUserIdForUpdateAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Check if a profile exists for the given user.</summary>
    Task<bool> ExistsByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Persist a new applicant profile.</summary>
    void Add(ApplicantProfile profile);
}

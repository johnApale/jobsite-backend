using Jobsite.Modules.Auth.Domain.Entities;

namespace Jobsite.Modules.Auth.Application.Interfaces;

/// <summary>
/// Repository for user lookups and persistence in the tenant database.
/// </summary>
public interface IUserRepository
{
    /// <summary>Get a user by ID (read-only).</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Get a user by email (read-only).</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Get a user by email with tracking enabled (for updates).</summary>
    Task<User?> GetByEmailForUpdateAsync(string email, CancellationToken ct = default);

    /// <summary>Get a user by ID with tracking enabled (for updates).</summary>
    Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Check if an email is already registered in this tenant.</summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);

    /// <summary>Find a user by OAuth provider and subject ID (read-only).</summary>
    Task<User?> GetByExternalLoginAsync(string provider, string providerSubjectId, CancellationToken ct = default);

    /// <summary>Persist a new user.</summary>
    void Add(User user);
}

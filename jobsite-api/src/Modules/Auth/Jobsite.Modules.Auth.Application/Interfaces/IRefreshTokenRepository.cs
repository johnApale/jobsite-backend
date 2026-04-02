using Jobsite.Modules.Auth.Domain.Entities;

namespace Jobsite.Modules.Auth.Application.Interfaces;

/// <summary>
/// Repository for refresh token lookups and persistence.
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>Find a refresh token by its SHA-256 hash (with tracking for revocation).</summary>
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Revoke all tokens in a family (replay detection).</summary>
    Task RevokeFamilyAsync(Guid familyId, CancellationToken ct = default);

    /// <summary>Persist a new refresh token.</summary>
    void Add(RefreshToken refreshToken);
}

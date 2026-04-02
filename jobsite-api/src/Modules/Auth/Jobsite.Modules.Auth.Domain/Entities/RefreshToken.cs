using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Auth.Domain.Entities;

/// <summary>
/// JWT refresh token with family-based replay detection — maps to <c>auth.refresh_tokens</c>.
/// Each login session starts a token family. On rotation, the old token is revoked and a new one
/// issued in the same family. If a revoked token is reused, the entire family is revoked.
/// </summary>
public sealed class RefreshToken : Entity
{
    /// <summary>The user this token belongs to.</summary>
    public Guid UserId { get; set; }

    /// <summary>SHA-256 hash of the refresh token. Never store the raw token.</summary>
    public string TokenHash { get; set; } = null!;

    /// <summary>Groups all tokens from one login session. Enables family-wide revocation on replay.</summary>
    public Guid FamilyId { get; set; }

    /// <summary>Set to true on rotation or explicit logout.</summary>
    public bool IsRevoked { get; set; }

    /// <summary>Absolute expiration — token is invalid after this regardless of revocation status.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>When this specific token was revoked (rotation or logout).</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Points to the token that replaced this one on rotation. NULL if this is the current active token.</summary>
    public Guid? ReplacedById { get; set; }

    /// <summary>Navigation property back to the owning user.</summary>
    public User User { get; set; } = null!;

    /// <summary>Whether this token has passed its expiration time.</summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>Revoke this token, recording the revocation timestamp.</summary>
    public void Revoke()
    {
        IsRevoked = true;
        RevokedAt = DateTime.UtcNow;
    }
}

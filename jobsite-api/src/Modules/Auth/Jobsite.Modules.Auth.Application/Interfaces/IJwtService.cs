using Jobsite.Modules.Auth.Domain.Entities;

namespace Jobsite.Modules.Auth.Application.Interfaces;

/// <summary>
/// Abstraction for JWT access token generation, refresh token generation, and token hashing.
/// </summary>
public interface IJwtService
{
    /// <summary>Generate a signed JWT access token with user claims.</summary>
    string GenerateAccessToken(User user, Guid tenantId);

    /// <summary>Generate a cryptographically random refresh token string.</summary>
    string GenerateRefreshToken();

    /// <summary>Compute SHA-256 hash of a token for storage.</summary>
    string HashToken(string token);

    /// <summary>Access token lifetime in minutes (from configuration).</summary>
    int AccessTokenExpirationMinutes { get; }

    /// <summary>Refresh token lifetime in days (from configuration).</summary>
    int RefreshTokenExpirationDays { get; }
}

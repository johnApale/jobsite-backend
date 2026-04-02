using Jobsite.Modules.Auth.Application.DTOs;

namespace Jobsite.Modules.Auth.Application.Interfaces;

/// <summary>
/// Application service interface for authentication operations.
/// </summary>
public interface IAuthService
{
    /// <summary>Register a new user with email/password.</summary>
    Task<AuthTokensResponse> RegisterAsync(RegisterRequest request, Guid tenantId, CancellationToken ct = default);

    /// <summary>Authenticate with email/password and issue tokens.</summary>
    Task<AuthTokensResponse> LoginAsync(LoginRequest request, Guid tenantId, CancellationToken ct = default);

    /// <summary>Rotate a refresh token and issue new access/refresh tokens.</summary>
    Task<AuthTokensResponse> RefreshTokenAsync(RefreshTokenRequest request, Guid tenantId, CancellationToken ct = default);

    /// <summary>Authenticate via OAuth provider and issue tokens.</summary>
    Task<AuthTokensResponse> OAuthLoginAsync(string provider, OAuthLoginRequest request, Guid tenantId, CancellationToken ct = default);

    /// <summary>Revoke a refresh token (logout).</summary>
    Task LogoutAsync(RefreshTokenRequest request, CancellationToken ct = default);

    /// <summary>Get the current authenticated user's profile.</summary>
    Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
}

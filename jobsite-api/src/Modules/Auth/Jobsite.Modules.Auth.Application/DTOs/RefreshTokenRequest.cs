namespace Jobsite.Modules.Auth.Application.DTOs;

/// <summary>Request body for <c>POST /api/v1/auth/refresh</c>.</summary>
public sealed class RefreshTokenRequest
{
    /// <summary>The refresh token to exchange for a new access token.</summary>
    public required string RefreshToken { get; init; }
}

namespace Jobsite.Modules.Auth.Application.DTOs;

/// <summary>Response body containing JWT access token and refresh token.</summary>
public sealed class AuthTokensResponse
{
    /// <summary>JWT access token.</summary>
    public required string AccessToken { get; init; }

    /// <summary>Refresh token for obtaining new access tokens.</summary>
    public required string RefreshToken { get; init; }

    /// <summary>Access token lifetime in seconds.</summary>
    public required int ExpiresIn { get; init; }

    /// <summary>Token type (always "Bearer").</summary>
    public string TokenType { get; init; } = "Bearer";
}

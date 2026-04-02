namespace Jobsite.Modules.Auth.Application.DTOs;

/// <summary>Request body for <c>POST /api/v1/auth/oauth/{provider}</c>.</summary>
public sealed class OAuthLoginRequest
{
    /// <summary>OAuth provider token (ID token or access token from the provider).</summary>
    public required string ProviderToken { get; init; }

    /// <summary>User's email from the OAuth provider.</summary>
    public required string Email { get; init; }

    /// <summary>Display name from the OAuth provider.</summary>
    public string? DisplayName { get; init; }
}

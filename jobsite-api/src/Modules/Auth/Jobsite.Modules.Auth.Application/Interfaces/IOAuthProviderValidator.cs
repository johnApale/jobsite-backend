namespace Jobsite.Modules.Auth.Application.Interfaces;

/// <summary>
/// Validated user info returned from an OAuth provider.
/// </summary>
public sealed class OAuthUserInfo
{
    public required string SubjectId { get; init; }
    public required string Email { get; init; }
    public string? DisplayName { get; init; }
    public bool EmailVerified { get; init; }
}

/// <summary>
/// Validates OAuth provider tokens and extracts user information.
/// Initially stubbed — real provider HTTP calls are deferred.
/// </summary>
public interface IOAuthProviderValidator
{
    /// <summary>Validate a provider token and extract user info.</summary>
    Task<OAuthUserInfo> ValidateTokenAsync(string provider, string providerToken, CancellationToken ct = default);
}

using Jobsite.Modules.Auth.Application.Interfaces;
using Jobsite.SharedKernel.Errors;

namespace Jobsite.Modules.Auth.Infrastructure.Security;

/// <summary>
/// Dispatches OAuth token validation to the appropriate provider-specific validator.
/// Registered as <see cref="IOAuthProviderValidator"/> in production DI.
/// </summary>
public sealed class OAuthProviderDispatcher : IOAuthProviderValidator
{
    private readonly GoogleOAuthValidator _google;
    private readonly AppleOAuthValidator _apple;
    private readonly FacebookOAuthValidator _facebook;

    public OAuthProviderDispatcher(
        GoogleOAuthValidator google,
        AppleOAuthValidator apple,
        FacebookOAuthValidator facebook)
    {
        _google = google;
        _apple = apple;
        _facebook = facebook;
    }

    public Task<OAuthUserInfo> ValidateTokenAsync(string provider, string providerToken, CancellationToken ct = default)
    {
        return provider.ToLowerInvariant() switch
        {
            "google" => _google.ValidateAsync(providerToken, ct),
            "apple" => _apple.ValidateAsync(providerToken, ct),
            "facebook" => _facebook.ValidateAsync(providerToken, ct),
            _ => throw AppErrors.InvalidRequest.WithMessage($"Unsupported OAuth provider: {provider}"),
        };
    }
}

using Jobsite.Modules.Auth.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Auth.Infrastructure.Security;

/// <summary>
/// Stub OAuth provider validator that trusts the incoming request payload.
/// Replace with real Google/Apple/Facebook HTTP token validation calls.
/// </summary>
public sealed class StubOAuthProviderValidator : IOAuthProviderValidator
{
    private readonly ILogger<StubOAuthProviderValidator> _logger;
    private readonly IHostEnvironment _environment;

    public StubOAuthProviderValidator(ILogger<StubOAuthProviderValidator> logger, IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public Task<OAuthUserInfo> ValidateTokenAsync(string provider, string providerToken, CancellationToken ct = default)
    {
        if (!_environment.IsDevelopment())
        {
            _logger.LogWarning(
                "StubOAuthProviderValidator is active in {Environment}. Replace with real provider validation",
                _environment.EnvironmentName);
        }

        _logger.LogDebug("Stub OAuth validation for provider {Provider} — trusting request payload", provider);

        // In production, this would call the provider's token-info endpoint:
        // Google: https://oauth2.googleapis.com/tokeninfo?id_token={token}
        // Apple: Decode and verify the JWT from Apple
        // Facebook: https://graph.facebook.com/me?access_token={token}
        //
        // The stub returns a deterministic subject ID derived from the token
        // so tests can use known tokens to get predictable results.
        OAuthUserInfo info = new()
        {
            SubjectId = $"stub_{provider.ToLowerInvariant()}_{providerToken.GetHashCode():X8}",
            Email = string.Empty,
            DisplayName = null,
            EmailVerified = true
        };

        return Task.FromResult(info);
    }
}

using System.IdentityModel.Tokens.Jwt;
using Jobsite.Modules.Auth.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Jobsite.Modules.Auth.Infrastructure.Security;

/// <summary>
/// Validates Apple Sign In ID tokens (JWTs signed by Apple).
/// Downloads Apple's public keys and validates the token signature.
/// </summary>
public sealed class AppleOAuthValidator
{
    private const string AppleKeysUrl = "https://appleid.apple.com/auth/keys";
    private const string AppleIssuer = "https://appleid.apple.com";

    private readonly HttpClient _httpClient;
    private readonly ILogger<AppleOAuthValidator> _logger;

    public AppleOAuthValidator(HttpClient httpClient, ILogger<AppleOAuthValidator> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<OAuthUserInfo> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        // Fetch Apple's public keys
        HttpResponseMessage keysResponse = await _httpClient.GetAsync(AppleKeysUrl, ct);
        keysResponse.EnsureSuccessStatusCode();

        string keysJson = await keysResponse.Content.ReadAsStringAsync(ct);
        JsonWebKeySet jwks = new(keysJson);

        TokenValidationParameters validationParameters = new()
        {
            ValidIssuer = AppleIssuer,
            ValidateAudience = false, // Audience is the app's client ID — validated at the caller level if needed
            IssuerSigningKeys = jwks.GetSigningKeys(),
            ValidateLifetime = true,
        };

        JwtSecurityTokenHandler handler = new();
        handler.ValidateToken(idToken, validationParameters, out SecurityToken validatedToken);

        JwtSecurityToken jwt = (JwtSecurityToken)validatedToken;

        string subjectId = jwt.Subject
            ?? throw new InvalidOperationException("Apple token missing 'sub' claim");
        string? email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
        bool emailVerified = jwt.Claims.FirstOrDefault(c => c.Type == "email_verified")?.Value == "true";

        _logger.LogDebug("Apple OAuth validated for subject {SubjectId}", subjectId);

        return new OAuthUserInfo
        {
            SubjectId = subjectId,
            Email = email ?? string.Empty,
            EmailVerified = emailVerified,
            DisplayName = null, // Apple does not include name in the ID token on subsequent logins
        };
    }
}

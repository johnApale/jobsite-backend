using System.Text.Json;
using Jobsite.Modules.Auth.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Auth.Infrastructure.Security;

/// <summary>
/// Validates Google OAuth ID tokens by calling the Google tokeninfo endpoint.
/// </summary>
public sealed class GoogleOAuthValidator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleOAuthValidator> _logger;

    public GoogleOAuthValidator(HttpClient httpClient, ILogger<GoogleOAuthValidator> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<OAuthUserInfo> ValidateAsync(string idToken, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(
            $"https://oauth2.googleapis.com/tokeninfo?id_token={Uri.EscapeDataString(idToken)}", ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Google token validation failed with status {StatusCode}", response.StatusCode);
            throw new InvalidOperationException("Invalid Google OAuth token");
        }

        using JsonDocument doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        JsonElement root = doc.RootElement;

        string subjectId = root.GetProperty("sub").GetString()
            ?? throw new InvalidOperationException("Google token missing 'sub' claim");
        string email = root.GetProperty("email").GetString()
            ?? throw new InvalidOperationException("Google token missing 'email' claim");
        bool emailVerified = root.TryGetProperty("email_verified", out JsonElement ev)
            && ev.GetString() == "true";
        string? displayName = root.TryGetProperty("name", out JsonElement name) ? name.GetString() : null;

        return new OAuthUserInfo
        {
            SubjectId = subjectId,
            Email = email,
            EmailVerified = emailVerified,
            DisplayName = displayName,
        };
    }
}

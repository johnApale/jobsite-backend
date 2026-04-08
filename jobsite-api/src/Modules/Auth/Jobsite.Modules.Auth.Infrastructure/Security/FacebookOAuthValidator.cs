using System.Text.Json;
using Jobsite.Modules.Auth.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Auth.Infrastructure.Security;

/// <summary>
/// Validates Facebook OAuth access tokens by calling the Graph API.
/// </summary>
public sealed class FacebookOAuthValidator
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FacebookOAuthValidator> _logger;

    public FacebookOAuthValidator(HttpClient httpClient, ILogger<FacebookOAuthValidator> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<OAuthUserInfo> ValidateAsync(string accessToken, CancellationToken ct = default)
    {
        HttpResponseMessage response = await _httpClient.GetAsync(
            $"https://graph.facebook.com/me?access_token={Uri.EscapeDataString(accessToken)}&fields=id,email,name", ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Facebook token validation failed with status {StatusCode}", response.StatusCode);
            throw new InvalidOperationException("Invalid Facebook OAuth token");
        }

        using JsonDocument doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);

        JsonElement root = doc.RootElement;

        string subjectId = root.GetProperty("id").GetString()
            ?? throw new InvalidOperationException("Facebook response missing 'id'");
        string? email = root.TryGetProperty("email", out JsonElement emailEl) ? emailEl.GetString() : null;
        string? displayName = root.TryGetProperty("name", out JsonElement name) ? name.GetString() : null;

        return new OAuthUserInfo
        {
            SubjectId = subjectId,
            Email = email ?? string.Empty,
            EmailVerified = email is not null,
            DisplayName = displayName,
        };
    }
}

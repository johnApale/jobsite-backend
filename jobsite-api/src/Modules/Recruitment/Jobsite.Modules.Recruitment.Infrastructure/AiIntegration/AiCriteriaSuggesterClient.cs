using System.Net.Http.Json;
using System.Text.Json;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Recruitment.Infrastructure.AiIntegration;

/// <summary>
/// HTTP client for the AI Service's criteria suggestion endpoint.
/// Gracefully returns null when the service is unavailable or returns an error.
/// Resilience policies (timeout, retry, circuit breaker) are configured via
/// <c>Microsoft.Extensions.Http.Resilience</c> in DI registration.
/// </summary>
public sealed class AiCriteriaSuggesterClient : IAiCriteriaSuggester
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<AiCriteriaSuggesterClient> _logger;

    public AiCriteriaSuggesterClient(HttpClient httpClient, ILogger<AiCriteriaSuggesterClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<AiCriteriaSuggestion>?> SuggestAsync(
        string jobTitle, string jobDescription, CancellationToken ct = default)
    {
        try
        {
            object requestBody = new { job_title = jobTitle, job_description = jobDescription };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/v1/ai/criteria/suggest", requestBody, JsonOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI Service returned {StatusCode} for criteria suggestion request",
                    response.StatusCode);
                return null;
            }

            List<AiCriteriaSuggestion>? result = await response.Content.ReadFromJsonAsync<List<AiCriteriaSuggestion>>(
                JsonOptions, ct);

            _logger.LogInformation("AI Service successfully suggested {Count} criteria", result?.Count ?? 0);
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "AI Service criteria suggestion failed, returning null");
            return null;
        }
    }
}

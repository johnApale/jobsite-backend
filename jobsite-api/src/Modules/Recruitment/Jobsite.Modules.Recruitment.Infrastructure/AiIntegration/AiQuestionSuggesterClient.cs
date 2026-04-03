using System.Net.Http.Json;
using System.Text.Json;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Recruitment.Infrastructure.AiIntegration;

/// <summary>
/// HTTP client for the AI Service's screening question suggestion endpoint.
/// Gracefully returns null when the service is unavailable or returns an error.
/// Resilience policies (timeout, retry, circuit breaker) are configured via
/// <c>Microsoft.Extensions.Http.Resilience</c> in DI registration.
/// </summary>
public sealed class AiQuestionSuggesterClient : IAiQuestionSuggester
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<AiQuestionSuggesterClient> _logger;

    public AiQuestionSuggesterClient(HttpClient httpClient, ILogger<AiQuestionSuggesterClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<AiQuestionSuggestion>?> SuggestAsync(
        string jobDescription, List<CriteriaResponse> criteria, CancellationToken ct = default)
    {
        try
        {
            object requestBody = new { job_description = jobDescription, criteria };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/v1/ai/assessment/suggest", requestBody, JsonOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI Service returned {StatusCode} for question suggestion request",
                    response.StatusCode);
                return null;
            }

            List<AiQuestionSuggestion>? result = await response.Content.ReadFromJsonAsync<List<AiQuestionSuggestion>>(
                JsonOptions, ct);

            _logger.LogInformation("AI Service successfully suggested {Count} questions", result?.Count ?? 0);
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "AI Service question suggestion failed, returning null");
            return null;
        }
    }
}

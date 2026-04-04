using System.Net.Http.Json;
using System.Text.Json;
using Jobsite.Modules.Screening.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Screening.Infrastructure.AiIntegration;

public sealed class AiCandidateFeedbackClient : IAiCandidateFeedbackClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<AiCandidateFeedbackClient> _logger;

    public AiCandidateFeedbackClient(HttpClient httpClient, ILogger<AiCandidateFeedbackClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string?> GenerateFeedbackAsync(
        string criteriaBreakdown, decimal overallScore, string transparencyLevel,
        CancellationToken ct = default)
    {
        try
        {
            object requestBody = new
            {
                criteria_breakdown = criteriaBreakdown,
                overall_score = overallScore,
                transparency_level = transparencyLevel
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/v1/ai/screening/feedback", requestBody, JsonOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI Feedback Service returned {StatusCode} — candidate feedback will be unavailable",
                    response.StatusCode);
                return null;
            }

            FeedbackResponse? result = await response.Content.ReadFromJsonAsync<FeedbackResponse>(JsonOptions, ct);
            return result?.Feedback;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "AI Feedback Service call failed — candidate feedback will be unavailable");
            return null;
        }
    }

    private sealed class FeedbackResponse
    {
        public string? Feedback { get; set; }
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Screening.Infrastructure.AiIntegration;

public sealed class AiAnswerScoringClient : IAiAnswerScoringClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<AiAnswerScoringClient> _logger;

    public AiAnswerScoringClient(HttpClient httpClient, ILogger<AiAnswerScoringClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<AnswerScore>?> ScoreAnswersAsync(
        List<AnswerScoringRequest> answers, CancellationToken ct = default)
    {
        try
        {
            object requestBody = new
            {
                answers = answers.Select(a => new
                {
                    question_id = a.QuestionId,
                    question_text = a.QuestionText,
                    response_text = a.ResponseText,
                    scoring_guidance = a.ScoringGuidance,
                    key_topics = a.KeyTopics
                })
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/v1/ai/screening/score-answers", requestBody, JsonOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI Answer Scoring returned {StatusCode} — free-text answers will remain unscored",
                    response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<List<AnswerScore>>(JsonOptions, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "AI Answer Scoring call failed — free-text answers will remain unscored");
            return null;
        }
    }
}

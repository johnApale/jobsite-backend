using System.Net.Http.Json;
using System.Text.Json;
using Jobsite.Modules.Profiles.Application.DTOs;
using Jobsite.Modules.Profiles.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Profiles.Infrastructure.AiIntegration;

/// <summary>
/// HTTP client for the AI Service's resume parsing endpoint.
/// Gracefully returns null when the service is unavailable or returns an error.
/// Resilience policies (timeout, retry, circuit breaker) are configured via
/// <c>Microsoft.Extensions.Http.Resilience</c> in DI registration.
/// </summary>
public sealed class AiResumeParserClient : IAiResumeParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<AiResumeParserClient> _logger;

    public AiResumeParserClient(HttpClient httpClient, ILogger<AiResumeParserClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AiResumeParseResult?> ParseAsync(string parsedText, CancellationToken ct = default)
    {
        try
        {
            object requestBody = new { parsed_text = parsedText };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/v1/ai/resumes/parse", requestBody, JsonOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI Service returned {StatusCode} for resume parse request",
                    response.StatusCode);
                return null;
            }

            AiResumeParseResult? result = await response.Content.ReadFromJsonAsync<AiResumeParseResult>(
                JsonOptions, ct);

            _logger.LogInformation("AI Service successfully parsed resume text");
            return result;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "AI Service resume parse failed, falling back to basic parsing only");
            return null;
        }
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Screening.Infrastructure.AiIntegration;

public sealed class AiScoringClient : IAiScoringClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<AiScoringClient> _logger;

    public AiScoringClient(HttpClient httpClient, ILogger<AiScoringClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AiScoringResult?> EvaluateAsync(
        List<CriteriaSnapshot> criteria, ApplicantDataSnapshot applicantData,
        CancellationToken ct = default)
    {
        try
        {
            object requestBody = new
            {
                criteria = criteria.Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    category = c.Category,
                    evaluation_method = c.EvaluationMethod,
                    is_required = c.IsRequired,
                    weight = c.Weight,
                    configuration = c.Configuration
                }),
                applicant = new
                {
                    profile_skills = applicantData.ProfileSkills,
                    resume_parsed_text = applicantData.ResumeParsedText,
                    resume_extracted_skills = applicantData.ResumeExtractedSkills,
                    ai_parsed_content = applicantData.AiParsedContent
                }
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync(
                "/api/v1/ai/screening/evaluate", requestBody, JsonOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "AI Scoring Service returned {StatusCode} — falling back to deterministic-only scoring",
                    response.StatusCode);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<AiScoringResult>(JsonOptions, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex,
                "AI Scoring Service call failed — falling back to deterministic-only scoring");
            return null;
        }
    }
}

using System.Text.Json;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Domain.Constants;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Screening.Application.Services;

/// <summary>
/// Rule-based scoring engine — evaluates applicant data against job criteria
/// using ExactMatch, RangeMatch, and keyword-based SemanticSimilarity.
/// Zero-cost, always runs, drives all routing decisions.
/// </summary>
public sealed class DeterministicScoringEngine : IDeterministicScoringEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<DeterministicScoringEngine> _logger;

    public DeterministicScoringEngine(ILogger<DeterministicScoringEngine> logger) => _logger = logger;

    public Task<ScoringResult> ScoreAsync(
        List<CriteriaSnapshot> criteria,
        ApplicantDataSnapshot applicantData,
        CancellationToken ct = default)
    {
        if (criteria.Count == 0)
        {
            return Task.FromResult(new ScoringResult
            {
                Breakdown = [],
                OverallScore = 0m
            });
        }

        List<CriterionScoreDto> breakdown = [];
        decimal totalWeight = criteria.Sum(c => c.Weight);

        foreach (CriteriaSnapshot criterion in criteria)
        {
            decimal score = criterion.EvaluationMethod switch
            {
                "ExactMatch" => ScoreExactMatch(criterion, applicantData),
                "RangeMatch" => ScoreRangeMatch(criterion, applicantData),
                "SemanticSimilarity" => ScoreSemanticSimilarity(criterion, applicantData),
                _ => 0m
            };

            score = Math.Clamp(score, 0m, 100m);

            string result = ScoreResult.FromScore(score);
            string reasoning = BuildReasoning(criterion, score, applicantData);

            breakdown.Add(new CriterionScoreDto
            {
                CriterionId = criterion.Id,
                CriterionName = criterion.Name,
                Category = criterion.Category,
                Weight = criterion.Weight,
                Score = Math.Round(score, 2),
                Result = result,
                Reasoning = reasoning
            });
        }

        decimal overallScore = totalWeight > 0
            ? Math.Round(breakdown.Sum(b => b.Score * b.Weight) / totalWeight, 2)
            : 0m;

        overallScore = Math.Clamp(overallScore, 0m, 100m);

        _logger.LogDebug("Deterministic scoring complete: {CriteriaCount} criteria, overall score {Score}",
            criteria.Count, overallScore);

        return Task.FromResult(new ScoringResult
        {
            Breakdown = breakdown,
            OverallScore = overallScore
        });
    }

    private static decimal ScoreExactMatch(CriteriaSnapshot criterion, ApplicantDataSnapshot applicant)
    {
        CriteriaConfig? config = ParseConfig(criterion.Configuration);
        if (config is null)
            return 0m;

        string searchText = BuildSearchText(applicant);

        switch (criterion.Category)
        {
            case "Skill":
                if (config.SkillName is not null &&
                    ContainsIgnoreCase(searchText, config.SkillName))
                    return 100m;
                break;

            case "Certification":
                if (config.CertificationName is not null &&
                    ContainsIgnoreCase(searchText, config.CertificationName))
                    return 100m;
                break;

            case "Education":
                if (config.DegreeLevel is not null &&
                    ContainsIgnoreCase(searchText, config.DegreeLevel))
                    return 100m;
                if (config.FieldOfStudy is not null &&
                    ContainsIgnoreCase(searchText, config.FieldOfStudy))
                    return 100m;
                break;

            case "Location":
                if (config.Location is not null &&
                    ContainsIgnoreCase(searchText, config.Location))
                    return 100m;
                break;

            default:
                if (config.Value is not null &&
                    ContainsIgnoreCase(searchText, config.Value))
                    return 100m;
                break;
        }

        return 0m;
    }

    private static decimal ScoreRangeMatch(CriteriaSnapshot criterion, ApplicantDataSnapshot applicant)
    {
        CriteriaConfig? config = ParseConfig(criterion.Configuration);
        if (config is null || config.MinYears is null)
            return 0m;

        int requiredYears = config.MinYears.Value;
        if (requiredYears <= 0)
            return 100m;

        int detectedYears = DetectYearsOfExperience(applicant, config.SkillName);

        if (detectedYears >= requiredYears)
            return 100m;

        return Math.Round((decimal)detectedYears / requiredYears * 100m, 2);
    }

    private static decimal ScoreSemanticSimilarity(CriteriaSnapshot criterion, ApplicantDataSnapshot applicant)
    {
        CriteriaConfig? config = ParseConfig(criterion.Configuration);
        string searchText = BuildSearchText(applicant);

        if (string.IsNullOrWhiteSpace(searchText))
            return 0m;

        List<string> keywords = [];

        if (config?.Keywords is not null)
            keywords.AddRange(config.Keywords);

        if (config?.SkillName is not null)
            keywords.Add(config.SkillName);

        if (config?.Value is not null)
            keywords.Add(config.Value);

        if (criterion.Name is not null && keywords.Count == 0)
            keywords.Add(criterion.Name);

        if (keywords.Count == 0)
            return 0m;

        int matched = keywords.Count(k => ContainsIgnoreCase(searchText, k));
        return Math.Round((decimal)matched / keywords.Count * 100m, 2);
    }

    private static int DetectYearsOfExperience(ApplicantDataSnapshot applicant, string? skillName)
    {
        if (applicant.AiParsedContent is not null)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(applicant.AiParsedContent);
                if (doc.RootElement.TryGetProperty("skills", out JsonElement skills) &&
                    skills.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement skill in skills.EnumerateArray())
                    {
                        if (skill.TryGetProperty("name", out JsonElement name) &&
                            skillName is not null &&
                            ContainsIgnoreCase(name.GetString() ?? "", skillName) &&
                            skill.TryGetProperty("years", out JsonElement years))
                        {
                            return years.GetInt32();
                        }
                    }
                }

                if (doc.RootElement.TryGetProperty("total_years_experience", out JsonElement totalYears))
                    return totalYears.GetInt32();
            }
            catch (JsonException)
            {
                // Fall through to text-based detection
            }
        }

        return 0;
    }

    private static string BuildSearchText(ApplicantDataSnapshot applicant)
    {
        List<string> parts = [];

        if (applicant.AiParsedContent is not null)
            parts.Add(applicant.AiParsedContent);
        if (applicant.ResumeParsedText is not null)
            parts.Add(applicant.ResumeParsedText);
        if (applicant.ResumeExtractedSkills is not null)
            parts.Add(applicant.ResumeExtractedSkills);
        if (applicant.ProfileSkills is not null)
            parts.Add(applicant.ProfileSkills);

        return string.Join(" ", parts);
    }

    private static string BuildReasoning(CriteriaSnapshot criterion, decimal score, ApplicantDataSnapshot applicant)
    {
        string result = ScoreResult.FromScore(score);
        return result switch
        {
            ScoreResult.MeetsRequirement =>
                $"Applicant meets the '{criterion.Name}' requirement (score: {score:F0})",
            ScoreResult.PartialMatch =>
                $"Partial match for '{criterion.Name}' — some relevant experience/skills detected (score: {score:F0})",
            _ =>
                $"No match found for '{criterion.Name}' in applicant's profile or resume"
        };
    }

    private static CriteriaConfig? ParseConfig(string configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration))
            return null;

        try
        {
            return JsonSerializer.Deserialize<CriteriaConfig>(configuration, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool ContainsIgnoreCase(string text, string value) =>
        text.Contains(value, StringComparison.OrdinalIgnoreCase);

    /// <summary>Internal DTO for deserializing the criteria configuration JSONB.</summary>
    private sealed class CriteriaConfig
    {
        public string? SkillName { get; set; }
        public string? Level { get; set; }
        public int? MinYears { get; set; }
        public string? CertificationName { get; set; }
        public string? DegreeLevel { get; set; }
        public string? FieldOfStudy { get; set; }
        public string? Location { get; set; }
        public string? Value { get; set; }
        public List<string>? Keywords { get; set; }
    }
}

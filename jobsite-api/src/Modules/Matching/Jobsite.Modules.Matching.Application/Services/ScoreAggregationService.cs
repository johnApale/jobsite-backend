using Jobsite.Modules.Matching.Application.DTOs;
using Jobsite.Modules.Matching.Domain.Constants;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Matching.Application.Services;

/// <summary>
/// Computes weighted composite scores from screening + assessment
/// using tenant-configured weights from <c>matching_settings</c>.
/// </summary>
public sealed class ScoreAggregationService : IScoreAggregationService
{
    private readonly ITenantSettingsReader _settingsReader;
    private readonly ILogger<ScoreAggregationService> _logger;

    public ScoreAggregationService(
        ITenantSettingsReader settingsReader,
        ILogger<ScoreAggregationService> logger)
    {
        _settingsReader = settingsReader;
        _logger = logger;
    }

    public async Task<(decimal CompositeScore, string MatchStrength)> ComputeCompositeScoreAsync(
        decimal screeningScore,
        decimal? assessmentScore,
        CancellationToken ct = default)
    {
        MatchingSettings settings = await LoadSettingsAsync(ct);

        decimal compositeScore;

        if (assessmentScore.HasValue)
        {
            // Both scores available — use configured weights
            decimal totalWeight = settings.ScreeningWeight + settings.AssessmentWeight;
            compositeScore = totalWeight > 0
                ? (screeningScore * settings.ScreeningWeight / totalWeight)
                  + (assessmentScore.Value * settings.AssessmentWeight / totalWeight)
                : screeningScore;
        }
        else
        {
            // No assessment yet — screening score is the full composite
            compositeScore = screeningScore;
        }

        compositeScore = Math.Round(compositeScore, 2);
        string matchStrength = MatchStrength.FromScore(compositeScore);

        _logger.LogDebug(
            "Computed composite score {Score} ({Strength}) — screening={Screening}, assessment={Assessment}",
            compositeScore, matchStrength, screeningScore, assessmentScore);

        return (compositeScore, matchStrength);
    }

    private async Task<MatchingSettings> LoadSettingsAsync(CancellationToken ct)
    {
        MatchingSettings? settings =
            await _settingsReader.GetSettingAsync<MatchingSettings>("matching_settings", ct);

        return settings ?? new MatchingSettings();
    }
}

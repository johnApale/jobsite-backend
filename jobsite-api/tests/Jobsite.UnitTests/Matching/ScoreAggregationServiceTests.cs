using FluentAssertions;
using Jobsite.Modules.Matching.Application.DTOs;
using Jobsite.Modules.Matching.Application.Services;
using Jobsite.Modules.Matching.Domain.Constants;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Matching;

public sealed class ScoreAggregationServiceTests
{
    private readonly ITenantSettingsReader _settingsReader = Substitute.For<ITenantSettingsReader>();
    private readonly ScoreAggregationService _service;

    public ScoreAggregationServiceTests()
    {
        _service = new ScoreAggregationService(
            _settingsReader,
            Substitute.For<ILogger<ScoreAggregationService>>());
    }

    private void SetupSettings(decimal screeningWeight = 60m, decimal assessmentWeight = 40m)
    {
        _settingsReader.GetSettingAsync<MatchingSettings>("matching_settings", Arg.Any<CancellationToken>())
            .Returns(new MatchingSettings
            {
                ScreeningWeight = screeningWeight,
                AssessmentWeight = assessmentWeight
            });
    }

    // ── Screening only (no assessment) ───────────────────────────────────

    [Fact]
    public async Task ComputeCompositeScore_NoAssessment_ReturnsScreeningScoreAsComposite()
    {
        // Arrange
        SetupSettings();

        // Act
        (decimal compositeScore, string matchStrength) =
            await _service.ComputeCompositeScoreAsync(85m, assessmentScore: null, CancellationToken.None);

        // Assert
        compositeScore.Should().Be(85m);
        matchStrength.Should().Be(MatchStrength.Strong);
    }

    [Fact]
    public async Task ComputeCompositeScore_NoAssessment_WeakScore_ReturnsWeak()
    {
        // Arrange
        SetupSettings();

        // Act
        (decimal compositeScore, string matchStrength) =
            await _service.ComputeCompositeScoreAsync(25m, assessmentScore: null, CancellationToken.None);

        // Assert
        compositeScore.Should().Be(25m);
        matchStrength.Should().Be(MatchStrength.Weak);
    }

    // ── Both scores with default weights (60/40) ────────────────────────

    [Fact]
    public async Task ComputeCompositeScore_BothScores_DefaultWeights_ComputesWeightedAverage()
    {
        // Arrange
        SetupSettings(screeningWeight: 60m, assessmentWeight: 40m);

        // Act — screening=80, assessment=90 → (80*60/100) + (90*40/100) = 48 + 36 = 84
        (decimal compositeScore, string matchStrength) =
            await _service.ComputeCompositeScoreAsync(80m, assessmentScore: 90m, CancellationToken.None);

        // Assert
        compositeScore.Should().Be(84m);
        matchStrength.Should().Be(MatchStrength.Strong);
    }

    [Fact]
    public async Task ComputeCompositeScore_BothScores_EqualWeights_ComputesSimpleAverage()
    {
        // Arrange
        SetupSettings(screeningWeight: 50m, assessmentWeight: 50m);

        // Act — screening=70, assessment=50 → (70*50/100) + (50*50/100) = 35 + 25 = 60
        (decimal compositeScore, string matchStrength) =
            await _service.ComputeCompositeScoreAsync(70m, assessmentScore: 50m, CancellationToken.None);

        // Assert
        compositeScore.Should().Be(60m);
        matchStrength.Should().Be(MatchStrength.Good);
    }

    [Fact]
    public async Task ComputeCompositeScore_BothScores_CustomWeights_ComputesCorrectly()
    {
        // Arrange — screening-heavy configuration
        SetupSettings(screeningWeight: 80m, assessmentWeight: 20m);

        // Act — screening=90, assessment=40 → (90*80/100) + (40*20/100) = 72 + 8 = 80
        (decimal compositeScore, string matchStrength) =
            await _service.ComputeCompositeScoreAsync(90m, assessmentScore: 40m, CancellationToken.None);

        // Assert
        compositeScore.Should().Be(80m);
        matchStrength.Should().Be(MatchStrength.Strong);
    }

    // ── Edge cases ───────────────────────────────────────────────────────

    [Fact]
    public async Task ComputeCompositeScore_ZeroWeights_FallsBackToScreeningScore()
    {
        // Arrange
        SetupSettings(screeningWeight: 0m, assessmentWeight: 0m);

        // Act
        (decimal compositeScore, string _) =
            await _service.ComputeCompositeScoreAsync(75m, assessmentScore: 60m, CancellationToken.None);

        // Assert — when total weight is 0, falls back to screening score
        compositeScore.Should().Be(75m);
    }

    [Fact]
    public async Task ComputeCompositeScore_NullSettings_UsesDefaults()
    {
        // Arrange — settings reader returns null (no tenant settings configured)
        _settingsReader.GetSettingAsync<MatchingSettings>("matching_settings", Arg.Any<CancellationToken>())
            .Returns((MatchingSettings?)null);

        // Act — uses default 60/40 weights
        (decimal compositeScore, string matchStrength) =
            await _service.ComputeCompositeScoreAsync(80m, assessmentScore: 90m, CancellationToken.None);

        // Assert — (80*60/100) + (90*40/100) = 48 + 36 = 84
        compositeScore.Should().Be(84m);
        matchStrength.Should().Be(MatchStrength.Strong);
    }

    [Fact]
    public async Task ComputeCompositeScore_RoundsToTwoDecimalPlaces()
    {
        // Arrange
        SetupSettings(screeningWeight: 60m, assessmentWeight: 40m);

        // Act — screening=77, assessment=83 → (77*60/100) + (83*40/100) = 46.2 + 33.2 = 79.4
        (decimal compositeScore, string _) =
            await _service.ComputeCompositeScoreAsync(77m, assessmentScore: 83m, CancellationToken.None);

        // Assert
        compositeScore.Should().Be(79.4m);
    }
}

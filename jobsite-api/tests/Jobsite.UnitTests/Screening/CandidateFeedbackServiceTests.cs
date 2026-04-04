using FluentAssertions;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Application.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Screening;

public sealed class CandidateFeedbackServiceTests
{
    private readonly IAiCandidateFeedbackClient _feedbackClient = Substitute.For<IAiCandidateFeedbackClient>();
    private readonly CandidateFeedbackService _service;

    public CandidateFeedbackServiceTests()
    {
        _service = new CandidateFeedbackService(
            _feedbackClient,
            Substitute.For<ILogger<CandidateFeedbackService>>());
    }

    [Fact]
    public async Task GenerateFeedbackAsync_AiReturnsFeedback_ReturnsString()
    {
        // Arrange
        string expectedFeedback = "Your profile demonstrates strong alignment with the role requirements.";
        _feedbackClient.GenerateFeedbackAsync(
            Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(expectedFeedback);

        // Act
        string? result = await _service.GenerateFeedbackAsync(
            """[{"score": 80}]""", 80m, "Detailed", CancellationToken.None);

        // Assert
        result.Should().Be(expectedFeedback);
    }

    [Fact]
    public async Task GenerateFeedbackAsync_AiUnavailable_ReturnsNullWithoutException()
    {
        // Arrange
        _feedbackClient.GenerateFeedbackAsync(
            Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        string? result = await _service.GenerateFeedbackAsync(
            """[{"score": 50}]""", 50m, "Summary", CancellationToken.None);

        // Assert — graceful degradation, no exception
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateFeedbackAsync_ForwardsCorrectTransparencyLevel()
    {
        // Arrange — verify the transparency level is forwarded to the AI client
        _feedbackClient.GenerateFeedbackAsync(
            Arg.Any<string>(), Arg.Any<decimal>(), "Detailed", Arg.Any<CancellationToken>())
            .Returns("Detailed feedback");

        // Act
        string? result = await _service.GenerateFeedbackAsync(
            """[{"score": 90}]""", 90m, "Detailed", CancellationToken.None);

        // Assert
        result.Should().Be("Detailed feedback");
        await _feedbackClient.Received(1).GenerateFeedbackAsync(
            Arg.Any<string>(), 90m, "Detailed", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateFeedbackAsync_ForwardsBreakdownAndScore()
    {
        // Arrange — verify the criteria breakdown and score are forwarded correctly
        string breakdown = """[{"criterion_name": "C#", "score": 95}]""";
        _feedbackClient.GenerateFeedbackAsync(
            breakdown, 95m, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Excellent match");

        // Act
        string? result = await _service.GenerateFeedbackAsync(
            breakdown, 95m, "Summary", CancellationToken.None);

        // Assert
        result.Should().Be("Excellent match");
        await _feedbackClient.Received(1).GenerateFeedbackAsync(
            breakdown, 95m, "Summary", Arg.Any<CancellationToken>());
    }
}

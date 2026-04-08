using FluentAssertions;
using Jobsite.Modules.HRWorkflows.Application.Services;
using Jobsite.Modules.HRWorkflows.Domain.Constants;
using Jobsite.Modules.HRWorkflows.Domain.Entities;

namespace Jobsite.UnitTests.HRWorkflows;

public sealed class FeedbackAggregationServiceTests
{
    private readonly FeedbackAggregationService _service = new();

    [Fact]
    public void AggregateRecommendation_NoPanelists_ReturnsNull()
    {
        // Arrange
        List<InterviewPanelist> panelists = [];

        // Act
        string? result = _service.AggregateRecommendation(panelists);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void AggregateRecommendation_NoFeedback_ReturnsNull()
    {
        // Arrange
        List<InterviewPanelist> panelists =
        [
            TestData.CreateInterviewPanelist(),
            TestData.CreateInterviewPanelist()
        ];

        // Act
        string? result = _service.AggregateRecommendation(panelists);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void AggregateRecommendation_MajorityStrongHire_ReturnsStrongHire()
    {
        // Arrange
        List<InterviewPanelist> panelists =
        [
            TestData.CreateInterviewPanelist(
                recommendation: InterviewRecommendation.StrongHire,
                feedbackSubmittedAt: DateTime.UtcNow),
            TestData.CreateInterviewPanelist(
                recommendation: InterviewRecommendation.StrongHire,
                feedbackSubmittedAt: DateTime.UtcNow),
            TestData.CreateInterviewPanelist(
                recommendation: InterviewRecommendation.Hire,
                feedbackSubmittedAt: DateTime.UtcNow)
        ];

        // Act
        string? result = _service.AggregateRecommendation(panelists);

        // Assert
        result.Should().Be(InterviewRecommendation.StrongHire);
    }

    [Fact]
    public void AggregateRecommendation_Tie_ReturnsHighestVoted()
    {
        // Arrange
        List<InterviewPanelist> panelists =
        [
            TestData.CreateInterviewPanelist(
                recommendation: InterviewRecommendation.StrongHire,
                feedbackSubmittedAt: DateTime.UtcNow),
            TestData.CreateInterviewPanelist(
                recommendation: InterviewRecommendation.NoHire,
                feedbackSubmittedAt: DateTime.UtcNow)
        ];

        // Act
        string? result = _service.AggregateRecommendation(panelists);

        // Assert — in tie, first ordered by count desc wins
        result.Should().NotBeNull();
    }

    [Fact]
    public void AggregateRecommendation_MixedWithPendingFeedback_OnlyCountsSubmitted()
    {
        // Arrange
        List<InterviewPanelist> panelists =
        [
            TestData.CreateInterviewPanelist(
                recommendation: InterviewRecommendation.Hire,
                feedbackSubmittedAt: DateTime.UtcNow),
            TestData.CreateInterviewPanelist(
                recommendation: InterviewRecommendation.Hire,
                feedbackSubmittedAt: DateTime.UtcNow),
            TestData.CreateInterviewPanelist() // no feedback yet
        ];

        // Act
        string? result = _service.AggregateRecommendation(panelists);

        // Assert
        result.Should().Be(InterviewRecommendation.Hire);
    }
}

using FluentAssertions;
using Jobsite.Modules.Screening.Domain.Constants;

namespace Jobsite.UnitTests.Screening;

public sealed class ScreeningConstantsTests
{
    // ── ScreeningStatus ──────────────────────────────────────────────────

    [Theory]
    [InlineData("Pending", true)]
    [InlineData("InProgress", true)]
    [InlineData("Completed", true)]
    [InlineData("Failed", true)]
    [InlineData("Unknown", false)]
    [InlineData("pending", false)]
    [InlineData("", false)]
    public void ScreeningStatus_IsValid_ReturnsExpected(string value, bool expected)
    {
        ScreeningStatus.IsValid(value).Should().Be(expected);
    }

    // ── MatchStrength ────────────────────────────────────────────────────

    [Theory]
    [InlineData("Strong", true)]
    [InlineData("Good", true)]
    [InlineData("Moderate", true)]
    [InlineData("Weak", true)]
    [InlineData("Excellent", false)]
    public void MatchStrength_IsValid_ReturnsExpected(string value, bool expected)
    {
        MatchStrength.IsValid(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(100, "Strong")]
    [InlineData(80, "Strong")]
    [InlineData(79, "Good")]
    [InlineData(60, "Good")]
    [InlineData(59, "Moderate")]
    [InlineData(40, "Moderate")]
    [InlineData(39, "Weak")]
    [InlineData(0, "Weak")]
    public void MatchStrength_FromScore_ReturnsExpectedStrength(decimal score, string expected)
    {
        MatchStrength.FromScore(score).Should().Be(expected);
    }

    // ── ScreeningOutcome ─────────────────────────────────────────────────

    [Theory]
    [InlineData("AutoAdvanced", true)]
    [InlineData("AutoRejected", true)]
    [InlineData("ManualReview", true)]
    [InlineData("ManuallyAdvanced", true)]
    [InlineData("ManuallyRejected", true)]
    [InlineData("Passed", false)]
    public void ScreeningOutcome_IsValid_ReturnsExpected(string value, bool expected)
    {
        ScreeningOutcome.IsValid(value).Should().Be(expected);
    }

    // ── ScoreResult ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("MeetsRequirement", true)]
    [InlineData("PartialMatch", true)]
    [InlineData("Missing", true)]
    [InlineData("NotAvailable", false)]
    public void ScoreResult_IsValid_ReturnsExpected(string value, bool expected)
    {
        ScoreResult.IsValid(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(100, "MeetsRequirement")]
    [InlineData(80, "MeetsRequirement")]
    [InlineData(79, "PartialMatch")]
    [InlineData(40, "PartialMatch")]
    [InlineData(39, "Missing")]
    [InlineData(0, "Missing")]
    public void ScoreResult_FromScore_ReturnsExpected(decimal score, string expected)
    {
        ScoreResult.FromScore(score).Should().Be(expected);
    }

    // ── ManualReviewPolicy ───────────────────────────────────────────────

    [Theory]
    [InlineData("QueueForReview", true)]
    [InlineData("AutoAdvanceAll", true)]
    [InlineData("AutoRejectAll", true)]
    [InlineData("NotifyAndHold", true)]
    [InlineData("Skip", false)]
    public void ManualReviewPolicy_IsValid_ReturnsExpected(string value, bool expected)
    {
        ManualReviewPolicy.IsValid(value).Should().Be(expected);
    }

    // ── TransparencyLevel ────────────────────────────────────────────────

    [Theory]
    [InlineData("None", true)]
    [InlineData("Summary", true)]
    [InlineData("Detailed", true)]
    [InlineData("Full", false)]
    public void TransparencyLevel_IsValid_ReturnsExpected(string value, bool expected)
    {
        TransparencyLevel.IsValid(value).Should().Be(expected);
    }
}

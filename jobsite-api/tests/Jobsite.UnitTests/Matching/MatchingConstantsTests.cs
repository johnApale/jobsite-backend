using FluentAssertions;
using Jobsite.Modules.Matching.Domain.Constants;

namespace Jobsite.UnitTests.Matching;

public sealed class MatchingConstantsTests
{
    // ── MatchStrength ────────────────────────────────────────────────────

    [Theory]
    [InlineData("Strong", true)]
    [InlineData("Good", true)]
    [InlineData("Moderate", true)]
    [InlineData("Weak", true)]
    [InlineData("Excellent", false)]
    [InlineData("strong", false)]
    [InlineData("", false)]
    public void MatchStrength_IsValid_ReturnsExpected(string value, bool expected)
    {
        MatchStrength.IsValid(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(100, "Strong")]
    [InlineData(80, "Strong")]
    [InlineData(79.99, "Good")]
    [InlineData(60, "Good")]
    [InlineData(59.99, "Moderate")]
    [InlineData(40, "Moderate")]
    [InlineData(39.99, "Weak")]
    [InlineData(0, "Weak")]
    public void MatchStrength_FromScore_ReturnsExpectedStrength(decimal score, string expected)
    {
        MatchStrength.FromScore(score).Should().Be(expected);
    }

    // ── ShortlistStatus ──────────────────────────────────────────────────

    [Theory]
    [InlineData("Draft", true)]
    [InlineData("Finalized", true)]
    [InlineData("Active", false)]
    [InlineData("draft", false)]
    [InlineData("", false)]
    public void ShortlistStatus_IsValid_ReturnsExpected(string value, bool expected)
    {
        ShortlistStatus.IsValid(value).Should().Be(expected);
    }

    // ── ShortlistCandidateSource ─────────────────────────────────────────

    [Theory]
    [InlineData("Algorithm", true)]
    [InlineData("Manual", true)]
    [InlineData("Auto", false)]
    [InlineData("algorithm", false)]
    [InlineData("", false)]
    public void ShortlistCandidateSource_IsValid_ReturnsExpected(string value, bool expected)
    {
        ShortlistCandidateSource.IsValid(value).Should().Be(expected);
    }
}

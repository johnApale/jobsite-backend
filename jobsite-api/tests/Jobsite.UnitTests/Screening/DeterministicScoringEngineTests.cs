using FluentAssertions;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Application.Services;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Screening;

public sealed class DeterministicScoringEngineTests
{
    private readonly DeterministicScoringEngine _engine = new(
        Substitute.For<ILogger<DeterministicScoringEngine>>());

    private static ApplicantDataSnapshot CreateApplicantData(
        string? profileSkills = null,
        string? resumeText = null,
        string? extractedSkills = null,
        string? aiParsedContent = null)
    {
        return new ApplicantDataSnapshot
        {
            UserId = Guid.NewGuid(),
            ProfileSkills = profileSkills,
            ResumeParsedText = resumeText,
            ResumeExtractedSkills = extractedSkills,
            AiParsedContent = aiParsedContent
        };
    }

    private static CriteriaSnapshot CreateCriteria(
        string name, string evaluationMethod, int weight, string configuration,
        string category = "Skill", bool isRequired = true)
    {
        return new CriteriaSnapshot
        {
            Id = Guid.NewGuid(),
            Name = name,
            Category = category,
            EvaluationMethod = evaluationMethod,
            IsRequired = isRequired,
            Weight = weight,
            Configuration = configuration
        };
    }

    [Fact]
    public async Task ScoreAsync_ExactMatch_SkillPresent_ReturnsFullScore()
    {
        // Arrange — ExactMatch for Skill category uses "skill_name" in config
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("C# Required", "ExactMatch", 100, """{"skill_name": "C#"}""")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            extractedSkills: """["C#", "Java", "Python"]""");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert
        result.OverallScore.Should().Be(100m);
        result.Breakdown.Should().HaveCount(1);
        result.Breakdown[0].Score.Should().Be(100m);
    }

    [Fact]
    public async Task ScoreAsync_ExactMatch_SkillMissing_ReturnsZero()
    {
        // Arrange
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("Rust Required", "ExactMatch", 100, """{"skill_name": "Rust"}""")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            extractedSkills: """["C#", "Java"]""");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert
        result.OverallScore.Should().Be(0m);
        result.Breakdown[0].Score.Should().Be(0m);
    }

    [Fact]
    public async Task ScoreAsync_SemanticSimilarity_PartialOverlap_ReturnsProportionalScore()
    {
        // Arrange — SemanticSimilarity uses "keywords" in config
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("Skills Match", "SemanticSimilarity", 100,
                """{"keywords": ["C#", "ASP.NET", "Docker", "Kubernetes"]}""", isRequired: false)
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            extractedSkills: """["C#", "ASP.NET"]""");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert
        result.OverallScore.Should().Be(50m); // 2 out of 4 keywords matched
    }

    [Fact]
    public async Task ScoreAsync_MultipleCriteria_WeightedAverage()
    {
        // Arrange — Skill uses skill_name, Education uses degree_level
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("C# Skill", "ExactMatch", 60, """{"skill_name": "C#"}"""),
            CreateCriteria("PhD Required", "ExactMatch", 40, """{"degree_level": "PhD"}""",
                category: "Education", isRequired: false)
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            extractedSkills: """["C#"]""",
            resumeText: "Software developer with BSc in Computer Science");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert — C# matches (100*60), PhD missing (0*40), weighted = 60
        result.OverallScore.Should().Be(60m);
        result.Breakdown.Should().HaveCount(2);
    }

    [Fact]
    public async Task ScoreAsync_EmptyCriteria_ReturnsZero()
    {
        // Arrange
        List<CriteriaSnapshot> criteria = [];
        ApplicantDataSnapshot applicant = CreateApplicantData(
            resumeText: "Some resume content");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert
        result.OverallScore.Should().Be(0m);
        result.Breakdown.Should().BeEmpty();
    }

    [Fact]
    public async Task ScoreAsync_RangeMatch_WithExperience_ReturnsFullScore()
    {
        // Arrange — RangeMatch uses min_years + skill_name; 4 years >= 3 required
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("C# Experience", "RangeMatch", 100,
                """{"min_years": 3, "skill_name": "C#"}""",
                category: "Experience")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            aiParsedContent: """{"skills": [{"name": "C#", "years": 4}]}""");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert
        result.OverallScore.Should().Be(100m);
    }

    [Fact]
    public async Task ScoreAsync_RangeMatch_PartialExperience_ReturnsProportionalScore()
    {
        // Arrange — 2 years out of 5 required = 40%
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("C# Experience", "RangeMatch", 100,
                """{"min_years": 5, "skill_name": "C#"}""",
                category: "Experience")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            aiParsedContent: """{"skills": [{"name": "C#", "years": 2}]}""");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert
        result.OverallScore.Should().Be(40m);
    }

    [Fact]
    public async Task ScoreAsync_ExactMatch_Education_MatchesDegreeLevel()
    {
        // Arrange
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("Bachelor's Degree", "ExactMatch", 100,
                """{"degree_level": "Bachelor"}""", category: "Education")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            resumeText: "Education: Bachelor of Science in Computer Science");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert
        result.OverallScore.Should().Be(100m);
    }

    [Fact]
    public async Task ScoreAsync_UnknownEvaluationMethod_ReturnsZero()
    {
        // Arrange
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("Custom", "UnknownMethod", 100, """{"skill_name": "C#"}""")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            extractedSkills: """["C#"]""");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert
        result.OverallScore.Should().Be(0m);
    }
}

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

    // ─── ExactMatch: Case-insensitive and category variants ─────────────

    [Fact]
    public async Task ScoreAsync_ExactMatch_CaseInsensitive_MatchesRegardlessOfCase()
    {
        // Arrange — config has "c#" lowercase, applicant has "C#" uppercase
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("C# Skill", "ExactMatch", 100, """{"skill_name": "c#"}""")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            extractedSkills: """["C#", "Java"]""");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert
        result.OverallScore.Should().Be(100m);
    }

    [Fact]
    public async Task ScoreAsync_ExactMatch_Certification_MatchesCertName()
    {
        // Arrange
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("AWS Certified", "ExactMatch", 100,
                """{"certification_name": "AWS Solutions Architect"}""", category: "Certification")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            resumeText: "Certifications: AWS Solutions Architect, Azure Developer");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert
        result.OverallScore.Should().Be(100m);
    }

    [Fact]
    public async Task ScoreAsync_ExactMatch_Location_MatchesLocationString()
    {
        // Arrange
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("Remote OK", "ExactMatch", 100,
                """{"location": "Remote"}""", category: "Location")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            profileSkills: "Remote work experience, New York");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert
        result.OverallScore.Should().Be(100m);
    }

    [Fact]
    public async Task ScoreAsync_ExactMatch_SkillInAiParsedContent_FoundViaSearchText()
    {
        // Arrange — skill appears in AI-parsed content but not in extracted skills or profile
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("Docker", "ExactMatch", 100, """{"skill_name": "Docker"}""")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            aiParsedContent: """{"skills": [{"name": "Docker", "years": 2}]}""");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert — BuildSearchText aggregates aiParsedContent so "Docker" is found
        result.OverallScore.Should().Be(100m);
    }

    // ─── RangeMatch: edge cases ─────────────────────────────────────────

    [Fact]
    public async Task ScoreAsync_RangeMatch_ZeroRequiredYears_ReturnsFullScore()
    {
        // Arrange — 0 required years should return 100 (no experience needed)
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("Any Experience", "RangeMatch", 100,
                """{"min_years": 0, "skill_name": "C#"}""", category: "Experience")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            aiParsedContent: """{"skills": [{"name": "C#", "years": 0}]}""");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert
        result.OverallScore.Should().Be(100m);
    }

    [Fact]
    public async Task ScoreAsync_RangeMatch_NoAiParsedData_ReturnsZero()
    {
        // Arrange — applicant has no AI-parsed content, so detected years = 0
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("5 Years C#", "RangeMatch", 100,
                """{"min_years": 5, "skill_name": "C#"}""", category: "Experience")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            resumeText: "5 years of C# experience");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert — DetectYearsOfExperience falls back to 0 without ai_parsed_content
        result.OverallScore.Should().Be(0m);
    }

    // ─── SemanticSimilarity: edge cases ─────────────────────────────────

    [Fact]
    public async Task ScoreAsync_SemanticSimilarity_AllKeywordsMatch_Returns100()
    {
        // Arrange
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("Tech Stack", "SemanticSimilarity", 100,
                """{"keywords": ["C#", "Docker", "PostgreSQL"]}""")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            extractedSkills: """["C#", "Docker", "PostgreSQL", "Redis"]""");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert
        result.OverallScore.Should().Be(100m);
    }

    [Fact]
    public async Task ScoreAsync_SemanticSimilarity_NoKeywordsMatch_ReturnsZero()
    {
        // Arrange
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("Tech Stack", "SemanticSimilarity", 100,
                """{"keywords": ["Rust", "Go", "Elixir"]}""")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            extractedSkills: """["C#", "Java"]""");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert
        result.OverallScore.Should().Be(0m);
    }

    [Fact]
    public async Task ScoreAsync_SemanticSimilarity_EmptySearchText_ReturnsZero()
    {
        // Arrange — applicant has no data at all
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("Skills", "SemanticSimilarity", 100,
                """{"keywords": ["C#"]}""")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData();

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert
        result.OverallScore.Should().Be(0m);
    }

    // ─── Weighted average: verified math ────────────────────────────────

    [Fact]
    public async Task ScoreAsync_WeightedAverage_VerifiesExactMath()
    {
        // Arrange — weight 3 (score 100 for C#) + weight 1 (score 0 for missing PhD)
        // Expected: (100*3 + 0*1) / (3+1) = 300/4 = 75.00
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("C#", "ExactMatch", 3, """{"skill_name": "C#"}"""),
            CreateCriteria("PhD", "ExactMatch", 1, """{"degree_level": "PhD"}""", category: "Education")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            extractedSkills: """["C#"]""",
            resumeText: "BSc in Computer Science");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert
        result.OverallScore.Should().Be(75m);
        result.Breakdown[0].Score.Should().Be(100m);
        result.Breakdown[0].Weight.Should().Be(3m);
        result.Breakdown[1].Score.Should().Be(0m);
        result.Breakdown[1].Weight.Should().Be(1m);
    }

    [Fact]
    public async Task ScoreAsync_AllNullApplicantData_ProducesZeroScores()
    {
        // Arrange — applicant has no profile, no resume, no AI data
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("C#", "ExactMatch", 50, """{"skill_name": "C#"}"""),
            CreateCriteria("Docker", "SemanticSimilarity", 50, """{"keywords": ["Docker"]}""")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData();

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert — all criteria score 0, overall = 0
        result.OverallScore.Should().Be(0m);
        result.Breakdown.Should().AllSatisfy(b => b.Score.Should().Be(0m));
    }

    [Fact]
    public async Task ScoreAsync_BreakdownContainsReasoningAndScoreResult()
    {
        // Arrange
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("C# Skill", "ExactMatch", 100, """{"skill_name": "C#"}""")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            extractedSkills: """["C#"]""");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert — breakdown entry has CriterionId, Name, Category, Weight, Result, Reasoning
        CriterionScoreDto entry = result.Breakdown[0];
        entry.CriterionId.Should().NotBeEmpty();
        entry.CriterionName.Should().Be("C# Skill");
        entry.Category.Should().Be("Skill");
        entry.Result.Should().Be("MeetsRequirement");
        entry.Reasoning.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ScoreAsync_InvalidConfiguration_ReturnsZero()
    {
        // Arrange — malformed JSON in configuration
        List<CriteriaSnapshot> criteria =
        [
            CreateCriteria("Bad Config", "ExactMatch", 100, "not-valid-json")
        ];

        ApplicantDataSnapshot applicant = CreateApplicantData(
            extractedSkills: """["C#"]""");

        // Act
        ScoringResult result = await _engine.ScoreAsync(criteria, applicant, CancellationToken.None);

        // Assert — fails gracefully, doesn't throw
        result.OverallScore.Should().Be(0m);
    }
}

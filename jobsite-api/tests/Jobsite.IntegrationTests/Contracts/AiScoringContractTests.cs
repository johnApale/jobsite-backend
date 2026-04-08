using System.Text.Json;
using FluentAssertions;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Infrastructure.AiIntegration;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Jobsite.IntegrationTests.Contracts;

/// <summary>
/// Contract tests for <see cref="AiScoringClient"/> → POST /api/v1/ai/screening/evaluate.
/// Verifies request body shape, endpoint path, and response deserialization
/// over a real HTTP connection via WireMock.
/// </summary>
[Collection("AiServiceContract")]
public sealed class AiScoringContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly AiServiceContractFixture _fixture;

    public AiScoringContractTests(AiServiceContractFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private AiScoringClient CreateClient()
    {
        HttpClient httpClient = new() { BaseAddress = new Uri(_fixture.BaseUrl) };
        return new AiScoringClient(httpClient, Substitute.For<ILogger<AiScoringClient>>());
    }

    [Fact]
    public async Task EvaluateAsync_SendsCorrectRequestBody_WithSnakeCaseFields()
    {
        // Arrange
        Guid criterionId = Guid.NewGuid();
        List<CriteriaSnapshot> criteria =
        [
            new CriteriaSnapshot
            {
                Id = criterionId, Name = "C#", Category = "Skill",
                EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 100m,
                Configuration = """{"skill_name": "C#"}"""
            }
        ];
        ApplicantDataSnapshot applicant = new()
        {
            UserId = Guid.NewGuid(),
            ProfileSkills = """["C#", ".NET"]""",
            ResumeParsedText = "5 years C# experience",
            ResumeExtractedSkills = """["C#"]""",
            AiParsedContent = """{"skills": [{"name": "C#", "years": 5}]}"""
        };

        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/screening/evaluate").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                    "breakdown": [{
                        "criterion_id": "00000000-0000-0000-0000-000000000001",
                        "criterion_name": "C#",
                        "category": "Skill",
                        "weight": 100,
                        "score": 92.5,
                        "result": "MeetsRequirement",
                        "reasoning": "Strong C# background"
                    }],
                    "overall_score": 92.5
                }
                """));

        AiScoringClient client = CreateClient();

        // Act
        AiScoringResult? result = await client.EvaluateAsync(criteria, applicant, CancellationToken.None);

        // Assert — verify response deserialization
        result.Should().NotBeNull();
        result!.OverallScore.Should().Be(92.5m);
        result.Breakdown.Should().HaveCount(1);
        result.Breakdown[0].CriterionName.Should().Be("C#");
        result.Breakdown[0].Score.Should().Be(92.5m);
        result.Breakdown[0].Result.Should().Be("MeetsRequirement");

        // Assert — verify request was sent correctly
        WireMock.Server.WireMockServer server = _fixture.Server;
        server.LogEntries.Should().HaveCount(1);
        WireMock.Logging.ILogEntry entry = server.LogEntries.First();
        entry.RequestMessage.Method.Should().Be("POST");
        entry.RequestMessage.Path.Should().Be("/api/v1/ai/screening/evaluate");

        // Verify request body has snake_case fields
        string requestBody = entry.RequestMessage.Body!;
        JsonDocument doc = JsonDocument.Parse(requestBody);
        JsonElement root = doc.RootElement;
        root.TryGetProperty("criteria", out JsonElement criteriaElement).Should().BeTrue();
        root.TryGetProperty("applicant", out JsonElement applicantElement).Should().BeTrue();

        JsonElement firstCriterion = criteriaElement.EnumerateArray().First();
        firstCriterion.TryGetProperty("evaluation_method", out _).Should().BeTrue();
        firstCriterion.TryGetProperty("is_required", out _).Should().BeTrue();

        applicantElement.TryGetProperty("profile_skills", out _).Should().BeTrue();
        applicantElement.TryGetProperty("resume_parsed_text", out _).Should().BeTrue();
        applicantElement.TryGetProperty("resume_extracted_skills", out _).Should().BeTrue();
        applicantElement.TryGetProperty("ai_parsed_content", out _).Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_ServerReturns500_ReturnsNull()
    {
        // Arrange
        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/screening/evaluate").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500));

        AiScoringClient client = CreateClient();
        List<CriteriaSnapshot> criteria =
        [
            new CriteriaSnapshot
            {
                Id = Guid.NewGuid(), Name = "Python", Category = "Skill",
                EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 50m,
                Configuration = """{"skill_name": "Python"}"""
            }
        ];
        ApplicantDataSnapshot applicant = new() { UserId = Guid.NewGuid() };

        // Act
        AiScoringResult? result = await client.EvaluateAsync(criteria, applicant, CancellationToken.None);

        // Assert — graceful degradation
        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_EmptyBreakdown_DeserializesCorrectly()
    {
        // Arrange
        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/screening/evaluate").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"breakdown": [], "overall_score": 0}"""));

        AiScoringClient client = CreateClient();
        List<CriteriaSnapshot> criteria = [];
        ApplicantDataSnapshot applicant = new() { UserId = Guid.NewGuid() };

        // Act
        AiScoringResult? result = await client.EvaluateAsync(criteria, applicant, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.OverallScore.Should().Be(0m);
        result.Breakdown.Should().BeEmpty();
    }

    [Fact]
    public async Task EvaluateAsync_MalformedJson_ReturnsNull()
    {
        // Arrange
        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/screening/evaluate").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("not valid json at all"));

        AiScoringClient client = CreateClient();
        List<CriteriaSnapshot> criteria =
        [
            new CriteriaSnapshot
            {
                Id = Guid.NewGuid(), Name = "Java", Category = "Skill",
                EvaluationMethod = "ExactMatch", IsRequired = false, Weight = 30m,
                Configuration = """{"skill_name": "Java"}"""
            }
        ];
        ApplicantDataSnapshot applicant = new() { UserId = Guid.NewGuid() };

        // Act
        AiScoringResult? result = await client.EvaluateAsync(criteria, applicant, CancellationToken.None);

        // Assert — JsonException caught, returns null
        result.Should().BeNull();
    }
}

using System.Text.Json;
using FluentAssertions;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Infrastructure.AiIntegration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Jobsite.IntegrationTests.Contracts;

/// <summary>
/// Contract tests for <see cref="AiQuestionSuggesterClient"/> → POST /api/v1/ai/assessment/suggest.
/// Verifies request body shape, endpoint path, and response deserialization
/// over a real HTTP connection via WireMock.
/// </summary>
[Collection("AiServiceContract")]
public sealed class AiQuestionSuggesterContractTests
{
    private readonly AiServiceContractFixture _fixture;

    public AiQuestionSuggesterContractTests(AiServiceContractFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private AiQuestionSuggesterClient CreateClient()
    {
        HttpClient httpClient = new() { BaseAddress = new Uri(_fixture.BaseUrl) };
        return new AiQuestionSuggesterClient(httpClient, Substitute.For<ILogger<AiQuestionSuggesterClient>>());
    }

    [Fact]
    public async Task SuggestAsync_SendsCorrectRequestBody_WithCriteriaContext()
    {
        // Arrange
        string jobDescription = "Build scalable APIs with .NET and deploy to Azure.";
        List<CriteriaResponse> criteria =
        [
            new CriteriaResponse
            {
                Id = Guid.NewGuid(), JobPostingId = Guid.NewGuid(),
                Name = "C# Proficiency", Category = "Skill",
                EvaluationMethod = "ExactMatch", IsRequired = true,
                Weight = 40m, Configuration = """{"skill_name": "C#"}""",
                DisplayOrder = 1,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            }
        ];

        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/assessment/suggest").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                [
                    {
                        "question_text": "Describe a recent project where you used C# to build a REST API.",
                        "question_type": "FreeText",
                        "timing": "AtApplication",
                        "is_required": true,
                        "weight": 25.0,
                        "expected_answer": null,
                        "options": null
                    },
                    {
                        "question_text": "Which Azure services have you deployed .NET applications to?",
                        "question_type": "MultipleChoice",
                        "timing": "AtApplication",
                        "is_required": false,
                        "weight": 15.0,
                        "expected_answer": "{\"correct_options\": [0, 1]}",
                        "options": "[\"App Service\", \"AKS\", \"VM\", \"None\"]"
                    }
                ]
                """));

        AiQuestionSuggesterClient client = CreateClient();

        // Act
        List<AiQuestionSuggestion>? result = await client.SuggestAsync(
            jobDescription, criteria, CancellationToken.None);

        // Assert — verify response deserialization
        result.Should().NotBeNull();
        result.Should().HaveCount(2);

        result![0].QuestionText.Should().Contain("C#");
        result[0].QuestionType.Should().Be("FreeText");
        result[0].Timing.Should().Be("AtApplication");
        result[0].IsRequired.Should().BeTrue();
        result[0].Weight.Should().Be(25.0m);
        result[0].ExpectedAnswer.Should().BeNull();

        result[1].QuestionType.Should().Be("MultipleChoice");
        result[1].Options.Should().NotBeNull();
        result[1].ExpectedAnswer.Should().Contain("correct_options");

        // Assert — verify request body structure
        WireMock.Logging.ILogEntry entry = _fixture.Server.LogEntries.First();
        entry.RequestMessage.Method.Should().Be("POST");
        entry.RequestMessage.Path.Should().Be("/api/v1/ai/assessment/suggest");

        string requestBody = entry.RequestMessage.Body!;
        JsonDocument doc = JsonDocument.Parse(requestBody);
        JsonElement root = doc.RootElement;
        root.TryGetProperty("job_description", out JsonElement descElement).Should().BeTrue();
        root.TryGetProperty("criteria", out JsonElement criteriaElement).Should().BeTrue();
        descElement.GetString().Should().Be(jobDescription);
        criteriaElement.GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task SuggestAsync_EmptyList_DeserializesCorrectly()
    {
        // Arrange
        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/assessment/suggest").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("[]"));

        AiQuestionSuggesterClient client = CreateClient();

        // Act
        List<AiQuestionSuggestion>? result = await client.SuggestAsync(
            "Any", [], CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SuggestAsync_ServerReturns500_ReturnsNull()
    {
        // Arrange
        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/assessment/suggest").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500));

        AiQuestionSuggesterClient client = CreateClient();

        // Act
        List<AiQuestionSuggestion>? result = await client.SuggestAsync(
            "Job", [], CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SuggestAsync_MalformedJson_ReturnsNull()
    {
        // Arrange
        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/assessment/suggest").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("bad json"));

        AiQuestionSuggesterClient client = CreateClient();

        // Act
        List<AiQuestionSuggestion>? result = await client.SuggestAsync(
            "Job", [], CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}

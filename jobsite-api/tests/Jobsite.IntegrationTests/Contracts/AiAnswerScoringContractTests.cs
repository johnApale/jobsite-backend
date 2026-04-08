using System.Text.Json;
using FluentAssertions;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Infrastructure.AiIntegration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Jobsite.IntegrationTests.Contracts;

/// <summary>
/// Contract tests for <see cref="AiAnswerScoringClient"/> → POST /api/v1/ai/screening/score-answers.
/// Verifies request body shape, endpoint path, and response deserialization
/// over a real HTTP connection via WireMock.
/// </summary>
[Collection("AiServiceContract")]
public sealed class AiAnswerScoringContractTests
{
    private readonly AiServiceContractFixture _fixture;

    public AiAnswerScoringContractTests(AiServiceContractFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private AiAnswerScoringClient CreateClient()
    {
        HttpClient httpClient = new() { BaseAddress = new Uri(_fixture.BaseUrl) };
        return new AiAnswerScoringClient(httpClient, Substitute.For<ILogger<AiAnswerScoringClient>>());
    }

    [Fact]
    public async Task ScoreAnswersAsync_SendsCorrectRequestBody_WithSnakeCaseFields()
    {
        // Arrange
        Guid questionId = Guid.NewGuid();
        List<AnswerScoringRequest> answers =
        [
            new AnswerScoringRequest
            {
                QuestionId = questionId,
                QuestionText = "Describe your .NET experience",
                ResponseText = "Built microservices with .NET 8 for 3 years",
                ScoringGuidance = "Look for hands-on experience with modern .NET",
                KeyTopics = ["microservices", ".NET", "APIs"]
            }
        ];

        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/screening/score-answers").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody($$"""
                [
                    {
                        "question_id": "{{questionId}}",
                        "score": 88.0,
                        "result": "MeetsRequirement",
                        "reasoning": "Demonstrates strong .NET experience with microservices"
                    }
                ]
                """));

        AiAnswerScoringClient client = CreateClient();

        // Act
        List<AnswerScore>? result = await client.ScoreAnswersAsync(answers, CancellationToken.None);

        // Assert — verify response deserialization
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].QuestionId.Should().Be(questionId);
        result[0].Score.Should().Be(88.0m);
        result[0].Result.Should().Be("MeetsRequirement");
        result[0].Reasoning.Should().Be("Demonstrates strong .NET experience with microservices");

        // Assert — verify request body structure
        WireMock.Logging.ILogEntry entry = _fixture.Server.LogEntries.First();
        entry.RequestMessage.Method.Should().Be("POST");
        entry.RequestMessage.Path.Should().Be("/api/v1/ai/screening/score-answers");

        string requestBody = entry.RequestMessage.Body!;
        JsonDocument doc = JsonDocument.Parse(requestBody);
        JsonElement root = doc.RootElement;
        root.TryGetProperty("answers", out JsonElement answersElement).Should().BeTrue();

        JsonElement firstAnswer = answersElement.EnumerateArray().First();
        firstAnswer.TryGetProperty("question_id", out _).Should().BeTrue();
        firstAnswer.TryGetProperty("question_text", out _).Should().BeTrue();
        firstAnswer.TryGetProperty("response_text", out _).Should().BeTrue();
        firstAnswer.TryGetProperty("scoring_guidance", out _).Should().BeTrue();
        firstAnswer.TryGetProperty("key_topics", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ScoreAnswersAsync_MultipleAnswers_DeserializesAll()
    {
        // Arrange
        Guid q1Id = Guid.NewGuid();
        Guid q2Id = Guid.NewGuid();
        List<AnswerScoringRequest> answers =
        [
            new AnswerScoringRequest
            {
                QuestionId = q1Id, QuestionText = "Q1",
                ResponseText = "Answer 1"
            },
            new AnswerScoringRequest
            {
                QuestionId = q2Id, QuestionText = "Q2",
                ResponseText = "Answer 2"
            }
        ];

        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/screening/score-answers").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody($$"""
                [
                    {"question_id": "{{q1Id}}", "score": 70.0, "result": "PartialMatch", "reasoning": "Adequate"},
                    {"question_id": "{{q2Id}}", "score": 95.0, "result": "MeetsRequirement", "reasoning": "Excellent"}
                ]
                """));

        AiAnswerScoringClient client = CreateClient();

        // Act
        List<AnswerScore>? result = await client.ScoreAnswersAsync(answers, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        result![0].Score.Should().Be(70.0m);
        result[1].Score.Should().Be(95.0m);
    }

    [Fact]
    public async Task ScoreAnswersAsync_ServerReturns500_ReturnsNull()
    {
        // Arrange
        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/screening/score-answers").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500));

        AiAnswerScoringClient client = CreateClient();
        List<AnswerScoringRequest> answers =
        [
            new AnswerScoringRequest
            {
                QuestionId = Guid.NewGuid(), QuestionText = "Q",
                ResponseText = "A"
            }
        ];

        // Act
        List<AnswerScore>? result = await client.ScoreAnswersAsync(answers, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ScoreAnswersAsync_MalformedJson_ReturnsNull()
    {
        // Arrange
        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/screening/score-answers").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{broken json"));

        AiAnswerScoringClient client = CreateClient();
        List<AnswerScoringRequest> answers =
        [
            new AnswerScoringRequest
            {
                QuestionId = Guid.NewGuid(), QuestionText = "Q",
                ResponseText = "A"
            }
        ];

        // Act
        List<AnswerScore>? result = await client.ScoreAnswersAsync(answers, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}

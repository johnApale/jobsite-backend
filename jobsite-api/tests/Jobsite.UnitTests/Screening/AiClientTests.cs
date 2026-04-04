using System.Net;
using System.Text.Json;
using FluentAssertions;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Infrastructure.AiIntegration;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Screening;

public sealed class AiScoringClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private static (AiScoringClient client, MockHttpMessageHandler handler) CreateClient()
    {
        MockHttpMessageHandler handler = new();
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:8000") };
        AiScoringClient client = new(httpClient, Substitute.For<ILogger<AiScoringClient>>());
        return (client, handler);
    }

    private static List<CriteriaSnapshot> SampleCriteria() =>
    [
        new CriteriaSnapshot
        {
            Id = Guid.NewGuid(), Name = "C#", Category = "Skill",
            EvaluationMethod = "ExactMatch", IsRequired = true, Weight = 100,
            Configuration = """{"skill_name": "C#"}"""
        }
    ];

    private static ApplicantDataSnapshot SampleApplicant() =>
        new() { UserId = Guid.NewGuid(), ResumeExtractedSkills = """["C#"]""" };

    [Fact]
    public async Task EvaluateAsync_SuccessResponse_DeserializesResult()
    {
        // Arrange
        (AiScoringClient client, MockHttpMessageHandler handler) = CreateClient();
        AiScoringResult expected = new()
        {
            OverallScore = 85m,
            Breakdown =
            [
                new CriterionScoreDto
                {
                    CriterionId = Guid.NewGuid(), CriterionName = "C#",
                    Category = "Skill", Weight = 100, Score = 85m,
                    Result = "MeetsRequirement", Reasoning = "Strong C# skills"
                }
            ]
        };
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(expected, JsonOptions),
                System.Text.Encoding.UTF8, "application/json")
        };

        // Act
        AiScoringResult? result = await client.EvaluateAsync(
            SampleCriteria(), SampleApplicant(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.OverallScore.Should().Be(85m);
        result.Breakdown.Should().HaveCount(1);
    }

    [Fact]
    public async Task EvaluateAsync_ServerError_ReturnsNull()
    {
        // Arrange
        (AiScoringClient client, MockHttpMessageHandler handler) = CreateClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        // Act
        AiScoringResult? result = await client.EvaluateAsync(
            SampleCriteria(), SampleApplicant(), CancellationToken.None);

        // Assert — graceful degradation, no exception
        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_NetworkError_ReturnsNull()
    {
        // Arrange
        (AiScoringClient client, MockHttpMessageHandler handler) = CreateClient();
        handler.ExceptionToThrow = new HttpRequestException("Connection refused");

        // Act
        AiScoringResult? result = await client.EvaluateAsync(
            SampleCriteria(), SampleApplicant(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}

public sealed class AiAnswerScoringClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private static (AiAnswerScoringClient client, MockHttpMessageHandler handler) CreateClient()
    {
        MockHttpMessageHandler handler = new();
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:8000") };
        AiAnswerScoringClient client = new(httpClient, Substitute.For<ILogger<AiAnswerScoringClient>>());
        return (client, handler);
    }

    private static List<AnswerScoringRequest> SampleAnswers() =>
    [
        new AnswerScoringRequest
        {
            QuestionId = Guid.NewGuid(),
            QuestionText = "Describe your C# experience",
            ResponseText = "5 years building .NET APIs"
        }
    ];

    [Fact]
    public async Task ScoreAnswersAsync_SuccessResponse_DeserializesScores()
    {
        // Arrange
        (AiAnswerScoringClient client, MockHttpMessageHandler handler) = CreateClient();
        Guid questionId = SampleAnswers()[0].QuestionId;
        List<AnswerScore> expected =
        [
            new AnswerScore
            {
                QuestionId = questionId, Score = 90m,
                Result = "MeetsRequirement", Reasoning = "Excellent experience"
            }
        ];
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(expected, JsonOptions),
                System.Text.Encoding.UTF8, "application/json")
        };

        // Act
        List<AnswerScore>? result = await client.ScoreAnswersAsync(
            SampleAnswers(), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].Score.Should().Be(90m);
    }

    [Fact]
    public async Task ScoreAnswersAsync_ServerError_ReturnsNull()
    {
        // Arrange
        (AiAnswerScoringClient client, MockHttpMessageHandler handler) = CreateClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        // Act
        List<AnswerScore>? result = await client.ScoreAnswersAsync(
            SampleAnswers(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ScoreAnswersAsync_NetworkError_ReturnsNull()
    {
        // Arrange
        (AiAnswerScoringClient client, MockHttpMessageHandler handler) = CreateClient();
        handler.ExceptionToThrow = new HttpRequestException("Connection refused");

        // Act
        List<AnswerScore>? result = await client.ScoreAnswersAsync(
            SampleAnswers(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}

public sealed class AiCandidateFeedbackClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private static (AiCandidateFeedbackClient client, MockHttpMessageHandler handler) CreateClient()
    {
        MockHttpMessageHandler handler = new();
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:8000") };
        AiCandidateFeedbackClient client = new(httpClient, Substitute.For<ILogger<AiCandidateFeedbackClient>>());
        return (client, handler);
    }

    [Fact]
    public async Task GenerateFeedbackAsync_SuccessResponse_ReturnsFeedbackString()
    {
        // Arrange
        (AiCandidateFeedbackClient client, MockHttpMessageHandler handler) = CreateClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"feedback": "Your profile shows strong skills in C# and .NET."}""",
                System.Text.Encoding.UTF8, "application/json")
        };

        // Act
        string? result = await client.GenerateFeedbackAsync(
            """[{"score": 85}]""", 85m, "Detailed", CancellationToken.None);

        // Assert
        result.Should().Be("Your profile shows strong skills in C# and .NET.");
    }

    [Fact]
    public async Task GenerateFeedbackAsync_ServerError_ReturnsNull()
    {
        // Arrange
        (AiCandidateFeedbackClient client, MockHttpMessageHandler handler) = CreateClient();
        handler.ResponseToReturn = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        // Act
        string? result = await client.GenerateFeedbackAsync(
            """[{"score": 85}]""", 85m, "Detailed", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateFeedbackAsync_NetworkError_ReturnsNull()
    {
        // Arrange
        (AiCandidateFeedbackClient client, MockHttpMessageHandler handler) = CreateClient();
        handler.ExceptionToThrow = new HttpRequestException("Connection refused");

        // Act
        string? result = await client.GenerateFeedbackAsync(
            """[{"score": 85}]""", 85m, "Summary", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}

/// <summary>
/// Testable HttpMessageHandler that returns a configured response or throws a configured exception.
/// </summary>
public sealed class MockHttpMessageHandler : HttpMessageHandler
{
    public HttpResponseMessage? ResponseToReturn { get; set; }
    public Exception? ExceptionToThrow { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (ExceptionToThrow is not null)
            throw ExceptionToThrow;

        return Task.FromResult(ResponseToReturn ?? new HttpResponseMessage(HttpStatusCode.OK));
    }
}

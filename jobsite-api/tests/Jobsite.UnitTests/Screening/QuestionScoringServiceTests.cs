using FluentAssertions;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Application.Services;
using Jobsite.Modules.Screening.Domain.Constants;
using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Screening;

public sealed class QuestionScoringServiceTests
{
    private readonly IAiAnswerScoringClient _aiClient = Substitute.For<IAiAnswerScoringClient>();
    private readonly QuestionScoringService _service;

    public QuestionScoringServiceTests()
    {
        _service = new QuestionScoringService(
            _aiClient,
            Substitute.For<ILogger<QuestionScoringService>>());
    }

    private static ScreeningQuestionResponse CreateResponse(Guid questionId,
        string? responseText = null, string? responseData = null)
    {
        return new ScreeningQuestionResponse
        {
            Id = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            QuestionId = questionId,
            ResponseText = responseText,
            ResponseData = responseData,
            SubmittedAt = DateTime.UtcNow
        };
    }

    private static QuestionSnapshot CreateQuestion(Guid id, string type,
        string? expectedAnswer = null, string? options = null)
    {
        return new QuestionSnapshot
        {
            Id = id,
            QuestionText = "Test question",
            QuestionType = type,
            Timing = "AtApplication",
            IsRequired = true,
            Weight = 10,
            ExpectedAnswer = expectedAnswer,
            Options = options
        };
    }

    [Fact]
    public async Task ScoreResponsesAsync_YesNo_CorrectAnswer_Returns100()
    {
        // Arrange
        Guid questionId = Guid.NewGuid();
        List<ScreeningQuestionResponse> responses =
        [
            CreateResponse(questionId, responseData: """{"answer": true}""")
        ];
        List<QuestionSnapshot> questions =
        [
            CreateQuestion(questionId, "YesNo", expectedAnswer: """{"correct": true}""")
        ];

        // Act
        List<AnswerScore> scores = await _service.ScoreResponsesAsync(responses, questions, CancellationToken.None);

        // Assert
        scores.Should().HaveCount(1);
        scores[0].Score.Should().Be(100m);
        scores[0].Result.Should().Be(ScoreResult.MeetsRequirement);
    }

    [Fact]
    public async Task ScoreResponsesAsync_YesNo_WrongAnswer_Returns0()
    {
        // Arrange
        Guid questionId = Guid.NewGuid();
        List<ScreeningQuestionResponse> responses =
        [
            CreateResponse(questionId, responseData: """{"answer": false}""")
        ];
        List<QuestionSnapshot> questions =
        [
            CreateQuestion(questionId, "YesNo", expectedAnswer: """{"correct": true}""")
        ];

        // Act
        List<AnswerScore> scores = await _service.ScoreResponsesAsync(responses, questions, CancellationToken.None);

        // Assert
        scores.Should().HaveCount(1);
        scores[0].Score.Should().Be(0m);
        scores[0].Result.Should().Be(ScoreResult.Missing);
    }

    [Fact]
    public async Task ScoreResponsesAsync_MultipleChoice_AllCorrect_Returns100()
    {
        // Arrange
        Guid questionId = Guid.NewGuid();
        List<ScreeningQuestionResponse> responses =
        [
            CreateResponse(questionId,
                responseData: """{"selected_options": [0, 2]}""")
        ];
        List<QuestionSnapshot> questions =
        [
            CreateQuestion(questionId, "MultipleChoice",
                expectedAnswer: """{"correct_options": [0, 2], "partial_credit": false}""")
        ];

        // Act
        List<AnswerScore> scores = await _service.ScoreResponsesAsync(responses, questions, CancellationToken.None);

        // Assert
        scores.Should().HaveCount(1);
        scores[0].Score.Should().Be(100m);
    }

    [Fact]
    public async Task ScoreResponsesAsync_MultipleChoice_PartialCredit_ReturnsProportional()
    {
        // Arrange — selected 1 of 2 correct, partial credit enabled
        Guid questionId = Guid.NewGuid();
        List<ScreeningQuestionResponse> responses =
        [
            CreateResponse(questionId,
                responseData: """{"selected_options": [0]}""")
        ];
        List<QuestionSnapshot> questions =
        [
            CreateQuestion(questionId, "MultipleChoice",
                expectedAnswer: """{"correct_options": [0, 2], "partial_credit": true}""")
        ];

        // Act
        List<AnswerScore> scores = await _service.ScoreResponsesAsync(responses, questions, CancellationToken.None);

        // Assert
        scores.Should().HaveCount(1);
        scores[0].Score.Should().Be(50m); // 1 out of 2 correct
    }

    [Fact]
    public async Task ScoreResponsesAsync_FreeText_DelegatesToAiClient()
    {
        // Arrange
        Guid questionId = Guid.NewGuid();
        List<ScreeningQuestionResponse> responses =
        [
            CreateResponse(questionId, responseText: "My experience with C# spans 5 years")
        ];
        List<QuestionSnapshot> questions =
        [
            CreateQuestion(questionId, "FreeText",
                expectedAnswer: """{"scoring_guidance": "Look for relevant experience", "key_topics": ["C#"]}""")
        ];

        List<AnswerScore> aiScores =
        [
            new AnswerScore
            {
                QuestionId = questionId,
                Score = 85m,
                Result = ScoreResult.MeetsRequirement,
                Reasoning = "Strong experience in C#"
            }
        ];

        _aiClient.ScoreAnswersAsync(Arg.Any<List<AnswerScoringRequest>>(), Arg.Any<CancellationToken>())
            .Returns(aiScores);

        // Act
        List<AnswerScore> scores = await _service.ScoreResponsesAsync(responses, questions, CancellationToken.None);

        // Assert
        scores.Should().HaveCount(1);
        scores[0].Score.Should().Be(85m);
        await _aiClient.Received(1).ScoreAnswersAsync(
            Arg.Is<List<AnswerScoringRequest>>(r => r.Count == 1 && r[0].QuestionId == questionId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScoreResponsesAsync_FreeText_AiUnavailable_ReturnsEmpty()
    {
        // Arrange
        Guid questionId = Guid.NewGuid();
        List<ScreeningQuestionResponse> responses =
        [
            CreateResponse(questionId, responseText: "My answer")
        ];
        List<QuestionSnapshot> questions =
        [
            CreateQuestion(questionId, "FreeText")
        ];

        _aiClient.ScoreAnswersAsync(Arg.Any<List<AnswerScoringRequest>>(), Arg.Any<CancellationToken>())
            .Returns((List<AnswerScore>?)null);

        // Act
        List<AnswerScore> scores = await _service.ScoreResponsesAsync(responses, questions, CancellationToken.None);

        // Assert — FreeText with no AI result returns no score entries
        scores.Should().BeEmpty();
    }

    [Fact]
    public async Task ScoreResponsesAsync_UnknownQuestion_IsSkipped()
    {
        // Arrange — response references a question not in the list
        Guid unknownQuestionId = Guid.NewGuid();
        List<ScreeningQuestionResponse> responses =
        [
            CreateResponse(unknownQuestionId, responseData: """{"answer": true}""")
        ];
        List<QuestionSnapshot> questions = []; // empty — no matching question

        // Act
        List<AnswerScore> scores = await _service.ScoreResponsesAsync(responses, questions, CancellationToken.None);

        // Assert
        scores.Should().BeEmpty();
    }

    [Fact]
    public async Task ScoreResponsesAsync_YesNo_MissingExpectedAnswer_ReturnsMissingScore()
    {
        // Arrange
        Guid questionId = Guid.NewGuid();
        List<ScreeningQuestionResponse> responses =
        [
            CreateResponse(questionId, responseData: """{"answer": true}""")
        ];
        List<QuestionSnapshot> questions =
        [
            CreateQuestion(questionId, "YesNo", expectedAnswer: null)
        ];

        // Act
        List<AnswerScore> scores = await _service.ScoreResponsesAsync(responses, questions, CancellationToken.None);

        // Assert
        scores.Should().HaveCount(1);
        scores[0].Score.Should().Be(0m);
    }
}

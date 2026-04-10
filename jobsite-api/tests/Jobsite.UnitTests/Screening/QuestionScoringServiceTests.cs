using FluentAssertions;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Application.Services;
using Jobsite.Modules.Screening.Domain.Constants;
using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Screening;

public sealed class QuestionScoringServiceTests
{
    private readonly QuestionScoringService _service;

    public QuestionScoringServiceTests()
    {
        _service = new QuestionScoringService(
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
    public void ScoreResponses_YesNo_CorrectAnswer_Returns100()
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
        (List<AnswerScore> scores, List<AnswerScoringRequest> pendingAi) = _service.ScoreResponses(responses, questions);

        // Assert
        scores.Should().HaveCount(1);
        scores[0].Score.Should().Be(100m);
        scores[0].Result.Should().Be(ScoreResult.MeetsRequirement);
        pendingAi.Should().BeEmpty();
    }

    [Fact]
    public void ScoreResponses_YesNo_WrongAnswer_Returns0()
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
        (List<AnswerScore> scores, List<AnswerScoringRequest> _) = _service.ScoreResponses(responses, questions);

        // Assert
        scores.Should().HaveCount(1);
        scores[0].Score.Should().Be(0m);
        scores[0].Result.Should().Be(ScoreResult.Missing);
    }

    [Fact]
    public void ScoreResponses_MultipleChoice_AllCorrect_Returns100()
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
        (List<AnswerScore> scores, List<AnswerScoringRequest> _) = _service.ScoreResponses(responses, questions);

        // Assert
        scores.Should().HaveCount(1);
        scores[0].Score.Should().Be(100m);
    }

    [Fact]
    public void ScoreResponses_MultipleChoice_PartialCredit_ReturnsProportional()
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
        (List<AnswerScore> scores, List<AnswerScoringRequest> _) = _service.ScoreResponses(responses, questions);

        // Assert
        scores.Should().HaveCount(1);
        scores[0].Score.Should().Be(50m); // 1 out of 2 correct
    }

    [Fact]
    public void ScoreResponses_FreeText_ReturnsPendingAiRequest()
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

        // Act
        (List<AnswerScore> scores, List<AnswerScoringRequest> pendingAi) =
            _service.ScoreResponses(responses, questions);

        // Assert — FreeText is not scored deterministically, but queued for async AI
        scores.Should().BeEmpty();
        pendingAi.Should().HaveCount(1);
        pendingAi[0].QuestionId.Should().Be(questionId);
        pendingAi[0].ResponseText.Should().Be("My experience with C# spans 5 years");
        pendingAi[0].ScoringGuidance.Should().Be("Look for relevant experience");
    }

    [Fact]
    public void ScoreResponses_FreeText_NoPendingWhenNoFreeText()
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
        (List<AnswerScore> scores, List<AnswerScoringRequest> pendingAi) =
            _service.ScoreResponses(responses, questions);

        // Assert — no FreeText questions = no pending AI requests
        scores.Should().HaveCount(1);
        pendingAi.Should().BeEmpty();
    }

    [Fact]
    public void ScoreResponses_UnknownQuestion_IsSkipped()
    {
        // Arrange — response references a question not in the list
        Guid unknownQuestionId = Guid.NewGuid();
        List<ScreeningQuestionResponse> responses =
        [
            CreateResponse(unknownQuestionId, responseData: """{"answer": true}""")
        ];
        List<QuestionSnapshot> questions = []; // empty — no matching question

        // Act
        (List<AnswerScore> scores, List<AnswerScoringRequest> pendingAi) =
            _service.ScoreResponses(responses, questions);

        // Assert
        scores.Should().BeEmpty();
        pendingAi.Should().BeEmpty();
    }

    [Fact]
    public void ScoreResponses_YesNo_MissingExpectedAnswer_ReturnsMissingScore()
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
        (List<AnswerScore> scores, List<AnswerScoringRequest> _) = _service.ScoreResponses(responses, questions);

        // Assert
        scores.Should().HaveCount(1);
        scores[0].Score.Should().Be(0m);
    }

    // ─── MultipleChoice: partial credit with incorrect selections ────────

    [Fact]
    public void ScoreResponses_MultipleChoice_PartialCreditWithIncorrect_PenalizesScore()
    {
        // Arrange — selected 2 correct + 1 incorrect out of 3 correct, with partial credit
        // Formula: max(0, (correctCount - incorrectCount) / totalCorrect * 100) = max(0, (2-1)/3*100) = 33.33
        Guid questionId = Guid.NewGuid();
        List<ScreeningQuestionResponse> responses =
        [
            CreateResponse(questionId,
                responseData: """{"selected_options": [0, 1, 3]}""")
        ];
        List<QuestionSnapshot> questions =
        [
            CreateQuestion(questionId, "MultipleChoice",
                expectedAnswer: """{"correct_options": [0, 1, 2], "partial_credit": true}""")
        ];

        // Act
        (List<AnswerScore> scores, List<AnswerScoringRequest> _) = _service.ScoreResponses(responses, questions);

        // Assert — 2 correct, 1 incorrect → (2-1)/3 * 100 = 33.33
        scores.Should().HaveCount(1);
        scores[0].Score.Should().Be(33.33m);
    }

    [Fact]
    public void ScoreResponses_MultipleChoice_AllOrNothing_WrongAnswer_ReturnsZero()
    {
        // Arrange — partial_credit=false, one option missing → 0
        Guid questionId = Guid.NewGuid();
        List<ScreeningQuestionResponse> responses =
        [
            CreateResponse(questionId,
                responseData: """{"selected_options": [0]}""")
        ];
        List<QuestionSnapshot> questions =
        [
            CreateQuestion(questionId, "MultipleChoice",
                expectedAnswer: """{"correct_options": [0, 2], "partial_credit": false}""")
        ];

        // Act
        (List<AnswerScore> scores, List<AnswerScoringRequest> _) = _service.ScoreResponses(responses, questions);

        // Assert — partial_credit=false, not exact match → 0
        scores.Should().HaveCount(1);
        scores[0].Score.Should().Be(0m);
    }

    [Fact]
    public void ScoreResponses_MultipleChoice_NoneSelected_ReturnsZero()
    {
        // Arrange
        Guid questionId = Guid.NewGuid();
        List<ScreeningQuestionResponse> responses =
        [
            CreateResponse(questionId,
                responseData: """{"selected_options": []}""")
        ];
        List<QuestionSnapshot> questions =
        [
            CreateQuestion(questionId, "MultipleChoice",
                expectedAnswer: """{"correct_options": [0, 1], "partial_credit": true}""")
        ];

        // Act
        (List<AnswerScore> scores, List<AnswerScoringRequest> _) = _service.ScoreResponses(responses, questions);

        // Assert
        scores.Should().HaveCount(1);
        scores[0].Score.Should().Be(0m);
    }

    // ─── FreeText: AI applies scores to response entities ───────────────

    [Fact]
    public void ScoreResponses_FreeText_BuildsRequestWithGuidanceAndTopics()
    {
        // Arrange — verify that FreeText builds a pending AI request with scoring guidance
        Guid questionId = Guid.NewGuid();
        ScreeningQuestionResponse response = CreateResponse(questionId,
            responseText: "I have 5 years of C# experience");
        List<ScreeningQuestionResponse> responses = [response];
        List<QuestionSnapshot> questions =
        [
            CreateQuestion(questionId, "FreeText",
                expectedAnswer: """{"scoring_guidance": "Look for years", "key_topics": ["C#"]}""")
        ];

        // Act
        (List<AnswerScore> scores, List<AnswerScoringRequest> pendingAi) =
            _service.ScoreResponses(responses, questions);

        // Assert — FreeText queued for async AI scoring
        scores.Should().BeEmpty();
        pendingAi.Should().HaveCount(1);
        pendingAi[0].QuestionId.Should().Be(questionId);
        pendingAi[0].ScoringGuidance.Should().Be("Look for years");
        pendingAi[0].KeyTopics.Should().Contain("C#");
    }

    // ─── Deterministic scoring also applies to response entities ────────

    [Fact]
    public void ScoreResponses_YesNo_AppliesScoreToResponseEntity()
    {
        // Arrange — verify that deterministic scoring also calls ApplyScore
        Guid questionId = Guid.NewGuid();
        ScreeningQuestionResponse response = CreateResponse(questionId,
            responseData: """{"answer": true}""");
        List<ScreeningQuestionResponse> responses = [response];
        List<QuestionSnapshot> questions =
        [
            CreateQuestion(questionId, "YesNo", expectedAnswer: """{"correct": true}""")
        ];

        // Act
        _service.ScoreResponses(responses, questions);

        // Assert — response entity has score fields set
        response.Score.Should().Be(100m);
        response.ScoreResult.Should().Be(ScoreResult.MeetsRequirement);
        response.ScoreReasoning.Should().Be("Correct answer");
        response.ScoredAt.Should().NotBeNull();
    }

    // ─── Mixed question types in single batch ───────────────────────────

    [Fact]
    public void ScoreResponses_MixedTypes_ScoresDeterministicAndQueuesFreeText()
    {
        // Arrange — YesNo + MC + FreeText in a single batch
        Guid yesNoId = Guid.NewGuid();
        Guid mcId = Guid.NewGuid();
        Guid freeTextId = Guid.NewGuid();

        List<ScreeningQuestionResponse> responses =
        [
            CreateResponse(yesNoId, responseData: """{"answer": true}"""),
            CreateResponse(mcId, responseData: """{"selected_options": [0, 2]}"""),
            CreateResponse(freeTextId, responseText: "My experience spans 5 years")
        ];

        List<QuestionSnapshot> questions =
        [
            CreateQuestion(yesNoId, "YesNo", expectedAnswer: """{"correct": true}"""),
            CreateQuestion(mcId, "MultipleChoice",
                expectedAnswer: """{"correct_options": [0, 2], "partial_credit": false}"""),
            CreateQuestion(freeTextId, "FreeText",
                expectedAnswer: """{"scoring_guidance": "Look for experience"}""")
        ];

        // Act
        (List<AnswerScore> scores, List<AnswerScoringRequest> pendingAi) =
            _service.ScoreResponses(responses, questions);

        // Assert — 2 deterministic scores (YesNo=100, MC=100), 1 pending AI request
        scores.Should().HaveCount(2);
        scores.Should().Contain(s => s.QuestionId == yesNoId && s.Score == 100m);
        scores.Should().Contain(s => s.QuestionId == mcId && s.Score == 100m);
        pendingAi.Should().HaveCount(1);
        pendingAi[0].QuestionId.Should().Be(freeTextId);
    }

    // ─── Malformed JSON ─────────────────────────────────────────────────

    [Fact]
    public void ScoreResponses_MultipleChoice_MalformedJson_ReturnsMissingScore()
    {
        // Arrange
        Guid questionId = Guid.NewGuid();
        List<ScreeningQuestionResponse> responses =
        [
            CreateResponse(questionId, responseData: "not-valid-json")
        ];
        List<QuestionSnapshot> questions =
        [
            CreateQuestion(questionId, "MultipleChoice",
                expectedAnswer: """{"correct_options": [0], "partial_credit": false}""")
        ];

        // Act
        (List<AnswerScore> scores, List<AnswerScoringRequest> _) = _service.ScoreResponses(responses, questions);

        // Assert — fails gracefully with 0 score
        scores.Should().HaveCount(1);
        scores[0].Score.Should().Be(0m);
        scores[0].Result.Should().Be(ScoreResult.Missing);
    }
}

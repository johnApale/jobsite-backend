using System.Text.Json;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Domain.Constants;
using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Screening.Application.Services;

/// <summary>
/// Scores screening question answers — MultipleChoice/YesNo deterministically.
/// FreeText answers are collected and returned as pending AI requests for broker publishing.
/// </summary>
public sealed class QuestionScoringService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<QuestionScoringService> _logger;

    public QuestionScoringService(
        ILogger<QuestionScoringService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Scores a list of question responses against their expected answers.
    /// Returns deterministic scores (MC/YN) and pending free-text requests for async AI scoring.
    /// </summary>
    public (List<AnswerScore> Scores, List<AnswerScoringRequest> PendingAiRequests) ScoreResponses(
        List<ScreeningQuestionResponse> responses,
        List<QuestionSnapshot> questions)
    {
        List<AnswerScore> scores = [];
        List<AnswerScoringRequest> freeTextRequests = [];

        foreach (ScreeningQuestionResponse response in responses)
        {
            QuestionSnapshot? question = questions.FirstOrDefault(q => q.Id == response.QuestionId);
            if (question is null)
                continue;

            switch (question.QuestionType)
            {
                case "MultipleChoice":
                    AnswerScore mcScore = ScoreMultipleChoice(response, question);
                    scores.Add(mcScore);
                    ApplyScore(response, mcScore);
                    break;

                case "YesNo":
                    AnswerScore ynScore = ScoreYesNo(response, question);
                    scores.Add(ynScore);
                    ApplyScore(response, ynScore);
                    break;

                case "FreeText":
                    freeTextRequests.Add(BuildFreeTextRequest(response, question));
                    break;
            }
        }

        if (freeTextRequests.Count > 0)
        {
            _logger.LogInformation(
                "{Count} FreeText answers queued for async AI scoring via broker",
                freeTextRequests.Count);
        }

        return (scores, freeTextRequests);
    }

    private static AnswerScore ScoreMultipleChoice(ScreeningQuestionResponse response, QuestionSnapshot question)
    {
        if (question.ExpectedAnswer is null || response.ResponseData is null)
            return CreateMissingScore(response.QuestionId, "No expected answer or response data");

        try
        {
            MultipleChoiceExpected? expected = JsonSerializer.Deserialize<MultipleChoiceExpected>(
                question.ExpectedAnswer, JsonOptions);
            MultipleChoiceResponse? actual = JsonSerializer.Deserialize<MultipleChoiceResponse>(
                response.ResponseData, JsonOptions);

            if (expected?.CorrectOptions is null || actual?.SelectedOptions is null)
                return CreateMissingScore(response.QuestionId, "Invalid expected answer or response format");

            HashSet<int> correctSet = [.. expected.CorrectOptions];
            HashSet<int> selectedSet = [.. actual.SelectedOptions];

            if (correctSet.Count == 0)
                return CreateMissingScore(response.QuestionId, "No correct options defined");

            int correctCount = selectedSet.Intersect(correctSet).Count();
            int incorrectCount = selectedSet.Except(correctSet).Count();

            decimal score;
            if (expected.PartialCredit)
            {
                score = incorrectCount > 0
                    ? Math.Max(0m, ((decimal)correctCount - incorrectCount) / correctSet.Count * 100m)
                    : (decimal)correctCount / correctSet.Count * 100m;
            }
            else
            {
                score = correctSet.SetEquals(selectedSet) ? 100m : 0m;
            }

            score = Math.Clamp(Math.Round(score, 2), 0m, 100m);

            return new AnswerScore
            {
                QuestionId = response.QuestionId,
                Score = score,
                Result = ScoreResult.FromScore(score),
                Reasoning = $"Selected {correctCount}/{correctSet.Count} correct options" +
                            (incorrectCount > 0 ? $", {incorrectCount} incorrect" : "")
            };
        }
        catch (JsonException)
        {
            return CreateMissingScore(response.QuestionId, "Failed to parse answer data");
        }
    }

    private static AnswerScore ScoreYesNo(ScreeningQuestionResponse response, QuestionSnapshot question)
    {
        if (question.ExpectedAnswer is null || response.ResponseData is null)
            return CreateMissingScore(response.QuestionId, "No expected answer or response data");

        try
        {
            YesNoExpected? expected = JsonSerializer.Deserialize<YesNoExpected>(
                question.ExpectedAnswer, JsonOptions);
            YesNoResponse? actual = JsonSerializer.Deserialize<YesNoResponse>(
                response.ResponseData, JsonOptions);

            if (expected is null || actual is null)
                return CreateMissingScore(response.QuestionId, "Invalid expected answer or response format");

            bool isCorrect = expected.Correct == actual.Answer;
            decimal score = isCorrect ? 100m : 0m;

            return new AnswerScore
            {
                QuestionId = response.QuestionId,
                Score = score,
                Result = ScoreResult.FromScore(score),
                Reasoning = isCorrect ? "Correct answer" : "Incorrect answer"
            };
        }
        catch (JsonException)
        {
            return CreateMissingScore(response.QuestionId, "Failed to parse answer data");
        }
    }

    private static AnswerScoringRequest BuildFreeTextRequest(
        ScreeningQuestionResponse response, QuestionSnapshot question)
    {
        string? scoringGuidance = null;
        List<string>? keyTopics = null;

        if (question.ExpectedAnswer is not null)
        {
            try
            {
                FreeTextExpected? expected = JsonSerializer.Deserialize<FreeTextExpected>(
                    question.ExpectedAnswer, JsonOptions);
                scoringGuidance = expected?.ScoringGuidance;
                keyTopics = expected?.KeyTopics;
            }
            catch (JsonException)
            {
                // Proceed without guidance
            }
        }

        return new AnswerScoringRequest
        {
            QuestionId = response.QuestionId,
            QuestionText = question.QuestionText,
            ResponseText = response.ResponseText ?? "",
            ScoringGuidance = scoringGuidance,
            KeyTopics = keyTopics
        };
    }

    private static void ApplyScore(ScreeningQuestionResponse response, AnswerScore score)
    {
        response.Score = score.Score;
        response.ScoreResult = score.Result;
        response.ScoreReasoning = score.Reasoning;
        response.ScoredAt = DateTime.UtcNow;
    }

    private static AnswerScore CreateMissingScore(Guid questionId, string reasoning) => new()
    {
        QuestionId = questionId,
        Score = 0m,
        Result = ScoreResult.Missing,
        Reasoning = reasoning
    };

    private sealed class MultipleChoiceExpected
    {
        public List<int>? CorrectOptions { get; set; }
        public bool PartialCredit { get; set; }
    }

    private sealed class MultipleChoiceResponse
    {
        public List<int>? SelectedOptions { get; set; }
    }

    private sealed class YesNoExpected
    {
        public bool Correct { get; set; }
    }

    private sealed class YesNoResponse
    {
        public bool Answer { get; set; }
    }

    private sealed class FreeTextExpected
    {
        public string? ScoringGuidance { get; set; }
        public List<string>? KeyTopics { get; set; }
    }
}

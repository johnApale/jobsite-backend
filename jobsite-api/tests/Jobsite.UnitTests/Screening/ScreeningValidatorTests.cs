using FluentAssertions;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Application.Validators;

namespace Jobsite.UnitTests.Screening;

public sealed class ScreeningValidatorTests
{
    // ── SubmitAssessmentRequestValidator ──────────────────────────────────

    [Fact]
    public void SubmitAssessmentRequestValidator_EmptyAnswers_Fails()
    {
        // Arrange
        SubmitAssessmentRequestValidator validator = new();
        SubmitAssessmentRequest request = new()
        {
            JobPostingId = Guid.NewGuid(),
            Answers = []
        };

        // Act
        FluentValidation.Results.ValidationResult result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Answers");
    }

    [Fact]
    public void SubmitAssessmentRequestValidator_AnswerWithEmptyQuestionId_Fails()
    {
        // Arrange
        SubmitAssessmentRequestValidator validator = new();
        SubmitAssessmentRequest request = new()
        {
            JobPostingId = Guid.NewGuid(),
            Answers =
            [
                new AssessmentAnswerDto { QuestionId = Guid.Empty, ResponseText = "answer" }
            ]
        };

        // Act
        FluentValidation.Results.ValidationResult result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void SubmitAssessmentRequestValidator_ValidRequest_Passes()
    {
        // Arrange
        SubmitAssessmentRequestValidator validator = new();
        SubmitAssessmentRequest request = new()
        {
            JobPostingId = Guid.NewGuid(),
            Answers =
            [
                new AssessmentAnswerDto { QuestionId = Guid.NewGuid(), ResponseText = "answer" }
            ]
        };

        // Act
        FluentValidation.Results.ValidationResult result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    // ── ManualReviewRequestValidator ─────────────────────────────────────

    [Fact]
    public void ManualReviewRequestValidator_EmptyOutcome_Fails()
    {
        // Arrange
        ManualReviewRequestValidator validator = new();
        ManualReviewRequest request = new()
        {
            Outcome = "",
            ReviewNotes = "notes"
        };

        // Act
        FluentValidation.Results.ValidationResult result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ManualReviewRequestValidator_InvalidOutcome_Fails()
    {
        // Arrange
        ManualReviewRequestValidator validator = new();
        ManualReviewRequest request = new()
        {
            Outcome = "AutoAdvanced",
            ReviewNotes = null
        };

        // Act
        FluentValidation.Results.ValidationResult result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ManualReviewRequestValidator_ValidManuallyAdvanced_Passes()
    {
        // Arrange
        ManualReviewRequestValidator validator = new();
        ManualReviewRequest request = new()
        {
            Outcome = "ManuallyAdvanced",
            ReviewNotes = "Candidate qualifies"
        };

        // Act
        FluentValidation.Results.ValidationResult result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ManualReviewRequestValidator_ReviewNotesTooLong_Fails()
    {
        // Arrange
        ManualReviewRequestValidator validator = new();
        ManualReviewRequest request = new()
        {
            Outcome = "ManuallyRejected",
            ReviewNotes = new string('x', 2001)
        };

        // Act
        FluentValidation.Results.ValidationResult result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ManualReviewRequestValidator_ReviewNotesExactly2000Chars_Passes()
    {
        // Arrange
        ManualReviewRequestValidator validator = new();
        ManualReviewRequest request = new()
        {
            Outcome = "ManuallyRejected",
            ReviewNotes = new string('x', 2000)
        };

        // Act
        FluentValidation.Results.ValidationResult result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ManualReviewRequestValidator_ManuallyRejected_Passes()
    {
        // Arrange
        ManualReviewRequestValidator validator = new();
        ManualReviewRequest request = new()
        {
            Outcome = "ManuallyRejected",
            ReviewNotes = null
        };

        // Act
        FluentValidation.Results.ValidationResult result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SubmitAssessmentRequestValidator_MultipleValidAnswers_Passes()
    {
        // Arrange
        SubmitAssessmentRequestValidator validator = new();
        SubmitAssessmentRequest request = new()
        {
            JobPostingId = Guid.NewGuid(),
            Answers =
            [
                new AssessmentAnswerDto { QuestionId = Guid.NewGuid(), ResponseText = "Answer 1" },
                new AssessmentAnswerDto { QuestionId = Guid.NewGuid(), ResponseData = """{"selected": ["a"]}""" },
                new AssessmentAnswerDto { QuestionId = Guid.NewGuid(), ResponseText = "Answer 3" }
            ]
        };

        // Act
        FluentValidation.Results.ValidationResult result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}

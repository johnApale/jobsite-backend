using FluentAssertions;
using FluentValidation.Results;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Validators;
using Jobsite.Modules.Recruitment.Domain.Constants;

namespace Jobsite.UnitTests.Recruitment;

public sealed class CreateQuestionRequestValidatorTests
{
    private readonly CreateQuestionRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        // Arrange
        CreateQuestionRequest request = TestData.CreateQuestionRequest();

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyQuestionText_Fails()
    {
        // Arrange
        CreateQuestionRequest request = TestData.CreateQuestionRequest(questionText: "");

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "QuestionText");
    }

    [Fact]
    public void Validate_InvalidQuestionType_Fails()
    {
        // Arrange
        CreateQuestionRequest request = TestData.CreateQuestionRequest(questionType: "EssayStyle");

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "QuestionType");
    }

    [Fact]
    public void Validate_InvalidTiming_Fails()
    {
        // Arrange
        CreateQuestionRequest request = TestData.CreateQuestionRequest(timing: "DuringInterview");

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Timing");
    }

    [Fact]
    public void Validate_WeightBelowZero_Fails()
    {
        // Arrange
        CreateQuestionRequest request = new()
        {
            QuestionText = "Test question?",
            QuestionType = QuestionType.YesNo,
            Timing = QuestionTiming.AtApplication,
            Weight = -1
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Weight");
    }

    [Fact]
    public void Validate_WeightAbove100_Fails()
    {
        // Arrange
        CreateQuestionRequest request = new()
        {
            QuestionText = "Test question?",
            QuestionType = QuestionType.YesNo,
            Timing = QuestionTiming.AtApplication,
            Weight = 101
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Weight");
    }

    [Fact]
    public void Validate_MultipleChoiceWithoutOptions_Fails()
    {
        // Arrange
        CreateQuestionRequest request = new()
        {
            QuestionText = "Which framework do you prefer?",
            QuestionType = QuestionType.MultipleChoice,
            Timing = QuestionTiming.AtApplication,
            Weight = 10,
            Options = null
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Options");
    }

    [Fact]
    public void Validate_MultipleChoiceWithOptions_Passes()
    {
        // Arrange
        CreateQuestionRequest request = new()
        {
            QuestionText = "Which framework do you prefer?",
            QuestionType = QuestionType.MultipleChoice,
            Timing = QuestionTiming.AtApplication,
            Weight = 10,
            Options = """["ASP.NET","Spring Boot","Django"]"""
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_YesNoWithoutOptions_Passes()
    {
        // Arrange
        CreateQuestionRequest request = new()
        {
            QuestionText = "Are you authorized to work?",
            QuestionType = QuestionType.YesNo,
            Timing = QuestionTiming.AtApplication,
            Weight = 10,
            Options = null
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NegativeDisplayOrder_Fails()
    {
        // Arrange
        CreateQuestionRequest request = new()
        {
            QuestionText = "Test question?",
            QuestionType = QuestionType.YesNo,
            Timing = QuestionTiming.AtApplication,
            Weight = 10,
            DisplayOrder = -1
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DisplayOrder");
    }

    [Fact]
    public void Validate_EmptyQuestionType_Fails()
    {
        // Arrange
        CreateQuestionRequest request = new()
        {
            QuestionText = "Test question?",
            QuestionType = "",
            Timing = QuestionTiming.AtApplication,
            Weight = 10
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "QuestionType");
    }
}

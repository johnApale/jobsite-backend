using FluentAssertions;
using FluentValidation.Results;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Validators;
using Jobsite.Modules.Recruitment.Domain.Constants;

namespace Jobsite.UnitTests.Recruitment;

public sealed class UpdateQuestionRequestValidatorTests
{
    private readonly UpdateQuestionRequestValidator _validator = new();

    [Fact]
    public void Validate_AllNullFields_Passes()
    {
        // Arrange
        UpdateQuestionRequest request = new();

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidPartialUpdate_Passes()
    {
        // Arrange
        UpdateQuestionRequest request = new()
        {
            QuestionText = "Updated question text?",
            Weight = 50
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_AllValidFields_Passes()
    {
        // Arrange
        UpdateQuestionRequest request = new()
        {
            QuestionText = "Are you authorized to work?",
            QuestionType = QuestionType.YesNo,
            Timing = QuestionTiming.AtApplication,
            Weight = 25,
            DisplayOrder = 3
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyQuestionText_Fails()
    {
        // Arrange
        UpdateQuestionRequest request = new() { QuestionText = "" };

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
        UpdateQuestionRequest request = new() { QuestionType = "EssayStyle" };

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
        UpdateQuestionRequest request = new() { Timing = "DuringInterview" };

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
        UpdateQuestionRequest request = new() { Weight = -1 };

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
        UpdateQuestionRequest request = new() { Weight = 101 };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Weight");
    }

    [Fact]
    public void Validate_NegativeDisplayOrder_Fails()
    {
        // Arrange
        UpdateQuestionRequest request = new() { DisplayOrder = -1 };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DisplayOrder");
    }
}

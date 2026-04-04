using FluentAssertions;
using FluentValidation.Results;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Validators;

namespace Jobsite.UnitTests.Recruitment;

public sealed class UpdateCriteriaRequestValidatorTests
{
    private readonly UpdateCriteriaRequestValidator _validator = new();

    [Fact]
    public void Validate_AllFieldsNull_Passes()
    {
        // Arrange
        UpdateCriteriaRequest request = new();

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidPartialUpdate_Passes()
    {
        // Arrange
        UpdateCriteriaRequest request = new()
        {
            Name = "Updated Criterion",
            Weight = 50.0m
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyNameWhenProvided_Fails()
    {
        // Arrange
        UpdateCriteriaRequest request = new() { Name = "" };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_NameTooLong_Fails()
    {
        // Arrange
        UpdateCriteriaRequest request = new() { Name = new string('A', 201) };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_InvalidCategory_Fails()
    {
        // Arrange
        UpdateCriteriaRequest request = new() { Category = "InvalidCategory" };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Category");
    }

    [Fact]
    public void Validate_InvalidEvaluationMethod_Fails()
    {
        // Arrange
        UpdateCriteriaRequest request = new() { EvaluationMethod = "InvalidMethod" };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "EvaluationMethod");
    }

    [Fact]
    public void Validate_WeightBelowZero_Fails()
    {
        // Arrange
        UpdateCriteriaRequest request = new() { Weight = -1 };

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
        UpdateCriteriaRequest request = new() { Weight = 101 };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Weight");
    }

    [Fact]
    public void Validate_EmptyConfigurationWhenProvided_Fails()
    {
        // Arrange
        UpdateCriteriaRequest request = new() { Configuration = "" };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Configuration");
    }

    [Fact]
    public void Validate_NegativeDisplayOrder_Fails()
    {
        // Arrange
        UpdateCriteriaRequest request = new() { DisplayOrder = -1 };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DisplayOrder");
    }
}

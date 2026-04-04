using FluentAssertions;
using FluentValidation.Results;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Validators;
using Jobsite.Modules.Recruitment.Domain.Constants;

namespace Jobsite.UnitTests.Recruitment;

public sealed class CreateCriteriaRequestValidatorTests
{
    private readonly CreateCriteriaRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        // Arrange
        CreateCriteriaRequest request = TestData.CreateCriteriaRequest();

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyName_Fails()
    {
        // Arrange
        CreateCriteriaRequest request = TestData.CreateCriteriaRequest(name: "");

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
        CreateCriteriaRequest request = TestData.CreateCriteriaRequest(name: new string('A', 201));

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
        CreateCriteriaRequest request = TestData.CreateCriteriaRequest(category: "InvalidCategory");

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
        CreateCriteriaRequest request = TestData.CreateCriteriaRequest(evaluationMethod: "InvalidMethod");

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
        CreateCriteriaRequest request = new()
        {
            Name = "C# Proficiency",
            Category = CriteriaCategory.Skill,
            EvaluationMethod = EvaluationMethod.ExactMatch,
            Weight = -1,
            Configuration = """{"skill_name":"C#"}"""
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
        CreateCriteriaRequest request = new()
        {
            Name = "C# Proficiency",
            Category = CriteriaCategory.Skill,
            EvaluationMethod = EvaluationMethod.ExactMatch,
            Weight = 101,
            Configuration = """{"skill_name":"C#"}"""
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Weight");
    }

    [Fact]
    public void Validate_EmptyConfiguration_Fails()
    {
        // Arrange
        CreateCriteriaRequest request = new()
        {
            Name = "C# Proficiency",
            Category = CriteriaCategory.Skill,
            EvaluationMethod = EvaluationMethod.ExactMatch,
            Weight = 25,
            Configuration = ""
        };

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
        CreateCriteriaRequest request = new()
        {
            Name = "C# Proficiency",
            Category = CriteriaCategory.Skill,
            EvaluationMethod = EvaluationMethod.ExactMatch,
            Weight = 25,
            Configuration = """{"skill_name":"C#"}""",
            DisplayOrder = -1
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DisplayOrder");
    }

    [Fact]
    public void Validate_EmptyCategory_Fails()
    {
        // Arrange
        CreateCriteriaRequest request = new()
        {
            Name = "C# Proficiency",
            Category = "",
            EvaluationMethod = EvaluationMethod.ExactMatch,
            Weight = 25,
            Configuration = """{"skill_name":"C#"}"""
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Category");
    }
}

using FluentAssertions;
using FluentValidation.Results;
using Jobsite.Modules.Profiles.Application.DTOs;
using Jobsite.Modules.Profiles.Application.Validators;

namespace Jobsite.UnitTests.Profiles;

public sealed class UpdateProfileRequestValidatorTests
{
    private readonly UpdateProfileRequestValidator _validator = new();

    [Fact]
    public void Validate_AllFieldsNull_Passes()
    {
        // Arrange
        UpdateProfileRequest request = new();

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidPartialUpdate_Passes()
    {
        // Arrange
        UpdateProfileRequest request = new()
        {
            FirstName = "Updated",
            City = "Cebu"
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyFirstNameWhenProvided_Fails()
    {
        // Arrange
        UpdateProfileRequest request = new()
        {
            FirstName = ""
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Fact]
    public void Validate_EmptyLastNameWhenProvided_Fails()
    {
        // Arrange
        UpdateProfileRequest request = new()
        {
            LastName = ""
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LastName");
    }

    [Fact]
    public void Validate_FirstNameTooLong_Fails()
    {
        // Arrange
        UpdateProfileRequest request = new()
        {
            FirstName = new string('A', 101)
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Fact]
    public void Validate_SkillWithInvalidLevel_Fails()
    {
        // Arrange
        UpdateProfileRequest request = new()
        {
            Skills = [new SkillDto { Name = "C#", Level = "Master" }]
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Level"));
    }
}

using FluentAssertions;
using FluentValidation.Results;
using Jobsite.Modules.Profiles.Application.DTOs;
using Jobsite.Modules.Profiles.Application.Validators;

namespace Jobsite.UnitTests.Profiles;

public sealed class CreateProfileRequestValidatorTests
{
    private readonly CreateProfileRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        // Arrange
        CreateProfileRequest request = TestData.CreateProfileRequest();

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyFirstName_Fails(string? firstName)
    {
        // Arrange
        CreateProfileRequest request = new()
        {
            FirstName = firstName!,
            LastName = "User"
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Fact]
    public void Validate_FirstNameTooLong_Fails()
    {
        // Arrange
        CreateProfileRequest request = new()
        {
            FirstName = new string('A', 101),
            LastName = "User"
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FirstName");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyLastName_Fails(string? lastName)
    {
        // Arrange
        CreateProfileRequest request = new()
        {
            FirstName = "Test",
            LastName = lastName!
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LastName");
    }

    [Fact]
    public void Validate_PhoneTooLong_Fails()
    {
        // Arrange
        CreateProfileRequest request = new()
        {
            FirstName = "Test",
            LastName = "User",
            Phone = new string('1', 21)
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Phone");
    }

    [Fact]
    public void Validate_SkillWithInvalidLevel_Fails()
    {
        // Arrange
        CreateProfileRequest request = new()
        {
            FirstName = "Test",
            LastName = "User",
            Skills = [new SkillDto { Name = "C#", Level = "Super" }]
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Level"));
    }

    [Fact]
    public void Validate_SkillWithValidLevel_Passes()
    {
        // Arrange
        CreateProfileRequest request = new()
        {
            FirstName = "Test",
            LastName = "User",
            Skills = [new SkillDto { Name = "C#", Level = "Advanced", Years = 5 }]
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_SkillWithEmptyName_Fails()
    {
        // Arrange
        CreateProfileRequest request = new()
        {
            FirstName = "Test",
            LastName = "User",
            Skills = [new SkillDto { Name = "" }]
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Name"));
    }

    [Fact]
    public void Validate_SkillWithNegativeYears_Fails()
    {
        // Arrange
        CreateProfileRequest request = new()
        {
            FirstName = "Test",
            LastName = "User",
            Skills = [new SkillDto { Name = "C#", Years = -1 }]
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("Years"));
    }
}

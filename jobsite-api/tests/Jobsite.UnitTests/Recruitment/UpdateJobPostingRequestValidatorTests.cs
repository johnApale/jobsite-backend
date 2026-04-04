using FluentAssertions;
using FluentValidation.Results;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Validators;
using Jobsite.Modules.Recruitment.Domain.Constants;

namespace Jobsite.UnitTests.Recruitment;

public sealed class UpdateJobPostingRequestValidatorTests
{
    private readonly UpdateJobPostingRequestValidator _validator = new();

    [Fact]
    public void Validate_AllFieldsNull_Passes()
    {
        // Arrange
        UpdateJobPostingRequest request = new();

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidPartialUpdate_Passes()
    {
        // Arrange
        UpdateJobPostingRequest request = new()
        {
            Title = "Updated Title",
            LocationType = LocationType.Remote
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyTitleWhenProvided_Fails()
    {
        // Arrange
        UpdateJobPostingRequest request = new() { Title = "" };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void Validate_TitleTooLong_Fails()
    {
        // Arrange
        UpdateJobPostingRequest request = new() { Title = new string('A', 201) };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void Validate_InvalidLocationType_Fails()
    {
        // Arrange
        UpdateJobPostingRequest request = new() { LocationType = "InvalidType" };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LocationType");
    }

    [Fact]
    public void Validate_InvalidEmploymentType_Fails()
    {
        // Arrange
        UpdateJobPostingRequest request = new() { EmploymentType = "InvalidType" };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "EmploymentType");
    }

    [Fact]
    public void Validate_NegativeSalaryMin_Fails()
    {
        // Arrange
        UpdateJobPostingRequest request = new() { SalaryMin = -1 };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SalaryMin");
    }

    [Fact]
    public void Validate_SalaryMinGreaterThanMax_Fails()
    {
        // Arrange
        UpdateJobPostingRequest request = new()
        {
            SalaryMin = 100000,
            SalaryMax = 50000
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SalaryMax");
    }

    [Fact]
    public void Validate_EmptyDescriptionWhenProvided_Fails()
    {
        // Arrange
        UpdateJobPostingRequest request = new() { Description = "" };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Description");
    }

    [Fact]
    public void Validate_CityTooLong_Fails()
    {
        // Arrange
        UpdateJobPostingRequest request = new() { City = new string('A', 101) };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "City");
    }

    [Fact]
    public void Validate_DepartmentTooLong_Fails()
    {
        // Arrange
        UpdateJobPostingRequest request = new() { Department = new string('A', 101) };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Department");
    }
}

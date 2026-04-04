using FluentAssertions;
using FluentValidation.Results;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Validators;

namespace Jobsite.UnitTests.Recruitment;

public sealed class UpdateClientCompanyRequestValidatorTests
{
    private readonly UpdateClientCompanyRequestValidator _validator = new();

    [Fact]
    public void Validate_AllFieldsNull_Passes()
    {
        // Arrange
        UpdateClientCompanyRequest request = new();

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidPartialUpdate_Passes()
    {
        // Arrange
        UpdateClientCompanyRequest request = new()
        {
            Name = "Updated Corp",
            Industry = "Healthcare"
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
        UpdateClientCompanyRequest request = new() { Name = "" };

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
        UpdateClientCompanyRequest request = new() { Name = new string('A', 201) };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void Validate_InvalidIndustry_Fails()
    {
        // Arrange
        UpdateClientCompanyRequest request = new() { Industry = "InvalidIndustry" };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Industry");
    }

    [Fact]
    public void Validate_InvalidStatus_Fails()
    {
        // Arrange
        UpdateClientCompanyRequest request = new() { Status = "InvalidStatus" };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Status");
    }

    [Fact]
    public void Validate_ValidStatus_Passes()
    {
        // Arrange
        UpdateClientCompanyRequest request = new() { Status = "Active" };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidEmailFormat_Fails()
    {
        // Arrange
        UpdateClientCompanyRequest request = new() { ContactEmail = "not-an-email" };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ContactEmail");
    }

    [Fact]
    public void Validate_ContactPhoneTooLong_Fails()
    {
        // Arrange
        UpdateClientCompanyRequest request = new() { ContactPhone = new string('1', 21) };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ContactPhone");
    }
}

using FluentAssertions;
using FluentValidation.Results;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Validators;

namespace Jobsite.UnitTests.Recruitment;

public sealed class CreateClientCompanyRequestValidatorTests
{
    private readonly CreateClientCompanyRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        // Arrange
        CreateClientCompanyRequest request = TestData.CreateClientCompanyRequest();

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyName_Fails()
    {
        // Arrange
        CreateClientCompanyRequest request = TestData.CreateClientCompanyRequest(name: "");

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
        CreateClientCompanyRequest request = TestData.CreateClientCompanyRequest(name: new string('A', 201));

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
        CreateClientCompanyRequest request = new()
        {
            Name = "Acme Corp",
            Industry = "InvalidIndustry"
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Industry");
    }

    [Fact]
    public void Validate_ValidIndustry_Passes()
    {
        // Arrange
        CreateClientCompanyRequest request = new()
        {
            Name = "Acme Corp",
            Industry = "Technology"
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidEmailFormat_Fails()
    {
        // Arrange
        CreateClientCompanyRequest request = new()
        {
            Name = "Acme Corp",
            ContactEmail = "not-an-email"
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ContactEmail");
    }

    [Fact]
    public void Validate_DisplayNameTooLong_Fails()
    {
        // Arrange
        CreateClientCompanyRequest request = new()
        {
            Name = "Acme Corp",
            DisplayName = new string('A', 201)
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "DisplayName");
    }

    [Fact]
    public void Validate_WebsiteTooLong_Fails()
    {
        // Arrange
        CreateClientCompanyRequest request = new()
        {
            Name = "Acme Corp",
            Website = new string('A', 2049)
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Website");
    }

    [Fact]
    public void Validate_ContactPhoneTooLong_Fails()
    {
        // Arrange
        CreateClientCompanyRequest request = new()
        {
            Name = "Acme Corp",
            ContactPhone = new string('1', 21)
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ContactPhone");
    }

    [Fact]
    public void Validate_NullOptionalFields_Passes()
    {
        // Arrange
        CreateClientCompanyRequest request = new()
        {
            Name = "Acme Corp"
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}

using FluentAssertions;
using FluentValidation.Results;
using Jobsite.Modules.Auth.Application.DTOs;
using Jobsite.Modules.Auth.Application.Validators;
using Jobsite.Modules.Auth.Domain.Constants;

namespace Jobsite.UnitTests.Auth;

/// <summary>
/// Tests for <see cref="RegisterRequestValidator"/>.
/// </summary>
public sealed class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_IsValid()
    {
        // Arrange
        RegisterRequest request = TestData.CreateRegisterRequest();

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyEmail_HasValidationError()
    {
        // Arrange
        RegisterRequest request = TestData.CreateRegisterRequest(email: "");

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_InvalidEmail_HasValidationError()
    {
        // Arrange
        RegisterRequest request = TestData.CreateRegisterRequest(email: "not-an-email");

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_ShortPassword_HasValidationError()
    {
        // Arrange
        RegisterRequest request = TestData.CreateRegisterRequest(password: "short");

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public void Validate_ValidRole_IsValid()
    {
        // Arrange
        RegisterRequest request = TestData.CreateRegisterRequest(role: UserRole.Recruiter);

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidRole_HasValidationError()
    {
        // Arrange
        RegisterRequest request = TestData.CreateRegisterRequest(role: "InvalidRole");

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Role");
    }

    [Fact]
    public void Validate_NullRole_IsValid()
    {
        // Arrange
        RegisterRequest request = TestData.CreateRegisterRequest(role: null);

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}

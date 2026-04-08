using FluentAssertions;
using FluentValidation.Results;
using Jobsite.Modules.Auth.Application.DTOs;
using Jobsite.Modules.Auth.Application.Validators;

namespace Jobsite.UnitTests.Auth;

/// <summary>
/// Tests for <see cref="ResetPasswordRequestValidator"/>.
/// </summary>
public sealed class ResetPasswordRequestValidatorTests
{
    private readonly ResetPasswordRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_IsValid()
    {
        ResetPasswordRequest request = new() { Email = "test@example.com", Token = "abc123", NewPassword = "NewPassword123!" };
        ValidationResult result = _validator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyEmail_HasValidationError()
    {
        ResetPasswordRequest request = new() { Email = "", Token = "abc123", NewPassword = "NewPassword123!" };
        ValidationResult result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_InvalidEmail_HasValidationError()
    {
        ResetPasswordRequest request = new() { Email = "not-an-email", Token = "abc123", NewPassword = "NewPassword123!" };
        ValidationResult result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_EmptyToken_HasValidationError()
    {
        ResetPasswordRequest request = new() { Email = "test@example.com", Token = "", NewPassword = "NewPassword123!" };
        ValidationResult result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Token");
    }

    [Fact]
    public void Validate_EmptyNewPassword_HasValidationError()
    {
        ResetPasswordRequest request = new() { Email = "test@example.com", Token = "abc123", NewPassword = "" };
        ValidationResult result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }

    [Fact]
    public void Validate_ShortNewPassword_HasValidationError()
    {
        ResetPasswordRequest request = new() { Email = "test@example.com", Token = "abc123", NewPassword = "short" };
        ValidationResult result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "NewPassword");
    }
}

using FluentAssertions;
using FluentValidation.Results;
using Jobsite.Modules.Auth.Application.DTOs;
using Jobsite.Modules.Auth.Application.Validators;

namespace Jobsite.UnitTests.Auth;

/// <summary>
/// Tests for <see cref="VerifyEmailRequestValidator"/>.
/// </summary>
public sealed class VerifyEmailRequestValidatorTests
{
    private readonly VerifyEmailRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_IsValid()
    {
        VerifyEmailRequest request = new() { Email = "test@example.com", Token = "abc123" };
        ValidationResult result = _validator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyEmail_HasValidationError()
    {
        VerifyEmailRequest request = new() { Email = "", Token = "abc123" };
        ValidationResult result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_InvalidEmail_HasValidationError()
    {
        VerifyEmailRequest request = new() { Email = "not-an-email", Token = "abc123" };
        ValidationResult result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_EmptyToken_HasValidationError()
    {
        VerifyEmailRequest request = new() { Email = "test@example.com", Token = "" };
        ValidationResult result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Token");
    }
}

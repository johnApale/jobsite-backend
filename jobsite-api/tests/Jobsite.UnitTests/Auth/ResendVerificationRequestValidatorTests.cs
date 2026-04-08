using FluentAssertions;
using FluentValidation.Results;
using Jobsite.Modules.Auth.Application.DTOs;
using Jobsite.Modules.Auth.Application.Validators;

namespace Jobsite.UnitTests.Auth;

/// <summary>
/// Tests for <see cref="ResendVerificationRequestValidator"/>.
/// </summary>
public sealed class ResendVerificationRequestValidatorTests
{
    private readonly ResendVerificationRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_IsValid()
    {
        ResendVerificationRequest request = new() { Email = "test@example.com" };
        ValidationResult result = _validator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyEmail_HasValidationError()
    {
        ResendVerificationRequest request = new() { Email = "" };
        ValidationResult result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validate_InvalidEmail_HasValidationError()
    {
        ResendVerificationRequest request = new() { Email = "not-an-email" };
        ValidationResult result = _validator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }
}

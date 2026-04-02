using FluentValidation;
using Jobsite.Modules.Auth.Application.DTOs;

namespace Jobsite.Modules.Auth.Application.Validators;

/// <summary>
/// Validates <see cref="LoginRequest"/> inputs.
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");
    }
}

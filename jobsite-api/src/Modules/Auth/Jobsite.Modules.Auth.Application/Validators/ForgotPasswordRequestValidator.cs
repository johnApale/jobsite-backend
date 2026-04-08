using FluentValidation;
using Jobsite.Modules.Auth.Application.DTOs;

namespace Jobsite.Modules.Auth.Application.Validators;

/// <summary>
/// Validates <see cref="ForgotPasswordRequest"/> inputs.
/// </summary>
public sealed class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address");
    }
}

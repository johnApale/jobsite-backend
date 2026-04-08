using FluentValidation;
using Jobsite.Modules.Auth.Application.DTOs;

namespace Jobsite.Modules.Auth.Application.Validators;

/// <summary>
/// Validates <see cref="VerifyEmailRequest"/> inputs.
/// </summary>
public sealed class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Verification token is required");
    }
}

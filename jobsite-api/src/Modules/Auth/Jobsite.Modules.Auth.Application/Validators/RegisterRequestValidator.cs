using FluentValidation;
using Jobsite.Modules.Auth.Application.DTOs;
using Jobsite.Modules.Auth.Domain.Constants;

namespace Jobsite.Modules.Auth.Application.Validators;

/// <summary>
/// Validates <see cref="RegisterRequest"/> inputs.
/// </summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address")
            .MaximumLength(254).WithMessage("Email must not exceed 254 characters");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters");

        RuleFor(x => x.Role)
            .Must(role => role is null || UserRole.IsValid(role))
            .WithMessage("Role must be one of: Applicant, Recruiter, HiringManager, Interviewer, AgencyAdmin");
    }
}

using FluentValidation;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Domain.Constants;

namespace Jobsite.Modules.Recruitment.Application.Validators;

public sealed class CreateClientCompanyRequestValidator : AbstractValidator<CreateClientCompanyRequest>
{
    public CreateClientCompanyRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Company name is required")
            .MaximumLength(200).WithMessage("Company name must not exceed 200 characters");

        RuleFor(x => x.DisplayName)
            .MaximumLength(200).WithMessage("Display name must not exceed 200 characters")
            .When(x => x.DisplayName is not null);

        RuleFor(x => x.Industry)
            .Must(Industry.IsValid!)
            .WithMessage("Invalid industry value")
            .When(x => x.Industry is not null);

        RuleFor(x => x.Website)
            .MaximumLength(2048).WithMessage("Website must not exceed 2048 characters")
            .When(x => x.Website is not null);

        RuleFor(x => x.ContactName)
            .MaximumLength(200).WithMessage("Contact name must not exceed 200 characters")
            .When(x => x.ContactName is not null);

        RuleFor(x => x.ContactEmail)
            .MaximumLength(254).WithMessage("Contact email must not exceed 254 characters")
            .EmailAddress().WithMessage("Contact email must be a valid email address")
            .When(x => x.ContactEmail is not null);

        RuleFor(x => x.ContactPhone)
            .MaximumLength(20).WithMessage("Contact phone must not exceed 20 characters")
            .When(x => x.ContactPhone is not null);
    }
}

using FluentValidation;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Domain.Constants;

namespace Jobsite.Modules.Recruitment.Application.Validators;

public sealed class UpdateJobPostingRequestValidator : AbstractValidator<UpdateJobPostingRequest>
{
    public UpdateJobPostingRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title must not be empty when provided")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters")
            .When(x => x.Title is not null);

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description must not be empty when provided")
            .When(x => x.Description is not null);

        RuleFor(x => x.LocationType)
            .Must(LocationType.IsValid)
            .WithMessage("Location type must be one of: OnSite, Remote, Hybrid")
            .When(x => x.LocationType is not null);

        RuleFor(x => x.City)
            .MaximumLength(100).WithMessage("City must not exceed 100 characters")
            .When(x => x.City is not null);

        RuleFor(x => x.Country)
            .MaximumLength(100).WithMessage("Country must not exceed 100 characters")
            .When(x => x.Country is not null);

        RuleFor(x => x.EmploymentType)
            .Must(EmploymentType.IsValid)
            .WithMessage("Employment type must be one of: FullTime, PartTime, Contract, Temporary, Internship")
            .When(x => x.EmploymentType is not null);

        RuleFor(x => x.SalaryMin)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum salary must be non-negative")
            .When(x => x.SalaryMin is not null);

        RuleFor(x => x.SalaryMax)
            .GreaterThanOrEqualTo(0).WithMessage("Maximum salary must be non-negative")
            .When(x => x.SalaryMax is not null);

        RuleFor(x => x.SalaryMax)
            .GreaterThanOrEqualTo(x => x.SalaryMin!.Value)
            .WithMessage("Maximum salary must be greater than or equal to minimum salary")
            .When(x => x.SalaryMin is not null && x.SalaryMax is not null);

        RuleFor(x => x.SalaryCurrency)
            .MaximumLength(3).WithMessage("Currency must be a 3-character ISO 4217 code")
            .When(x => x.SalaryCurrency is not null);

        RuleFor(x => x.Department)
            .MaximumLength(100).WithMessage("Department must not exceed 100 characters")
            .When(x => x.Department is not null);
    }
}

using FluentValidation;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Domain.Constants;

namespace Jobsite.Modules.Recruitment.Application.Validators;

public sealed class CreateJobPostingRequestValidator : AbstractValidator<CreateJobPostingRequest>
{
    public CreateJobPostingRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required");

        RuleFor(x => x.LocationType)
            .NotEmpty().WithMessage("Location type is required")
            .Must(LocationType.IsValid)
            .WithMessage("Location type must be one of: OnSite, Remote, Hybrid");

        RuleFor(x => x.City)
            .NotEmpty().WithMessage("City is required for OnSite and Hybrid locations")
            .MaximumLength(100).WithMessage("City must not exceed 100 characters")
            .When(x => x.LocationType is not null && x.LocationType != LocationType.Remote);

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("Country is required for OnSite and Hybrid locations")
            .MaximumLength(100).WithMessage("Country must not exceed 100 characters")
            .When(x => x.LocationType is not null && x.LocationType != LocationType.Remote);

        RuleFor(x => x.EmploymentType)
            .NotEmpty().WithMessage("Employment type is required")
            .Must(EmploymentType.IsValid)
            .WithMessage("Employment type must be one of: FullTime, PartTime, Contract, Temporary, Internship");

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
            .NotEmpty().WithMessage("Currency is required when salary is provided")
            .MaximumLength(3).WithMessage("Currency must be a 3-character ISO 4217 code")
            .When(x => x.SalaryMin is not null || x.SalaryMax is not null);

        RuleFor(x => x.Department)
            .MaximumLength(100).WithMessage("Department must not exceed 100 characters")
            .When(x => x.Department is not null);
    }
}

using FluentValidation;
using Jobsite.Modules.HRWorkflows.Application.DTOs;
using Jobsite.Modules.HRWorkflows.Domain.Constants;

namespace Jobsite.Modules.HRWorkflows.Application.Validators;

public sealed class CreateOfferRequestValidator : AbstractValidator<CreateOfferRequest>
{
    public CreateOfferRequestValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.Salary).GreaterThan(0);
        RuleFor(x => x.SalaryCurrency).NotEmpty().Length(3)
            .WithMessage("Currency must be a 3-letter ISO 4217 code");
        RuleFor(x => x.SalaryPeriod).NotEmpty()
            .Must(SalaryPeriod.IsValid)
            .WithMessage("Invalid salary period. Valid values: Annual, Monthly, Hourly");
        RuleFor(x => x.EmploymentType).NotEmpty()
            .Must(OfferEmploymentType.IsValid)
            .WithMessage("Invalid employment type. Valid values: FullTime, PartTime, Contract, Temporary");
    }
}

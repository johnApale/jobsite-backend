using FluentValidation;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Domain.Constants;

namespace Jobsite.Modules.Recruitment.Application.Validators;

public sealed class UpdateCriteriaRequestValidator : AbstractValidator<UpdateCriteriaRequest>
{
    public UpdateCriteriaRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Criterion name must not be empty when provided")
            .MaximumLength(200).WithMessage("Criterion name must not exceed 200 characters")
            .When(x => x.Name is not null);

        RuleFor(x => x.Category)
            .Must(CriteriaCategory.IsValid!)
            .WithMessage("Category must be one of: Skill, Experience, Certification, Education, Location, Custom")
            .When(x => x.Category is not null);

        RuleFor(x => x.EvaluationMethod)
            .Must(EvaluationMethod.IsValid!)
            .WithMessage("Evaluation method must be one of: ExactMatch, RangeMatch, SemanticSimilarity")
            .When(x => x.EvaluationMethod is not null);

        RuleFor(x => x.Weight)
            .InclusiveBetween(0, 100).WithMessage("Weight must be between 0 and 100")
            .When(x => x.Weight is not null);

        RuleFor(x => x.Configuration)
            .NotEmpty().WithMessage("Configuration must not be empty when provided")
            .When(x => x.Configuration is not null);

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Display order must be non-negative")
            .When(x => x.DisplayOrder is not null);
    }
}

using FluentValidation;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Domain.Constants;

namespace Jobsite.Modules.Recruitment.Application.Validators;

public sealed class CreateCriteriaRequestValidator : AbstractValidator<CreateCriteriaRequest>
{
    public CreateCriteriaRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Criterion name is required")
            .MaximumLength(200).WithMessage("Criterion name must not exceed 200 characters");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required")
            .Must(CriteriaCategory.IsValid)
            .WithMessage("Category must be one of: Skill, Experience, Certification, Education, Location, Custom");

        RuleFor(x => x.EvaluationMethod)
            .NotEmpty().WithMessage("Evaluation method is required")
            .Must(EvaluationMethod.IsValid)
            .WithMessage("Evaluation method must be one of: ExactMatch, RangeMatch, SemanticSimilarity");

        RuleFor(x => x.Weight)
            .InclusiveBetween(0, 100).WithMessage("Weight must be between 0 and 100");

        RuleFor(x => x.Configuration)
            .NotEmpty().WithMessage("Configuration is required");

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Display order must be non-negative");
    }
}

using FluentValidation;
using Jobsite.Modules.Profiles.Application.DTOs;
using Jobsite.Modules.Profiles.Domain.Constants;

namespace Jobsite.Modules.Profiles.Application.Validators;

public sealed class CreateProfileRequestValidator : AbstractValidator<CreateProfileRequest>
{
    public CreateProfileRequestValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required")
            .MaximumLength(100).WithMessage("First name must not exceed 100 characters");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required")
            .MaximumLength(100).WithMessage("Last name must not exceed 100 characters");

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("Phone must not exceed 20 characters")
            .When(x => x.Phone is not null);

        RuleFor(x => x.City)
            .MaximumLength(100).WithMessage("City must not exceed 100 characters")
            .When(x => x.City is not null);

        RuleFor(x => x.Country)
            .MaximumLength(100).WithMessage("Country must not exceed 100 characters")
            .When(x => x.Country is not null);

        RuleForEach(x => x.Skills).ChildRules(skill =>
        {
            skill.RuleFor(s => s.Name)
                .NotEmpty().WithMessage("Skill name is required")
                .MaximumLength(100).WithMessage("Skill name must not exceed 100 characters");

            skill.RuleFor(s => s.Level)
                .Must(level => level is null || SkillLevel.IsValid(level))
                .WithMessage("Skill level must be one of: Beginner, Intermediate, Advanced, Expert");

            skill.RuleFor(s => s.Years)
                .GreaterThanOrEqualTo(0).WithMessage("Skill years must be non-negative")
                .When(s => s.Years is not null);
        }).When(x => x.Skills is not null);
    }
}

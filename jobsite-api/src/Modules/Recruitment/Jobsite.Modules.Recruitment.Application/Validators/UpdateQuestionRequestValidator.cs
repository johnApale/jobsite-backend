using FluentValidation;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Domain.Constants;

namespace Jobsite.Modules.Recruitment.Application.Validators;

public sealed class UpdateQuestionRequestValidator : AbstractValidator<UpdateQuestionRequest>
{
    public UpdateQuestionRequestValidator()
    {
        RuleFor(x => x.QuestionText)
            .NotEmpty().WithMessage("Question text must not be empty when provided")
            .When(x => x.QuestionText is not null);

        RuleFor(x => x.QuestionType)
            .Must(QuestionType.IsValid!)
            .WithMessage("Question type must be one of: FreeText, MultipleChoice, YesNo")
            .When(x => x.QuestionType is not null);

        RuleFor(x => x.Timing)
            .Must(QuestionTiming.IsValid!)
            .WithMessage("Timing must be one of: AtApplication, AfterScreening")
            .When(x => x.Timing is not null);

        RuleFor(x => x.Weight)
            .InclusiveBetween(0, 100).WithMessage("Weight must be between 0 and 100")
            .When(x => x.Weight is not null);

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Display order must be non-negative")
            .When(x => x.DisplayOrder is not null);
    }
}

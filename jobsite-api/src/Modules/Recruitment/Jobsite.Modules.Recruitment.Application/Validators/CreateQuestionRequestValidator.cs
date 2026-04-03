using FluentValidation;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Domain.Constants;

namespace Jobsite.Modules.Recruitment.Application.Validators;

public sealed class CreateQuestionRequestValidator : AbstractValidator<CreateQuestionRequest>
{
    public CreateQuestionRequestValidator()
    {
        RuleFor(x => x.QuestionText)
            .NotEmpty().WithMessage("Question text is required");

        RuleFor(x => x.QuestionType)
            .NotEmpty().WithMessage("Question type is required")
            .Must(QuestionType.IsValid)
            .WithMessage("Question type must be one of: FreeText, MultipleChoice, YesNo");

        RuleFor(x => x.Timing)
            .NotEmpty().WithMessage("Timing is required")
            .Must(QuestionTiming.IsValid)
            .WithMessage("Timing must be one of: AtApplication, AfterScreening");

        RuleFor(x => x.Weight)
            .InclusiveBetween(0, 100).WithMessage("Weight must be between 0 and 100");

        RuleFor(x => x.Options)
            .NotEmpty().WithMessage("Options are required for MultipleChoice questions")
            .When(x => x.QuestionType == QuestionType.MultipleChoice);

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("Display order must be non-negative");
    }
}

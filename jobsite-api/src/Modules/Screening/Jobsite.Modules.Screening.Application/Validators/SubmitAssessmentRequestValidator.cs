using FluentValidation;
using Jobsite.Modules.Screening.Application.DTOs;

namespace Jobsite.Modules.Screening.Application.Validators;

public sealed class SubmitAssessmentRequestValidator : AbstractValidator<SubmitAssessmentRequest>
{
    public SubmitAssessmentRequestValidator()
    {
        RuleFor(x => x.Answers)
            .NotEmpty().WithMessage("At least one answer is required");

        RuleForEach(x => x.Answers).ChildRules(answer =>
        {
            answer.RuleFor(a => a.QuestionId)
                .NotEmpty().WithMessage("Question ID is required");
        });
    }
}

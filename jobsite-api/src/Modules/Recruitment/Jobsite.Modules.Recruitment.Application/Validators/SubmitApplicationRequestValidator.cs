using FluentValidation;
using Jobsite.Modules.Recruitment.Application.DTOs;

namespace Jobsite.Modules.Recruitment.Application.Validators;

public sealed class SubmitApplicationRequestValidator : AbstractValidator<SubmitApplicationRequest>
{
    public SubmitApplicationRequestValidator()
    {
        RuleFor(x => x.ResumeId)
            .NotEmpty().WithMessage("Resume ID is required");

        RuleFor(x => x.CoverLetterUrl)
            .MaximumLength(2048).WithMessage("Cover letter URL must not exceed 2048 characters")
            .When(x => x.CoverLetterUrl is not null);

        RuleForEach(x => x.QuestionAnswers).ChildRules(answer =>
        {
            answer.RuleFor(a => a.QuestionId)
                .NotEmpty().WithMessage("Question ID is required");
        }).When(x => x.QuestionAnswers is not null);
    }
}

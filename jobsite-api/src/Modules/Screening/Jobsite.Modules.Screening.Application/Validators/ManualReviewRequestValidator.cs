using FluentValidation;
using Jobsite.Modules.Screening.Application.DTOs;
using Jobsite.Modules.Screening.Domain.Constants;

namespace Jobsite.Modules.Screening.Application.Validators;

public sealed class ManualReviewRequestValidator : AbstractValidator<ManualReviewRequest>
{
    public ManualReviewRequestValidator()
    {
        RuleFor(x => x.Outcome)
            .NotEmpty().WithMessage("Outcome is required")
            .Must(value => value is ScreeningOutcome.ManuallyAdvanced or ScreeningOutcome.ManuallyRejected)
            .WithMessage("Outcome must be ManuallyAdvanced or ManuallyRejected");

        RuleFor(x => x.ReviewNotes)
            .MaximumLength(2000).WithMessage("Review notes must not exceed 2000 characters")
            .When(x => x.ReviewNotes is not null);
    }
}

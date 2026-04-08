using FluentValidation;
using Jobsite.Modules.HRWorkflows.Application.DTOs;
using Jobsite.Modules.HRWorkflows.Domain.Constants;

namespace Jobsite.Modules.HRWorkflows.Application.Validators;

public sealed class SubmitFeedbackRequestValidator : AbstractValidator<SubmitFeedbackRequest>
{
    public SubmitFeedbackRequestValidator()
    {
        RuleFor(x => x.Rating)
            .InclusiveBetween(1.0m, 5.0m)
            .WithMessage("Rating must be between 1.0 and 5.0");
        RuleFor(x => x.Recommendation).NotEmpty()
            .Must(InterviewRecommendation.IsValid)
            .WithMessage("Invalid recommendation. Valid values: StrongHire, Hire, NoHire, StrongNoHire");
    }
}

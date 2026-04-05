using FluentValidation;
using Jobsite.Modules.Matching.Application.DTOs;

namespace Jobsite.Modules.Matching.Application.Validators;

public sealed class GenerateShortlistRequestValidator : AbstractValidator<GenerateShortlistRequest>
{
    public GenerateShortlistRequestValidator()
    {
        RuleFor(x => x.JobPostingId)
            .NotEmpty().WithMessage("job_posting_id is required");
    }
}

using FluentValidation;
using Jobsite.Modules.Matching.Application.DTOs;

namespace Jobsite.Modules.Matching.Application.Validators;

public sealed class AddCandidateToShortlistRequestValidator : AbstractValidator<AddCandidateToShortlistRequest>
{
    public AddCandidateToShortlistRequestValidator()
    {
        RuleFor(x => x.ApplicationId)
            .NotEmpty().WithMessage("application_id is required");
    }
}

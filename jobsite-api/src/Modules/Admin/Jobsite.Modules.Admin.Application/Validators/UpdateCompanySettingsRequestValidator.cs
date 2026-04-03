using FluentValidation;
using Jobsite.Modules.Admin.Application.DTOs;

namespace Jobsite.Modules.Admin.Application.Validators;

/// <summary>
/// Validates <see cref="UpdateCompanySettingsRequest"/> inputs.
/// </summary>
public sealed class UpdateCompanySettingsRequestValidator : AbstractValidator<UpdateCompanySettingsRequest>
{
    public UpdateCompanySettingsRequestValidator()
    {
        RuleFor(x => x.DefaultTimezone)
            .MaximumLength(50).WithMessage("Timezone must not exceed 50 characters")
            .When(x => x.DefaultTimezone is not null);

        RuleFor(x => x.DefaultCurrency)
            .Length(3).WithMessage("Currency must be exactly 3 characters (ISO 4217)")
            .When(x => x.DefaultCurrency is not null);

        When(x => x.ScreeningSettings is not null, () =>
        {
            RuleFor(x => x.ScreeningSettings!.AutoAdvanceThreshold)
                .InclusiveBetween(0, 100).WithMessage("Auto-advance threshold must be between 0 and 100");

            RuleFor(x => x.ScreeningSettings!.AutoRejectThreshold)
                .InclusiveBetween(0, 100).WithMessage("Auto-reject threshold must be between 0 and 100");

            RuleFor(x => x.ScreeningSettings!.ManualReviewPolicy)
                .Must(p => p is "QueueForReview" or "AutoAdvanceAll" or "AutoRejectAll" or "NotifyAndHold")
                .WithMessage("Manual review policy must be one of: QueueForReview, AutoAdvanceAll, AutoRejectAll, NotifyAndHold");

            RuleFor(x => x.ScreeningSettings!.CandidateTransparencyLevel)
                .Must(l => l is "None" or "Summary" or "Detailed")
                .WithMessage("Candidate transparency level must be one of: None, Summary, Detailed");
        });

        When(x => x.MatchingSettings is not null, () =>
        {
            RuleFor(x => x.MatchingSettings!.ScreeningWeight)
                .InclusiveBetween(0, 100).WithMessage("Screening weight must be between 0 and 100");

            RuleFor(x => x.MatchingSettings!.AssessmentWeight)
                .InclusiveBetween(0, 100).WithMessage("Assessment weight must be between 0 and 100");

            RuleFor(x => x.MatchingSettings!.ShortlistSize)
                .GreaterThan(0).WithMessage("Shortlist size must be greater than 0");
        });

        When(x => x.AssessmentSettings is not null, () =>
        {
            RuleFor(x => x.AssessmentSettings!.TimeLimitMinutes)
                .GreaterThan(0).WithMessage("Time limit must be greater than 0");

            RuleFor(x => x.AssessmentSettings!.PartialCompletionPolicy)
                .Must(p => p is "ScorePartial" or "MarkIncomplete")
                .WithMessage("Partial completion policy must be one of: ScorePartial, MarkIncomplete");

            RuleFor(x => x.AssessmentSettings!.CompletionPolicy)
                .Must(p => p is "AutoAdvance" or "QueueForReview")
                .WithMessage("Completion policy must be one of: AutoAdvance, QueueForReview");
        });

        When(x => x.AuthSettings is not null, () =>
        {
            RuleFor(x => x.AuthSettings!.PasswordMinLength)
                .InclusiveBetween(6, 128).WithMessage("Password minimum length must be between 6 and 128");
        });

        When(x => x.ProfileSettings is not null, () =>
        {
            RuleFor(x => x.ProfileSettings!.MinimumSkillsCount)
                .GreaterThanOrEqualTo(0).WithMessage("Minimum skills count must be 0 or greater");

            RuleFor(x => x.ProfileSettings!.AiParsingProvider)
                .Must(p => p is "OpenAI" or "Anthropic" or "AzureOpenAI")
                .WithMessage("AI parsing provider must be one of: OpenAI, Anthropic, AzureOpenAI");
        });
    }
}

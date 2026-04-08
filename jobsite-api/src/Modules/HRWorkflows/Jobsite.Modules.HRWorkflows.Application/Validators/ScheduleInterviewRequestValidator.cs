using FluentValidation;
using Jobsite.Modules.HRWorkflows.Application.DTOs;
using Jobsite.Modules.HRWorkflows.Domain.Constants;

namespace Jobsite.Modules.HRWorkflows.Application.Validators;

public sealed class ScheduleInterviewRequestValidator : AbstractValidator<ScheduleInterviewRequest>
{
    public ScheduleInterviewRequestValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.InterviewType).NotEmpty()
            .Must(InterviewType.IsValid)
            .WithMessage("Invalid interview type. Valid values: InPerson, Video, Phone");
        RuleFor(x => x.ScheduledAt).NotEmpty()
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Scheduled date must be in the future");
        RuleFor(x => x.DurationMinutes).GreaterThan(0).LessThanOrEqualTo(480);
        RuleFor(x => x.PanelistUserIds).NotEmpty()
            .WithMessage("At least one panelist is required");
    }
}

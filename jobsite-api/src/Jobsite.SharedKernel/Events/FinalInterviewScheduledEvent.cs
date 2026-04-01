using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Raised when an HR workflow schedules a final (human) interview for a candidate.
/// Consumed by: Recruitment module (to update application status to FinalInterview).
/// </summary>
public sealed class FinalInterviewScheduledEvent : IDomainEvent
{
    public required Guid ApplicationId { get; init; }
    public required Guid InterviewId { get; init; }
    public required DateTime ScheduledAt { get; init; }
}

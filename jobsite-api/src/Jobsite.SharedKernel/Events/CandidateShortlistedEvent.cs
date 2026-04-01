using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Raised when a candidate is shortlisted after screening + matching.
/// Consumed by: HR Workflows module (to schedule final interviews).
/// </summary>
public sealed class CandidateShortlistedEvent : IDomainEvent
{
    public required Guid ApplicationId { get; init; }
    public required Guid JobPostingId { get; init; }
    public required Guid ApplicantUserId { get; init; }
    public required DateTime ShortlistedAt { get; init; }
}

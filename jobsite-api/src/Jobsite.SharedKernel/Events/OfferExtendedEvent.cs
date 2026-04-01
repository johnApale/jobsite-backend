using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Raised when an offer is extended to a candidate.
/// Consumed by: Recruitment module (to update application status to Offered).
/// </summary>
public sealed class OfferExtendedEvent : IDomainEvent
{
    public required Guid ApplicationId { get; init; }
    public required Guid OfferId { get; init; }
    public required DateTime OfferedAt { get; init; }
}

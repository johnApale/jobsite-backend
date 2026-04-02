using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Raised when a new user registers (email/password or OAuth).
/// Consumed by: Profiles module (to auto-create an ApplicantProfile).
/// </summary>
public sealed class UserRegisteredEvent : IDomainEvent
{
    public required Guid UserId { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required DateTime RegisteredAt { get; init; }
}

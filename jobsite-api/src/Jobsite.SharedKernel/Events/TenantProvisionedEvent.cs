using Jobsite.SharedKernel.Domain;

namespace Jobsite.SharedKernel.Events;

/// <summary>
/// Raised when a new tenant has been provisioned (database created, schemas migrated).
/// Consumed by: Admin module (to seed default CompanySettings).
/// </summary>
public sealed class TenantProvisionedEvent : IDomainEvent
{
    public required Guid TenantId { get; init; }
    public required string TenantName { get; init; }
    public required string OwnerEmail { get; init; }
    public required string ConnectionString { get; init; }
    public required DateTime ProvisionedAt { get; init; }
}

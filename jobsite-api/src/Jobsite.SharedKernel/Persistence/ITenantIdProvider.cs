namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Provides the current tenant ID from the ambient request scope.
/// Implemented in the Api layer using <c>HttpContext.Items["TenantId"]</c>.
/// Used by Application services that need to include TenantId in integration events.
/// </summary>
public interface ITenantIdProvider
{
    Guid TenantId { get; }
}

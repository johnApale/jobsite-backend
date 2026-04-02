using Microsoft.EntityFrameworkCore;

namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Factory for creating per-tenant <see cref="DbContext"/> instances.
/// Resolves the connection string from the current HTTP request context
/// or accepts an explicit connection string for non-HTTP scenarios
/// (e.g., message broker consumers, background jobs).
/// </summary>
public interface ITenantDbContextFactory<TContext> where TContext : TenantDbContext
{
    /// <summary>
    /// Creates a <typeparamref name="TContext"/> using the tenant connection string
    /// from <c>HttpContext.Items["TenantConnectionString"]</c>.
    /// Throws if no tenant context is available (e.g., outside an HTTP request).
    /// </summary>
    TContext CreateDbContext();

    /// <summary>
    /// Creates a <typeparamref name="TContext"/> using an explicit connection string.
    /// Used by MassTransit consumers and background jobs that resolve the tenant
    /// via event payload <c>TenantId</c> → catalog lookup.
    /// </summary>
    TContext CreateDbContext(string connectionString);
}

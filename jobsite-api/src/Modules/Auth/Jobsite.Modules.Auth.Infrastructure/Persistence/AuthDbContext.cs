using Jobsite.Modules.Auth.Domain.Entities;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Auth.Infrastructure.Persistence;

/// <summary>
/// Per-tenant DbContext for the Auth module.
/// Manages users, external logins, and refresh tokens in the <c>auth</c> schema.
/// </summary>
public sealed class AuthDbContext : TenantDbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options, IDomainEventDispatcher? dispatcher = null)
        : base(options, dispatcher)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<UserExternalLogin> UserExternalLogins => Set<UserExternalLogin>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);
    }
}

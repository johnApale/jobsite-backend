using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Jobsite.Modules.Auth.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations tooling.
/// Uses a tenant DB connection string for generating Auth schema migrations.
/// </summary>
public sealed class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        // Resolve Jobsite.Api directory from either the project dir (dotnet ef --project)
        // or the startup-project dir (dotnet ef --startup-project).
        string cwd = Directory.GetCurrentDirectory();
        string apiDir = Path.Combine(cwd, "..", "..", "..", "Jobsite.Api");
        if (!Directory.Exists(apiDir))
        {
            apiDir = cwd; // Already in Jobsite.Api (startup-project sets CWD)
        }

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(apiDir)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        string connectionString = configuration.GetConnectionString("TenantDb")
            ?? configuration.GetConnectionString("CatalogDb")
            ?? throw new InvalidOperationException(
                "TenantDb or CatalogDb connection string not found. Set ConnectionStrings__TenantDb env var or configure appsettings.json.");

        DbContextOptionsBuilder<AuthDbContext> optionsBuilder = new();

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "auth"));

        optionsBuilder.UseSnakeCaseNamingConvention();

        return new AuthDbContext(optionsBuilder.Options);
    }
}

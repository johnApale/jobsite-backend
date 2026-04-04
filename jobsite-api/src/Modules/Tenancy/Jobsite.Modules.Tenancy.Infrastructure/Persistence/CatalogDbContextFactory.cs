using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Jobsite.Modules.Tenancy.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations tooling.
/// Reads connection string from appsettings / environment variables.
/// </summary>
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
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

        string connectionString = configuration.GetConnectionString("CatalogDb")
            ?? throw new InvalidOperationException(
                "CatalogDb connection string not found. Set ConnectionStrings__CatalogDb env var or configure appsettings.json.");

        DbContextOptionsBuilder<CatalogDbContext> optionsBuilder = new();

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "catalog"));

        optionsBuilder.UseSnakeCaseNamingConvention();

        return new CatalogDbContext(optionsBuilder.Options);
    }
}

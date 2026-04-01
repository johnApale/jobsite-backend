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
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "Jobsite.Api"))
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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Jobsite.Modules.Profiles.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF Core migrations tooling.
/// Uses a tenant DB connection string for generating Profiles schema migrations.
/// </summary>
public sealed class ProfilesDbContextFactory : IDesignTimeDbContextFactory<ProfilesDbContext>
{
    public ProfilesDbContext CreateDbContext(string[] args)
    {
        string cwd = Directory.GetCurrentDirectory();
        string apiDir = File.Exists(Path.Combine(cwd, "appsettings.json"))
            ? cwd
            : Path.GetFullPath(Path.Combine(cwd, "..", "..", "..", "Jobsite.Api"));

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

        DbContextOptionsBuilder<ProfilesDbContext> optionsBuilder = new();

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "profiles"));

        optionsBuilder.UseSnakeCaseNamingConvention();

        return new ProfilesDbContext(optionsBuilder.Options);
    }
}

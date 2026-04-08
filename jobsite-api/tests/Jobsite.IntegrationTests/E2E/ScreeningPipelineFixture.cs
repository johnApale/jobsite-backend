using Jobsite.Modules.Screening.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Jobsite.IntegrationTests.E2E;

/// <summary>
/// Shared fixture for end-to-end screening pipeline tests.
/// Spins up a real PostgreSQL container and creates the screening schema.
/// Tests exercise the full pipeline: scoring → routing → persistence.
/// </summary>
public sealed class ScreeningPipelineFixture : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .Build();

        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        // Create schema once
        await using ScreeningDbContext ctx = CreateDbContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    public ScreeningDbContext CreateDbContext()
    {
        DbContextOptions<ScreeningDbContext> options = new DbContextOptionsBuilder<ScreeningDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ScreeningDbContext(options);
    }

    /// <summary>Truncates all tables for test isolation.</summary>
    public async Task ResetDataAsync()
    {
        await using ScreeningDbContext ctx = CreateDbContext();
        await ctx.Database.ExecuteSqlRawAsync("""
            DO $$ DECLARE r RECORD;
            BEGIN
                FOR r IN (SELECT tablename, schemaname FROM pg_tables
                          WHERE schemaname NOT IN ('pg_catalog', 'information_schema'))
                LOOP
                    EXECUTE format('TRUNCATE TABLE %I.%I CASCADE', r.schemaname, r.tablename);
                END LOOP;
            END $$;
            """);
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }
}

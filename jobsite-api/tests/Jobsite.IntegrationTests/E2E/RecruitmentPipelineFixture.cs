using Jobsite.Modules.Recruitment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Jobsite.IntegrationTests.E2E;

/// <summary>
/// Shared fixture for end-to-end recruitment pipeline tests.
/// Spins up a real PostgreSQL container and creates the recruitment schema.
/// Tests exercise the full flow: company → job posting → publish → application → withdraw.
/// </summary>
public sealed class RecruitmentPipelineFixture : IAsyncLifetime
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
        await using RecruitmentDbContext ctx = CreateDbContext();
        await ctx.Database.EnsureCreatedAsync();
    }

    public RecruitmentDbContext CreateDbContext()
    {
        DbContextOptions<RecruitmentDbContext> options = new DbContextOptionsBuilder<RecruitmentDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new RecruitmentDbContext(options);
    }

    /// <summary>Truncates all tables for test isolation.</summary>
    public async Task ResetDataAsync()
    {
        await using RecruitmentDbContext ctx = CreateDbContext();
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

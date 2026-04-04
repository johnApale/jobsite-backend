using Jobsite.Modules.Recruitment.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Jobsite.IntegrationTests.Recruitment;

/// <summary>
/// Shared fixture that spins up a real PostgreSQL container via Testcontainers,
/// creates the Recruitment schema, and exposes a RecruitmentDbContext for integration tests.
/// </summary>
public sealed class RecruitmentIntegrationFixture : IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;

    public RecruitmentDbContext DbContext { get; private set; } = null!;
    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .Build();

        await _postgres.StartAsync();

        ConnectionString = _postgres.GetConnectionString();

        DbContextOptions<RecruitmentDbContext> options = new DbContextOptionsBuilder<RecruitmentDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        DbContext = new RecruitmentDbContext(options);
        await DbContext.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Truncates all tables for test isolation between tests.
    /// </summary>
    public async Task ResetDataAsync()
    {
        await DbContext.Database.ExecuteSqlRawAsync("""
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
        await DbContext.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

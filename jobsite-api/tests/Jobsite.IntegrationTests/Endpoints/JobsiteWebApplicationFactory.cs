using Jobsite.Modules.Admin.Infrastructure.Persistence;
using Jobsite.Modules.Auth.Infrastructure.Persistence;
using Jobsite.Modules.HRWorkflows.Infrastructure.Persistence;
using Jobsite.Modules.Matching.Infrastructure.Persistence;
using Jobsite.Modules.Profiles.Infrastructure.Persistence;
using Jobsite.Modules.Recruitment.Infrastructure.Persistence;
using Jobsite.Modules.Screening.Infrastructure.Persistence;
using Jobsite.Modules.Tenancy.Domain.Constants;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Jobsite.Modules.Tenancy.Infrastructure.Persistence;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Jobsite.IntegrationTests.Endpoints;

/// <summary>
/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> that boots the full application
/// against a real PostgreSQL container via Testcontainers.
/// Seeds a test tenant so endpoint tests can resolve tenant context via the Host header.
/// </summary>
public sealed class JobsiteWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer _postgres = null!;

    public const string TestJwtSecret = "integration-test-jwt-secret-at-least-32-chars!!";
    public const string TestPlatformApiKey = "integration-test-platform-api-key";
    public const string TestTenantSubdomain = "testcorp";
    public const string TestTenantName = "Test Corporation";

    public string ConnectionString { get; private set; } = null!;
    public Guid TestTenantId { get; private set; }

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:17-alpine")
            .Build();

        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        // Environment variables are available before the host reads configuration,
        // ensuring values captured eagerly in AddJobsiteModules() use test settings.
        Environment.SetEnvironmentVariable("ConnectionStrings__CatalogDb", ConnectionString);
        Environment.SetEnvironmentVariable("App__JwtSecret", TestJwtSecret);
        Environment.SetEnvironmentVariable("App__RateLimiting__GlobalRequestsPerMinute", "10000");
        Environment.SetEnvironmentVariable("App__RateLimiting__AuthRequestsPerMinute", "10000");
        Environment.SetEnvironmentVariable("App__RateLimiting__AiRequestsPerMinute", "10000");

        await ApplyAllMigrationsAsync();
        await SeedTestTenantAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:CatalogDb"] = ConnectionString,
                ["App:JwtSecret"] = TestJwtSecret,
                ["App:PlatformApiKey"] = TestPlatformApiKey,
                ["App:JwtIssuer"] = "jobsite-iconnect",
                ["App:JwtAudience"] = "jobsite-iconnect",
                ["App:JwtExpirationMinutes"] = "60",
                ["App:AiServiceUrl"] = "http://localhost:9999",
                ["App:MessageBroker:Host"] = "localhost",
                ["App:MessageBroker:Port"] = "5672",
                ["App:MessageBroker:Username"] = "guest",
                ["App:MessageBroker:Password"] = "guest",
                ["App:MessageBroker:VirtualHost"] = "/",
                ["App:Redis:ConnectionString"] = "",
                ["App:OpenTelemetry:OtlpEndpoint"] = "",
                ["App:RateLimiting:GlobalRequestsPerMinute"] = "10000",
                ["App:RateLimiting:AuthRequestsPerMinute"] = "10000",
                ["App:RateLimiting:AiRequestsPerMinute"] = "10000",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace CatalogDbContext — the minimal hosting model captures the connection
            // string before ConfigureAppConfiguration takes effect, so we must re-register it.
            services.RemoveAll<CatalogDbContext>();
            services.RemoveAll<DbContextOptions<CatalogDbContext>>();
            services.AddDbContext<CatalogDbContext>(options =>
                options.UseNpgsql(ConnectionString)
                       .UseSnakeCaseNamingConvention());

            // Replace MassTransit RabbitMQ transport with in-memory test harness
            services.AddMassTransitTestHarness();
        });
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> whose Host header resolves to the test tenant.
    /// </summary>
    public HttpClient CreateTenantClient(string? subdomain = null)
    {
        string host = $"{subdomain ?? TestTenantSubdomain}.jobsite.com";
        HttpClient client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri($"http://{host}")
        });
        return client;
    }

    /// <summary>
    /// Truncates all tenant-scoped data (preserves the tenant record itself).
    /// Call between tests for data isolation.
    /// </summary>
    public async Task ResetTenantDataAsync()
    {
        DbContextOptions<AuthDbContext> options = BuildOptions<AuthDbContext>();
        await using AuthDbContext ctx = new(options);
        await ctx.Database.ExecuteSqlRawAsync("""
            DO $$ DECLARE r RECORD;
            BEGIN
                FOR r IN (SELECT tablename, schemaname FROM pg_tables
                          WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
                          AND tablename NOT IN ('__EFMigrationsHistory', 'tenants', 'tenant_brandings'))
                LOOP
                    EXECUTE format('TRUNCATE TABLE %I.%I CASCADE', r.schemaname, r.tablename);
                END LOOP;
            END $$;
            """);
    }

    private async Task ApplyAllMigrationsAsync()
    {
        // Catalog (tenancy module — manages the tenants table)
        await using CatalogDbContext catalog = new(BuildOptions<CatalogDbContext>());
        await catalog.Database.MigrateAsync();

        // Tenant-scoped modules (all share one database in tests)
        await using AuthDbContext auth = new(BuildOptions<AuthDbContext>());
        await auth.Database.MigrateAsync();

        await using AdminDbContext admin = new(BuildOptions<AdminDbContext>());
        await admin.Database.MigrateAsync();

        await using ProfilesDbContext profiles = new(BuildOptions<ProfilesDbContext>());
        await profiles.Database.MigrateAsync();

        await using RecruitmentDbContext recruitment = new(BuildOptions<RecruitmentDbContext>());
        await recruitment.Database.MigrateAsync();

        await using ScreeningDbContext screening = new(BuildOptions<ScreeningDbContext>());
        await screening.Database.MigrateAsync();

        await using MatchingDbContext matching = new(BuildOptions<MatchingDbContext>());
        await matching.Database.MigrateAsync();

        await using HRWorkflowsDbContext hrWorkflows = new(BuildOptions<HRWorkflowsDbContext>());
        await hrWorkflows.Database.MigrateAsync();
    }

    private async Task SeedTestTenantAsync()
    {
        await using CatalogDbContext catalog = new(BuildOptions<CatalogDbContext>());

        Tenant tenant = new()
        {
            Name = TestTenantName,
            Subdomain = TestTenantSubdomain,
            ConnectionString = ConnectionString,
            Status = TenantStatus.Active,
            OwnerName = "Test Owner",
            OwnerEmail = "owner@testcorp.com",
            ContactName = "Test Contact",
            ContactEmail = "contact@testcorp.com"
        };

        catalog.Tenants.Add(tenant);
        await catalog.SaveChangesAsync();
        TestTenantId = tenant.Id;
    }

    private DbContextOptions<T> BuildOptions<T>() where T : DbContext
    {
        return new DbContextOptionsBuilder<T>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;
    }

    public new async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__CatalogDb", null);
        Environment.SetEnvironmentVariable("App__JwtSecret", null);
        Environment.SetEnvironmentVariable("App__RateLimiting__GlobalRequestsPerMinute", null);
        Environment.SetEnvironmentVariable("App__RateLimiting__AuthRequestsPerMinute", null);
        Environment.SetEnvironmentVariable("App__RateLimiting__AiRequestsPerMinute", null);

        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

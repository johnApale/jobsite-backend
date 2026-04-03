using FluentAssertions;
using Jobsite.Modules.Admin.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.IntegrationTests.Admin;

/// <summary>
/// Integration tests validating AdminDbContext schema creation, table mapping,
/// and default values against a real PostgreSQL container.
/// </summary>
[Collection("Admin")]
public sealed class AdminDbContextTests : IAsyncLifetime
{
    private readonly AdminIntegrationFixture _fixture;

    public AdminDbContextTests(AdminIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Schema_AdminSchemaExists()
    {
        // Act
        await using Npgsql.NpgsqlConnection conn = new(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using Npgsql.NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT schema_name FROM information_schema.schemata WHERE schema_name = 'admin'";
        object? result = await cmd.ExecuteScalarAsync();
        string? schemaName = result?.ToString();

        // Assert
        schemaName.Should().Be("admin");
    }

    [Fact]
    public async Task CompanySettings_DefaultValues_AppliedByDatabase()
    {
        // Arrange
        CompanySettings settings = new()
        {
            AuthSettings = "{}",
            ProfileSettings = "{}",
            ScreeningSettings = "{}",
            MatchingSettings = "{}",
            AssessmentSettings = "{}",
            NotificationSettings = "{}"
        };

        // Act
        _fixture.DbContext.CompanySettings.Add(settings);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        CompanySettings? persisted = await _fixture.DbContext.CompanySettings
            .AsNoTracking()
            .FirstOrDefaultAsync();

        // Assert
        persisted.Should().NotBeNull();
        persisted!.Id.Should().NotBe(Guid.Empty);
        persisted.DefaultTimezone.Should().Be("UTC");
        persisted.DefaultCurrency.Should().Be("USD");
        persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        persisted.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task AuditLogs_Indexes_Exist()
    {
        // Act
        await using Npgsql.NpgsqlConnection conn = new(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using Npgsql.NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT indexname FROM pg_indexes
            WHERE schemaname = 'admin' AND tablename = 'audit_logs'
            ORDER BY indexname
            """;

        List<string> indexes = [];
        await using Npgsql.NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
        }

        // Assert
        indexes.Should().Contain("ix_audit_logs_actor_id");
        indexes.Should().Contain("ix_audit_logs_action");
        indexes.Should().Contain("ix_audit_logs_entity");
        indexes.Should().Contain("ix_audit_logs_performed_at");
    }

    [Fact]
    public async Task AuditLog_PersistsWithJsonbDetails()
    {
        // Arrange
        AuditLog log = new()
        {
            ActorId = Guid.NewGuid(),
            ActorEmail = "admin@test.com",
            ActorRole = "AgencyAdmin",
            Action = "SettingsUpdated",
            EntityType = "CompanySettings",
            EntityId = Guid.NewGuid(),
            Details = "{\"changed_fields\": [\"timezone\"]}",
            IpAddress = "127.0.0.1",
            UserAgent = "TestAgent/1.0",
            PerformedAt = DateTime.UtcNow
        };

        // Act
        _fixture.DbContext.AuditLogs.Add(log);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        AuditLog? persisted = await _fixture.DbContext.AuditLogs
            .AsNoTracking()
            .FirstOrDefaultAsync(al => al.Id == log.Id);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.Details.Should().Contain("changed_fields");
        persisted.IpAddress.Should().Be("127.0.0.1");
    }
}

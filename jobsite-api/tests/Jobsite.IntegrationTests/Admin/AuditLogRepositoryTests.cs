using FluentAssertions;
using Jobsite.Modules.Admin.Application.DTOs;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.Modules.Admin.Domain.Entities;
using Jobsite.Modules.Admin.Infrastructure.Repositories;

namespace Jobsite.IntegrationTests.Admin;

/// <summary>
/// Integration tests for AuditLogRepository against a real PostgreSQL container.
/// Validates cursor-based pagination, filtering, and persistence.
/// </summary>
[Collection("Admin")]
public sealed class AuditLogRepositoryTests : IAsyncLifetime
{
    private readonly AdminIntegrationFixture _fixture;
    private readonly AuditLogRepository _sut;

    public AuditLogRepositoryTests(AdminIntegrationFixture fixture)
    {
        _fixture = fixture;
        _sut = new AuditLogRepository(fixture.DbContext);
    }

    public Task InitializeAsync() => _fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Add_WithValidLog_PersistsToDatabase()
    {
        // Arrange
        AuditLog log = CreateAuditLog(AuditAction.UserRegistered, AuditEntityType.User);

        // Act
        _sut.Add(log);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        AuditLogPageResponse result = await _sut.GetPageAsync(
            new AuditLogQueryParameters { PageSize = 10 }, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Action.Should().Be(AuditAction.UserRegistered);
    }

    [Fact]
    public async Task GetPageAsync_WithNoFilter_ReturnsAll()
    {
        // Arrange
        await SeedAuditLogs(5);

        // Act
        AuditLogPageResponse result = await _sut.GetPageAsync(
            new AuditLogQueryParameters { PageSize = 10 }, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetPageAsync_WithActionFilter_ReturnsFiltered()
    {
        // Arrange
        await SeedAuditLogs(3, action: AuditAction.UserRegistered);
        await SeedAuditLogs(2, action: AuditAction.SettingsUpdated);

        // Act
        AuditLogPageResponse result = await _sut.GetPageAsync(
            new AuditLogQueryParameters { Action = AuditAction.SettingsUpdated, PageSize = 10 },
            CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => i.Action == AuditAction.SettingsUpdated);
    }

    [Fact]
    public async Task GetPageAsync_WithDateRange_ReturnsFiltered()
    {
        // Arrange
        AuditLog oldLog = CreateAuditLog(AuditAction.UserRegistered, AuditEntityType.User);
        oldLog.PerformedAt = DateTime.UtcNow.AddDays(-10);
        _sut.Add(oldLog);

        AuditLog recentLog = CreateAuditLog(AuditAction.SettingsUpdated, AuditEntityType.CompanySettings);
        recentLog.PerformedAt = DateTime.UtcNow;
        _sut.Add(recentLog);

        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        AuditLogPageResponse result = await _sut.GetPageAsync(
            new AuditLogQueryParameters
            {
                DateFrom = DateTime.UtcNow.AddDays(-1),
                PageSize = 10
            }, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Action.Should().Be(AuditAction.SettingsUpdated);
    }

    [Fact]
    public async Task GetPageAsync_RespectsPageSize()
    {
        // Arrange
        await SeedAuditLogs(5);

        // Act
        AuditLogPageResponse result = await _sut.GetPageAsync(
            new AuditLogQueryParameters { PageSize = 2 }, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.NextCursor.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetPageAsync_WithCursor_ReturnNextPage()
    {
        // Arrange
        await SeedAuditLogs(5);

        // Act — first page
        AuditLogPageResponse firstPage = await _sut.GetPageAsync(
            new AuditLogQueryParameters { PageSize = 3 }, CancellationToken.None);

        // Act — second page using cursor
        AuditLogPageResponse secondPage = await _sut.GetPageAsync(
            new AuditLogQueryParameters { Cursor = firstPage.NextCursor, PageSize = 3 },
            CancellationToken.None);

        // Assert
        firstPage.Items.Should().HaveCount(3);
        secondPage.Items.Should().HaveCount(2);

        // No overlap between pages
        List<Guid> firstPageIds = firstPage.Items.Select(i => i.Id).ToList();
        List<Guid> secondPageIds = secondPage.Items.Select(i => i.Id).ToList();
        firstPageIds.Should().NotIntersectWith(secondPageIds);
    }

    [Fact]
    public async Task GetPageAsync_WithActorIdFilter_ReturnsFiltered()
    {
        // Arrange
        Guid targetActorId = Guid.NewGuid();
        AuditLog matchingLog = CreateAuditLog(AuditAction.UserRegistered, AuditEntityType.User);
        matchingLog.ActorId = targetActorId;
        _sut.Add(matchingLog);

        AuditLog otherLog = CreateAuditLog(AuditAction.SettingsUpdated, AuditEntityType.CompanySettings);
        _sut.Add(otherLog);

        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        AuditLogPageResponse result = await _sut.GetPageAsync(
            new AuditLogQueryParameters { ActorId = targetActorId, PageSize = 10 },
            CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].ActorId.Should().Be(targetActorId);
    }

    private static AuditLog CreateAuditLog(string action, string entityType) => new()
    {
        ActorId = Guid.NewGuid(),
        ActorEmail = "test@example.com",
        ActorRole = "AgencyAdmin",
        Action = action,
        EntityType = entityType,
        EntityId = Guid.NewGuid(),
        PerformedAt = DateTime.UtcNow
    };

    private async Task SeedAuditLogs(int count, string? action = null)
    {
        for (int i = 0; i < count; i++)
        {
            AuditLog log = CreateAuditLog(
                action ?? AuditAction.UserRegistered,
                AuditEntityType.User);
            log.PerformedAt = DateTime.UtcNow.AddMinutes(-i);
            _sut.Add(log);
        }
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();
    }
}

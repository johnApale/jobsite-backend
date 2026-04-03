using FluentAssertions;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.Modules.Admin.Domain.Entities;
using Jobsite.Modules.Admin.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.IntegrationTests.Admin;

/// <summary>
/// Integration tests for CompanySettingsRepository against a real PostgreSQL container.
/// </summary>
[Collection("Admin")]
public sealed class CompanySettingsRepositoryTests : IAsyncLifetime
{
    private readonly AdminIntegrationFixture _fixture;
    private readonly CompanySettingsRepository _sut;

    public CompanySettingsRepositoryTests(AdminIntegrationFixture fixture)
    {
        _fixture = fixture;
        _sut = new CompanySettingsRepository(fixture.DbContext);
    }

    public Task InitializeAsync() => _fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetAsync_WhenSeeded_ReturnsSettings()
    {
        // Arrange
        CompanySettings settings = new()
        {
            DefaultTimezone = "UTC",
            DefaultCurrency = "USD",
            AuthSettings = "{}",
            ProfileSettings = "{}",
            ScreeningSettings = "{}",
            MatchingSettings = "{}",
            AssessmentSettings = "{}",
            NotificationSettings = "{}"
        };
        _fixture.DbContext.CompanySettings.Add(settings);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        CompanySettings? result = await _sut.GetAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.DefaultTimezone.Should().Be("UTC");
        result.DefaultCurrency.Should().Be("USD");
    }

    [Fact]
    public async Task GetAsync_WhenEmpty_ReturnsNull()
    {
        // Act
        CompanySettings? result = await _sut.GetAsync(CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetForUpdateAsync_ReturnsTrackedEntity()
    {
        // Arrange
        CompanySettings settings = new()
        {
            DefaultTimezone = "UTC",
            DefaultCurrency = "USD",
            AuthSettings = "{}",
            ProfileSettings = "{}",
            ScreeningSettings = "{}",
            MatchingSettings = "{}",
            AssessmentSettings = "{}",
            NotificationSettings = "{}"
        };
        _fixture.DbContext.CompanySettings.Add(settings);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        CompanySettings? result = await _sut.GetForUpdateAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        EntityState state = _fixture.DbContext.Entry(result!).State;
        state.Should().Be(EntityState.Unchanged, "entity should be tracked by change tracker");
    }

    [Fact]
    public async Task SaveChanges_WithUpdatedSettings_PersistsChanges()
    {
        // Arrange
        CompanySettings settings = new()
        {
            DefaultTimezone = "UTC",
            DefaultCurrency = "USD",
            AuthSettings = "{}",
            ProfileSettings = "{}",
            ScreeningSettings = "{}",
            MatchingSettings = "{}",
            AssessmentSettings = "{}",
            NotificationSettings = "{}"
        };
        _fixture.DbContext.CompanySettings.Add(settings);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        CompanySettings? tracked = await _sut.GetForUpdateAsync(CancellationToken.None);
        tracked!.DefaultTimezone = "America/New_York";
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        CompanySettings? updated = await _sut.GetAsync(CancellationToken.None);

        // Assert
        updated!.DefaultTimezone.Should().Be("America/New_York");
    }

    [Fact]
    public async Task Add_WithValidSettings_PersistsToDatabase()
    {
        // Arrange
        CompanySettings settings = new()
        {
            DefaultTimezone = "Europe/London",
            DefaultCurrency = "GBP",
            AuthSettings = "{\"password_min_length\": 10}",
            ProfileSettings = "{}",
            ScreeningSettings = "{}",
            MatchingSettings = "{}",
            AssessmentSettings = "{}",
            NotificationSettings = "{}"
        };

        // Act
        _sut.Add(settings);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        CompanySettings? persisted = await _fixture.DbContext.CompanySettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == settings.Id);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.DefaultTimezone.Should().Be("Europe/London");
        persisted.DefaultCurrency.Should().Be("GBP");
        persisted.AuthSettings.Should().Contain("password_min_length");
    }
}

using FluentAssertions;
using Jobsite.Modules.Admin.Application.DTOs;
using Jobsite.Modules.Admin.Application.DTOs.Settings;
using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Application.Services;
using Jobsite.Modules.Admin.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using NSubstitute;

namespace Jobsite.UnitTests.Admin;

public sealed class AdminSettingsServiceTests
{
    private readonly ICompanySettingsRepository _settingsRepo = Substitute.For<ICompanySettingsRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly AdminSettingsService _sut;

    public AdminSettingsServiceTests()
    {
        _sut = new AdminSettingsService(_settingsRepo, _unitOfWork);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenSettingsExist_ReturnsSettings()
    {
        // Arrange
        CompanySettings settings = TestData.CreateCompanySettings();
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        // Act
        CompanySettingsResponse result = await _sut.GetSettingsAsync(CancellationToken.None);

        // Assert
        result.Id.Should().Be(settings.Id);
        result.DefaultTimezone.Should().Be("UTC");
        result.DefaultCurrency.Should().Be("USD");
        result.AuthSettings.Should().NotBeNull();
        result.ProfileSettings.Should().NotBeNull();
        result.ScreeningSettings.Should().NotBeNull();
        result.MatchingSettings.Should().NotBeNull();
        result.AssessmentSettings.Should().NotBeNull();
        result.NotificationSettings.Should().NotBeNull();
    }

    [Fact]
    public async Task GetSettingsAsync_WhenNoSettings_ThrowsSettingsNotFound()
    {
        // Arrange
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns((CompanySettings?)null);

        // Act
        Func<Task> act = () => _sut.GetSettingsAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AppError>()
            .Where(e => e.Code == "SETTINGS_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateSettingsAsync_WithValidPatch_UpdatesOnlyProvidedFields()
    {
        // Arrange
        CompanySettings settings = TestData.CreateCompanySettings();
        _settingsRepo.GetForUpdateAsync(Arg.Any<CancellationToken>()).Returns(settings);

        UpdateCompanySettingsRequest request = new()
        {
            DefaultTimezone = "America/New_York"
        };

        // Act
        CompanySettingsResponse result = await _sut.UpdateSettingsAsync(request, CancellationToken.None);

        // Assert
        result.DefaultTimezone.Should().Be("America/New_York");
        result.DefaultCurrency.Should().Be("USD"); // unchanged
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSettingsAsync_WithNullFields_PreservesExistingValues()
    {
        // Arrange
        CompanySettings settings = TestData.CreateCompanySettings(defaultTimezone: "Europe/London");
        _settingsRepo.GetForUpdateAsync(Arg.Any<CancellationToken>()).Returns(settings);

        UpdateCompanySettingsRequest request = new(); // all null

        // Act
        CompanySettingsResponse result = await _sut.UpdateSettingsAsync(request, CancellationToken.None);

        // Assert
        result.DefaultTimezone.Should().Be("Europe/London");
        result.DefaultCurrency.Should().Be("USD");
    }

    [Fact]
    public async Task UpdateSettingsAsync_WhenNoSettings_ThrowsSettingsNotFound()
    {
        // Arrange
        _settingsRepo.GetForUpdateAsync(Arg.Any<CancellationToken>()).Returns((CompanySettings?)null);

        UpdateCompanySettingsRequest request = new() { DefaultTimezone = "UTC" };

        // Act
        Func<Task> act = () => _sut.UpdateSettingsAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AppError>()
            .Where(e => e.Code == "SETTINGS_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateSettingsAsync_WithAuthSettings_SerializesToJsonb()
    {
        // Arrange
        CompanySettings settings = TestData.CreateCompanySettings();
        _settingsRepo.GetForUpdateAsync(Arg.Any<CancellationToken>()).Returns(settings);

        UpdateCompanySettingsRequest request = new()
        {
            AuthSettings = new AuthSettingsDto
            {
                PasswordMinLength = 12,
                AllowSelfRegistration = false
            }
        };

        // Act
        CompanySettingsResponse result = await _sut.UpdateSettingsAsync(request, CancellationToken.None);

        // Assert
        result.AuthSettings.PasswordMinLength.Should().Be(12);
        result.AuthSettings.AllowSelfRegistration.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateSettingsAsync_WithMultipleFields_UpdatesAll()
    {
        // Arrange
        CompanySettings settings = TestData.CreateCompanySettings();
        _settingsRepo.GetForUpdateAsync(Arg.Any<CancellationToken>()).Returns(settings);

        UpdateCompanySettingsRequest request = new()
        {
            DefaultTimezone = "Asia/Tokyo",
            DefaultCurrency = "JPY",
            MatchingSettings = new MatchingSettingsDto
            {
                ScreeningWeight = 60,
                AssessmentWeight = 40
            }
        };

        // Act
        CompanySettingsResponse result = await _sut.UpdateSettingsAsync(request, CancellationToken.None);

        // Assert
        result.DefaultTimezone.Should().Be("Asia/Tokyo");
        result.DefaultCurrency.Should().Be("JPY");
        result.MatchingSettings.ScreeningWeight.Should().Be(60);
        result.MatchingSettings.AssessmentWeight.Should().Be(40);
    }

    [Fact]
    public async Task GetSettingsAsync_DeserializesAllSettingsBlocks()
    {
        // Arrange
        CompanySettings settings = TestData.CreateCompanySettings();
        _settingsRepo.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        // Act
        CompanySettingsResponse result = await _sut.GetSettingsAsync(CancellationToken.None);

        // Assert
        result.AuthSettings.PasswordMinLength.Should().Be(8);
        result.AuthSettings.AllowSelfRegistration.Should().BeTrue();
        result.ProfileSettings.MinimumSkillsCount.Should().Be(3);
        result.ScreeningSettings.AutoAdvanceThreshold.Should().Be(70.0);
        result.MatchingSettings.ScreeningWeight.Should().Be(100);
        result.AssessmentSettings.TimeLimitMinutes.Should().Be(60);
        result.NotificationSettings.NotifyOnNewApplication.Should().BeTrue();
    }
}

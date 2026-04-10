using Jobsite.Modules.Admin.Application.EventHandlers;
using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.Modules.Admin.Domain.Entities;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Admin;

public sealed class TenantProvisionedHandlerTests
{
    private readonly ICompanySettingsRepository _settingsRepo = Substitute.For<ICompanySettingsRepository>();
    private readonly IAuditLogService _auditLogService = Substitute.For<IAuditLogService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<TenantProvisionedHandler> _logger = Substitute.For<ILogger<TenantProvisionedHandler>>();
    private readonly TenantProvisionedHandler _sut;

    public TenantProvisionedHandlerTests()
    {
        _sut = new TenantProvisionedHandler(_settingsRepo, _auditLogService, _unitOfWork, _logger);
    }

    [Fact]
    public async Task Handle_SeedsDefaultCompanySettings()
    {
        // Arrange
        TenantProvisionedEvent @event = new()
        {
            TenantId = Guid.NewGuid(),
            TenantName = "Test Corp",
            OwnerEmail = "owner@test.com",
            ConnectionString = "Host=localhost;Database=test",
            ProvisionedAt = DateTime.UtcNow
        };

        // Act
        await _sut.HandleAsync(@event, CancellationToken.None);

        // Assert
        _settingsRepo.Received(1).Add(Arg.Is<CompanySettings>(s =>
            s.DefaultTimezone == "UTC" &&
            s.DefaultCurrency == "USD" &&
            s.AuthSettings != null &&
            s.ProfileSettings != null &&
            s.ScreeningSettings != null &&
            s.MatchingSettings != null &&
            s.AssessmentSettings != null &&
            s.NotificationSettings != null));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CreatesAuditLogEntry()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        TenantProvisionedEvent @event = new()
        {
            TenantId = tenantId,
            TenantName = "Test Corp",
            OwnerEmail = "owner@test.com",
            ConnectionString = "Host=localhost;Database=test",
            ProvisionedAt = DateTime.UtcNow
        };

        // Act
        await _sut.HandleAsync(@event, CancellationToken.None);

        // Assert
        await _auditLogService.Received(1).LogAsync(
            Guid.Empty, "system", "System",
            AuditAction.TenantProvisioned, AuditEntityType.Tenant,
            tenantId, Arg.Any<object?>(), null, null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ScreeningSettingsContainDefaultEvaluationCriteria()
    {
        // Arrange
        TenantProvisionedEvent @event = new()
        {
            TenantId = Guid.NewGuid(),
            TenantName = "Test Corp",
            OwnerEmail = "owner@test.com",
            ConnectionString = "Host=localhost;Database=test",
            ProvisionedAt = DateTime.UtcNow
        };

        // Act
        await _sut.HandleAsync(@event, CancellationToken.None);

        // Assert
        _settingsRepo.Received(1).Add(Arg.Is<CompanySettings>(s =>
            s.ScreeningSettings.Contains("Skills Match") &&
            s.ScreeningSettings.Contains("Experience Level") &&
            s.ScreeningSettings.Contains("Education") &&
            s.ScreeningSettings.Contains("Resume Quality")));
    }
}

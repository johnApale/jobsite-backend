using FluentAssertions;
using Jobsite.Modules.Admin.Application.DTOs;
using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Application.Services;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.Modules.Admin.Domain.Entities;
using Jobsite.SharedKernel.Persistence;
using NSubstitute;

namespace Jobsite.UnitTests.Admin;

public sealed class AuditLogServiceTests
{
    private readonly IAuditLogRepository _auditLogRepo = Substitute.For<IAuditLogRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly AuditLogService _sut;

    public AuditLogServiceTests()
    {
        _sut = new AuditLogService(_auditLogRepo, _unitOfWork);
    }

    [Fact]
    public async Task LogAsync_WithValidData_CreatesAuditLog()
    {
        // Arrange
        Guid actorId = Guid.NewGuid();

        // Act
        await _sut.LogAsync(
            actorId, "admin@example.com", "AgencyAdmin",
            AuditAction.SettingsUpdated, AuditEntityType.CompanySettings,
            Guid.NewGuid(), new { field = "timezone" },
            "127.0.0.1", "TestAgent/1.0", CancellationToken.None);

        // Assert
        _auditLogRepo.Received(1).Add(Arg.Is<AuditLog>(log =>
            log.ActorId == actorId &&
            log.ActorEmail == "admin@example.com" &&
            log.ActorRole == "AgencyAdmin" &&
            log.Action == AuditAction.SettingsUpdated &&
            log.EntityType == AuditEntityType.CompanySettings &&
            log.Details != null));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogAsync_WithNullEntityId_CreatesLogWithNullEntityId()
    {
        // Arrange & Act
        await _sut.LogAsync(
            Guid.NewGuid(), "system@example.com", "System",
            AuditAction.TenantProvisioned, AuditEntityType.Tenant,
            null, null, null, null, CancellationToken.None);

        // Assert
        _auditLogRepo.Received(1).Add(Arg.Is<AuditLog>(log =>
            log.EntityId == null && log.Details == null));
    }

    [Fact]
    public async Task LogAsync_WithDetails_SerializesAsJson()
    {
        // Arrange & Act
        await _sut.LogAsync(
            Guid.NewGuid(), "admin@example.com", "AgencyAdmin",
            AuditAction.SettingsUpdated, AuditEntityType.CompanySettings,
            Guid.NewGuid(), new { changed_fields = new[] { "timezone", "currency" } },
            null, null, CancellationToken.None);

        // Assert
        _auditLogRepo.Received(1).Add(Arg.Is<AuditLog>(log =>
            log.Details != null && log.Details.Contains("changed_fields")));
    }

    [Fact]
    public async Task LogAsync_SetsPerformedAtToUtcNow()
    {
        // Arrange
        DateTime before = DateTime.UtcNow;

        // Act
        await _sut.LogAsync(
            Guid.NewGuid(), "admin@example.com", "AgencyAdmin",
            AuditAction.SettingsUpdated, AuditEntityType.CompanySettings,
            null, null, null, null, CancellationToken.None);

        DateTime after = DateTime.UtcNow;

        // Assert
        _auditLogRepo.Received(1).Add(Arg.Is<AuditLog>(log =>
            log.PerformedAt >= before && log.PerformedAt <= after));
    }

    [Fact]
    public async Task QueryAsync_DelegatesToRepository()
    {
        // Arrange
        AuditLogQueryParameters parameters = new() { Action = AuditAction.UserRegistered, PageSize = 10 };
        AuditLogPageResponse expectedResponse = new() { Items = [], NextCursor = null };
        _auditLogRepo.GetPageAsync(parameters, Arg.Any<CancellationToken>()).Returns(expectedResponse);

        // Act
        AuditLogPageResponse result = await _sut.QueryAsync(parameters, CancellationToken.None);

        // Assert
        result.Should().BeSameAs(expectedResponse);
        await _auditLogRepo.Received(1).GetPageAsync(parameters, Arg.Any<CancellationToken>());
    }
}

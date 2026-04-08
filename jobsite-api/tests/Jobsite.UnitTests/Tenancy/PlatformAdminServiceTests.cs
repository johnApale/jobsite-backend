using FluentAssertions;
using Jobsite.Modules.Tenancy.Application.DTOs;
using Jobsite.Modules.Tenancy.Application.Interfaces;
using Jobsite.Modules.Tenancy.Application.Services;
using Jobsite.Modules.Tenancy.Domain.Constants;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using NSubstitute;

namespace Jobsite.UnitTests.Tenancy;

/// <summary>Tests for PlatformAdminService application service.</summary>
public sealed class PlatformAdminServiceTests
{
    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PlatformAdminService _sut;

    public PlatformAdminServiceTests()
    {
        _tenantRepository = Substitute.For<ITenantRepository>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _sut = new PlatformAdminService(_tenantRepository, _unitOfWork);
    }

    // --- GetTenantsAsync ---

    [Fact]
    public async Task GetTenantsAsync_ReturnsPaginatedList()
    {
        // Arrange
        Tenant tenant1 = TestData.CreateTenant(name: "Acme Corp", subdomain: "acme");
        Tenant tenant2 = TestData.CreateTenant(name: "Beta Inc", subdomain: "beta");
        List<Tenant> tenants = [tenant1, tenant2];

        _tenantRepository.GetListAsync(null, null, null, 20, Arg.Any<CancellationToken>())
            .Returns((tenants, false));

        TenantQueryParameters parameters = new() { PageSize = 20 };

        // Act
        TenantListResponse result = await _sut.GetTenantsAsync(parameters, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.HasMore.Should().BeFalse();
        result.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task GetTenantsAsync_WithMoreResults_ReturnsNextCursor()
    {
        // Arrange
        Tenant tenant1 = TestData.CreateTenant(name: "Acme Corp", subdomain: "acme");
        List<Tenant> tenants = [tenant1];

        _tenantRepository.GetListAsync(null, null, null, 1, Arg.Any<CancellationToken>())
            .Returns((tenants, true));

        TenantQueryParameters parameters = new() { PageSize = 1 };

        // Act
        TenantListResponse result = await _sut.GetTenantsAsync(parameters, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.HasMore.Should().BeTrue();
        result.NextCursor.Should().Be(tenant1.Id.ToString());
    }

    [Fact]
    public async Task GetTenantsAsync_WithStatusFilter_PassesFilterToRepository()
    {
        // Arrange
        _tenantRepository.GetListAsync("Active", null, null, 20, Arg.Any<CancellationToken>())
            .Returns((new List<Tenant>(), false));

        TenantQueryParameters parameters = new() { Status = "Active", PageSize = 20 };

        // Act
        await _sut.GetTenantsAsync(parameters, CancellationToken.None);

        // Assert
        await _tenantRepository.Received(1).GetListAsync("Active", null, null, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetTenantsAsync_WithSearchFilter_PassesSearchToRepository()
    {
        // Arrange
        _tenantRepository.GetListAsync(null, "acme", null, 20, Arg.Any<CancellationToken>())
            .Returns((new List<Tenant>(), false));

        TenantQueryParameters parameters = new() { Search = "acme", PageSize = 20 };

        // Act
        await _sut.GetTenantsAsync(parameters, CancellationToken.None);

        // Assert
        await _tenantRepository.Received(1).GetListAsync(null, "acme", null, 20, Arg.Any<CancellationToken>());
    }

    // --- GetTenantByIdAsync ---

    [Fact]
    public async Task GetTenantByIdAsync_ExistingTenant_ReturnsTenantResponse()
    {
        // Arrange
        Tenant tenant = TestData.CreateTenant();
        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Act
        TenantResponse result = await _sut.GetTenantByIdAsync(tenant.Id, CancellationToken.None);

        // Assert
        result.Id.Should().Be(tenant.Id);
        result.Name.Should().Be(tenant.Name);
        result.Status.Should().Be(TenantStatus.Active);
    }

    [Fact]
    public async Task GetTenantByIdAsync_NonExistentId_ThrowsTenantNotFound()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        _tenantRepository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        // Act
        Func<Task> act = async () => await _sut.GetTenantByIdAsync(id, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("TENANT_NOT_FOUND");
    }

    // --- SuspendTenantAsync ---

    [Fact]
    public async Task SuspendTenantAsync_ActiveTenant_SuspendsSuccessfully()
    {
        // Arrange
        Tenant tenant = TestData.CreateTenant(status: TenantStatus.Active);
        _tenantRepository.GetByIdForUpdateAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Act
        TenantResponse result = await _sut.SuspendTenantAsync(tenant.Id, CancellationToken.None);

        // Assert
        result.Status.Should().Be(TenantStatus.Suspended);
        result.DeactivatedAt.Should().NotBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuspendTenantAsync_NonExistentTenant_ThrowsTenantNotFound()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        _tenantRepository.GetByIdForUpdateAsync(id, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        // Act
        Func<Task> act = async () => await _sut.SuspendTenantAsync(id, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("TENANT_NOT_FOUND");
    }

    [Fact]
    public async Task SuspendTenantAsync_AlreadySuspendedTenant_ThrowsInvalidRequest()
    {
        // Arrange
        Tenant tenant = TestData.CreateTenant(status: TenantStatus.Suspended);
        _tenantRepository.GetByIdForUpdateAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Act
        Func<Task> act = async () => await _sut.SuspendTenantAsync(tenant.Id, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_REQUEST");
    }

    [Fact]
    public async Task SuspendTenantAsync_ProvisioningTenant_ThrowsInvalidRequest()
    {
        // Arrange
        Tenant tenant = TestData.CreateTenant(status: TenantStatus.Provisioning);
        _tenantRepository.GetByIdForUpdateAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Act
        Func<Task> act = async () => await _sut.SuspendTenantAsync(tenant.Id, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_REQUEST");
    }

    // --- ReactivateTenantAsync ---

    [Fact]
    public async Task ReactivateTenantAsync_SuspendedTenant_ReactivatesSuccessfully()
    {
        // Arrange
        Tenant tenant = TestData.CreateTenant(status: TenantStatus.Suspended);
        tenant.DeactivatedAt = DateTime.UtcNow.AddDays(-1);
        _tenantRepository.GetByIdForUpdateAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Act
        TenantResponse result = await _sut.ReactivateTenantAsync(tenant.Id, CancellationToken.None);

        // Assert
        result.Status.Should().Be(TenantStatus.Active);
        result.DeactivatedAt.Should().BeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReactivateTenantAsync_NonExistentTenant_ThrowsTenantNotFound()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        _tenantRepository.GetByIdForUpdateAsync(id, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        // Act
        Func<Task> act = async () => await _sut.ReactivateTenantAsync(id, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("TENANT_NOT_FOUND");
    }

    [Fact]
    public async Task ReactivateTenantAsync_ActiveTenant_ThrowsInvalidRequest()
    {
        // Arrange
        Tenant tenant = TestData.CreateTenant(status: TenantStatus.Active);
        _tenantRepository.GetByIdForUpdateAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Act
        Func<Task> act = async () => await _sut.ReactivateTenantAsync(tenant.Id, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_REQUEST");
    }

    // --- Branding mapping ---

    [Fact]
    public async Task GetTenantByIdAsync_WithBranding_MapsBrandingToResponse()
    {
        // Arrange
        Tenant tenant = TestData.CreateTenant();
        tenant.Branding = TestData.CreateTenantBranding(tenantId: tenant.Id);
        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Act
        TenantResponse result = await _sut.GetTenantByIdAsync(tenant.Id, CancellationToken.None);

        // Assert
        result.Branding.Should().NotBeNull();
        result.Branding!.LogoUrl.Should().Be(tenant.Branding.LogoUrl);
        result.Branding.PrimaryColor.Should().Be(tenant.Branding.PrimaryColor);
    }

    [Fact]
    public async Task GetTenantByIdAsync_WithNullBranding_ReturnsNullBranding()
    {
        // Arrange
        Tenant tenant = TestData.CreateTenant();
        tenant.Branding = null;
        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Act
        TenantResponse result = await _sut.GetTenantByIdAsync(tenant.Id, CancellationToken.None);

        // Assert
        result.Branding.Should().BeNull();
    }
}

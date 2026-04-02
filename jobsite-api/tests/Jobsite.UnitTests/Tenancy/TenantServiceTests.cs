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

/// <summary>Tests for TenantService application service.</summary>
public sealed class TenantServiceTests
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantProvisioner _tenantProvisioner;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TenantService _sut;

    public TenantServiceTests()
    {
        _tenantRepository = Substitute.For<ITenantRepository>();
        _tenantProvisioner = Substitute.For<ITenantProvisioner>();
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _sut = new TenantService(_tenantRepository, _tenantProvisioner, _unitOfWork);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingTenant_ReturnsTenantResponse()
    {
        // Arrange
        Tenant tenant = TestData.CreateTenant();
        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Act
        TenantResponse result = await _sut.GetByIdAsync(tenant.Id, CancellationToken.None);

        // Assert
        result.Id.Should().Be(tenant.Id);
        result.Name.Should().Be(tenant.Name);
        result.Subdomain.Should().Be(tenant.Subdomain);
        result.Status.Should().Be(tenant.Status);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ThrowsTenantNotFound()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        _tenantRepository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        // Act
        Func<Task> act = async () => await _sut.GetByIdAsync(id, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("TENANT_NOT_FOUND");
        error.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_CreatesProvisioningTenant()
    {
        // Arrange
        RegisterTenantRequest request = TestData.CreateRegisterTenantRequest();
        _tenantRepository.SubdomainExistsAsync(request.Subdomain, Arg.Any<CancellationToken>())
            .Returns(false);
        _tenantRepository.NameExistsAsync(request.Name, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        TenantResponse result = await _sut.RegisterAsync(request, CancellationToken.None);

        // Assert
        result.Name.Should().Be(request.Name);
        result.Subdomain.Should().Be(request.Subdomain.ToLowerInvariant());
        result.Status.Should().Be(TenantStatus.Provisioning);
        result.OwnerName.Should().Be(request.OwnerName);
        result.OwnerEmail.Should().Be(request.OwnerEmail);
        _tenantRepository.Received(1).Add(Arg.Any<Tenant>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_DuplicateSubdomain_ThrowsInvalidRequest()
    {
        // Arrange
        RegisterTenantRequest request = TestData.CreateRegisterTenantRequest(subdomain: "taken");
        _tenantRepository.SubdomainExistsAsync("taken", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        Func<Task> act = async () => await _sut.RegisterAsync(request, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_REQUEST");
        error.Message.Should().Contain("taken");
    }

    [Fact]
    public async Task RegisterAsync_DuplicateName_ThrowsInvalidRequest()
    {
        // Arrange
        RegisterTenantRequest request = TestData.CreateRegisterTenantRequest(name: "Existing Corp");
        _tenantRepository.SubdomainExistsAsync(request.Subdomain, Arg.Any<CancellationToken>())
            .Returns(false);
        _tenantRepository.NameExistsAsync("Existing Corp", Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        Func<Task> act = async () => await _sut.RegisterAsync(request, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_REQUEST");
        error.Message.Should().Contain("Existing Corp");
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_SubdomainIsLowercased()
    {
        // Arrange
        RegisterTenantRequest request = TestData.CreateRegisterTenantRequest(subdomain: "AcMe");
        _tenantRepository.SubdomainExistsAsync("AcMe", Arg.Any<CancellationToken>())
            .Returns(false);
        _tenantRepository.NameExistsAsync(request.Name, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        TenantResponse result = await _sut.RegisterAsync(request, CancellationToken.None);

        // Assert
        result.Subdomain.Should().Be("acme");
    }

    [Fact]
    public async Task GetByIdAsync_TenantWithBranding_IncludesBrandingInResponse()
    {
        // Arrange
        Tenant tenant = TestData.CreateTenant();
        TenantBranding branding = TestData.CreateTenantBranding(tenant.Id);
        branding.Tenant = tenant;
        tenant.Branding = branding;
        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Act
        TenantResponse result = await _sut.GetByIdAsync(tenant.Id, CancellationToken.None);

        // Assert
        result.Branding.Should().NotBeNull();
        result.Branding!.PrimaryColor.Should().Be("#1A73E8");
        result.Branding.Tagline.Should().Be("Test tagline");
    }

    [Fact]
    public async Task GetByIdAsync_TenantWithoutBranding_BrandingIsNull()
    {
        // Arrange
        Tenant tenant = TestData.CreateTenant();
        _tenantRepository.GetByIdAsync(tenant.Id, Arg.Any<CancellationToken>())
            .Returns(tenant);

        // Act
        TenantResponse result = await _sut.GetByIdAsync(tenant.Id, CancellationToken.None);

        // Assert
        result.Branding.Should().BeNull();
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_TriggersProvisioning()
    {
        // Arrange
        RegisterTenantRequest request = TestData.CreateRegisterTenantRequest();
        _tenantRepository.SubdomainExistsAsync(request.Subdomain, Arg.Any<CancellationToken>())
            .Returns(false);
        _tenantRepository.NameExistsAsync(request.Name, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.RegisterAsync(request, CancellationToken.None);

        // Assert
        await _tenantProvisioner.Received(1).ProvisionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_ProvisioningCalledAfterSave()
    {
        // Arrange
        RegisterTenantRequest request = TestData.CreateRegisterTenantRequest();
        _tenantRepository.SubdomainExistsAsync(request.Subdomain, Arg.Any<CancellationToken>())
            .Returns(false);
        _tenantRepository.NameExistsAsync(request.Name, Arg.Any<CancellationToken>())
            .Returns(false);

        bool savedBeforeProvisioning = false;
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(0))
            .AndDoes(_ => savedBeforeProvisioning = true);
        _tenantProvisioner.ProvisionAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                savedBeforeProvisioning.Should().BeTrue("provisioning should occur after saving the tenant");
                return Task.CompletedTask;
            });

        // Act
        await _sut.RegisterAsync(request, CancellationToken.None);

        // Assert
        savedBeforeProvisioning.Should().BeTrue();
    }
}

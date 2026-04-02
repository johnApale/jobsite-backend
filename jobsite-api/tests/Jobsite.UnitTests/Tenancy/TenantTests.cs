using FluentAssertions;
using Jobsite.Modules.Tenancy.Domain.Constants;
using Jobsite.Modules.Tenancy.Domain.Entities;

namespace Jobsite.UnitTests.Tenancy;

/// <summary>Tests for Tenant entity.</summary>
public sealed class TenantTests
{
    [Fact]
    public void Tenant_InheritsAggregateRoot_HasDomainEvents()
    {
        // Arrange & Act
        Tenant tenant = TestData.CreateTenant();

        // Assert
        tenant.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Tenant_NewInstance_HasExpectedDefaults()
    {
        // Arrange & Act
        Tenant tenant = TestData.CreateTenant();

        // Assert
        tenant.Name.Should().Be("Acme Corp");
        tenant.Subdomain.Should().Be("acme");
        tenant.Status.Should().Be(TenantStatus.Active);
        tenant.ProvisionedAt.Should().BeNull();
        tenant.DeactivatedAt.Should().BeNull();
        tenant.Branding.Should().BeNull();
    }

    [Fact]
    public void Tenant_WithBranding_NavigationPropertySet()
    {
        // Arrange
        Tenant tenant = TestData.CreateTenant();
        TenantBranding branding = TestData.CreateTenantBranding(tenant.Id);
        branding.Tenant = tenant;

        // Act
        tenant.Branding = branding;

        // Assert
        tenant.Branding.Should().NotBeNull();
        tenant.Branding!.TenantId.Should().Be(tenant.Id);
        tenant.Branding.PrimaryColor.Should().Be("#1A73E8");
    }
}

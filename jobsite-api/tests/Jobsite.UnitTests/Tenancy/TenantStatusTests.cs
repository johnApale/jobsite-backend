using FluentAssertions;
using Jobsite.Modules.Tenancy.Domain.Constants;

namespace Jobsite.UnitTests.Tenancy;

/// <summary>Tests for TenantStatus constants and validation.</summary>
public sealed class TenantStatusTests
{
    [Fact]
    public void IsValid_Provisioning_ReturnsTrue()
    {
        // Arrange & Act
        bool result = TenantStatus.IsValid("Provisioning");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_Active_ReturnsTrue()
    {
        // Arrange & Act
        bool result = TenantStatus.IsValid("Active");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_Suspended_ReturnsTrue()
    {
        // Arrange & Act
        bool result = TenantStatus.IsValid("Suspended");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_Deactivated_ReturnsTrue()
    {
        // Arrange & Act
        bool result = TenantStatus.IsValid("Deactivated");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_UnknownStatus_ReturnsFalse()
    {
        // Arrange & Act
        bool result = TenantStatus.IsValid("Deleted");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_LowercaseStatus_ReturnsFalse()
    {
        // Arrange & Act — status values are PascalCase per convention
        bool result = TenantStatus.IsValid("active");

        // Assert
        result.Should().BeFalse();
    }
}

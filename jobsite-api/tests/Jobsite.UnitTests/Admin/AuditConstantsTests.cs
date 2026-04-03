using FluentAssertions;
using Jobsite.Modules.Admin.Domain.Constants;

namespace Jobsite.UnitTests.Admin;

public sealed class AuditConstantsTests
{
    [Theory]
    [InlineData("UserRegistered")]
    [InlineData("SettingsUpdated")]
    [InlineData("ApplicationSubmitted")]
    [InlineData("CvScreeningCompleted")]
    [InlineData("CandidateShortlisted")]
    [InlineData("FinalInterviewScheduled")]
    [InlineData("OfferExtended")]
    [InlineData("TenantProvisioned")]
    public void AuditAction_IsValid_WithKnownAction_ReturnsTrue(string action)
    {
        // Act & Assert
        AuditAction.IsValid(action).Should().BeTrue();
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData("userregistered")] // case-sensitive
    public void AuditAction_IsValid_WithUnknownAction_ReturnsFalse(string action)
    {
        // Act & Assert
        AuditAction.IsValid(action).Should().BeFalse();
    }

    [Theory]
    [InlineData("User")]
    [InlineData("CompanySettings")]
    [InlineData("Application")]
    [InlineData("ScreeningResult")]
    [InlineData("FinalInterview")]
    [InlineData("JobOffer")]
    [InlineData("Tenant")]
    public void AuditEntityType_IsValid_WithKnownType_ReturnsTrue(string entityType)
    {
        // Act & Assert
        AuditEntityType.IsValid(entityType).Should().BeTrue();
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData("user")] // case-sensitive
    public void AuditEntityType_IsValid_WithUnknownType_ReturnsFalse(string entityType)
    {
        // Act & Assert
        AuditEntityType.IsValid(entityType).Should().BeFalse();
    }
}

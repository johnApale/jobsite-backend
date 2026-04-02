using FluentAssertions;
using Jobsite.Modules.Auth.Domain.Constants;

namespace Jobsite.UnitTests.Auth;

/// <summary>
/// Tests for Auth domain constants (UserRole, UserStatus, ExternalLoginProvider).
/// </summary>
public sealed class AuthConstantsTests
{
    // ── UserRole ─────────────────────────────────────────────────────────

    [Fact]
    public void UserRole_IsValid_ValidRole_ReturnsTrue()
    {
        // Arrange & Act & Assert
        UserRole.IsValid(UserRole.Applicant).Should().BeTrue();
        UserRole.IsValid(UserRole.Recruiter).Should().BeTrue();
        UserRole.IsValid(UserRole.HiringManager).Should().BeTrue();
        UserRole.IsValid(UserRole.Interviewer).Should().BeTrue();
        UserRole.IsValid(UserRole.AgencyAdmin).Should().BeTrue();
    }

    [Fact]
    public void UserRole_IsValid_InvalidRole_ReturnsFalse()
    {
        // Arrange & Act & Assert
        UserRole.IsValid("Unknown").Should().BeFalse();
        UserRole.IsValid("").Should().BeFalse();
        UserRole.IsValid("applicant").Should().BeFalse(); // case-sensitive
    }

    // ── UserStatus ───────────────────────────────────────────────────────

    [Fact]
    public void UserStatus_IsValid_ValidStatus_ReturnsTrue()
    {
        // Arrange & Act & Assert
        UserStatus.IsValid(UserStatus.Active).Should().BeTrue();
        UserStatus.IsValid(UserStatus.Invited).Should().BeTrue();
        UserStatus.IsValid(UserStatus.Deactivated).Should().BeTrue();
    }

    [Fact]
    public void UserStatus_IsValid_InvalidStatus_ReturnsFalse()
    {
        // Arrange & Act & Assert
        UserStatus.IsValid("Suspended").Should().BeFalse();
        UserStatus.IsValid("").Should().BeFalse();
    }

    // ── ExternalLoginProvider ────────────────────────────────────────────

    [Fact]
    public void ExternalLoginProvider_IsValid_ValidProvider_ReturnsTrue()
    {
        // Arrange & Act & Assert
        ExternalLoginProvider.IsValid(ExternalLoginProvider.Google).Should().BeTrue();
        ExternalLoginProvider.IsValid(ExternalLoginProvider.Apple).Should().BeTrue();
        ExternalLoginProvider.IsValid(ExternalLoginProvider.Facebook).Should().BeTrue();
    }

    [Fact]
    public void ExternalLoginProvider_IsValid_InvalidProvider_ReturnsFalse()
    {
        // Arrange & Act & Assert
        ExternalLoginProvider.IsValid("Twitter").Should().BeFalse();
        ExternalLoginProvider.IsValid("").Should().BeFalse();
        ExternalLoginProvider.IsValid("google").Should().BeFalse(); // case-sensitive
    }
}

using FluentAssertions;
using Jobsite.Modules.Auth.Domain.Entities;

namespace Jobsite.UnitTests.Auth;

/// <summary>
/// Tests for <see cref="RefreshToken"/> entity behavior.
/// </summary>
public sealed class RefreshTokenTests
{
    [Fact]
    public void Revoke_SetsIsRevokedAndRevokedAt()
    {
        // Arrange
        RefreshToken token = TestData.CreateRefreshToken();

        // Act
        token.Revoke();

        // Assert
        token.IsRevoked.Should().BeTrue();
        token.RevokedAt.Should().NotBeNull();
        token.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void IsExpired_WhenExpiresAtInPast_ReturnsTrue()
    {
        // Arrange
        RefreshToken token = TestData.CreateRefreshToken(expiresAt: DateTime.UtcNow.AddMinutes(-1));

        // Act
        bool isExpired = token.IsExpired;

        // Assert
        isExpired.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenExpiresAtInFuture_ReturnsFalse()
    {
        // Arrange
        RefreshToken token = TestData.CreateRefreshToken(expiresAt: DateTime.UtcNow.AddDays(30));

        // Act
        bool isExpired = token.IsExpired;

        // Assert
        isExpired.Should().BeFalse();
    }

    [Fact]
    public void CreateRefreshToken_DefaultsIsRevokedToFalse()
    {
        // Arrange & Act
        RefreshToken token = TestData.CreateRefreshToken();

        // Assert
        token.IsRevoked.Should().BeFalse();
        token.RevokedAt.Should().BeNull();
    }
}

using FluentAssertions;
using Jobsite.Modules.Auth.Infrastructure.Security;

namespace Jobsite.UnitTests.Auth;

/// <summary>
/// Tests for <see cref="PasswordHasher"/> BCrypt implementation.
/// </summary>
public sealed class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void HashPassword_ReturnsNonEmptyHash()
    {
        // Arrange
        string password = "TestPassword123!";

        // Act
        string hash = _hasher.HashPassword(password);

        // Assert
        hash.Should().NotBeNullOrWhiteSpace();
        hash.Should().StartWith("$2a$");
    }

    [Fact]
    public void HashPassword_DifferentInputs_ProduceDifferentHashes()
    {
        // Arrange & Act
        string hash1 = _hasher.HashPassword("password1");
        string hash2 = _hasher.HashPassword("password2");

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void HashPassword_SameInput_ProducesDifferentHashes_DueToSalt()
    {
        // Arrange & Act
        string hash1 = _hasher.HashPassword("samePassword");
        string hash2 = _hasher.HashPassword("samePassword");

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        // Arrange
        string password = "SecurePass123!";
        string hash = _hasher.HashPassword(password);

        // Act
        bool result = _hasher.VerifyPassword(password, hash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        // Arrange
        string hash = _hasher.HashPassword("CorrectPassword");

        // Act
        bool result = _hasher.VerifyPassword("WrongPassword", hash);

        // Assert
        result.Should().BeFalse();
    }
}

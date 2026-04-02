using FluentAssertions;
using Jobsite.Modules.Auth.Domain.Constants;
using Jobsite.Modules.Auth.Domain.Entities;
using Jobsite.SharedKernel.Events;

namespace Jobsite.UnitTests.Auth;

/// <summary>
/// Tests for <see cref="User"/> aggregate root behavior.
/// </summary>
public sealed class UserTests
{
    [Fact]
    public void CreateUser_WithValidData_SetsProperties()
    {
        // Arrange & Act
        User user = TestData.CreateUser(email: "alice@acme.com", role: UserRole.Recruiter);

        // Assert
        user.Email.Should().Be("alice@acme.com");
        user.Role.Should().Be(UserRole.Recruiter);
        user.Status.Should().Be(UserStatus.Active);
        user.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Raise_DomainEvent_IsCapturedInDomainEvents()
    {
        // Arrange
        User user = TestData.CreateUser();
        UserRegisteredEvent evt = new()
        {
            UserId = user.Id,
            Email = user.Email,
            Role = user.Role,
            RegisteredAt = DateTime.UtcNow
        };

        // Act
        user.Raise(evt);

        // Assert
        user.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<UserRegisteredEvent>();
    }

    [Fact]
    public void ExternalLogins_DefaultsToEmptyList()
    {
        // Arrange & Act
        User user = TestData.CreateUser();

        // Assert
        user.ExternalLogins.Should().BeEmpty();
    }

    [Fact]
    public void RefreshTokens_DefaultsToEmptyList()
    {
        // Arrange & Act
        User user = TestData.CreateUser();

        // Assert
        user.RefreshTokens.Should().BeEmpty();
    }
}

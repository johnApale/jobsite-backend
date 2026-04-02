using FluentAssertions;
using Jobsite.Modules.Auth.Domain.Constants;
using Jobsite.Modules.Auth.Domain.Entities;
using Jobsite.Modules.Auth.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.IntegrationTests.Auth;

/// <summary>
/// Integration tests for UserRepository against a real PostgreSQL container.
/// Validates EF Core configurations, snake_case mapping, CHECK constraints,
/// unique indexes, and query behavior.
/// </summary>
[Collection("Auth")]
public sealed class UserRepositoryTests : IAsyncLifetime
{
    private readonly AuthIntegrationFixture _fixture;
    private readonly UserRepository _sut;

    public UserRepositoryTests(AuthIntegrationFixture fixture)
    {
        _fixture = fixture;
        _sut = new UserRepository(fixture.DbContext);
    }

    public Task InitializeAsync() => _fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Add_ValidUser_PersistsToDatabase()
    {
        // Arrange
        User user = IntegrationTestData.CreateUser(
            email: "persist@test.com",
            firstName: "Persist",
            lastName: "Corp");

        // Act
        _sut.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        User? persisted = await _fixture.DbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == "persist@test.com");

        persisted.Should().NotBeNull();
        persisted!.FirstName.Should().Be("Persist");
        persisted.LastName.Should().Be("Corp");
        persisted.Id.Should().NotBe(Guid.Empty, "database should assign a UUID");
        persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        persisted.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task GetByIdAsync_ExistingUser_ReturnsUser()
    {
        // Arrange
        User user = IntegrationTestData.CreateUser(email: "byid@test.com");
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        User? result = await _sut.GetByIdAsync(user.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be("byid@test.com");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Arrange & Act
        User? result = await _sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByEmailAsync_ExistingEmail_ReturnsUser()
    {
        // Arrange
        User user = IntegrationTestData.CreateUser(email: "findme@test.com");
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        User? result = await _sut.GetByEmailAsync("findme@test.com", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be("findme@test.com");
    }

    [Fact]
    public async Task GetByEmailAsync_NonExistentEmail_ReturnsNull()
    {
        // Arrange & Act
        User? result = await _sut.GetByEmailAsync("noone@test.com", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByEmailForUpdateAsync_ExistingEmail_ReturnsTrackedUser()
    {
        // Arrange
        User user = IntegrationTestData.CreateUser(email: "tracked@test.com");
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        User? result = await _sut.GetByEmailForUpdateAsync("tracked@test.com", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be("tracked@test.com");

        // Verify it's tracked (not AsNoTracking)
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry? entry =
            _fixture.DbContext.ChangeTracker.Entries<User>()
                .FirstOrDefault(e => e.Entity.Id == result.Id);
        entry.Should().NotBeNull();
    }

    [Fact]
    public async Task EmailExistsAsync_ExistingEmail_ReturnsTrue()
    {
        // Arrange
        User user = IntegrationTestData.CreateUser(email: "exists@test.com");
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        bool result = await _sut.EmailExistsAsync("exists@test.com", CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task EmailExistsAsync_NonExistentEmail_ReturnsFalse()
    {
        // Arrange & Act
        bool result = await _sut.EmailExistsAsync("nope@test.com", CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetByExternalLoginAsync_ExistingProvider_ReturnsUserWithExternalLogins()
    {
        // Arrange
        User user = IntegrationTestData.CreateUser(email: "oauth@test.com");
        string subjectId = "google-subject-123";
        user.ExternalLogins.Add(new UserExternalLogin
        {
            Provider = ExternalLoginProvider.Google,
            ProviderSubjectId = subjectId,
            ProviderEmail = "oauth@test.com",
            LinkedAt = DateTime.UtcNow
        });
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        User? result = await _sut.GetByExternalLoginAsync(
            ExternalLoginProvider.Google, subjectId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Email.Should().Be("oauth@test.com");
        result.ExternalLogins.Should().HaveCount(1);
        result.ExternalLogins[0].ProviderSubjectId.Should().Be(subjectId);
    }

    [Fact]
    public async Task GetByExternalLoginAsync_NonExistentProvider_ReturnsNull()
    {
        // Arrange & Act
        User? result = await _sut.GetByExternalLoginAsync(
            ExternalLoginProvider.Google, "no-such-subject", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Add_DuplicateEmail_ThrowsDbUpdateException()
    {
        // Arrange
        User first = IntegrationTestData.CreateUser(email: "dup@test.com");
        _fixture.DbContext.Users.Add(first);
        await _fixture.DbContext.SaveChangesAsync();

        User second = IntegrationTestData.CreateUser(email: "dup@test.com");
        _fixture.DbContext.Users.Add(second);

        // Act
        Func<Task> act = async () => await _fixture.DbContext.SaveChangesAsync();

        // Assert — unique index on email should reject this
        await act.Should().ThrowAsync<DbUpdateException>();

        // Clean up the failed tracked entity
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Add_InvalidRole_ThrowsDbUpdateException()
    {
        // Arrange — "SuperAdmin" violates the CHECK constraint chk_users_role
        User user = IntegrationTestData.CreateUser(role: "SuperAdmin");
        _fixture.DbContext.Users.Add(user);

        // Act
        Func<Task> act = async () => await _fixture.DbContext.SaveChangesAsync();

        // Assert — CHECK constraint should reject this
        await act.Should().ThrowAsync<DbUpdateException>();

        // Clean up the failed tracked entity
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Add_InvalidStatus_ThrowsDbUpdateException()
    {
        // Arrange — "Banned" violates the CHECK constraint chk_users_status
        User user = IntegrationTestData.CreateUser(status: "Banned");
        _fixture.DbContext.Users.Add(user);

        // Act
        Func<Task> act = async () => await _fixture.DbContext.SaveChangesAsync();

        // Assert — CHECK constraint should reject this
        await act.Should().ThrowAsync<DbUpdateException>();

        // Clean up the failed tracked entity
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Add_AllValidRoles_PersistSuccessfully()
    {
        // Arrange & Act — ensure all valid roles pass CHECK constraint
        string[] validRoles =
        [
            UserRole.Applicant, UserRole.Recruiter, UserRole.HiringManager,
            UserRole.Interviewer, UserRole.AgencyAdmin
        ];

        foreach (string role in validRoles)
        {
            User user = IntegrationTestData.CreateUser(role: role);
            _fixture.DbContext.Users.Add(user);
        }

        Func<Task> act = async () => await _fixture.DbContext.SaveChangesAsync();

        // Assert — all valid roles should be accepted
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Add_AllValidStatuses_PersistSuccessfully()
    {
        // Arrange & Act — ensure all valid statuses pass CHECK constraint
        string[] validStatuses =
        [
            UserStatus.Active, UserStatus.Invited, UserStatus.Deactivated
        ];

        foreach (string status in validStatuses)
        {
            User user = IntegrationTestData.CreateUser(status: status);
            _fixture.DbContext.Users.Add(user);
        }

        Func<Task> act = async () => await _fixture.DbContext.SaveChangesAsync();

        // Assert — all valid statuses should be accepted
        await act.Should().NotThrowAsync();
    }
}

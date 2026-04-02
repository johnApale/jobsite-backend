using FluentAssertions;
using Jobsite.Modules.Auth.Domain.Entities;
using Jobsite.Modules.Auth.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.IntegrationTests.Auth;

/// <summary>
/// Integration tests for RefreshTokenRepository against a real PostgreSQL container.
/// Validates token persistence, hash lookups, family-based revocation, and unique constraints.
/// </summary>
[Collection("Auth")]
public sealed class RefreshTokenRepositoryTests : IAsyncLifetime
{
    private readonly AuthIntegrationFixture _fixture;
    private readonly RefreshTokenRepository _sut;

    public RefreshTokenRepositoryTests(AuthIntegrationFixture fixture)
    {
        _fixture = fixture;
        _sut = new RefreshTokenRepository(fixture.DbContext);
    }

    public Task InitializeAsync() => _fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Add_ValidToken_PersistsToDatabase()
    {
        // Arrange
        User user = IntegrationTestData.CreateUser(email: "tokenuser@test.com");
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        RefreshToken token = IntegrationTestData.CreateRefreshToken(
            userId: user.Id,
            tokenHash: "unique-hash-persist");

        // Act
        _sut.Add(token);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        RefreshToken? persisted = await _fixture.DbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == "unique-hash-persist");

        persisted.Should().NotBeNull();
        persisted!.UserId.Should().Be(user.Id);
        persisted.IsRevoked.Should().BeFalse();
        persisted.Id.Should().NotBe(Guid.Empty);
        persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task GetByTokenHashAsync_ExistingHash_ReturnsToken()
    {
        // Arrange
        User user = IntegrationTestData.CreateUser(email: "hashuser@test.com");
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        RefreshToken token = IntegrationTestData.CreateRefreshToken(
            userId: user.Id,
            tokenHash: "find-this-hash");
        _fixture.DbContext.RefreshTokens.Add(token);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        RefreshToken? result = await _sut.GetByTokenHashAsync("find-this-hash", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.TokenHash.Should().Be("find-this-hash");
        result.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task GetByTokenHashAsync_NonExistentHash_ReturnsNull()
    {
        // Arrange & Act
        RefreshToken? result = await _sut.GetByTokenHashAsync("no-such-hash", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RevokeFamilyAsync_MultipleFamilyTokens_RevokesAllInFamily()
    {
        // Arrange
        User user = IntegrationTestData.CreateUser(email: "familyuser@test.com");
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        Guid familyId = Guid.NewGuid();
        Guid otherFamilyId = Guid.NewGuid();

        RefreshToken token1 = IntegrationTestData.CreateRefreshToken(
            userId: user.Id, tokenHash: "family-hash-1", familyId: familyId);
        RefreshToken token2 = IntegrationTestData.CreateRefreshToken(
            userId: user.Id, tokenHash: "family-hash-2", familyId: familyId);
        RefreshToken otherToken = IntegrationTestData.CreateRefreshToken(
            userId: user.Id, tokenHash: "other-family-hash", familyId: otherFamilyId);

        _fixture.DbContext.RefreshTokens.AddRange(token1, token2, otherToken);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        await _sut.RevokeFamilyAsync(familyId, CancellationToken.None);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert — family tokens should be revoked
        RefreshToken? revoked1 = await _fixture.DbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == "family-hash-1");
        RefreshToken? revoked2 = await _fixture.DbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == "family-hash-2");
        RefreshToken? untouched = await _fixture.DbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == "other-family-hash");

        revoked1!.IsRevoked.Should().BeTrue("token in target family should be revoked");
        revoked1.RevokedAt.Should().NotBeNull();
        revoked2!.IsRevoked.Should().BeTrue("token in target family should be revoked");
        revoked2.RevokedAt.Should().NotBeNull();
        untouched!.IsRevoked.Should().BeFalse("token in other family should not be affected");
    }

    [Fact]
    public async Task RevokeFamilyAsync_AlreadyRevokedTokens_SkipsAlreadyRevoked()
    {
        // Arrange
        User user = IntegrationTestData.CreateUser(email: "alreadyrevoked@test.com");
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        Guid familyId = Guid.NewGuid();

        RefreshToken activeToken = IntegrationTestData.CreateRefreshToken(
            userId: user.Id, tokenHash: "active-token", familyId: familyId);
        RefreshToken revokedToken = IntegrationTestData.CreateRefreshToken(
            userId: user.Id, tokenHash: "revoked-token", familyId: familyId);
        revokedToken.Revoke();

        _fixture.DbContext.RefreshTokens.AddRange(activeToken, revokedToken);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        await _sut.RevokeFamilyAsync(familyId, CancellationToken.None);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert — only the active one should have been revoked in this operation
        RefreshToken? result = await _fixture.DbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == "active-token");

        result!.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task Add_DuplicateTokenHash_ThrowsDbUpdateException()
    {
        // Arrange
        User user = IntegrationTestData.CreateUser(email: "duphash@test.com");
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        RefreshToken first = IntegrationTestData.CreateRefreshToken(
            userId: user.Id, tokenHash: "same-hash");
        _fixture.DbContext.RefreshTokens.Add(first);
        await _fixture.DbContext.SaveChangesAsync();

        RefreshToken second = IntegrationTestData.CreateRefreshToken(
            userId: user.Id, tokenHash: "same-hash");
        _fixture.DbContext.RefreshTokens.Add(second);

        // Act
        Func<Task> act = async () => await _fixture.DbContext.SaveChangesAsync();

        // Assert — unique index on token_hash should reject this
        await act.Should().ThrowAsync<DbUpdateException>();

        // Clean up the failed tracked entity
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task CascadeDelete_WhenUserDeleted_DeletesRefreshTokens()
    {
        // Arrange
        User user = IntegrationTestData.CreateUser(email: "cascade@test.com");
        _fixture.DbContext.Users.Add(user);
        await _fixture.DbContext.SaveChangesAsync();

        RefreshToken token = IntegrationTestData.CreateRefreshToken(
            userId: user.Id, tokenHash: "cascade-token");
        _fixture.DbContext.RefreshTokens.Add(token);
        await _fixture.DbContext.SaveChangesAsync();

        // Act — delete the user
        User? trackedUser = await _fixture.DbContext.Users
            .FirstOrDefaultAsync(u => u.Id == user.Id);
        _fixture.DbContext.Users.Remove(trackedUser!);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert — refresh token should be cascade deleted
        RefreshToken? orphanedToken = await _fixture.DbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TokenHash == "cascade-token");

        orphanedToken.Should().BeNull("refresh tokens should be cascade deleted with user");
    }
}

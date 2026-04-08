using FluentAssertions;
using Jobsite.Modules.Profiles.Domain.Constants;
using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.Modules.Profiles.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.IntegrationTests.Profiles;

/// <summary>
/// Integration tests for ApplicantProfileRepository and ResumeRepository
/// against a real PostgreSQL container.
/// </summary>
[Collection("Profiles")]
public sealed class ProfilesRepositoryTests : IAsyncLifetime
{
    private readonly ProfilesIntegrationFixture _fixture;

    public ProfilesRepositoryTests(ProfilesIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ─── ApplicantProfileRepository ──────────────────────────────────────

    [Fact]
    public async Task GetByUserIdAsync_Exists_ReturnsProfile()
    {
        // Arrange
        ApplicantProfileRepository repo = new(_fixture.DbContext);
        Guid userId = Guid.NewGuid();

        ApplicantProfile profile = new()
        {
            Id = userId,
            FirstName = "Jane",
            LastName = "Doe",
            City = "London",
            Country = "UK"
        };
        repo.Add(profile);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        ApplicantProfile? found = await repo.GetByUserIdAsync(userId, CancellationToken.None);

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(userId);
        found.FirstName.Should().Be("Jane");
        found.City.Should().Be("London");
    }

    [Fact]
    public async Task GetByUserIdAsync_NotExists_ReturnsNull()
    {
        // Arrange
        ApplicantProfileRepository repo = new(_fixture.DbContext);

        // Act
        ApplicantProfile? found = await repo.GetByUserIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdForUpdateAsync_ReturnsTrackedEntity()
    {
        // Arrange
        ApplicantProfileRepository repo = new(_fixture.DbContext);
        Guid userId = Guid.NewGuid();

        ApplicantProfile profile = new()
        {
            Id = userId,
            FirstName = "Track",
            LastName = "Test"
        };
        repo.Add(profile);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        ApplicantProfile? tracked = await repo.GetByUserIdForUpdateAsync(userId, CancellationToken.None);

        // Assert — entity is tracked (can be mutated and saved)
        tracked.Should().NotBeNull();
        tracked!.City = "Updated City";
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        ApplicantProfile? updated = await repo.GetByUserIdAsync(userId, CancellationToken.None);
        updated!.City.Should().Be("Updated City");
    }

    [Fact]
    public async Task ExistsByUserIdAsync_TrueWhenExists()
    {
        // Arrange
        ApplicantProfileRepository repo = new(_fixture.DbContext);
        Guid userId = Guid.NewGuid();

        ApplicantProfile profile = new()
        {
            Id = userId,
            FirstName = "Exists",
            LastName = "Test"
        };
        repo.Add(profile);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        bool exists = await repo.ExistsByUserIdAsync(userId, CancellationToken.None);
        bool notExists = await repo.ExistsByUserIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        exists.Should().BeTrue();
        notExists.Should().BeFalse();
    }

    // ─── ResumeRepository ────────────────────────────────────────────────

    private async Task<Guid> SeedProfileAsync()
    {
        Guid userId = Guid.NewGuid();
        ApplicantProfile profile = new()
        {
            Id = userId,
            FirstName = "Resume",
            LastName = "Test"
        };
        _fixture.DbContext.ApplicantProfiles.Add(profile);
        await _fixture.DbContext.SaveChangesAsync();
        return userId;
    }

    [Fact]
    public async Task ResumeRepo_GetByIdAsync_Exists_ReturnsResume()
    {
        // Arrange
        Guid userId = await SeedProfileAsync();
        ResumeRepository repo = new(_fixture.DbContext);

        Resume resume = new()
        {
            UserId = userId,
            FileUrl = "https://blob.storage/r.pdf",
            OriginalFilename = "r.pdf",
            FileSizeBytes = 1024,
            FileType = FileType.Pdf
        };
        repo.Add(resume);
        await _fixture.DbContext.SaveChangesAsync();
        Guid resumeId = resume.Id;
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        Resume? found = await repo.GetByIdAsync(resumeId, CancellationToken.None);

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(resumeId);
        found.OriginalFilename.Should().Be("r.pdf");
    }

    [Fact]
    public async Task ResumeRepo_GetByIdAsync_NotExists_ReturnsNull()
    {
        // Arrange
        ResumeRepository repo = new(_fixture.DbContext);

        // Act
        Resume? found = await repo.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task ResumeRepo_GetByIdForUpdateAsync_ReturnsTrackedEntity()
    {
        // Arrange
        Guid userId = await SeedProfileAsync();
        ResumeRepository repo = new(_fixture.DbContext);

        Resume resume = new()
        {
            UserId = userId,
            FileUrl = "https://blob.storage/track.pdf",
            OriginalFilename = "track.pdf",
            FileSizeBytes = 1024,
            FileType = FileType.Pdf
        };
        repo.Add(resume);
        await _fixture.DbContext.SaveChangesAsync();
        Guid resumeId = resume.Id;
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        Resume? tracked = await repo.GetByIdForUpdateAsync(resumeId, CancellationToken.None);

        // Assert — entity is tracked (can be mutated and saved)
        tracked.Should().NotBeNull();
        tracked!.IsParsed = true;
        tracked.ParsedText = "Updated parsed text";
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        Resume? updated = await repo.GetByIdAsync(resumeId, CancellationToken.None);
        updated!.IsParsed.Should().BeTrue();
        updated.ParsedText.Should().Be("Updated parsed text");
    }

    [Fact]
    public async Task ResumeRepo_GetByUserIdAsync_ReturnsOrderedByCreatedAtDescending()
    {
        // Arrange
        Guid userId = await SeedProfileAsync();
        ResumeRepository repo = new(_fixture.DbContext);

        Resume older = new()
        {
            UserId = userId,
            FileUrl = "https://blob.storage/old.pdf",
            OriginalFilename = "old.pdf",
            FileSizeBytes = 1024,
            FileType = FileType.Pdf
        };
        repo.Add(older);
        await _fixture.DbContext.SaveChangesAsync();

        // Small delay to ensure distinct CreatedAt values
        await Task.Delay(50);

        Resume newer = new()
        {
            UserId = userId,
            FileUrl = "https://blob.storage/new.pdf",
            OriginalFilename = "new.pdf",
            FileSizeBytes = 2048,
            FileType = FileType.Pdf
        };
        repo.Add(newer);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        List<Resume> resumes = await repo.GetByUserIdAsync(userId, CancellationToken.None);

        // Assert — ordered by CreatedAt descending (newest first)
        resumes.Should().HaveCount(2);
        resumes[0].OriginalFilename.Should().Be("new.pdf");
        resumes[1].OriginalFilename.Should().Be("old.pdf");
    }

    [Fact]
    public async Task ResumeRepo_GetLatestByUserIdAsync_ReturnsLatestResume()
    {
        // Arrange
        Guid userId = await SeedProfileAsync();
        ResumeRepository repo = new(_fixture.DbContext);

        Resume notLatest = new()
        {
            UserId = userId,
            FileUrl = "https://blob.storage/old.pdf",
            OriginalFilename = "old.pdf",
            FileSizeBytes = 1024,
            FileType = FileType.Pdf,
            IsLatest = false
        };
        Resume latest = new()
        {
            UserId = userId,
            FileUrl = "https://blob.storage/latest.pdf",
            OriginalFilename = "latest.pdf",
            FileSizeBytes = 2048,
            FileType = FileType.Pdf,
            IsLatest = true
        };
        repo.Add(notLatest);
        repo.Add(latest);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        Resume? found = await repo.GetLatestByUserIdAsync(userId, CancellationToken.None);

        // Assert
        found.Should().NotBeNull();
        found!.OriginalFilename.Should().Be("latest.pdf");
        found.IsLatest.Should().BeTrue();
    }

    [Fact]
    public async Task ResumeRepo_GetLatestByUserIdAsync_NoLatest_ReturnsNull()
    {
        // Arrange
        Guid userId = await SeedProfileAsync();
        ResumeRepository repo = new(_fixture.DbContext);

        Resume resume = new()
        {
            UserId = userId,
            FileUrl = "https://blob.storage/old.pdf",
            OriginalFilename = "old.pdf",
            FileSizeBytes = 1024,
            FileType = FileType.Pdf,
            IsLatest = false
        };
        repo.Add(resume);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        Resume? found = await repo.GetLatestByUserIdAsync(userId, CancellationToken.None);

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task ResumeRepo_HasAnyByUserIdAsync_TrueWhenExists()
    {
        // Arrange
        Guid userId = await SeedProfileAsync();
        ResumeRepository repo = new(_fixture.DbContext);

        Resume resume = new()
        {
            UserId = userId,
            FileUrl = "https://blob.storage/any.pdf",
            OriginalFilename = "any.pdf",
            FileSizeBytes = 1024,
            FileType = FileType.Pdf
        };
        repo.Add(resume);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        bool exists = await repo.HasAnyByUserIdAsync(userId, CancellationToken.None);
        bool notExists = await repo.HasAnyByUserIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        exists.Should().BeTrue();
        notExists.Should().BeFalse();
    }

    [Fact]
    public async Task ResumeRepo_MarkPreviousAsNotLatestAsync_ClearsPreviousLatestFlag()
    {
        // Arrange
        Guid userId = await SeedProfileAsync();
        ResumeRepository repo = new(_fixture.DbContext);

        Resume latest1 = new()
        {
            UserId = userId,
            FileUrl = "https://blob.storage/v1.pdf",
            OriginalFilename = "v1.pdf",
            FileSizeBytes = 1024,
            FileType = FileType.Pdf,
            IsLatest = true
        };
        Resume latest2 = new()
        {
            UserId = userId,
            FileUrl = "https://blob.storage/v2.pdf",
            OriginalFilename = "v2.pdf",
            FileSizeBytes = 2048,
            FileType = FileType.Pdf,
            IsLatest = true
        };
        repo.Add(latest1);
        repo.Add(latest2);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        await repo.MarkPreviousAsNotLatestAsync(userId, CancellationToken.None);
        _fixture.DbContext.ChangeTracker.Clear();

        // Assert — all resumes should have IsLatest = false
        List<Resume> resumes = await _fixture.DbContext.Resumes
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .ToListAsync();
        resumes.Should().HaveCount(2);
        resumes.Should().OnlyContain(r => !r.IsLatest);
    }
}

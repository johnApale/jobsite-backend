using FluentAssertions;
using Jobsite.Modules.Matching.Domain.Constants;
using Jobsite.Modules.Matching.Domain.Entities;
using Jobsite.Modules.Matching.Infrastructure.Persistence.Repositories;

namespace Jobsite.IntegrationTests.Matching;

/// <summary>
/// Integration tests for CandidateMatchRepository and ShortlistRepository
/// against a real PostgreSQL container.
/// </summary>
[Collection("Matching")]
public sealed class MatchingRepositoryTests : IAsyncLifetime
{
    private readonly MatchingIntegrationFixture _fixture;

    public MatchingRepositoryTests(MatchingIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ─── CandidateMatchRepository ────────────────────────────────────────

    [Fact]
    public async Task GetByApplicationIdAsync_Exists_ReturnsMatch()
    {
        // Arrange
        CandidateMatchRepository repo = new(_fixture.DbContext);
        Guid applicationId = Guid.NewGuid();

        CandidateMatch match = new()
        {
            ApplicationId = applicationId,
            JobPostingId = Guid.NewGuid(),
            ApplicantUserId = Guid.NewGuid(),
            ScreeningScore = 80m,
            CompositeScore = 80m,
            MatchStrength = MatchStrength.Strong,
            ScreeningCompletedAt = DateTime.UtcNow
        };
        repo.Add(match);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        CandidateMatch? found = await repo.GetByApplicationIdAsync(applicationId, CancellationToken.None);

        // Assert
        found.Should().NotBeNull();
        found!.ApplicationId.Should().Be(applicationId);
        found.ScreeningScore.Should().Be(80m);
    }

    [Fact]
    public async Task GetByApplicationIdAsync_NotExists_ReturnsNull()
    {
        // Arrange
        CandidateMatchRepository repo = new(_fixture.DbContext);

        // Act
        CandidateMatch? found = await repo.GetByApplicationIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task GetByApplicationIdForUpdateAsync_ReturnsTrackedEntity()
    {
        // Arrange
        CandidateMatchRepository repo = new(_fixture.DbContext);
        Guid applicationId = Guid.NewGuid();

        CandidateMatch match = new()
        {
            ApplicationId = applicationId,
            JobPostingId = Guid.NewGuid(),
            ApplicantUserId = Guid.NewGuid(),
            ScreeningScore = 60m,
            CompositeScore = 60m,
            MatchStrength = MatchStrength.Good,
            ScreeningCompletedAt = DateTime.UtcNow
        };
        repo.Add(match);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        CandidateMatch? tracked = await repo.GetByApplicationIdForUpdateAsync(applicationId, CancellationToken.None);

        // Assert — entity is tracked (can be mutated and saved)
        tracked.Should().NotBeNull();
        tracked!.AssessmentScore = 95m;
        tracked.CompositeScore = 77.50m;
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        CandidateMatch? updated = await repo.GetByApplicationIdAsync(applicationId, CancellationToken.None);
        updated!.AssessmentScore.Should().Be(95m);
        updated.CompositeScore.Should().Be(77.50m);
    }

    [Fact]
    public async Task GetByJobPostingIdAsync_ReturnsOrderedByCompositeScoreDescending()
    {
        // Arrange
        CandidateMatchRepository repo = new(_fixture.DbContext);
        Guid jobPostingId = Guid.NewGuid();

        CandidateMatch lowScore = new()
        {
            ApplicationId = Guid.NewGuid(),
            JobPostingId = jobPostingId,
            ApplicantUserId = Guid.NewGuid(),
            ScreeningScore = 40m,
            CompositeScore = 40m,
            MatchStrength = MatchStrength.Weak,
            ScreeningCompletedAt = DateTime.UtcNow
        };
        CandidateMatch highScore = new()
        {
            ApplicationId = Guid.NewGuid(),
            JobPostingId = jobPostingId,
            ApplicantUserId = Guid.NewGuid(),
            ScreeningScore = 90m,
            CompositeScore = 90m,
            MatchStrength = MatchStrength.Strong,
            ScreeningCompletedAt = DateTime.UtcNow
        };
        CandidateMatch midScore = new()
        {
            ApplicationId = Guid.NewGuid(),
            JobPostingId = jobPostingId,
            ApplicantUserId = Guid.NewGuid(),
            ScreeningScore = 65m,
            CompositeScore = 65m,
            MatchStrength = MatchStrength.Good,
            ScreeningCompletedAt = DateTime.UtcNow
        };

        // Add in random order — should be returned sorted
        repo.Add(lowScore);
        repo.Add(highScore);
        repo.Add(midScore);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        List<CandidateMatch> matches = await repo.GetByJobPostingIdAsync(jobPostingId, CancellationToken.None);

        // Assert — ordered by CompositeScore descending
        matches.Should().HaveCount(3);
        matches[0].CompositeScore.Should().Be(90m);
        matches[1].CompositeScore.Should().Be(65m);
        matches[2].CompositeScore.Should().Be(40m);
    }

    [Fact]
    public async Task GetByJobPostingIdAsync_DifferentJobPosting_ReturnsEmpty()
    {
        // Arrange
        CandidateMatchRepository repo = new(_fixture.DbContext);

        CandidateMatch match = new()
        {
            ApplicationId = Guid.NewGuid(),
            JobPostingId = Guid.NewGuid(),
            ApplicantUserId = Guid.NewGuid(),
            ScreeningScore = 80m,
            CompositeScore = 80m,
            MatchStrength = MatchStrength.Strong,
            ScreeningCompletedAt = DateTime.UtcNow
        };
        repo.Add(match);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        List<CandidateMatch> matches = await repo.GetByJobPostingIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        matches.Should().BeEmpty();
    }

    // ─── ShortlistRepository ─────────────────────────────────────────────

    private Shortlist CreateShortlist(Guid? jobPostingId = null, string? status = null) => new()
    {
        JobPostingId = jobPostingId ?? Guid.NewGuid(),
        Status = status ?? ShortlistStatus.Draft,
        GeneratedBy = "Algorithm",
        TotalCandidates = 2
    };

    private ShortlistCandidate CreateCandidate(Guid shortlistId) => new()
    {
        ShortlistId = shortlistId,
        ApplicationId = Guid.NewGuid(),
        ApplicantUserId = Guid.NewGuid(),
        CompositeScore = 85m,
        Rank = 1,
        Source = ShortlistCandidateSource.Algorithm,
        AddedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task ShortlistRepo_GetByIdAsync_IncludesCandidates()
    {
        // Arrange
        ShortlistRepository repo = new(_fixture.DbContext);

        Shortlist shortlist = CreateShortlist();
        _fixture.DbContext.Shortlists.Add(shortlist);
        await _fixture.DbContext.SaveChangesAsync();

        ShortlistCandidate c1 = CreateCandidate(shortlist.Id);
        ShortlistCandidate c2 = CreateCandidate(shortlist.Id);
        c2.Rank = 2;
        c2.CompositeScore = 75m;
        _fixture.DbContext.ShortlistCandidates.AddRange(c1, c2);
        await _fixture.DbContext.SaveChangesAsync();
        Guid shortlistId = shortlist.Id;
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        Shortlist? found = await repo.GetByIdAsync(shortlistId, CancellationToken.None);

        // Assert
        found.Should().NotBeNull();
        found!.Id.Should().Be(shortlistId);
        found.Candidates.Should().HaveCount(2);
    }

    [Fact]
    public async Task ShortlistRepo_GetByIdAsync_NotExists_ReturnsNull()
    {
        // Arrange
        ShortlistRepository repo = new(_fixture.DbContext);

        // Act
        Shortlist? found = await repo.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task ShortlistRepo_GetByIdForUpdateAsync_ReturnsTrackedEntityWithCandidates()
    {
        // Arrange
        ShortlistRepository repo = new(_fixture.DbContext);

        Shortlist shortlist = CreateShortlist();
        _fixture.DbContext.Shortlists.Add(shortlist);
        await _fixture.DbContext.SaveChangesAsync();

        ShortlistCandidate candidate = CreateCandidate(shortlist.Id);
        _fixture.DbContext.ShortlistCandidates.Add(candidate);
        await _fixture.DbContext.SaveChangesAsync();
        Guid shortlistId = shortlist.Id;
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        Shortlist? tracked = await repo.GetByIdForUpdateAsync(shortlistId, CancellationToken.None);

        // Assert — entity is tracked (can be mutated and saved)
        tracked.Should().NotBeNull();
        tracked!.Status = ShortlistStatus.Finalized;
        tracked.FinalizedAt = DateTime.UtcNow;
        tracked.FinalizedBy = Guid.NewGuid();
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        Shortlist? updated = await repo.GetByIdAsync(shortlistId, CancellationToken.None);
        updated!.Status.Should().Be(ShortlistStatus.Finalized);
        updated.Candidates.Should().HaveCount(1);
    }

    [Fact]
    public async Task ShortlistRepo_GetDraftByJobPostingIdAsync_ReturnsDraftOnly()
    {
        // Arrange
        ShortlistRepository repo = new(_fixture.DbContext);
        Guid jobPostingId = Guid.NewGuid();

        Shortlist draft = CreateShortlist(jobPostingId, ShortlistStatus.Draft);
        Shortlist finalized = CreateShortlist(jobPostingId, ShortlistStatus.Finalized);
        finalized.FinalizedAt = DateTime.UtcNow;
        finalized.FinalizedBy = Guid.NewGuid();

        _fixture.DbContext.Shortlists.AddRange(draft, finalized);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        Shortlist? found = await repo.GetDraftByJobPostingIdAsync(jobPostingId, CancellationToken.None);

        // Assert
        found.Should().NotBeNull();
        found!.Status.Should().Be(ShortlistStatus.Draft);
    }

    [Fact]
    public async Task ShortlistRepo_GetDraftByJobPostingIdAsync_NoDraft_ReturnsNull()
    {
        // Arrange
        ShortlistRepository repo = new(_fixture.DbContext);
        Guid jobPostingId = Guid.NewGuid();

        Shortlist finalized = CreateShortlist(jobPostingId, ShortlistStatus.Finalized);
        finalized.FinalizedAt = DateTime.UtcNow;
        finalized.FinalizedBy = Guid.NewGuid();
        _fixture.DbContext.Shortlists.Add(finalized);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        Shortlist? found = await repo.GetDraftByJobPostingIdAsync(jobPostingId, CancellationToken.None);

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task ShortlistRepo_GetByJobPostingIdAsync_ReturnsOrderedByCreatedAtDescending()
    {
        // Arrange
        ShortlistRepository repo = new(_fixture.DbContext);
        Guid jobPostingId = Guid.NewGuid();

        Shortlist older = CreateShortlist(jobPostingId, ShortlistStatus.Finalized);
        older.FinalizedAt = DateTime.UtcNow;
        older.FinalizedBy = Guid.NewGuid();
        _fixture.DbContext.Shortlists.Add(older);
        await _fixture.DbContext.SaveChangesAsync();

        // Small delay to ensure distinct CreatedAt values
        await Task.Delay(50);

        Shortlist newer = CreateShortlist(jobPostingId);
        _fixture.DbContext.Shortlists.Add(newer);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        List<Shortlist> shortlists = await repo.GetByJobPostingIdAsync(jobPostingId, CancellationToken.None);

        // Assert — ordered by CreatedAt descending (newest first)
        shortlists.Should().HaveCount(2);
        shortlists[0].Status.Should().Be(ShortlistStatus.Draft);
        shortlists[1].Status.Should().Be(ShortlistStatus.Finalized);
    }

    [Fact]
    public async Task ShortlistRepo_GetByJobPostingIdAsync_DifferentJob_ReturnsEmpty()
    {
        // Arrange
        ShortlistRepository repo = new(_fixture.DbContext);

        Shortlist shortlist = CreateShortlist();
        _fixture.DbContext.Shortlists.Add(shortlist);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        List<Shortlist> shortlists = await repo.GetByJobPostingIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        shortlists.Should().BeEmpty();
    }
}

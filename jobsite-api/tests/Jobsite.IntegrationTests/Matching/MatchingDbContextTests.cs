using FluentAssertions;
using Jobsite.Modules.Matching.Domain.Constants;
using Jobsite.Modules.Matching.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.IntegrationTests.Matching;

/// <summary>
/// Integration tests validating MatchingDbContext schema creation, table mapping,
/// CHECK constraints, indexes, unique constraints, and cascade behavior against a real PostgreSQL container.
/// </summary>
[Collection("Matching")]
public sealed class MatchingDbContextTests : IAsyncLifetime
{
    private readonly MatchingIntegrationFixture _fixture;

    public MatchingDbContextTests(MatchingIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ─── Schema ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Schema_MatchingSchemaExists()
    {
        // Act
        await using Npgsql.NpgsqlConnection conn = new(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using Npgsql.NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT schema_name FROM information_schema.schemata WHERE schema_name = 'matching'";
        object? result = await cmd.ExecuteScalarAsync();
        string? schemaName = result?.ToString();

        // Assert
        schemaName.Should().Be("matching");
    }

    // ─── CandidateMatch persistence ──────────────────────────────────────

    [Fact]
    public async Task CandidateMatch_Persists_AllFieldsCorrectly()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid jobPostingId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();
        DateTime screeningDone = DateTime.UtcNow.AddMinutes(-10);
        DateTime assessmentDone = DateTime.UtcNow;

        CandidateMatch match = new()
        {
            ApplicationId = applicationId,
            JobPostingId = jobPostingId,
            ApplicantUserId = applicantUserId,
            ScreeningScore = 85.50m,
            AssessmentScore = 90.00m,
            CompositeScore = 87.75m,
            MatchStrength = MatchStrength.Strong,
            Rank = 1,
            ScreeningCompletedAt = screeningDone,
            AssessmentCompletedAt = assessmentDone
        };

        // Act
        _fixture.DbContext.CandidateMatches.Add(match);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        CandidateMatch? persisted = await _fixture.DbContext.CandidateMatches
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ApplicationId == applicationId);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.ApplicationId.Should().Be(applicationId);
        persisted.JobPostingId.Should().Be(jobPostingId);
        persisted.ApplicantUserId.Should().Be(applicantUserId);
        persisted.ScreeningScore.Should().Be(85.50m);
        persisted.AssessmentScore.Should().Be(90.00m);
        persisted.CompositeScore.Should().Be(87.75m);
        persisted.MatchStrength.Should().Be(MatchStrength.Strong);
        persisted.Rank.Should().Be(1);
        persisted.ScreeningCompletedAt.Should().BeCloseTo(screeningDone, TimeSpan.FromSeconds(1));
        persisted.AssessmentCompletedAt.Should().BeCloseTo(assessmentDone, TimeSpan.FromSeconds(1));
        persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        persisted.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task CandidateMatch_SharedPrimaryKey_ValueGeneratedNever()
    {
        // Arrange — ApplicationId is provided, not auto-generated
        Guid applicationId = Guid.NewGuid();

        CandidateMatch match = new()
        {
            ApplicationId = applicationId,
            JobPostingId = Guid.NewGuid(),
            ApplicantUserId = Guid.NewGuid(),
            ScreeningScore = 75m,
            CompositeScore = 75m,
            MatchStrength = MatchStrength.Good,
            ScreeningCompletedAt = DateTime.UtcNow
        };

        // Act
        _fixture.DbContext.CandidateMatches.Add(match);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        CandidateMatch? persisted = await _fixture.DbContext.CandidateMatches
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ApplicationId == applicationId);

        // Assert — PK is the exact GUID we provided
        persisted.Should().NotBeNull();
        persisted!.ApplicationId.Should().Be(applicationId);
    }

    // ─── CandidateMatch CHECK constraints ────────────────────────────────

    [Fact]
    public async Task CandidateMatch_CheckConstraint_RejectsInvalidMatchStrength()
    {
        // Arrange
        CandidateMatch match = new()
        {
            ApplicationId = Guid.NewGuid(),
            JobPostingId = Guid.NewGuid(),
            ApplicantUserId = Guid.NewGuid(),
            ScreeningScore = 50m,
            CompositeScore = 50m,
            MatchStrength = "SuperStrong",
            ScreeningCompletedAt = DateTime.UtcNow
        };

        // Act
        _fixture.DbContext.CandidateMatches.Add(match);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ─── Shortlist persistence ───────────────────────────────────────────

    [Fact]
    public async Task Shortlist_Persists_AllFieldsCorrectly()
    {
        // Arrange
        Guid jobPostingId = Guid.NewGuid();
        Guid finalizedBy = Guid.NewGuid();
        DateTime finalizedAt = DateTime.UtcNow;

        Shortlist shortlist = new()
        {
            JobPostingId = jobPostingId,
            Status = ShortlistStatus.Finalized,
            GeneratedBy = "Algorithm",
            TotalCandidates = 5,
            FinalizedAt = finalizedAt,
            FinalizedBy = finalizedBy
        };

        // Act
        _fixture.DbContext.Shortlists.Add(shortlist);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        Shortlist? persisted = await _fixture.DbContext.Shortlists
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.JobPostingId == jobPostingId);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.JobPostingId.Should().Be(jobPostingId);
        persisted.Status.Should().Be(ShortlistStatus.Finalized);
        persisted.GeneratedBy.Should().Be("Algorithm");
        persisted.TotalCandidates.Should().Be(5);
        persisted.FinalizedAt.Should().BeCloseTo(finalizedAt, TimeSpan.FromSeconds(1));
        persisted.FinalizedBy.Should().Be(finalizedBy);
        persisted.Id.Should().NotBe(Guid.Empty);
        persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        persisted.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Shortlist_DefaultValues_DraftStatus()
    {
        // Arrange — minimal fields, status defaults to Draft
        Shortlist shortlist = new()
        {
            JobPostingId = Guid.NewGuid(),
            GeneratedBy = "Algorithm",
            TotalCandidates = 3
        };

        // Act
        _fixture.DbContext.Shortlists.Add(shortlist);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        Shortlist? persisted = await _fixture.DbContext.Shortlists
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == shortlist.Id);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(ShortlistStatus.Draft);
        persisted.FinalizedAt.Should().BeNull();
        persisted.FinalizedBy.Should().BeNull();
    }

    // ─── Shortlist CHECK constraints ─────────────────────────────────────

    [Fact]
    public async Task Shortlist_CheckConstraint_RejectsInvalidStatus()
    {
        // Arrange
        Shortlist shortlist = new()
        {
            JobPostingId = Guid.NewGuid(),
            Status = "InvalidStatus",
            GeneratedBy = "Algorithm",
            TotalCandidates = 1
        };

        // Act
        _fixture.DbContext.Shortlists.Add(shortlist);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ─── ShortlistCandidate persistence ──────────────────────────────────

    [Fact]
    public async Task ShortlistCandidate_Persists_AllFieldsCorrectly()
    {
        // Arrange — need a Shortlist first (FK constraint)
        Shortlist shortlist = new()
        {
            JobPostingId = Guid.NewGuid(),
            GeneratedBy = "Algorithm",
            TotalCandidates = 1
        };
        _fixture.DbContext.Shortlists.Add(shortlist);
        await _fixture.DbContext.SaveChangesAsync();

        Guid applicationId = Guid.NewGuid();
        Guid applicantUserId = Guid.NewGuid();
        DateTime addedAt = DateTime.UtcNow;

        ShortlistCandidate candidate = new()
        {
            ShortlistId = shortlist.Id,
            ApplicationId = applicationId,
            ApplicantUserId = applicantUserId,
            CompositeScore = 92.50m,
            Rank = 1,
            Source = ShortlistCandidateSource.Algorithm,
            Status = ShortlistCandidateStatus.Approved,
            AddedAt = addedAt
        };

        // Act
        _fixture.DbContext.ShortlistCandidates.Add(candidate);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        ShortlistCandidate? persisted = await _fixture.DbContext.ShortlistCandidates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ApplicationId == applicationId);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.ShortlistId.Should().Be(shortlist.Id);
        persisted.ApplicationId.Should().Be(applicationId);
        persisted.ApplicantUserId.Should().Be(applicantUserId);
        persisted.CompositeScore.Should().Be(92.50m);
        persisted.Rank.Should().Be(1);
        persisted.Source.Should().Be(ShortlistCandidateSource.Algorithm);
        persisted.Status.Should().Be(ShortlistCandidateStatus.Approved);
        persisted.AddedAt.Should().BeCloseTo(addedAt, TimeSpan.FromSeconds(1));
        persisted.RemovedAt.Should().BeNull();
        persisted.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task ShortlistCandidate_DefaultStatus_IsPending()
    {
        // Arrange
        Shortlist shortlist = new()
        {
            JobPostingId = Guid.NewGuid(),
            GeneratedBy = "Algorithm",
            TotalCandidates = 1
        };
        _fixture.DbContext.Shortlists.Add(shortlist);
        await _fixture.DbContext.SaveChangesAsync();

        ShortlistCandidate candidate = new()
        {
            ShortlistId = shortlist.Id,
            ApplicationId = Guid.NewGuid(),
            ApplicantUserId = Guid.NewGuid(),
            CompositeScore = 80m,
            Rank = 1,
            Source = ShortlistCandidateSource.Manual,
            AddedAt = DateTime.UtcNow
        };

        // Act
        _fixture.DbContext.ShortlistCandidates.Add(candidate);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        ShortlistCandidate? persisted = await _fixture.DbContext.ShortlistCandidates
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == candidate.Id);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(ShortlistCandidateStatus.Pending);
    }

    // ─── ShortlistCandidate CHECK constraints ────────────────────────────

    [Fact]
    public async Task ShortlistCandidate_CheckConstraint_RejectsInvalidSource()
    {
        // Arrange
        Shortlist shortlist = new()
        {
            JobPostingId = Guid.NewGuid(),
            GeneratedBy = "Algorithm",
            TotalCandidates = 1
        };
        _fixture.DbContext.Shortlists.Add(shortlist);
        await _fixture.DbContext.SaveChangesAsync();

        ShortlistCandidate candidate = new()
        {
            ShortlistId = shortlist.Id,
            ApplicationId = Guid.NewGuid(),
            ApplicantUserId = Guid.NewGuid(),
            CompositeScore = 50m,
            Rank = 1,
            Source = "InvalidSource",
            AddedAt = DateTime.UtcNow
        };

        // Act
        _fixture.DbContext.ShortlistCandidates.Add(candidate);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task ShortlistCandidate_CheckConstraint_RejectsInvalidStatus()
    {
        // Arrange
        Shortlist shortlist = new()
        {
            JobPostingId = Guid.NewGuid(),
            GeneratedBy = "Algorithm",
            TotalCandidates = 1
        };
        _fixture.DbContext.Shortlists.Add(shortlist);
        await _fixture.DbContext.SaveChangesAsync();

        ShortlistCandidate candidate = new()
        {
            ShortlistId = shortlist.Id,
            ApplicationId = Guid.NewGuid(),
            ApplicantUserId = Guid.NewGuid(),
            CompositeScore = 50m,
            Rank = 1,
            Source = ShortlistCandidateSource.Algorithm,
            Status = "InvalidStatus",
            AddedAt = DateTime.UtcNow
        };

        // Act
        _fixture.DbContext.ShortlistCandidates.Add(candidate);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ─── Unique constraint ───────────────────────────────────────────────

    [Fact]
    public async Task ShortlistCandidate_UniqueConstraint_PreventseDuplicateShortlistApp()
    {
        // Arrange
        Shortlist shortlist = new()
        {
            JobPostingId = Guid.NewGuid(),
            GeneratedBy = "Algorithm",
            TotalCandidates = 1
        };
        _fixture.DbContext.Shortlists.Add(shortlist);
        await _fixture.DbContext.SaveChangesAsync();

        Guid applicationId = Guid.NewGuid();

        ShortlistCandidate first = new()
        {
            ShortlistId = shortlist.Id,
            ApplicationId = applicationId,
            ApplicantUserId = Guid.NewGuid(),
            CompositeScore = 80m,
            Rank = 1,
            Source = ShortlistCandidateSource.Algorithm,
            AddedAt = DateTime.UtcNow
        };
        _fixture.DbContext.ShortlistCandidates.Add(first);
        await _fixture.DbContext.SaveChangesAsync();

        ShortlistCandidate duplicate = new()
        {
            ShortlistId = shortlist.Id,
            ApplicationId = applicationId,
            ApplicantUserId = Guid.NewGuid(),
            CompositeScore = 70m,
            Rank = 2,
            Source = ShortlistCandidateSource.Manual,
            AddedAt = DateTime.UtcNow
        };

        // Act
        _fixture.DbContext.ShortlistCandidates.Add(duplicate);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ─── Cascade delete ──────────────────────────────────────────────────

    [Fact]
    public async Task CascadeDelete_DeletingShortlist_DeletesCandidates()
    {
        // Arrange
        Shortlist shortlist = new()
        {
            JobPostingId = Guid.NewGuid(),
            GeneratedBy = "Algorithm",
            TotalCandidates = 2
        };
        _fixture.DbContext.Shortlists.Add(shortlist);
        await _fixture.DbContext.SaveChangesAsync();

        ShortlistCandidate c1 = new()
        {
            ShortlistId = shortlist.Id,
            ApplicationId = Guid.NewGuid(),
            ApplicantUserId = Guid.NewGuid(),
            CompositeScore = 90m,
            Rank = 1,
            Source = ShortlistCandidateSource.Algorithm,
            AddedAt = DateTime.UtcNow
        };
        ShortlistCandidate c2 = new()
        {
            ShortlistId = shortlist.Id,
            ApplicationId = Guid.NewGuid(),
            ApplicantUserId = Guid.NewGuid(),
            CompositeScore = 85m,
            Rank = 2,
            Source = ShortlistCandidateSource.Algorithm,
            AddedAt = DateTime.UtcNow
        };
        _fixture.DbContext.ShortlistCandidates.AddRange(c1, c2);
        await _fixture.DbContext.SaveChangesAsync();
        Guid shortlistId = shortlist.Id;
        _fixture.DbContext.ChangeTracker.Clear();

        // Act — delete the shortlist
        Shortlist? tracked = await _fixture.DbContext.Shortlists
            .FirstOrDefaultAsync(s => s.Id == shortlistId);
        _fixture.DbContext.Shortlists.Remove(tracked!);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Assert — candidates should be cascade-deleted
        List<ShortlistCandidate> orphans = await _fixture.DbContext.ShortlistCandidates
            .AsNoTracking()
            .Where(c => c.ShortlistId == shortlistId)
            .ToListAsync();
        orphans.Should().BeEmpty();
    }

    // ─── Indexes ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CandidateMatches_Indexes_Exist()
    {
        // Act
        await using Npgsql.NpgsqlConnection conn = new(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using Npgsql.NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT indexname FROM pg_indexes
            WHERE schemaname = 'matching' AND tablename = 'candidate_matches'
            ORDER BY indexname
            """;

        List<string> indexes = [];
        await using Npgsql.NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
        }

        // Assert
        indexes.Should().Contain("ix_candidate_matches_job_posting_id");
        indexes.Should().Contain("ix_candidate_matches_applicant_user_id");
        indexes.Should().Contain("ix_candidate_matches_composite_score");
        indexes.Should().Contain("ix_candidate_matches_match_strength");
    }

    [Fact]
    public async Task Shortlists_Indexes_Exist()
    {
        // Act
        await using Npgsql.NpgsqlConnection conn = new(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using Npgsql.NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT indexname FROM pg_indexes
            WHERE schemaname = 'matching' AND tablename = 'shortlists'
            ORDER BY indexname
            """;

        List<string> indexes = [];
        await using Npgsql.NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
        }

        // Assert
        indexes.Should().Contain("ix_shortlists_job_posting_id");
        indexes.Should().Contain("ix_shortlists_status");
    }

    [Fact]
    public async Task ShortlistCandidates_Indexes_Exist()
    {
        // Act
        await using Npgsql.NpgsqlConnection conn = new(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using Npgsql.NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT indexname FROM pg_indexes
            WHERE schemaname = 'matching' AND tablename = 'shortlist_candidates'
            ORDER BY indexname
            """;

        List<string> indexes = [];
        await using Npgsql.NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
        }

        // Assert
        indexes.Should().Contain("ix_shortlist_candidates_application_id");
        indexes.Should().Contain("uq_shortlist_candidates_shortlist_app");
    }
}

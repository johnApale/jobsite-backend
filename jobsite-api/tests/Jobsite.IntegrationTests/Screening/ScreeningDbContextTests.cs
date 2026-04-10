using FluentAssertions;
using Jobsite.Modules.Screening.Domain.Constants;
using Jobsite.Modules.Screening.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.IntegrationTests.Screening;

/// <summary>
/// Integration tests validating ScreeningDbContext schema creation, table mapping,
/// CHECK constraints, indexes, and repository queries against a real PostgreSQL container.
/// </summary>
[Collection("Screening")]
public sealed class ScreeningDbContextTests : IAsyncLifetime
{
    private readonly ScreeningIntegrationFixture _fixture;

    public ScreeningDbContextTests(ScreeningIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ─── Schema ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Schema_ScreeningSchemaExists()
    {
        // Act
        await using Npgsql.NpgsqlConnection conn = new(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using Npgsql.NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT schema_name FROM information_schema.schemata WHERE schema_name = 'screening'";
        object? result = await cmd.ExecuteScalarAsync();
        string? schemaName = result?.ToString();

        // Assert
        schemaName.Should().Be("screening");
    }

    // ─── ScreeningResult persistence ─────────────────────────────────────

    [Fact]
    public async Task ScreeningResult_Persists_AllFieldsCorrectly()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        DateTime now = DateTime.UtcNow;

        ScreeningResult result = new()
        {
            ApplicationId = applicationId,
            Status = ScreeningStatus.Completed,
            OverallScore = 85.50m,
            MatchStrength = MatchStrength.Strong,
            Outcome = ScreeningOutcome.AutoAdvanced,
            CriteriaScoreBreakdown = """[{"criterion": "Experience", "score": 90}]""",
            AiCriteriaScoreBreakdown = """[{"criterion": "Experience", "ai_score": 88}]""",
            AiOverallScore = 88.00m,
            QuestionScoreBreakdown = """[{"question": 1, "score": 100}]""",
            AssessmentScore = 92.50m,
            CandidateFeedback = """{"summary": "Strong match"}""",
            AutoAdvanceThreshold = 70.00m,
            AutoRejectThreshold = 30.00m,
            ReviewedBy = Guid.NewGuid(),
            ReviewedAt = now,
            ReviewNotes = "Excellent candidate",
            StartedAt = now.AddMinutes(-2),
            CompletedAt = now
        };

        // Act
        _fixture.DbContext.ScreeningResults.Add(result);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        ScreeningResult? persisted = await _fixture.DbContext.ScreeningResults
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ApplicationId == applicationId);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.ApplicationId.Should().Be(applicationId);
        persisted.Status.Should().Be(ScreeningStatus.Completed);
        persisted.OverallScore.Should().Be(85.50m);
        persisted.MatchStrength.Should().Be(MatchStrength.Strong);
        persisted.Outcome.Should().Be(ScreeningOutcome.AutoAdvanced);
        persisted.CriteriaScoreBreakdown.Should().Contain("Experience");
        persisted.AiOverallScore.Should().Be(88.00m);
        persisted.AssessmentScore.Should().Be(92.50m);
        persisted.AutoAdvanceThreshold.Should().Be(70.00m);
        persisted.AutoRejectThreshold.Should().Be(30.00m);
        persisted.ReviewNotes.Should().Be("Excellent candidate");
        persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        persisted.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task ScreeningResult_DefaultValues_AppliedByDatabase()
    {
        // Arrange — minimal required fields
        ScreeningResult result = new()
        {
            ApplicationId = Guid.NewGuid(),
            AutoAdvanceThreshold = 70.00m,
            AutoRejectThreshold = 30.00m
        };

        // Act
        _fixture.DbContext.ScreeningResults.Add(result);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        ScreeningResult? persisted = await _fixture.DbContext.ScreeningResults
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ApplicationId == result.ApplicationId);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(ScreeningStatus.Pending);
        persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        persisted.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));
        persisted.OverallScore.Should().BeNull();
        persisted.Outcome.Should().BeNull();
    }

    [Fact]
    public async Task ScreeningResult_CheckConstraint_RejectsInvalidStatus()
    {
        // Arrange
        ScreeningResult result = new()
        {
            ApplicationId = Guid.NewGuid(),
            Status = "InvalidStatus",
            AutoAdvanceThreshold = 70m,
            AutoRejectThreshold = 30m
        };

        // Act
        _fixture.DbContext.ScreeningResults.Add(result);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();

        // Clean up tracked entity
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task ScreeningResult_CheckConstraint_RejectsInvalidOutcome()
    {
        // Arrange
        ScreeningResult result = new()
        {
            ApplicationId = Guid.NewGuid(),
            Status = ScreeningStatus.Completed,
            Outcome = "InvalidOutcome",
            AutoAdvanceThreshold = 70m,
            AutoRejectThreshold = 30m
        };

        // Act
        _fixture.DbContext.ScreeningResults.Add(result);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task ScreeningResult_CheckConstraint_RejectsInvalidMatchStrength()
    {
        // Arrange
        ScreeningResult result = new()
        {
            ApplicationId = Guid.NewGuid(),
            Status = ScreeningStatus.Completed,
            MatchStrength = "SuperStrong",
            AutoAdvanceThreshold = 70m,
            AutoRejectThreshold = 30m
        };

        // Act
        _fixture.DbContext.ScreeningResults.Add(result);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    // ─── Indexes ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ScreeningResults_Indexes_Exist()
    {
        // Act
        await using Npgsql.NpgsqlConnection conn = new(_fixture.ConnectionString);
        await conn.OpenAsync();

        await using Npgsql.NpgsqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT indexname FROM pg_indexes
            WHERE schemaname = 'screening' AND tablename = 'screening_results'
            ORDER BY indexname
            """;

        List<string> indexes = [];
        await using Npgsql.NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexes.Add(reader.GetString(0));
        }

        // Assert
        indexes.Should().Contain("ix_screening_results_status");
        indexes.Should().Contain("ix_screening_results_match_strength");
        indexes.Should().Contain("ix_screening_results_outcome");
        indexes.Should().Contain("ix_screening_results_overall_score");
    }

    // ─── ScreeningQuestionResponse persistence ───────────────────────────

    [Fact]
    public async Task QuestionResponse_Persists_AllFieldsCorrectly()
    {
        // Arrange — need a ScreeningResult first (FK constraint)
        Guid applicationId = Guid.NewGuid();
        ScreeningResult screeningResult = new()
        {
            ApplicationId = applicationId,
            AutoAdvanceThreshold = 70m,
            AutoRejectThreshold = 30m
        };
        _fixture.DbContext.ScreeningResults.Add(screeningResult);
        await _fixture.DbContext.SaveChangesAsync();

        Guid questionId = Guid.NewGuid();
        DateTime submittedAt = DateTime.UtcNow;

        ScreeningQuestionResponse response = new()
        {
            ApplicationId = applicationId,
            QuestionId = questionId,
            ResponseText = "I have 5 years of experience in .NET",
            ResponseData = """{"selected": ["option_a", "option_b"]}""",
            Score = 85.50m,
            ScoreResult = ScoreResult.MeetsRequirement,
            ScoreReasoning = "Matches required experience",
            SubmittedAt = submittedAt,
            ScoredAt = submittedAt.AddSeconds(5)
        };

        // Act
        _fixture.DbContext.ScreeningQuestionResponses.Add(response);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        ScreeningQuestionResponse? persisted = await _fixture.DbContext.ScreeningQuestionResponses
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.ApplicationId == applicationId && r.QuestionId == questionId);

        // Assert
        persisted.Should().NotBeNull();
        persisted!.ResponseText.Should().Be("I have 5 years of experience in .NET");
        persisted.ResponseData.Should().Contain("option_a");
        persisted.Score.Should().Be(85.50m);
        persisted.ScoreResult.Should().Be(ScoreResult.MeetsRequirement);
        persisted.ScoreReasoning.Should().Be("Matches required experience");
        persisted.Id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task QuestionResponse_UniqueConstraint_PreventseDuplicateAppQuestion()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        ScreeningResult screeningResult = new()
        {
            ApplicationId = applicationId,
            AutoAdvanceThreshold = 70m,
            AutoRejectThreshold = 30m
        };
        _fixture.DbContext.ScreeningResults.Add(screeningResult);
        await _fixture.DbContext.SaveChangesAsync();

        Guid questionId = Guid.NewGuid();

        ScreeningQuestionResponse first = new()
        {
            ApplicationId = applicationId,
            QuestionId = questionId,
            SubmittedAt = DateTime.UtcNow
        };
        _fixture.DbContext.ScreeningQuestionResponses.Add(first);
        await _fixture.DbContext.SaveChangesAsync();

        ScreeningQuestionResponse duplicate = new()
        {
            ApplicationId = applicationId,
            QuestionId = questionId,
            SubmittedAt = DateTime.UtcNow
        };

        // Act
        _fixture.DbContext.ScreeningQuestionResponses.Add(duplicate);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task QuestionResponse_CheckConstraint_RejectsInvalidScoreResult()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        ScreeningResult screeningResult = new()
        {
            ApplicationId = applicationId,
            AutoAdvanceThreshold = 70m,
            AutoRejectThreshold = 30m
        };
        _fixture.DbContext.ScreeningResults.Add(screeningResult);
        await _fixture.DbContext.SaveChangesAsync();

        ScreeningQuestionResponse response = new()
        {
            ApplicationId = applicationId,
            QuestionId = Guid.NewGuid(),
            ScoreResult = "Invalid",
            SubmittedAt = DateTime.UtcNow
        };

        // Act
        _fixture.DbContext.ScreeningQuestionResponses.Add(response);
        Func<Task> act = () => _fixture.DbContext.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        _fixture.DbContext.ChangeTracker.Clear();
    }
}

using FluentAssertions;
using Jobsite.Modules.Screening.Domain.Constants;
using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.Modules.Screening.Infrastructure.Persistence.Repositories;

namespace Jobsite.IntegrationTests.Screening;

/// <summary>
/// Integration tests for ScreeningResultRepository and ScreeningQuestionResponseRepository
/// against a real PostgreSQL container.
/// </summary>
[Collection("Screening")]
public sealed class ScreeningRepositoryTests : IAsyncLifetime
{
    private readonly ScreeningIntegrationFixture _fixture;

    public ScreeningRepositoryTests(ScreeningIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => _fixture.ResetDataAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ─── ScreeningResultRepository ───────────────────────────────────────

    [Fact]
    public async Task GetByApplicationIdAsync_Exists_ReturnsResult()
    {
        // Arrange
        ScreeningResultRepository repo = new(_fixture.DbContext);
        Guid applicationId = Guid.NewGuid();

        ScreeningResult result = new()
        {
            ApplicationId = applicationId,
            Status = ScreeningStatus.Completed,
            OverallScore = 75m,
            AutoAdvanceThreshold = 70m,
            AutoRejectThreshold = 30m
        };
        repo.Add(result);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        ScreeningResult? found = await repo.GetByApplicationIdAsync(applicationId, CancellationToken.None);

        // Assert
        found.Should().NotBeNull();
        found!.ApplicationId.Should().Be(applicationId);
        found.OverallScore.Should().Be(75m);
    }

    [Fact]
    public async Task GetByApplicationIdAsync_NotExists_ReturnsNull()
    {
        // Arrange
        ScreeningResultRepository repo = new(_fixture.DbContext);

        // Act
        ScreeningResult? found = await repo.GetByApplicationIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task GetByApplicationIdForUpdateAsync_ReturnsTrackedEntity()
    {
        // Arrange
        ScreeningResultRepository repo = new(_fixture.DbContext);
        Guid applicationId = Guid.NewGuid();

        ScreeningResult result = new()
        {
            ApplicationId = applicationId,
            Status = ScreeningStatus.Pending,
            AutoAdvanceThreshold = 70m,
            AutoRejectThreshold = 30m
        };
        repo.Add(result);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        ScreeningResult? tracked = await repo.GetByApplicationIdForUpdateAsync(applicationId, CancellationToken.None);

        // Assert — entity is tracked (can be mutated and saved)
        tracked.Should().NotBeNull();
        tracked!.Status = ScreeningStatus.InProgress;
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();
        ScreeningResult? updated = await repo.GetByApplicationIdAsync(applicationId, CancellationToken.None);
        updated!.Status.Should().Be(ScreeningStatus.InProgress);
    }

    [Fact]
    public async Task ListAsync_FiltersByStatus_ReturnsCursorPagination()
    {
        // Arrange
        ScreeningResultRepository repo = new(_fixture.DbContext);

        ScreeningResult completed1 = new()
        {
            ApplicationId = Guid.NewGuid(),
            Status = ScreeningStatus.Completed,
            OverallScore = 80m,
            AutoAdvanceThreshold = 70m,
            AutoRejectThreshold = 30m
        };
        ScreeningResult completed2 = new()
        {
            ApplicationId = Guid.NewGuid(),
            Status = ScreeningStatus.Completed,
            OverallScore = 60m,
            AutoAdvanceThreshold = 70m,
            AutoRejectThreshold = 30m
        };
        ScreeningResult pending = new()
        {
            ApplicationId = Guid.NewGuid(),
            Status = ScreeningStatus.Pending,
            AutoAdvanceThreshold = 70m,
            AutoRejectThreshold = 30m
        };

        repo.Add(completed1);
        repo.Add(completed2);
        repo.Add(pending);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        Jobsite.Modules.Screening.Application.DTOs.ScreeningResultQueryParameters parameters = new()
        {
            Status = ScreeningStatus.Completed,
            PageSize = 10
        };
        Jobsite.Modules.Screening.Application.DTOs.ScreeningResultListResponse response =
            await repo.ListAsync(parameters, CancellationToken.None);

        // Assert — only Completed results returned, Pending excluded
        response.Items.Should().HaveCount(2);
        response.Items.Should().OnlyContain(i => i.Status == ScreeningStatus.Completed);
        response.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task ListAsync_CursorPagination_ReturnsCorrectPage()
    {
        // Arrange
        ScreeningResultRepository repo = new(_fixture.DbContext);

        // Create 3 results to paginate
        for (int i = 0; i < 3; i++)
        {
            ScreeningResult result = new()
            {
                ApplicationId = Guid.NewGuid(),
                Status = ScreeningStatus.Completed,
                OverallScore = 50m + i * 10,
                AutoAdvanceThreshold = 70m,
                AutoRejectThreshold = 30m
            };
            repo.Add(result);
            await _fixture.DbContext.SaveChangesAsync();
            // Small delay to ensure distinct CreatedAt values
            await Task.Delay(50);
        }

        // Act — first page of 2
        Jobsite.Modules.Screening.Application.DTOs.ScreeningResultQueryParameters firstPage = new()
        {
            PageSize = 2
        };
        Jobsite.Modules.Screening.Application.DTOs.ScreeningResultListResponse firstResponse =
            await repo.ListAsync(firstPage, CancellationToken.None);

        // Assert
        firstResponse.Items.Should().HaveCount(2);
        firstResponse.HasMore.Should().BeTrue();
        firstResponse.NextCursor.Should().NotBeNullOrEmpty();

        // Act — second page using cursor
        Jobsite.Modules.Screening.Application.DTOs.ScreeningResultQueryParameters secondPage = new()
        {
            PageSize = 2,
            Cursor = firstResponse.NextCursor
        };
        Jobsite.Modules.Screening.Application.DTOs.ScreeningResultListResponse secondResponse =
            await repo.ListAsync(secondPage, CancellationToken.None);

        // Assert
        secondResponse.Items.Should().HaveCount(1);
        secondResponse.HasMore.Should().BeFalse();
    }

    // ─── ScreeningQuestionResponseRepository ─────────────────────────────

    [Fact]
    public async Task QuestionResponseRepo_GetByApplicationIdAsync_ReturnsOrderedBySubmittedAt()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        ScreeningResultRepository resultRepo = new(_fixture.DbContext);
        ScreeningQuestionResponseRepository responseRepo = new(_fixture.DbContext);

        ScreeningResult screeningResult = new()
        {
            ApplicationId = applicationId,
            AutoAdvanceThreshold = 70m,
            AutoRejectThreshold = 30m
        };
        resultRepo.Add(screeningResult);
        await _fixture.DbContext.SaveChangesAsync();

        DateTime baseTime = DateTime.UtcNow;
        ScreeningQuestionResponse older = new()
        {
            ApplicationId = applicationId,
            QuestionId = Guid.NewGuid(),
            ResponseText = "First",
            SubmittedAt = baseTime.AddMinutes(-5)
        };
        ScreeningQuestionResponse newer = new()
        {
            ApplicationId = applicationId,
            QuestionId = Guid.NewGuid(),
            ResponseText = "Second",
            SubmittedAt = baseTime
        };

        responseRepo.Add(newer); // add in reverse order
        responseRepo.Add(older);
        await _fixture.DbContext.SaveChangesAsync();
        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        List<ScreeningQuestionResponse> responses =
            await responseRepo.GetByApplicationIdAsync(applicationId, CancellationToken.None);

        // Assert — ordered by SubmittedAt ascending
        responses.Should().HaveCount(2);
        responses[0].ResponseText.Should().Be("First");
        responses[1].ResponseText.Should().Be("Second");
    }

    [Fact]
    public async Task QuestionResponseRepo_ExistsByApplicationAndQuestionAsync_TrueWhenExists()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid questionId = Guid.NewGuid();
        ScreeningResultRepository resultRepo = new(_fixture.DbContext);
        ScreeningQuestionResponseRepository responseRepo = new(_fixture.DbContext);

        ScreeningResult screeningResult = new()
        {
            ApplicationId = applicationId,
            AutoAdvanceThreshold = 70m,
            AutoRejectThreshold = 30m
        };
        resultRepo.Add(screeningResult);
        await _fixture.DbContext.SaveChangesAsync();

        ScreeningQuestionResponse response = new()
        {
            ApplicationId = applicationId,
            QuestionId = questionId,
            SubmittedAt = DateTime.UtcNow
        };
        responseRepo.Add(response);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        bool exists = await responseRepo.ExistsByApplicationAndQuestionAsync(
            applicationId, questionId, CancellationToken.None);
        bool notExists = await responseRepo.ExistsByApplicationAndQuestionAsync(
            applicationId, Guid.NewGuid(), CancellationToken.None);

        // Assert
        exists.Should().BeTrue();
        notExists.Should().BeFalse();
    }
}

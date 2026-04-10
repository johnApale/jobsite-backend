using FluentAssertions;
using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.Modules.Screening.Infrastructure.Consumers;
using Jobsite.Modules.Screening.Infrastructure.Persistence;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Screening;

public sealed class AnswersScoredConsumerTests : IDisposable
{
    private readonly ITenantConnectionResolver _connectionResolver = Substitute.For<ITenantConnectionResolver>();
    private readonly ITenantDbContextFactory<ScreeningDbContext> _contextFactory =
        Substitute.For<ITenantDbContextFactory<ScreeningDbContext>>();
    private readonly ILogger<AnswersScoredConsumer> _logger =
        Substitute.For<ILogger<AnswersScoredConsumer>>();
    private readonly AnswersScoredConsumer _sut;
    private readonly List<ScreeningDbContext> _contexts = [];

    public AnswersScoredConsumerTests()
    {
        _sut = new AnswersScoredConsumer(_connectionResolver, _contextFactory, _logger);
    }

    public void Dispose()
    {
        foreach (ScreeningDbContext context in _contexts)
        {
            context.Dispose();
        }
    }

    private ScreeningDbContext CreateInMemoryContext(string dbName)
    {
        DbContextOptions<ScreeningDbContext> options = new DbContextOptionsBuilder<ScreeningDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        ScreeningDbContext context = new(options);
        _contexts.Add(context);
        return context;
    }

    private static ConsumeContext<AnswersScored> CreateConsumeContext(AnswersScored message)
    {
        ConsumeContext<AnswersScored> context = Substitute.For<ConsumeContext<AnswersScored>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    private (ScreeningDbContext Seed, ScreeningDbContext Assert) SetupFactory(Guid tenantId)
    {
        string dbName = Guid.NewGuid().ToString();
        ScreeningDbContext seedDb = CreateInMemoryContext(dbName);
        ScreeningDbContext consumerDb = CreateInMemoryContext(dbName);
        ScreeningDbContext assertDb = CreateInMemoryContext(dbName);

        _connectionResolver.GetConnectionStringAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns("Host=localhost;Database=test");
        _contextFactory.CreateDbContext("Host=localhost;Database=test").Returns(consumerDb);

        return (seedDb, assertDb);
    }

    private static async Task SeedResponsesAsync(
        ScreeningDbContext db, Guid applicationId, params Guid[] questionIds)
    {
        foreach (Guid questionId in questionIds)
        {
            db.ScreeningQuestionResponses.Add(new ScreeningQuestionResponse
            {
                Id = Guid.NewGuid(),
                ApplicationId = applicationId,
                QuestionId = questionId,
                ResponseText = "Sample answer text",
                SubmittedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Consume_ValidScores_UpdatesResponseRecords()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        Guid q1 = Guid.NewGuid();
        Guid q2 = Guid.NewGuid();
        (ScreeningDbContext seedDb, ScreeningDbContext assertDb) = SetupFactory(tenantId);
        await SeedResponsesAsync(seedDb, applicationId, q1, q2);

        AnswersScored message = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            ScoresJson = $$"""
            [
                {"question_id": "{{q1}}", "score": 85.0, "result": "MeetsRequirement", "reasoning": "Strong answer"},
                {"question_id": "{{q2}}", "score": 60.0, "result": "PartialMatch", "reasoning": "Missing detail"}
            ]
            """,
            CorrelationId = Guid.NewGuid().ToString(),
            OccurredAt = DateTime.UtcNow
        };

        // Act
        await _sut.Consume(CreateConsumeContext(message));

        // Assert
        List<ScreeningQuestionResponse> responses = await assertDb.ScreeningQuestionResponses
            .Where(r => r.ApplicationId == applicationId)
            .ToListAsync();

        ScreeningQuestionResponse r1 = responses.Single(r => r.QuestionId == q1);
        r1.Score.Should().Be(85m);
        r1.ScoreResult.Should().Be("MeetsRequirement");
        r1.ScoreReasoning.Should().Be("Strong answer");
        r1.ScoredAt.Should().NotBeNull();

        ScreeningQuestionResponse r2 = responses.Single(r => r.QuestionId == q2);
        r2.Score.Should().Be(60m);
        r2.ScoreResult.Should().Be("PartialMatch");
    }

    [Fact]
    public async Task Consume_EmptyScoresJson_DoesNotThrow()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        (ScreeningDbContext _, ScreeningDbContext _) = SetupFactory(tenantId);

        AnswersScored message = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = Guid.NewGuid(),
            ScoresJson = "[]",
            CorrelationId = Guid.NewGuid().ToString(),
            OccurredAt = DateTime.UtcNow
        };

        // Act
        Func<Task> act = async () => await _sut.Consume(CreateConsumeContext(message));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Consume_UnmatchedQuestionId_SkipsGracefully()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        Guid existingQ = Guid.NewGuid();
        Guid unknownQ = Guid.NewGuid();
        (ScreeningDbContext seedDb, ScreeningDbContext assertDb) = SetupFactory(tenantId);
        await SeedResponsesAsync(seedDb, applicationId, existingQ);

        AnswersScored message = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            ScoresJson = $$"""
            [
                {"question_id": "{{existingQ}}", "score": 90.0, "result": "MeetsRequirement", "reasoning": "Good"},
                {"question_id": "{{unknownQ}}", "score": 50.0, "result": "Missing", "reasoning": "N/A"}
            ]
            """,
            CorrelationId = Guid.NewGuid().ToString(),
            OccurredAt = DateTime.UtcNow
        };

        // Act
        await _sut.Consume(CreateConsumeContext(message));

        // Assert — existing one updated, unknown one silently skipped
        ScreeningQuestionResponse? updated = await assertDb.ScreeningQuestionResponses
            .FirstOrDefaultAsync(r => r.QuestionId == existingQ);
        updated!.Score.Should().Be(90m);
    }
}

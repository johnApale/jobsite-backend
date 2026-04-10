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

public sealed class ScreeningEvaluatedConsumerTests : IDisposable
{
    private readonly ITenantConnectionResolver _connectionResolver = Substitute.For<ITenantConnectionResolver>();
    private readonly ITenantDbContextFactory<ScreeningDbContext> _contextFactory =
        Substitute.For<ITenantDbContextFactory<ScreeningDbContext>>();
    private readonly ILogger<ScreeningEvaluatedConsumer> _logger =
        Substitute.For<ILogger<ScreeningEvaluatedConsumer>>();
    private readonly ScreeningEvaluatedConsumer _sut;
    private readonly List<ScreeningDbContext> _contexts = [];

    public ScreeningEvaluatedConsumerTests()
    {
        _sut = new ScreeningEvaluatedConsumer(_connectionResolver, _contextFactory, _logger);
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

    private static ConsumeContext<ScreeningEvaluated> CreateConsumeContext(ScreeningEvaluated message)
    {
        ConsumeContext<ScreeningEvaluated> context = Substitute.For<ConsumeContext<ScreeningEvaluated>>();
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

    private static async Task<ScreeningResult> SeedScreeningResultAsync(
        ScreeningDbContext db, Guid applicationId)
    {
        ScreeningResult result = new()
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            Status = "Completed",
            OverallScore = 75m,
            CriteriaScoreBreakdown = """[{"name": "Python", "score": 75}]""",
            AutoAdvanceThreshold = 80m,
            AutoRejectThreshold = 30m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.ScreeningResults.Add(result);
        await db.SaveChangesAsync();
        return result;
    }

    [Fact]
    public async Task Consume_ValidEvent_UpdatesAiFields()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        (ScreeningDbContext seedDb, ScreeningDbContext assertDb) = SetupFactory(tenantId);
        await SeedScreeningResultAsync(seedDb, applicationId);

        ScreeningEvaluated message = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            BreakdownJson = """[{"name": "Python", "ai_score": 90}]""",
            OverallScore = 90m,
            CorrelationId = Guid.NewGuid().ToString(),
            OccurredAt = DateTime.UtcNow
        };

        // Act
        await _sut.Consume(CreateConsumeContext(message));

        // Assert
        ScreeningResult? updated = await assertDb.ScreeningResults
            .FirstOrDefaultAsync(r => r.ApplicationId == applicationId);
        updated!.AiCriteriaScoreBreakdown.Should().Be(message.BreakdownJson);
        updated.AiOverallScore.Should().Be(90m);
    }

    [Fact]
    public async Task Consume_ValidEvent_PreservesDeterministicScore()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        (ScreeningDbContext seedDb, ScreeningDbContext assertDb) = SetupFactory(tenantId);
        await SeedScreeningResultAsync(seedDb, applicationId);

        ScreeningEvaluated message = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            BreakdownJson = """[{"name": "Python", "ai_score": 90}]""",
            OverallScore = 90m,
            CorrelationId = Guid.NewGuid().ToString(),
            OccurredAt = DateTime.UtcNow
        };

        // Act
        await _sut.Consume(CreateConsumeContext(message));

        // Assert — existing deterministic score untouched
        ScreeningResult? updated = await assertDb.ScreeningResults
            .FirstOrDefaultAsync(r => r.ApplicationId == applicationId);
        updated!.OverallScore.Should().Be(75m);
        updated.CriteriaScoreBreakdown.Should().NotBeNull();
    }

    [Fact]
    public async Task Consume_ResultNotFound_DoesNotThrow()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        (ScreeningDbContext _, ScreeningDbContext _) = SetupFactory(tenantId);

        ScreeningEvaluated message = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = Guid.NewGuid(),
            BreakdownJson = "[]",
            OverallScore = 50m,
            CorrelationId = Guid.NewGuid().ToString(),
            OccurredAt = DateTime.UtcNow
        };

        // Act
        Func<Task> act = async () => await _sut.Consume(CreateConsumeContext(message));

        // Assert
        await act.Should().NotThrowAsync();
    }
}

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

public sealed class FeedbackGeneratedConsumerTests : IDisposable
{
    private readonly ITenantConnectionResolver _connectionResolver = Substitute.For<ITenantConnectionResolver>();
    private readonly ITenantDbContextFactory<ScreeningDbContext> _contextFactory =
        Substitute.For<ITenantDbContextFactory<ScreeningDbContext>>();
    private readonly ILogger<FeedbackGeneratedConsumer> _logger =
        Substitute.For<ILogger<FeedbackGeneratedConsumer>>();
    private readonly FeedbackGeneratedConsumer _sut;
    private readonly List<ScreeningDbContext> _contexts = [];

    public FeedbackGeneratedConsumerTests()
    {
        _sut = new FeedbackGeneratedConsumer(_connectionResolver, _contextFactory, _logger);
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

    private static ConsumeContext<FeedbackGenerated> CreateConsumeContext(FeedbackGenerated message)
    {
        ConsumeContext<FeedbackGenerated> context = Substitute.For<ConsumeContext<FeedbackGenerated>>();
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
            OverallScore = 85m,
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
    public async Task Consume_ValidEvent_SetsCandidateFeedback()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        (ScreeningDbContext seedDb, ScreeningDbContext assertDb) = SetupFactory(tenantId);
        await SeedScreeningResultAsync(seedDb, applicationId);

        FeedbackGenerated message = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            Feedback = "Your profile shows strong Python skills with relevant Django experience.",
            CorrelationId = Guid.NewGuid().ToString(),
            OccurredAt = DateTime.UtcNow
        };

        // Act
        await _sut.Consume(CreateConsumeContext(message));

        // Assert
        ScreeningResult? updated = await assertDb.ScreeningResults
            .FirstOrDefaultAsync(r => r.ApplicationId == applicationId);
        updated!.CandidateFeedback.Should().Be(message.Feedback);
    }

    [Fact]
    public async Task Consume_ValidEvent_PreservesExistingFields()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        (ScreeningDbContext seedDb, ScreeningDbContext assertDb) = SetupFactory(tenantId);
        await SeedScreeningResultAsync(seedDb, applicationId);

        FeedbackGenerated message = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = applicationId,
            Feedback = "Good candidate.",
            CorrelationId = Guid.NewGuid().ToString(),
            OccurredAt = DateTime.UtcNow
        };

        // Act
        await _sut.Consume(CreateConsumeContext(message));

        // Assert — existing score fields untouched
        ScreeningResult? updated = await assertDb.ScreeningResults
            .FirstOrDefaultAsync(r => r.ApplicationId == applicationId);
        updated!.OverallScore.Should().Be(85m);
        updated.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task Consume_ResultNotFound_DoesNotThrow()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        (ScreeningDbContext _, ScreeningDbContext _) = SetupFactory(tenantId);

        FeedbackGenerated message = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = tenantId,
            ApplicationId = Guid.NewGuid(),
            Feedback = "Some feedback",
            CorrelationId = Guid.NewGuid().ToString(),
            OccurredAt = DateTime.UtcNow
        };

        // Act
        Func<Task> act = async () => await _sut.Consume(CreateConsumeContext(message));

        // Assert
        await act.Should().NotThrowAsync();
    }
}

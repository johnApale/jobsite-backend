using FluentAssertions;
using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.Modules.Profiles.Infrastructure.Consumers;
using Jobsite.Modules.Profiles.Infrastructure.Persistence;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Jobsite.UnitTests.Profiles;

public sealed class ResumeUploadedConsumerTests : IDisposable
{
    private readonly ITenantConnectionResolver _connectionResolver = Substitute.For<ITenantConnectionResolver>();
    private readonly ITenantDbContextFactory<ProfilesDbContext> _contextFactory =
        Substitute.For<ITenantDbContextFactory<ProfilesDbContext>>();
    private readonly IResumeParser _resumeParser = Substitute.For<IResumeParser>();
    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();
    private readonly ILogger<ResumeUploadedConsumer> _logger =
        Substitute.For<ILogger<ResumeUploadedConsumer>>();
    private readonly ResumeUploadedConsumer _sut;
    private readonly List<ProfilesDbContext> _contexts = [];

    public ResumeUploadedConsumerTests()
    {
        _sut = new ResumeUploadedConsumer(
            _connectionResolver, _contextFactory, _resumeParser, _eventPublisher, _logger);
    }

    public void Dispose()
    {
        foreach (ProfilesDbContext context in _contexts)
        {
            context.Dispose();
        }
    }

    private ProfilesDbContext CreateInMemoryContext(string dbName)
    {
        DbContextOptions<ProfilesDbContext> options = new DbContextOptionsBuilder<ProfilesDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        ProfilesDbContext context = new(options);
        _contexts.Add(context);
        return context;
    }

    private ConsumeContext<ResumeUploadedEvent> CreateConsumeContext(ResumeUploadedEvent message)
    {
        ConsumeContext<ResumeUploadedEvent> context = Substitute.For<ConsumeContext<ResumeUploadedEvent>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    private ResumeUploadedEvent CreateEvent(Guid? resumeId = null, Guid? tenantId = null) => new()
    {
        EventId = Guid.NewGuid(),
        ResumeId = resumeId ?? Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        TenantId = tenantId ?? Guid.NewGuid(),
        FileUrl = "resumes/test-resume.pdf",
        FileType = "PDF",
        CorrelationId = Guid.NewGuid().ToString(),
        OccurredAt = DateTime.UtcNow
    };

    private async Task<Resume> SeedResumeAsync(ProfilesDbContext db, Guid resumeId, bool isParsed = false)
    {
        Resume resume = new()
        {
            Id = resumeId,
            UserId = Guid.NewGuid(),
            FileUrl = "resumes/test-resume.pdf",
            OriginalFilename = "test-resume.pdf",
            FileSizeBytes = 1024,
            FileType = "PDF",
            IsLatest = true,
            IsParsed = isParsed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Resumes.Add(resume);
        await db.SaveChangesAsync();
        return resume;
    }

    /// <summary>
    /// Sets up seed + consumer + assertion contexts sharing the same InMemory database.
    /// The consumer uses <c>await using</c> which disposes its context,
    /// so we must use separate instances for seeding/assertion.
    /// </summary>
    private (ProfilesDbContext Seed, ProfilesDbContext Assert) SetupFactory(Guid tenantId)
    {
        string dbName = Guid.NewGuid().ToString();
        ProfilesDbContext seedDb = CreateInMemoryContext(dbName);
        ProfilesDbContext consumerDb = CreateInMemoryContext(dbName);
        ProfilesDbContext assertDb = CreateInMemoryContext(dbName);

        _connectionResolver.GetConnectionStringAsync(tenantId, Arg.Any<CancellationToken>())
            .Returns("Host=localhost;Database=test");
        _contextFactory.CreateDbContext("Host=localhost;Database=test").Returns(consumerDb);

        return (seedDb, assertDb);
    }

    // ── Consume — Happy Path ─────────────────────────────────────────────

    [Fact]
    public async Task Consume_ValidEvent_RunsBasicParserAndPersists()
    {
        // Arrange
        ResumeUploadedEvent message = CreateEvent();
        (ProfilesDbContext seedDb, ProfilesDbContext assertDb) = SetupFactory(message.TenantId);
        await SeedResumeAsync(seedDb, message.ResumeId);

        _resumeParser.ParseAsync(message.FileUrl, message.FileType, Arg.Any<CancellationToken>())
            .Returns(new ResumeParseResult { ParsedText = "Experienced .NET developer", ExtractedSkills = "[\"C#\"]" });

        // Act
        await _sut.Consume(CreateConsumeContext(message));

        // Assert
        Resume? updated = await assertDb.Resumes.FindAsync(message.ResumeId);
        updated!.ParsedText.Should().Be("Experienced .NET developer");
        updated.ExtractedSkills.Should().Be("[\"C#\"]");
        updated.IsParsed.Should().BeTrue();
        updated.ParseError.Should().BeNull();
    }

    [Fact]
    public async Task Consume_ValidEvent_PublishesResumeParseRequestedEvent()
    {
        // Arrange
        ResumeUploadedEvent message = CreateEvent();
        (ProfilesDbContext seedDb, ProfilesDbContext assertDb) = SetupFactory(message.TenantId);
        await SeedResumeAsync(seedDb, message.ResumeId);

        _resumeParser.ParseAsync(message.FileUrl, message.FileType, Arg.Any<CancellationToken>())
            .Returns(new ResumeParseResult { ParsedText = "Developer with C# skills" });

        // Act
        await _sut.Consume(CreateConsumeContext(message));

        // Assert — event published for async AI parsing
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<ResumeParseRequested>(e =>
                e.ResumeId == message.ResumeId &&
                e.TenantId == message.TenantId &&
                e.ParsedText == "Developer with C# skills"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_ValidEvent_DoesNotStoreAiParsedContentSynchronously()
    {
        // Arrange — AI parsing is now async; AiParsedContent is not set during basic parse
        ResumeUploadedEvent message = CreateEvent();
        (ProfilesDbContext seedDb, ProfilesDbContext assertDb) = SetupFactory(message.TenantId);
        await SeedResumeAsync(seedDb, message.ResumeId);

        _resumeParser.ParseAsync(message.FileUrl, message.FileType, Arg.Any<CancellationToken>())
            .Returns(new ResumeParseResult { ParsedText = "Some text" });

        // Act
        await _sut.Consume(CreateConsumeContext(message));

        // Assert
        Resume? updated = await assertDb.Resumes.FindAsync(message.ResumeId);
        updated!.IsParsed.Should().BeTrue();
        updated.ParsedText.Should().Be("Some text");
        updated.AiParsedContent.Should().BeNull();
    }

    [Fact]
    public async Task Consume_BasicParserFails_SetsParseErrorAndRethrows()
    {
        // Arrange
        ResumeUploadedEvent message = CreateEvent();
        (ProfilesDbContext seedDb, ProfilesDbContext assertDb) = SetupFactory(message.TenantId);
        await SeedResumeAsync(seedDb, message.ResumeId);

        _resumeParser.ParseAsync(message.FileUrl, message.FileType, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Corrupt PDF"));

        // Act
        Func<Task> act = () => _sut.Consume(CreateConsumeContext(message));

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        Resume? updated = await assertDb.Resumes.FindAsync(message.ResumeId);
        updated!.ParseError.Should().Be("Corrupt PDF");
        updated.IsParsed.Should().BeFalse();
    }

    [Fact]
    public async Task Consume_ResumeNotFound_LogsWarningAndReturns()
    {
        // Arrange
        ResumeUploadedEvent message = CreateEvent();
        (ProfilesDbContext _, ProfilesDbContext _) = SetupFactory(message.TenantId);

        // Act — no resume seeded in DB
        await _sut.Consume(CreateConsumeContext(message));

        // Assert
        await _resumeParser.DidNotReceive()
            .ParseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_AlreadyParsed_SkipsProcessing()
    {
        // Arrange
        ResumeUploadedEvent message = CreateEvent();
        (ProfilesDbContext seedDb, ProfilesDbContext _) = SetupFactory(message.TenantId);
        await SeedResumeAsync(seedDb, message.ResumeId, isParsed: true);

        // Act
        await _sut.Consume(CreateConsumeContext(message));

        // Assert
        await _resumeParser.DidNotReceive()
            .ParseAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_ValidEvent_ResolvesCorrectTenantConnection()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        ResumeUploadedEvent message = CreateEvent(tenantId: tenantId);
        (ProfilesDbContext seedDb, ProfilesDbContext _) = SetupFactory(tenantId);
        await SeedResumeAsync(seedDb, message.ResumeId);

        _resumeParser.ParseAsync(message.FileUrl, message.FileType, Arg.Any<CancellationToken>())
            .Returns(new ResumeParseResult { ParsedText = "text" });

        // Act
        await _sut.Consume(CreateConsumeContext(message));

        // Assert
        await _connectionResolver.Received(1).GetConnectionStringAsync(tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Consume_SuccessfulParse_SetsParsedAtTimestamp()
    {
        // Arrange
        ResumeUploadedEvent message = CreateEvent();
        (ProfilesDbContext seedDb, ProfilesDbContext assertDb) = SetupFactory(message.TenantId);
        await SeedResumeAsync(seedDb, message.ResumeId);

        DateTime before = DateTime.UtcNow;
        _resumeParser.ParseAsync(message.FileUrl, message.FileType, Arg.Any<CancellationToken>())
            .Returns(new ResumeParseResult { ParsedText = "text" });

        // Act
        await _sut.Consume(CreateConsumeContext(message));

        // Assert
        Resume? updated = await assertDb.Resumes.FindAsync(message.ResumeId);
        updated!.IsParsed.Should().BeTrue();
        updated.ParsedAt.Should().NotBeNull();
        updated.ParsedAt.Should().BeOnOrAfter(before);
    }
}

using FluentAssertions;
using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.Modules.Profiles.Infrastructure.Consumers;
using Jobsite.Modules.Profiles.Infrastructure.Persistence;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Profiles;

public sealed class ResumeParsedConsumerTests : IDisposable
{
    private readonly ITenantConnectionResolver _connectionResolver = Substitute.For<ITenantConnectionResolver>();
    private readonly ITenantDbContextFactory<ProfilesDbContext> _contextFactory =
        Substitute.For<ITenantDbContextFactory<ProfilesDbContext>>();
    private readonly ILogger<ResumeParsedConsumer> _logger =
        Substitute.For<ILogger<ResumeParsedConsumer>>();
    private readonly ResumeParsedConsumer _sut;
    private readonly List<ProfilesDbContext> _contexts = [];

    public ResumeParsedConsumerTests()
    {
        _sut = new ResumeParsedConsumer(_connectionResolver, _contextFactory, _logger);
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

    private static ConsumeContext<ResumeParsed> CreateConsumeContext(ResumeParsed message)
    {
        ConsumeContext<ResumeParsed> context = Substitute.For<ConsumeContext<ResumeParsed>>();
        context.Message.Returns(message);
        context.CancellationToken.Returns(CancellationToken.None);
        return context;
    }

    private static ResumeParsed CreateEvent(Guid? resumeId = null, Guid? tenantId = null) => new()
    {
        EventId = Guid.NewGuid(),
        ResumeId = resumeId ?? Guid.NewGuid(),
        TenantId = tenantId ?? Guid.NewGuid(),
        AiParsedContent = """{"skills": ["C#", ".NET"], "experience_years": 5}""",
        CorrelationId = Guid.NewGuid().ToString(),
        OccurredAt = DateTime.UtcNow
    };

    private async Task<Resume> SeedResumeAsync(ProfilesDbContext db, Guid resumeId)
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
            IsParsed = true,
            ParsedText = "Experienced .NET developer",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Resumes.Add(resume);
        await db.SaveChangesAsync();
        return resume;
    }

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

    [Fact]
    public async Task Consume_ValidEvent_UpdatesAiParsedContent()
    {
        // Arrange
        ResumeParsed message = CreateEvent();
        (ProfilesDbContext seedDb, ProfilesDbContext assertDb) = SetupFactory(message.TenantId);
        await SeedResumeAsync(seedDb, message.ResumeId);

        // Act
        await _sut.Consume(CreateConsumeContext(message));

        // Assert
        Resume? updated = await assertDb.Resumes.FindAsync(message.ResumeId);
        updated!.AiParsedContent.Should().Be(message.AiParsedContent);
    }

    [Fact]
    public async Task Consume_ResumeNotFound_DoesNotThrow()
    {
        // Arrange
        ResumeParsed message = CreateEvent();
        (ProfilesDbContext _, ProfilesDbContext _) = SetupFactory(message.TenantId);
        // No resume seeded

        // Act
        Func<Task> act = async () => await _sut.Consume(CreateConsumeContext(message));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Consume_ValidEvent_PreservesExistingFields()
    {
        // Arrange
        ResumeParsed message = CreateEvent();
        (ProfilesDbContext seedDb, ProfilesDbContext assertDb) = SetupFactory(message.TenantId);
        await SeedResumeAsync(seedDb, message.ResumeId);

        // Act
        await _sut.Consume(CreateConsumeContext(message));

        // Assert — existing fields untouched
        Resume? updated = await assertDb.Resumes.FindAsync(message.ResumeId);
        updated!.ParsedText.Should().Be("Experienced .NET developer");
        updated.IsParsed.Should().BeTrue();
        updated.AiParsedContent.Should().Be(message.AiParsedContent);
    }
}

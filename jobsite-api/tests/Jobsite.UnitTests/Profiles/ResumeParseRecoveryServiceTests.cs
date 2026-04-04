using FluentAssertions;
using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.Modules.Profiles.Infrastructure.Persistence;
using Jobsite.Modules.Profiles.Infrastructure.Services;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Jobsite.UnitTests.Profiles;

public sealed class ResumeParseRecoveryServiceTests : IDisposable
{
    private readonly ITenantConnectionResolver _connectionResolver = Substitute.For<ITenantConnectionResolver>();
    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();
    private readonly ITenantDbContextFactory<ProfilesDbContext> _contextFactory =
        Substitute.For<ITenantDbContextFactory<ProfilesDbContext>>();
    private readonly ILogger<ResumeParseRecoveryService> _logger =
        Substitute.For<ILogger<ResumeParseRecoveryService>>();
    private readonly List<ProfilesDbContext> _contexts = [];

    public void Dispose()
    {
        foreach (ProfilesDbContext context in _contexts)
        {
            context.Dispose();
        }
    }

    private ProfilesDbContext CreateInMemoryContext()
    {
        DbContextOptions<ProfilesDbContext> options = new DbContextOptionsBuilder<ProfilesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        ProfilesDbContext context = new(options);
        _contexts.Add(context);
        return context;
    }

    private ResumeParseRecoveryService CreateService()
    {
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(ITenantConnectionResolver)).Returns(_connectionResolver);
        serviceProvider.GetService(typeof(IEventPublisher)).Returns(_eventPublisher);
        serviceProvider.GetService(typeof(ITenantDbContextFactory<ProfilesDbContext>)).Returns(_contextFactory);

        IServiceScope scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        // Wrap in AsyncServiceScope-compatible mock
        IServiceScopeFactory scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        return new ResumeParseRecoveryService(scopeFactory, _logger);
    }

    private async Task<Resume> SeedResumeAsync(
        ProfilesDbContext db, bool isParsed = false, string? parseError = null)
    {
        Resume resume = new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            FileUrl = "resumes/test.pdf",
            OriginalFilename = "test.pdf",
            FileSizeBytes = 1024,
            FileType = "PDF",
            IsLatest = true,
            IsParsed = isParsed,
            ParseError = parseError,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Resumes.Add(resume);
        await db.SaveChangesAsync();
        return resume;
    }

    private async Task ExecuteServiceAsync(ResumeParseRecoveryService service, int timeoutMs = 10000)
    {
        using CancellationTokenSource cts = new(timeoutMs);
        try
        {
            await service.StartAsync(cts.Token);
            // Give the background service time to complete its Task.Delay + processing
            await Task.Delay(7000, cts.Token);
            await service.StopAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when service completes or times out
        }
    }

    // ── Tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_UnparsedResumesExist_RepublishesEvents()
    {
        // Arrange
        ProfilesDbContext db = CreateInMemoryContext();
        Resume resume1 = await SeedResumeAsync(db);
        Resume resume2 = await SeedResumeAsync(db);
        Guid tenantId = Guid.NewGuid();

        _connectionResolver.GetAllConnectionsAsync(Arg.Any<CancellationToken>())
            .Returns([new TenantConnection { TenantId = tenantId, ConnectionString = "connstr" }]);
        _contextFactory.CreateDbContext("connstr").Returns(db);

        ResumeParseRecoveryService service = CreateService();

        // Act
        await ExecuteServiceAsync(service);

        // Assert
        await _eventPublisher.Received(2).PublishAsync(
            Arg.Is<ResumeUploadedEvent>(e => e.ResumeId == resume1.Id || e.ResumeId == resume2.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_NoUnparsedResumes_PublishesNothing()
    {
        // Arrange
        ProfilesDbContext db = CreateInMemoryContext();
        await SeedResumeAsync(db, isParsed: true); // already parsed

        _connectionResolver.GetAllConnectionsAsync(Arg.Any<CancellationToken>())
            .Returns([new TenantConnection { TenantId = Guid.NewGuid(), ConnectionString = "connstr" }]);
        _contextFactory.CreateDbContext("connstr").Returns(db);

        ResumeParseRecoveryService service = CreateService();

        // Act
        await ExecuteServiceAsync(service);

        // Assert
        await _eventPublisher.DidNotReceive()
            .PublishAsync(Arg.Any<ResumeUploadedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_MultipleTenants_ProcessesAllTenants()
    {
        // Arrange
        ProfilesDbContext db1 = CreateInMemoryContext();
        ProfilesDbContext db2 = CreateInMemoryContext();
        await SeedResumeAsync(db1);
        await SeedResumeAsync(db2);

        Guid tenantId1 = Guid.NewGuid();
        Guid tenantId2 = Guid.NewGuid();

        _connectionResolver.GetAllConnectionsAsync(Arg.Any<CancellationToken>())
            .Returns([
                new TenantConnection { TenantId = tenantId1, ConnectionString = "connstr1" },
                new TenantConnection { TenantId = tenantId2, ConnectionString = "connstr2" }
            ]);
        _contextFactory.CreateDbContext("connstr1").Returns(db1);
        _contextFactory.CreateDbContext("connstr2").Returns(db2);

        ResumeParseRecoveryService service = CreateService();

        // Act
        await ExecuteServiceAsync(service);

        // Assert
        await _eventPublisher.Received(2).PublishAsync(
            Arg.Any<ResumeUploadedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_TenantFails_ContinuesWithOtherTenants()
    {
        // Arrange
        ProfilesDbContext db2 = CreateInMemoryContext();
        await SeedResumeAsync(db2);

        Guid tenantId1 = Guid.NewGuid();
        Guid tenantId2 = Guid.NewGuid();

        _connectionResolver.GetAllConnectionsAsync(Arg.Any<CancellationToken>())
            .Returns([
                new TenantConnection { TenantId = tenantId1, ConnectionString = "connstr1" },
                new TenantConnection { TenantId = tenantId2, ConnectionString = "connstr2" }
            ]);
        _contextFactory.CreateDbContext("connstr1")
            .Throws(new InvalidOperationException("DB connection failed"));
        _contextFactory.CreateDbContext("connstr2").Returns(db2);

        ResumeParseRecoveryService service = CreateService();

        // Act
        await ExecuteServiceAsync(service);

        // Assert — tenant 2 should still be processed despite tenant 1 failure
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Any<ResumeUploadedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_OnlyReprocessesNotParsedAndNoError()
    {
        // Arrange
        ProfilesDbContext db = CreateInMemoryContext();
        await SeedResumeAsync(db, isParsed: false, parseError: null);        // should be re-queued
        await SeedResumeAsync(db, isParsed: true);                           // already parsed — skip
        await SeedResumeAsync(db, isParsed: false, parseError: "Failed");    // has error — skip

        _connectionResolver.GetAllConnectionsAsync(Arg.Any<CancellationToken>())
            .Returns([new TenantConnection { TenantId = Guid.NewGuid(), ConnectionString = "connstr" }]);
        _contextFactory.CreateDbContext("connstr").Returns(db);

        ResumeParseRecoveryService service = CreateService();

        // Act
        await ExecuteServiceAsync(service);

        // Assert — only the first resume (not parsed, no error) should be re-queued
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Any<ResumeUploadedEvent>(), Arg.Any<CancellationToken>());
    }
}

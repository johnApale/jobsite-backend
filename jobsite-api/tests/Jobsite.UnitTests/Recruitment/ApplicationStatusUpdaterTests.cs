using FluentAssertions;
using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Infrastructure.CrossModule;
using Jobsite.Modules.Recruitment.Infrastructure.Persistence;
using Jobsite.SharedKernel.Errors;
using Microsoft.EntityFrameworkCore;
using ApplicationEntity = Jobsite.Modules.Recruitment.Domain.Entities.Application;

namespace Jobsite.UnitTests.Recruitment;

public sealed class ApplicationStatusUpdaterTests : IDisposable
{
    private readonly List<RecruitmentDbContext> _contexts = [];

    public void Dispose()
    {
        foreach (RecruitmentDbContext context in _contexts)
        {
            context.Dispose();
        }
    }

    private RecruitmentDbContext CreateInMemoryContext()
    {
        DbContextOptions<RecruitmentDbContext> options = new DbContextOptionsBuilder<RecruitmentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        RecruitmentDbContext context = new(options);
        _contexts.Add(context);
        return context;
    }

    private static ApplicationEntity CreateApplication(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        JobPostingId = Guid.NewGuid(),
        ApplicantId = Guid.NewGuid(),
        ResumeId = Guid.NewGuid(),
        Status = ApplicationStatus.Submitted,
        SubmittedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task UpdateStatusAsync_ValidApplication_UpdatesStatusAndSaves()
    {
        // Arrange
        RecruitmentDbContext db = CreateInMemoryContext();
        ApplicationEntity application = CreateApplication();
        db.Applications.Add(application);
        await db.SaveChangesAsync();

        ApplicationStatusUpdater sut = new(db);

        // Act
        await sut.UpdateStatusAsync(application.Id, ApplicationStatus.Screening, null, null, CancellationToken.None);

        // Assert
        ApplicationEntity? updated = await db.Applications.FindAsync(application.Id);
        updated!.Status.Should().Be(ApplicationStatus.Screening);
    }

    [Fact]
    public async Task UpdateStatusAsync_SetsRejectionReason_WhenProvided()
    {
        // Arrange
        RecruitmentDbContext db = CreateInMemoryContext();
        ApplicationEntity application = CreateApplication();
        db.Applications.Add(application);
        await db.SaveChangesAsync();

        ApplicationStatusUpdater sut = new(db);

        // Act
        await sut.UpdateStatusAsync(
            application.Id,
            ApplicationStatus.Rejected,
            "Does not meet minimum requirements",
            "Screening",
            CancellationToken.None);

        // Assert
        ApplicationEntity? updated = await db.Applications.FindAsync(application.Id);
        updated!.Status.Should().Be(ApplicationStatus.Rejected);
        updated.RejectionReason.Should().Be("Does not meet minimum requirements");
        updated.RejectedAtStage.Should().Be("Screening");
    }

    [Fact]
    public async Task UpdateStatusAsync_SetsUpdatedAt()
    {
        // Arrange
        RecruitmentDbContext db = CreateInMemoryContext();
        ApplicationEntity application = CreateApplication();
        db.Applications.Add(application);
        await db.SaveChangesAsync();

        ApplicationStatusUpdater sut = new(db);
        DateTime before = DateTime.UtcNow;

        // Act
        await sut.UpdateStatusAsync(application.Id, ApplicationStatus.Assessment, null, null, CancellationToken.None);

        // Assert
        ApplicationEntity? updated = await db.Applications.FindAsync(application.Id);
        updated!.UpdatedAt.Should().BeOnOrAfter(before);
    }

    [Fact]
    public async Task UpdateStatusAsync_NonExistentApplication_ThrowsAppError()
    {
        // Arrange
        RecruitmentDbContext db = CreateInMemoryContext();
        ApplicationStatusUpdater sut = new(db);

        // Act
        Func<Task> act = () => sut.UpdateStatusAsync(
            Guid.NewGuid(), ApplicationStatus.Screening, null, null, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("APPLICATION_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateStatusAsync_NullRejectionFields_SetsNullValues()
    {
        // Arrange
        RecruitmentDbContext db = CreateInMemoryContext();
        ApplicationEntity application = CreateApplication();
        application.RejectionReason = "Old reason";
        application.RejectedAtStage = "OldStage";
        db.Applications.Add(application);
        await db.SaveChangesAsync();

        ApplicationStatusUpdater sut = new(db);

        // Act
        await sut.UpdateStatusAsync(application.Id, ApplicationStatus.Screening, null, null, CancellationToken.None);

        // Assert
        ApplicationEntity? updated = await db.Applications.FindAsync(application.Id);
        updated!.RejectionReason.Should().BeNull();
        updated.RejectedAtStage.Should().BeNull();
    }
}

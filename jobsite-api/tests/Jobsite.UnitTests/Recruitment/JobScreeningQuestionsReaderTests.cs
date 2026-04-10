using FluentAssertions;
using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.Modules.Recruitment.Infrastructure.CrossModule;
using Jobsite.Modules.Recruitment.Infrastructure.Persistence;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.UnitTests.Recruitment;

public sealed class JobScreeningQuestionsReaderTests : IDisposable
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

    private static JobScreeningQuestion CreateQuestion(
        Guid jobPostingId, int displayOrder = 0, string? timing = null, string? questionText = null) => new()
        {
            Id = Guid.NewGuid(),
            JobPostingId = jobPostingId,
            QuestionText = questionText ?? "Do you have 5+ years experience?",
            QuestionType = QuestionType.YesNo,
            Timing = timing ?? QuestionTiming.AtApplication,
            IsRequired = true,
            Weight = 10.0m,
            DisplayOrder = displayOrder,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

    // ── GetQuestionsForJobAsync ──────────────────────────────────────────

    [Fact]
    public async Task GetQuestionsForJobAsync_ExistingJob_ReturnsQuestionSnapshots()
    {
        // Arrange
        RecruitmentDbContext db = CreateInMemoryContext();
        Guid jobPostingId = Guid.NewGuid();
        db.JobScreeningQuestions.Add(CreateQuestion(jobPostingId, questionText: "Question 1"));
        db.JobScreeningQuestions.Add(CreateQuestion(jobPostingId, displayOrder: 1, questionText: "Question 2"));
        await db.SaveChangesAsync();

        JobScreeningQuestionsReader sut = new(db);

        // Act
        List<QuestionSnapshot> results = await sut.GetQuestionsForJobAsync(jobPostingId, CancellationToken.None);

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(q => q.QuestionText == "Question 1");
        results.Should().Contain(q => q.QuestionText == "Question 2");
    }

    [Fact]
    public async Task GetQuestionsForJobAsync_NoQuestions_ReturnsEmptyList()
    {
        // Arrange
        RecruitmentDbContext db = CreateInMemoryContext();
        JobScreeningQuestionsReader sut = new(db);

        // Act
        List<QuestionSnapshot> results = await sut.GetQuestionsForJobAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetQuestionsForJobAsync_OrdersByDisplayOrder()
    {
        // Arrange
        RecruitmentDbContext db = CreateInMemoryContext();
        Guid jobPostingId = Guid.NewGuid();
        db.JobScreeningQuestions.Add(CreateQuestion(jobPostingId, displayOrder: 2, questionText: "Third"));
        db.JobScreeningQuestions.Add(CreateQuestion(jobPostingId, displayOrder: 0, questionText: "First"));
        db.JobScreeningQuestions.Add(CreateQuestion(jobPostingId, displayOrder: 1, questionText: "Second"));
        await db.SaveChangesAsync();

        JobScreeningQuestionsReader sut = new(db);

        // Act
        List<QuestionSnapshot> results = await sut.GetQuestionsForJobAsync(jobPostingId, CancellationToken.None);

        // Assert
        results.Should().HaveCount(3);
        results[0].QuestionText.Should().Be("First");
        results[1].QuestionText.Should().Be("Second");
        results[2].QuestionText.Should().Be("Third");
    }

    // ── HasAfterScreeningQuestionsAsync ──────────────────────────────────

    [Fact]
    public async Task HasAfterScreeningQuestionsAsync_HasQuestions_ReturnsTrue()
    {
        // Arrange
        RecruitmentDbContext db = CreateInMemoryContext();
        Guid jobPostingId = Guid.NewGuid();
        db.JobScreeningQuestions.Add(CreateQuestion(jobPostingId, timing: QuestionTiming.AfterScreening));
        await db.SaveChangesAsync();

        JobScreeningQuestionsReader sut = new(db);

        // Act
        bool result = await sut.HasAfterScreeningQuestionsAsync(jobPostingId, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasAfterScreeningQuestionsAsync_NoQuestions_ReturnsFalse()
    {
        // Arrange
        RecruitmentDbContext db = CreateInMemoryContext();
        Guid jobPostingId = Guid.NewGuid();
        db.JobScreeningQuestions.Add(CreateQuestion(jobPostingId, timing: QuestionTiming.AtApplication));
        await db.SaveChangesAsync();

        JobScreeningQuestionsReader sut = new(db);

        // Act
        bool result = await sut.HasAfterScreeningQuestionsAsync(jobPostingId, CancellationToken.None);

        // Assert
        result.Should().BeFalse();
    }
}

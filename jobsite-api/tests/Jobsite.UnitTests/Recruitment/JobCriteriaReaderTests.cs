using FluentAssertions;
using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.Modules.Recruitment.Infrastructure.CrossModule;
using Jobsite.Modules.Recruitment.Infrastructure.Persistence;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.UnitTests.Recruitment;

public sealed class JobCriteriaReaderTests : IDisposable
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

    private static JobEvaluationCriteria CreateCriteria(
        Guid jobPostingId, int displayOrder = 0, string? name = null) => new()
    {
        Id = Guid.NewGuid(),
        JobPostingId = jobPostingId,
        Name = name ?? "C# Proficiency",
        Category = CriteriaCategory.Skill,
        EvaluationMethod = EvaluationMethod.SemanticSimilarity,
        IsRequired = true,
        Weight = 25.0m,
        Configuration = """{"skill_name":"C#"}""",
        DisplayOrder = displayOrder,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task GetCriteriaForJobAsync_ExistingJob_ReturnsCriteriaSnapshots()
    {
        // Arrange
        RecruitmentDbContext db = CreateInMemoryContext();
        Guid jobPostingId = Guid.NewGuid();
        db.JobEvaluationCriteria.Add(CreateCriteria(jobPostingId, name: "C# Proficiency"));
        db.JobEvaluationCriteria.Add(CreateCriteria(jobPostingId, displayOrder: 1, name: "SQL Knowledge"));
        await db.SaveChangesAsync();

        JobCriteriaReader sut = new(db);

        // Act
        List<CriteriaSnapshot> results = await sut.GetCriteriaForJobAsync(jobPostingId, CancellationToken.None);

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(c => c.Name == "C# Proficiency");
        results.Should().Contain(c => c.Name == "SQL Knowledge");
    }

    [Fact]
    public async Task GetCriteriaForJobAsync_NoCriteria_ReturnsEmptyList()
    {
        // Arrange
        RecruitmentDbContext db = CreateInMemoryContext();
        JobCriteriaReader sut = new(db);

        // Act
        List<CriteriaSnapshot> results = await sut.GetCriteriaForJobAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCriteriaForJobAsync_OrdersByDisplayOrder()
    {
        // Arrange
        RecruitmentDbContext db = CreateInMemoryContext();
        Guid jobPostingId = Guid.NewGuid();
        db.JobEvaluationCriteria.Add(CreateCriteria(jobPostingId, displayOrder: 2, name: "Third"));
        db.JobEvaluationCriteria.Add(CreateCriteria(jobPostingId, displayOrder: 0, name: "First"));
        db.JobEvaluationCriteria.Add(CreateCriteria(jobPostingId, displayOrder: 1, name: "Second"));
        await db.SaveChangesAsync();

        JobCriteriaReader sut = new(db);

        // Act
        List<CriteriaSnapshot> results = await sut.GetCriteriaForJobAsync(jobPostingId, CancellationToken.None);

        // Assert
        results.Should().HaveCount(3);
        results[0].Name.Should().Be("First");
        results[1].Name.Should().Be("Second");
        results[2].Name.Should().Be("Third");
    }

    [Fact]
    public async Task GetCriteriaForJobAsync_MapsAllFields()
    {
        // Arrange
        RecruitmentDbContext db = CreateInMemoryContext();
        Guid jobPostingId = Guid.NewGuid();
        Guid criteriaId = Guid.NewGuid();
        db.JobEvaluationCriteria.Add(new JobEvaluationCriteria
        {
            Id = criteriaId,
            JobPostingId = jobPostingId,
            Name = "Python Expertise",
            Category = CriteriaCategory.Skill,
            EvaluationMethod = EvaluationMethod.ExactMatch,
            IsRequired = false,
            Weight = 15.5m,
            Configuration = """{"min_years":3}""",
            DisplayOrder = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        JobCriteriaReader sut = new(db);

        // Act
        List<CriteriaSnapshot> results = await sut.GetCriteriaForJobAsync(jobPostingId, CancellationToken.None);

        // Assert
        CriteriaSnapshot snapshot = results.Single();
        snapshot.Id.Should().Be(criteriaId);
        snapshot.Name.Should().Be("Python Expertise");
        snapshot.Category.Should().Be(CriteriaCategory.Skill);
        snapshot.EvaluationMethod.Should().Be(EvaluationMethod.ExactMatch);
        snapshot.IsRequired.Should().BeFalse();
        snapshot.Weight.Should().Be(15.5m);
        snapshot.Configuration.Should().Be("""{"min_years":3}""");
    }
}

using FluentAssertions;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.Modules.Recruitment.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using ApplicationEntity = Jobsite.Modules.Recruitment.Domain.Entities.Application;

namespace Jobsite.IntegrationTests.Recruitment;

/// <summary>
/// Integration tests for Recruitment repositories (JobPostingRepository, ApplicationRepository,
/// ClientCompanyRepository) against a real PostgreSQL container.
/// Validates CRUD operations, cursor-based pagination, filtering, and query behavior.
/// </summary>
[Collection("Recruitment")]
public sealed class RecruitmentRepositoryTests : IAsyncLifetime
{
    private readonly RecruitmentIntegrationFixture _fixture;
    private JobPostingRepository _jobPostingRepo = null!;
    private ApplicationRepository _applicationRepo = null!;
    private ClientCompanyRepository _clientCompanyRepo = null!;

    public RecruitmentRepositoryTests(RecruitmentIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.ResetDataAsync();
        _jobPostingRepo = new JobPostingRepository(_fixture.DbContext);
        _applicationRepo = new ApplicationRepository(_fixture.DbContext);
        _clientCompanyRepo = new ClientCompanyRepository(_fixture.DbContext);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ── JobPostingRepository ─────────────────────────────────────────────

    [Fact]
    public async Task JobPosting_Add_PersistsToDatabase()
    {
        // Arrange
        JobPosting posting = CreateJobPosting("Add Test Job");

        // Act
        _jobPostingRepo.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        _fixture.DbContext.ChangeTracker.Clear();
        JobPosting? persisted = await _fixture.DbContext.JobPostings
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Title == "Add Test Job");

        persisted.Should().NotBeNull();
        persisted!.Id.Should().NotBe(Guid.Empty);
        persisted.Status.Should().Be(JobPostingStatus.Draft);
    }

    [Fact]
    public async Task JobPosting_GetByIdAsync_ExistingId_ReturnsJobPosting()
    {
        // Arrange
        JobPosting posting = CreateJobPosting("GetById Test");
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        JobPosting? result = await _jobPostingRepo.GetByIdAsync(posting.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("GetById Test");
    }

    [Fact]
    public async Task JobPosting_GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Arrange & Act
        JobPosting? result = await _jobPostingRepo.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task JobPosting_GetByIdForUpdateAsync_ReturnsTrackedEntity()
    {
        // Arrange
        JobPosting posting = CreateJobPosting("ForUpdate Test");
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        JobPosting? result = await _jobPostingRepo.GetByIdForUpdateAsync(posting.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry? entry =
            _fixture.DbContext.ChangeTracker.Entries<JobPosting>()
                .FirstOrDefault(e => e.Entity.Id == result!.Id);
        entry.Should().NotBeNull("entity should be tracked for updates");
    }

    [Fact]
    public async Task JobPosting_GetByIdWithDetailsAsync_IncludesCriteriaAndQuestions()
    {
        // Arrange
        JobPosting posting = CreateJobPosting("WithDetails Test");
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        JobEvaluationCriteria criteria = new()
        {
            JobPostingId = posting.Id,
            Name = "Python",
            Category = CriteriaCategory.Skill,
            EvaluationMethod = EvaluationMethod.ExactMatch,
            IsRequired = true,
            Weight = 50m,
            Configuration = """{"skill_name": "Python"}""",
            DisplayOrder = 1
        };
        _fixture.DbContext.JobEvaluationCriteria.Add(criteria);

        JobScreeningQuestion question = new()
        {
            JobPostingId = posting.Id,
            QuestionText = "Why apply?",
            QuestionType = QuestionType.FreeText,
            Timing = QuestionTiming.AtApplication,
            IsRequired = true,
            Weight = 5m,
            DisplayOrder = 1
        };
        _fixture.DbContext.JobScreeningQuestions.Add(question);
        await _fixture.DbContext.SaveChangesAsync();

        _fixture.DbContext.ChangeTracker.Clear();

        // Act
        JobPosting? result = await _jobPostingRepo.GetByIdWithDetailsAsync(posting.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Criteria.Should().HaveCount(1);
        result.Criteria[0].Name.Should().Be("Python");
        result.Questions.Should().HaveCount(1);
        result.Questions[0].QuestionText.Should().Be("Why apply?");
    }

    [Fact]
    public async Task JobPosting_ExistsByIdAsync_ExistingId_ReturnsTrue()
    {
        // Arrange
        JobPosting posting = CreateJobPosting("Exists Test");
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        bool exists = await _jobPostingRepo.ExistsByIdAsync(posting.Id, CancellationToken.None);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task JobPosting_ExistsByIdAsync_NonExistentId_ReturnsFalse()
    {
        // Arrange & Act
        bool exists = await _jobPostingRepo.ExistsByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task JobPosting_ListAsync_ReturnsCursorPaginatedResults()
    {
        // Arrange — create 5 job postings
        for (int i = 1; i <= 5; i++)
        {
            JobPosting posting = CreateJobPosting($"Paginated Job {i}");
            _fixture.DbContext.JobPostings.Add(posting);
            await _fixture.DbContext.SaveChangesAsync();
            await Task.Delay(10); // Ensure distinct CreatedAt
        }

        // Act — page 1
        JobPostingQueryParameters page1Params = new() { PageSize = 3 };
        JobPostingListResponse page1 = await _jobPostingRepo.ListAsync(page1Params, CancellationToken.None);

        // Assert
        page1.Items.Should().HaveCount(3);
        page1.NextCursor.Should().NotBeNull();

        // Act — page 2
        JobPostingQueryParameters page2Params = new()
        {
            PageSize = 3,
            Cursor = page1.NextCursor
        };
        JobPostingListResponse page2 = await _jobPostingRepo.ListAsync(page2Params, CancellationToken.None);

        // Assert
        page2.Items.Should().HaveCount(2);
        page2.NextCursor.Should().BeNull();

        // Verify ordering (newest first)
        page1.Items[0].CreatedAt.Should().BeOnOrAfter(page1.Items[1].CreatedAt);
    }

    [Fact]
    public async Task JobPosting_ListAsync_FilterByStatus_ReturnsMatchingOnly()
    {
        // Arrange
        JobPosting draft = CreateJobPosting("Draft for Filter");
        _fixture.DbContext.JobPostings.Add(draft);

        JobPosting published = CreateJobPosting("Published for Filter");
        published.Publish();
        _fixture.DbContext.JobPostings.Add(published);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        JobPostingQueryParameters filterParams = new() { Status = JobPostingStatus.Published };
        JobPostingListResponse results = await _jobPostingRepo.ListAsync(filterParams, CancellationToken.None);

        // Assert
        results.Items.Should().AllSatisfy(j => j.Status.Should().Be(JobPostingStatus.Published));
    }

    // ── ApplicationRepository ────────────────────────────────────────────

    [Fact]
    public async Task Application_Add_PersistsToDatabase()
    {
        // Arrange — need a job posting (FK constraint)
        JobPosting posting = CreateJobPosting("App Add Test Job");
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        Guid applicantId = Guid.NewGuid();
        ApplicationEntity application = new()
        {
            JobPostingId = posting.Id,
            ApplicantId = applicantId,
            ResumeId = Guid.NewGuid(),
            SubmittedAt = DateTime.UtcNow
        };

        // Act
        _applicationRepo.Add(application);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        _fixture.DbContext.ChangeTracker.Clear();
        ApplicationEntity? persisted = await _fixture.DbContext.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ApplicantId == applicantId);
        persisted.Should().NotBeNull();
        persisted!.JobPostingId.Should().Be(posting.Id);
    }

    [Fact]
    public async Task Application_GetByIdAsync_ExistingId_ReturnsApplication()
    {
        // Arrange
        JobPosting posting = CreateJobPosting("App GetById Job");
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        ApplicationEntity application = CreateApplication(posting.Id);
        _fixture.DbContext.Applications.Add(application);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        ApplicationEntity? result = await _applicationRepo.GetByIdAsync(application.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.JobPostingId.Should().Be(posting.Id);
    }

    [Fact]
    public async Task Application_GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Arrange & Act
        ApplicationEntity? result = await _applicationRepo.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Application_ExistsByApplicantAndJobAsync_Duplicate_ReturnsTrue()
    {
        // Arrange
        JobPosting posting = CreateJobPosting("Exists App Job");
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        Guid applicantId = Guid.NewGuid();
        ApplicationEntity application = CreateApplication(posting.Id, applicantId);
        _fixture.DbContext.Applications.Add(application);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        bool exists = await _applicationRepo.ExistsByApplicantAndJobAsync(
            applicantId, posting.Id, CancellationToken.None);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task Application_ExistsByApplicantAndJobAsync_NoDuplicate_ReturnsFalse()
    {
        // Arrange & Act
        bool exists = await _applicationRepo.ExistsByApplicantAndJobAsync(
            Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Application_ListAsync_FilterByJobPosting_ReturnsMatchingOnly()
    {
        // Arrange
        JobPosting posting1 = CreateJobPosting("Filter Job 1");
        JobPosting posting2 = CreateJobPosting("Filter Job 2");
        _fixture.DbContext.JobPostings.AddRange(posting1, posting2);
        await _fixture.DbContext.SaveChangesAsync();

        ApplicationEntity app1 = CreateApplication(posting1.Id);
        ApplicationEntity app2 = CreateApplication(posting2.Id);
        _fixture.DbContext.Applications.AddRange(app1, app2);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        ApplicationQueryParameters filterParams = new() { JobPostingId = posting1.Id };
        ApplicationListResponse results = await _applicationRepo.ListAsync(filterParams, CancellationToken.None);

        // Assert
        results.Items.Should().HaveCount(1);
        results.Items[0].JobPostingId.Should().Be(posting1.Id);
    }

    [Fact]
    public async Task Application_ListAsync_FilterByStatus_ReturnsMatchingOnly()
    {
        // Arrange
        JobPosting posting = CreateJobPosting("Status Filter Job");
        _fixture.DbContext.JobPostings.Add(posting);
        await _fixture.DbContext.SaveChangesAsync();

        ApplicationEntity submitted = CreateApplication(posting.Id);
        submitted.Status = ApplicationStatus.Submitted;

        ApplicationEntity withdrawn = CreateApplication(posting.Id);
        withdrawn.Status = ApplicationStatus.Withdrawn;
        withdrawn.WithdrawnAt = DateTime.UtcNow;

        _fixture.DbContext.Applications.AddRange(submitted, withdrawn);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        ApplicationQueryParameters filterParams = new() { Status = ApplicationStatus.Withdrawn };
        ApplicationListResponse results = await _applicationRepo.ListAsync(filterParams, CancellationToken.None);

        // Assert
        results.Items.Should().AllSatisfy(a => a.Status.Should().Be(ApplicationStatus.Withdrawn));
    }

    // ── ClientCompanyRepository ──────────────────────────────────────────

    [Fact]
    public async Task ClientCompany_Add_PersistsToDatabase()
    {
        // Arrange
        ClientCompany company = CreateClientCompany("Add Test Corp");

        // Act
        _clientCompanyRepo.Add(company);
        await _fixture.DbContext.SaveChangesAsync();

        // Assert
        _fixture.DbContext.ChangeTracker.Clear();
        ClientCompany? persisted = await _fixture.DbContext.ClientCompanies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name == "Add Test Corp");
        persisted.Should().NotBeNull();
        persisted!.Status.Should().Be(ClientCompanyStatus.Active);
    }

    [Fact]
    public async Task ClientCompany_GetByIdAsync_ExistingId_ReturnsCompany()
    {
        // Arrange
        ClientCompany company = CreateClientCompany("GetById Corp");
        _fixture.DbContext.ClientCompanies.Add(company);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        ClientCompany? result = await _clientCompanyRepo.GetByIdAsync(company.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("GetById Corp");
    }

    [Fact]
    public async Task ClientCompany_GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Arrange & Act
        ClientCompany? result = await _clientCompanyRepo.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ClientCompany_ExistsByIdAsync_ExistingId_ReturnsTrue()
    {
        // Arrange
        ClientCompany company = CreateClientCompany("Exists Corp");
        _fixture.DbContext.ClientCompanies.Add(company);
        await _fixture.DbContext.SaveChangesAsync();

        // Act
        bool exists = await _clientCompanyRepo.ExistsByIdAsync(company.Id, CancellationToken.None);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ClientCompany_ExistsByIdAsync_NonExistentId_ReturnsFalse()
    {
        // Arrange & Act
        bool exists = await _clientCompanyRepo.ExistsByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ClientCompany_ListAsync_ReturnsCursorPaginatedResults()
    {
        // Arrange
        for (int i = 1; i <= 4; i++)
        {
            ClientCompany company = CreateClientCompany($"Paginated Corp {i}");
            _fixture.DbContext.ClientCompanies.Add(company);
            await _fixture.DbContext.SaveChangesAsync();
            await Task.Delay(10);
        }

        // Act — page 1
        ClientCompanyQueryParameters page1Params = new() { PageSize = 2 };
        ClientCompanyListResponse page1 = await _clientCompanyRepo.ListAsync(page1Params, CancellationToken.None);

        // Assert
        page1.Items.Should().HaveCount(2);
        page1.NextCursor.Should().NotBeNull();

        // Act — page 2
        ClientCompanyQueryParameters page2Params = new()
        {
            PageSize = 2,
            Cursor = page1.NextCursor
        };
        ClientCompanyListResponse page2 = await _clientCompanyRepo.ListAsync(page2Params, CancellationToken.None);

        // Assert
        page2.Items.Should().HaveCount(2);

        // Verify no overlap
        List<Guid> allIds = page1.Items.Select(c => c.Id).Concat(page2.Items.Select(c => c.Id)).ToList();
        allIds.Should().OnlyHaveUniqueItems();
    }

    // ── Factory Methods ──────────────────────────────────────────────────

    private static JobPosting CreateJobPosting(string title) => new()
    {
        Title = title,
        Description = $"Description for {title}",
        LocationType = LocationType.Remote,
        EmploymentType = EmploymentType.FullTime,
        PostedBy = Guid.NewGuid()
    };

    private static ApplicationEntity CreateApplication(Guid jobPostingId, Guid? applicantId = null) => new()
    {
        JobPostingId = jobPostingId,
        ApplicantId = applicantId ?? Guid.NewGuid(),
        Status = ApplicationStatus.Submitted,
        ResumeId = Guid.NewGuid(),
        SubmittedAt = DateTime.UtcNow
    };

    private static ClientCompany CreateClientCompany(string name) => new()
    {
        Name = name,
        Status = ClientCompanyStatus.Active
    };
}

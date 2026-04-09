using FluentAssertions;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Application.Services;
using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.Modules.Recruitment.Infrastructure.Persistence;
using Jobsite.Modules.Recruitment.Infrastructure.Persistence.Repositories;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using ApplicationEntity = Jobsite.Modules.Recruitment.Domain.Entities.Application;

namespace Jobsite.IntegrationTests.E2E;

/// <summary>
/// End-to-end recruitment pipeline tests exercising the full flow:
/// create client company → create job posting → publish → submit application → withdraw.
///
/// Uses real PostgreSQL (Testcontainers) for repositories + real services.
/// Cross-module dependencies (resume ownership verification) are substituted via NSubstitute.
/// </summary>
[Collection("RecruitmentPipeline")]
public sealed class RecruitmentPipelineTests : IAsyncLifetime
{
    private readonly RecruitmentPipelineFixture _fixture;

    private RecruitmentDbContext _db = null!;
    private JobPostingRepository _jobPostingRepo = null!;
    private ApplicationRepository _applicationRepo = null!;
    private ClientCompanyRepository _clientCompanyRepo = null!;

    // Cross-module stubs
    private IResumeOwnershipVerifier _resumeVerifier = null!;

    public RecruitmentPipelineTests(RecruitmentPipelineFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetDataAsync();

        _db = _fixture.CreateDbContext();
        _jobPostingRepo = new JobPostingRepository(_db);
        _applicationRepo = new ApplicationRepository(_db);
        _clientCompanyRepo = new ClientCompanyRepository(_db);

        _resumeVerifier = Substitute.For<IResumeOwnershipVerifier>();
        _resumeVerifier.IsOwnedByUserAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true); // Default: resume always belongs to applicant
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
    }

    private RecruitmentService CreateRecruitmentService()
    {
        return new RecruitmentService(_jobPostingRepo, _clientCompanyRepo, _db);
    }

    private ClientCompanyService CreateClientCompanyService()
    {
        return new ClientCompanyService(_clientCompanyRepo, _db);
    }

    private ApplicationService CreateApplicationService()
    {
        return new ApplicationService(_applicationRepo, _jobPostingRepo, _resumeVerifier, _db);
    }

    // ── Test: Full Recruitment Flow ───────────────────────────────────────

    [Fact]
    public async Task FullRecruitmentFlow_CompanyToApplicationWithdrawal_Succeeds()
    {
        // Arrange
        ClientCompanyService companyService = CreateClientCompanyService();
        RecruitmentService recruitmentService = CreateRecruitmentService();
        ApplicationService applicationService = CreateApplicationService();
        Guid recruiterId = Guid.NewGuid();
        Guid applicantId = Guid.NewGuid();
        Guid resumeId = Guid.NewGuid();

        // Step 1: Create client company
        CreateClientCompanyRequest companyReq = new()
        {
            Name = "Acme Tech Corp",
            DisplayName = "Acme",
            Industry = Industry.Technology,
            ContactName = "HR Director",
            ContactEmail = "hr@acme.com"
        };
        ClientCompanyResponse company = await companyService.CreateAsync(companyReq, CancellationToken.None);
        company.Name.Should().Be("Acme Tech Corp");
        company.Status.Should().Be(ClientCompanyStatus.Active);

        // Step 2: Create job posting linked to company
        CreateJobPostingRequest jobReq = new()
        {
            Title = "Senior .NET Developer",
            Description = "Build amazing things with .NET 10",
            Requirements = "5+ years C# experience",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime,
            SalaryMin = 100000m,
            SalaryMax = 150000m,
            SalaryCurrency = "USD",
            Department = "Engineering",
            ClientCompanyId = company.Id
        };
        JobPostingResponse job = await recruitmentService.CreateAsync(jobReq, recruiterId, CancellationToken.None);
        job.Status.Should().Be(JobPostingStatus.Draft);
        job.ClientCompanyId.Should().Be(company.Id);
        job.PostedBy.Should().Be(recruiterId);

        // Step 3: Publish the job
        JobPostingResponse published = await recruitmentService.PublishAsync(job.Id, CancellationToken.None);
        published.Status.Should().Be(JobPostingStatus.Published);
        published.PublishedAt.Should().NotBeNull();

        // Step 4: Submit application
        SubmitApplicationRequest appReq = new()
        {
            ResumeId = resumeId,
            CoverLetterUrl = "https://storage.example.com/cover.pdf"
        };
        ApplicationResponse application = await applicationService.SubmitAsync(
            job.Id, appReq, applicantId, CancellationToken.None);
        application.Status.Should().Be(ApplicationStatus.Submitted);
        application.JobPostingId.Should().Be(job.Id);
        application.ApplicantId.Should().Be(applicantId);
        application.ResumeId.Should().Be(resumeId);
        application.SubmittedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));

        // Step 5: Withdraw application
        ApplicationResponse withdrawn = await applicationService.WithdrawAsync(
            application.Id, applicantId, CancellationToken.None);
        withdrawn.Status.Should().Be(ApplicationStatus.Withdrawn);
        withdrawn.WithdrawnAt.Should().NotBeNull();

        // Step 6: Close the job posting
        JobPostingResponse closed = await recruitmentService.CloseAsync(job.Id, CancellationToken.None);
        closed.Status.Should().Be(JobPostingStatus.Closed);
        closed.ClosedAt.Should().NotBeNull();

        // Verify all data persisted correctly
        JobPosting? persistedJob = await _db.JobPostings
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == job.Id);
        persistedJob.Should().NotBeNull();
        persistedJob!.Status.Should().Be(JobPostingStatus.Closed);

        ApplicationEntity? persistedApp = await _db.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == application.Id);
        persistedApp.Should().NotBeNull();
        persistedApp!.Status.Should().Be(ApplicationStatus.Withdrawn);
    }

    // ── Test: Job Posting Lifecycle ───────────────────────────────────────

    [Fact]
    public async Task JobPosting_DraftToPublishedToClosed_LifecycleSucceeds()
    {
        // Arrange
        RecruitmentService service = CreateRecruitmentService();
        Guid recruiterId = Guid.NewGuid();

        CreateJobPostingRequest req = new()
        {
            Title = "Lifecycle Test Job",
            Description = "Testing lifecycle transitions",
            LocationType = LocationType.OnSite,
            City = "Manila",
            Country = "Philippines",
            EmploymentType = EmploymentType.Contract
        };

        // Act — create (Draft)
        JobPostingResponse draft = await service.CreateAsync(req, recruiterId, CancellationToken.None);
        draft.Status.Should().Be(JobPostingStatus.Draft);

        // Act — publish
        JobPostingResponse published = await service.PublishAsync(draft.Id, CancellationToken.None);
        published.Status.Should().Be(JobPostingStatus.Published);
        published.PublishedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Act — close
        JobPostingResponse closed = await service.CloseAsync(draft.Id, CancellationToken.None);
        closed.Status.Should().Be(JobPostingStatus.Closed);
        closed.ClosedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    // ── Test: Publish Non-Draft Throws ────────────────────────────────────

    [Fact]
    public async Task Publish_NonDraftJob_ThrowsUnprocessableEntity()
    {
        // Arrange
        RecruitmentService service = CreateRecruitmentService();
        Guid recruiterId = Guid.NewGuid();

        CreateJobPostingRequest req = new()
        {
            Title = "Already Published",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime
        };
        JobPostingResponse job = await service.CreateAsync(req, recruiterId, CancellationToken.None);
        await service.PublishAsync(job.Id, CancellationToken.None);

        // Act — try to publish again
        Func<Task> act = async () => await service.PublishAsync(job.Id, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("UNPROCESSABLE_ENTITY");
    }

    // ── Test: Close Non-Published Throws ──────────────────────────────────

    [Fact]
    public async Task Close_DraftJob_ThrowsUnprocessableEntity()
    {
        // Arrange
        RecruitmentService service = CreateRecruitmentService();
        Guid recruiterId = Guid.NewGuid();

        CreateJobPostingRequest req = new()
        {
            Title = "Draft Job",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime
        };
        JobPostingResponse job = await service.CreateAsync(req, recruiterId, CancellationToken.None);

        // Act — try to close a Draft job
        Func<Task> act = async () => await service.CloseAsync(job.Id, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("UNPROCESSABLE_ENTITY");
    }

    // ── Test: Application to Non-Published Job Throws ─────────────────────

    [Fact]
    public async Task SubmitApplication_DraftJob_ThrowsUnprocessableEntity()
    {
        // Arrange
        RecruitmentService recruitmentService = CreateRecruitmentService();
        ApplicationService applicationService = CreateApplicationService();
        Guid recruiterId = Guid.NewGuid();

        CreateJobPostingRequest jobReq = new()
        {
            Title = "Draft Job Application",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime
        };
        JobPostingResponse job = await recruitmentService.CreateAsync(jobReq, recruiterId, CancellationToken.None);

        SubmitApplicationRequest appReq = new()
        {
            ResumeId = Guid.NewGuid()
        };

        // Act
        Func<Task> act = async () => await applicationService.SubmitAsync(
            job.Id, appReq, Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("UNPROCESSABLE_ENTITY");
    }

    // ── Test: Duplicate Application Throws ────────────────────────────────

    [Fact]
    public async Task SubmitApplication_AlreadyApplied_ThrowsDuplicateApplication()
    {
        // Arrange
        RecruitmentService recruitmentService = CreateRecruitmentService();
        ApplicationService applicationService = CreateApplicationService();
        Guid recruiterId = Guid.NewGuid();
        Guid applicantId = Guid.NewGuid();

        CreateJobPostingRequest jobReq = new()
        {
            Title = "Duplicate App Test",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime
        };
        JobPostingResponse job = await recruitmentService.CreateAsync(jobReq, recruiterId, CancellationToken.None);
        await recruitmentService.PublishAsync(job.Id, CancellationToken.None);

        SubmitApplicationRequest appReq = new() { ResumeId = Guid.NewGuid() };
        await applicationService.SubmitAsync(job.Id, appReq, applicantId, CancellationToken.None);

        // Act — second application from the same applicant
        SubmitApplicationRequest duplicateReq = new() { ResumeId = Guid.NewGuid() };
        Func<Task> act = async () => await applicationService.SubmitAsync(
            job.Id, duplicateReq, applicantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("DUPLICATE_APPLICATION");
    }

    // ── Test: Resume Not Owned Throws ─────────────────────────────────────

    [Fact]
    public async Task SubmitApplication_ResumeNotOwned_ThrowsResumeNotFound()
    {
        // Arrange
        _resumeVerifier.IsOwnedByUserAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        RecruitmentService recruitmentService = CreateRecruitmentService();
        ApplicationService applicationService = CreateApplicationService();
        Guid recruiterId = Guid.NewGuid();

        CreateJobPostingRequest jobReq = new()
        {
            Title = "Resume Ownership Test",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime
        };
        JobPostingResponse job = await recruitmentService.CreateAsync(jobReq, recruiterId, CancellationToken.None);
        await recruitmentService.PublishAsync(job.Id, CancellationToken.None);

        SubmitApplicationRequest appReq = new() { ResumeId = Guid.NewGuid() };

        // Act
        Func<Task> act = async () => await applicationService.SubmitAsync(
            job.Id, appReq, Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("RESUME_NOT_FOUND");
    }

    // ── Test: Withdraw Not Own Application Throws ─────────────────────────

    [Fact]
    public async Task WithdrawApplication_NotOwnApplication_ThrowsForbidden()
    {
        // Arrange
        RecruitmentService recruitmentService = CreateRecruitmentService();
        ApplicationService applicationService = CreateApplicationService();
        Guid recruiterId = Guid.NewGuid();
        Guid applicantId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();

        CreateJobPostingRequest jobReq = new()
        {
            Title = "Withdraw Auth Test",
            Description = "Test",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime
        };
        JobPostingResponse job = await recruitmentService.CreateAsync(jobReq, recruiterId, CancellationToken.None);
        await recruitmentService.PublishAsync(job.Id, CancellationToken.None);

        SubmitApplicationRequest appReq = new() { ResumeId = Guid.NewGuid() };
        ApplicationResponse application = await applicationService.SubmitAsync(
            job.Id, appReq, applicantId, CancellationToken.None);

        // Act — different user tries to withdraw
        Func<Task> act = async () => await applicationService.WithdrawAsync(
            application.Id, otherUserId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("FORBIDDEN");
    }

    // ── Test: Update Draft Job Posting ────────────────────────────────────

    [Fact]
    public async Task UpdateJobPosting_DraftJob_UpdatesAllFields()
    {
        // Arrange
        RecruitmentService service = CreateRecruitmentService();
        Guid recruiterId = Guid.NewGuid();

        CreateJobPostingRequest createReq = new()
        {
            Title = "Original Title",
            Description = "Original Description",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime
        };
        JobPostingResponse job = await service.CreateAsync(createReq, recruiterId, CancellationToken.None);

        UpdateJobPostingRequest updateReq = new()
        {
            Title = "Updated Title",
            Description = "Updated Description",
            LocationType = LocationType.Hybrid,
            City = "London",
            Country = "UK",
            SalaryMin = 80000m,
            SalaryMax = 120000m,
            SalaryCurrency = "GBP"
        };

        // Act
        JobPostingResponse updated = await service.UpdateAsync(job.Id, updateReq, CancellationToken.None);

        // Assert
        updated.Title.Should().Be("Updated Title");
        updated.Description.Should().Be("Updated Description");
        updated.LocationType.Should().Be(LocationType.Hybrid);
        updated.City.Should().Be("London");
        updated.Country.Should().Be("UK");
        updated.SalaryMin.Should().Be(80000m);
        updated.SalaryMax.Should().Be(120000m);
        updated.SalaryCurrency.Should().Be("GBP");

        // Verify persistence
        JobPosting? persisted = await _db.JobPostings
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == job.Id);
        persisted.Should().NotBeNull();
        persisted!.Title.Should().Be("Updated Title");
    }

    // ── Test: Job Posting with Details ─────────────────────────────────────

    [Fact]
    public async Task GetJobById_WithCriteriaAndQuestions_ReturnsDetails()
    {
        // Arrange — create job posting with criteria and questions directly
        RecruitmentService service = CreateRecruitmentService();
        Guid recruiterId = Guid.NewGuid();

        CreateJobPostingRequest jobReq = new()
        {
            Title = "Details Test Job",
            Description = "Job with criteria and questions",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime
        };
        JobPostingResponse job = await service.CreateAsync(jobReq, recruiterId, CancellationToken.None);

        // Add criteria and questions directly to database
        JobEvaluationCriteria criteria = new()
        {
            JobPostingId = job.Id,
            Name = "C# Proficiency",
            Category = CriteriaCategory.Skill,
            EvaluationMethod = EvaluationMethod.ExactMatch,
            IsRequired = true,
            Weight = 60m,
            Configuration = """{"skill_name": "C#"}""",
            DisplayOrder = 1
        };
        _db.JobEvaluationCriteria.Add(criteria);

        JobScreeningQuestion question = new()
        {
            JobPostingId = job.Id,
            QuestionText = "Why do you want this job?",
            QuestionType = QuestionType.FreeText,
            Timing = QuestionTiming.AtApplication,
            IsRequired = true,
            Weight = 10m,
            DisplayOrder = 1
        };
        _db.JobScreeningQuestions.Add(question);
        await _db.SaveChangesAsync();

        // Act — get job with details
        JobPostingResponse details = await service.GetByIdAsync(job.Id, CancellationToken.None);

        // Assert
        details.Criteria.Should().NotBeNull();
        details.Criteria.Should().HaveCount(1);
        details.Criteria![0].Name.Should().Be("C# Proficiency");
        details.Criteria[0].Category.Should().Be(CriteriaCategory.Skill);

        details.Questions.Should().NotBeNull();
        details.Questions.Should().HaveCount(1);
        details.Questions![0].QuestionText.Should().Be("Why do you want this job?");
        details.Questions[0].QuestionType.Should().Be(QuestionType.FreeText);
    }

    // ── Test: List Job Postings with Pagination ───────────────────────────

    [Fact]
    public async Task ListJobPostings_MultipleJobs_ReturnsPaginatedResults()
    {
        // Arrange — create 5 job postings
        RecruitmentService service = CreateRecruitmentService();
        Guid recruiterId = Guid.NewGuid();

        for (int i = 1; i <= 5; i++)
        {
            CreateJobPostingRequest req = new()
            {
                Title = $"Pagination Test Job {i}",
                Description = $"Test description {i}",
                LocationType = LocationType.Remote,
                EmploymentType = EmploymentType.FullTime
            };
            await service.CreateAsync(req, recruiterId, CancellationToken.None);
            await Task.Delay(10); // Ensure distinct CreatedAt for stable ordering
        }

        // Act — page 1 (3 items)
        JobPostingQueryParameters page1Params = new() { PageSize = 3 };
        JobPostingListResponse page1 = await service.ListAsync(page1Params, CancellationToken.None);

        page1.Items.Should().HaveCount(3);
        page1.NextCursor.Should().NotBeNull();

        // Act — page 2
        JobPostingQueryParameters page2Params = new()
        {
            PageSize = 3,
            Cursor = page1.NextCursor
        };
        JobPostingListResponse page2 = await service.ListAsync(page2Params, CancellationToken.None);

        // Assert — remaining 2 items
        page2.Items.Should().HaveCount(2);
        page2.NextCursor.Should().BeNull("no more pages");

        // Verify no overlap between pages
        List<Guid> allIds = page1.Items.Select(j => j.Id).Concat(page2.Items.Select(j => j.Id)).ToList();
        allIds.Should().OnlyHaveUniqueItems();
    }

    // ── Test: List with Status Filter ─────────────────────────────────────

    [Fact]
    public async Task ListJobPostings_FilterByStatus_ReturnsMatchingOnly()
    {
        // Arrange
        RecruitmentService service = CreateRecruitmentService();
        Guid recruiterId = Guid.NewGuid();

        CreateJobPostingRequest draftReq = new()
        {
            Title = "Draft Filter Job",
            Description = "Draft",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime
        };
        await service.CreateAsync(draftReq, recruiterId, CancellationToken.None);

        CreateJobPostingRequest publishReq = new()
        {
            Title = "Published Filter Job",
            Description = "Will be published",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime
        };
        JobPostingResponse toPublish = await service.CreateAsync(publishReq, recruiterId, CancellationToken.None);
        await service.PublishAsync(toPublish.Id, CancellationToken.None);

        // Act — filter by Published
        JobPostingQueryParameters filterParams = new() { Status = JobPostingStatus.Published };
        JobPostingListResponse results = await service.ListAsync(filterParams, CancellationToken.None);

        // Assert — only the published job
        results.Items.Should().HaveCount(1);
        results.Items[0].Status.Should().Be(JobPostingStatus.Published);
        results.Items[0].Title.Should().Be("Published Filter Job");
    }

    // ── Test: Client Company CRUD ─────────────────────────────────────────

    [Fact]
    public async Task ClientCompany_CreateUpdateRetrieve_Succeeds()
    {
        // Arrange
        ClientCompanyService service = CreateClientCompanyService();

        CreateClientCompanyRequest createReq = new()
        {
            Name = "CRUD Corp",
            DisplayName = "CRUD",
            IsAnonymous = false,
            Industry = Industry.Technology,
            Website = "https://crud.example.com",
            ContactName = "Alice",
            ContactEmail = "alice@crud.com"
        };

        // Act — create
        ClientCompanyResponse created = await service.CreateAsync(createReq, CancellationToken.None);
        created.Name.Should().Be("CRUD Corp");
        created.Status.Should().Be(ClientCompanyStatus.Active);

        // Act — update
        UpdateClientCompanyRequest updateReq = new()
        {
            DisplayName = "CRUD Technologies",
            Notes = "Premium partner"
        };
        ClientCompanyResponse updated = await service.UpdateAsync(created.Id, updateReq, CancellationToken.None);
        updated.DisplayName.Should().Be("CRUD Technologies");
        updated.Notes.Should().Be("Premium partner");

        // Act — retrieve
        ClientCompanyResponse retrieved = await service.GetByIdAsync(created.Id, CancellationToken.None);
        retrieved.Name.Should().Be("CRUD Corp");
        retrieved.DisplayName.Should().Be("CRUD Technologies");
        retrieved.Notes.Should().Be("Premium partner");
    }

    // ── Test: Get Non-Existent Job Throws ─────────────────────────────────

    [Fact]
    public async Task GetJobById_NonExistent_ThrowsJobPostingNotFound()
    {
        // Arrange
        RecruitmentService service = CreateRecruitmentService();

        // Act
        Func<Task> act = async () => await service.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("JOB_POSTING_NOT_FOUND");
    }
}

using FluentAssertions;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Application.Services;
using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using NSubstitute;

namespace Jobsite.UnitTests.Recruitment;

public sealed class RecruitmentServiceTests
{
    private readonly IJobPostingRepository _jobPostingRepo = Substitute.For<IJobPostingRepository>();
    private readonly IClientCompanyRepository _clientCompanyRepo = Substitute.For<IClientCompanyRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RecruitmentService _sut;

    public RecruitmentServiceTests()
    {
        _sut = new RecruitmentService(_jobPostingRepo, _clientCompanyRepo, _unitOfWork);
    }

    // ── CreateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidRequest_ReturnsResponseWithDraftStatus()
    {
        // Arrange
        CreateJobPostingRequest request = TestData.CreateJobPostingRequest();
        Guid postedBy = Guid.NewGuid();

        // Act
        JobPostingResponse response = await _sut.CreateAsync(request, postedBy, CancellationToken.None);

        // Assert
        response.Title.Should().Be(request.Title);
        response.Status.Should().Be(JobPostingStatus.Draft);
        response.PostedBy.Should().Be(postedBy);
        _jobPostingRepo.Received(1).Add(Arg.Any<JobPosting>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithClientCompanyId_ValidatesCompanyExists()
    {
        // Arrange
        Guid companyId = Guid.NewGuid();
        CreateJobPostingRequest request = new()
        {
            Title = "Job",
            Description = "Description",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime,
            ClientCompanyId = companyId
        };
        _clientCompanyRepo.ExistsByIdAsync(companyId, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        JobPostingResponse response = await _sut.CreateAsync(request, Guid.NewGuid(), CancellationToken.None);

        // Assert
        response.ClientCompanyId.Should().Be(companyId);
    }

    [Fact]
    public async Task CreateAsync_InvalidClientCompanyId_ThrowsClientCompanyNotFound()
    {
        // Arrange
        Guid companyId = Guid.NewGuid();
        CreateJobPostingRequest request = new()
        {
            Title = "Job",
            Description = "Description",
            LocationType = LocationType.Remote,
            EmploymentType = EmploymentType.FullTime,
            ClientCompanyId = companyId
        };
        _clientCompanyRepo.ExistsByIdAsync(companyId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        Func<Task> act = () => _sut.CreateAsync(request, Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("CLIENT_COMPANY_NOT_FOUND");
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsResponse()
    {
        // Arrange
        JobPosting jobPosting = TestData.CreateJobPosting();
        _jobPostingRepo.GetByIdWithDetailsAsync(jobPosting.Id, Arg.Any<CancellationToken>()).Returns(jobPosting);

        // Act
        JobPostingResponse response = await _sut.GetByIdAsync(jobPosting.Id, CancellationToken.None);

        // Assert
        response.Id.Should().Be(jobPosting.Id);
        response.Title.Should().Be(jobPosting.Title);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ThrowsJobPostingNotFound()
    {
        // Arrange
        _jobPostingRepo.GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((JobPosting?)null);

        // Act
        Func<Task> act = () => _sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("JOB_POSTING_NOT_FOUND");
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesOnlyProvidedFields()
    {
        // Arrange
        JobPosting jobPosting = TestData.CreateJobPosting();
        string originalDescription = jobPosting.Description;
        _jobPostingRepo.GetByIdForUpdateAsync(jobPosting.Id, Arg.Any<CancellationToken>()).Returns(jobPosting);

        UpdateJobPostingRequest request = new() { Title = "Updated Title" };

        // Act
        JobPostingResponse response = await _sut.UpdateAsync(jobPosting.Id, request, CancellationToken.None);

        // Assert
        response.Title.Should().Be("Updated Title");
        response.Description.Should().Be(originalDescription);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_NonExistentId_ThrowsJobPostingNotFound()
    {
        // Arrange
        _jobPostingRepo.GetByIdForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((JobPosting?)null);

        // Act
        Func<Task> act = () => _sut.UpdateAsync(Guid.NewGuid(), new UpdateJobPostingRequest(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("JOB_POSTING_NOT_FOUND");
    }

    // ── PublishAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_DraftJobPosting_TransitionsToPublished()
    {
        // Arrange
        JobPosting jobPosting = TestData.CreateJobPosting(status: JobPostingStatus.Draft);
        _jobPostingRepo.GetByIdForUpdateAsync(jobPosting.Id, Arg.Any<CancellationToken>()).Returns(jobPosting);

        // Act
        JobPostingResponse response = await _sut.PublishAsync(jobPosting.Id, CancellationToken.None);

        // Assert
        response.Status.Should().Be(JobPostingStatus.Published);
        response.PublishedAt.Should().NotBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PublishAsync_NonDraftJobPosting_ThrowsUnprocessableEntity()
    {
        // Arrange
        JobPosting jobPosting = TestData.CreateJobPosting(status: JobPostingStatus.Published);
        _jobPostingRepo.GetByIdForUpdateAsync(jobPosting.Id, Arg.Any<CancellationToken>()).Returns(jobPosting);

        // Act
        Func<Task> act = () => _sut.PublishAsync(jobPosting.Id, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("UNPROCESSABLE_ENTITY");
    }

    [Fact]
    public async Task PublishAsync_NonExistentId_ThrowsJobPostingNotFound()
    {
        // Arrange
        _jobPostingRepo.GetByIdForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((JobPosting?)null);

        // Act
        Func<Task> act = () => _sut.PublishAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("JOB_POSTING_NOT_FOUND");
    }

    // ── CloseAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task CloseAsync_PublishedJobPosting_TransitionsToClosed()
    {
        // Arrange
        JobPosting jobPosting = TestData.CreateJobPosting(status: JobPostingStatus.Published);
        _jobPostingRepo.GetByIdForUpdateAsync(jobPosting.Id, Arg.Any<CancellationToken>()).Returns(jobPosting);

        // Act
        JobPostingResponse response = await _sut.CloseAsync(jobPosting.Id, CancellationToken.None);

        // Assert
        response.Status.Should().Be(JobPostingStatus.Closed);
        response.ClosedAt.Should().NotBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CloseAsync_NonPublishedJobPosting_ThrowsUnprocessableEntity()
    {
        // Arrange
        JobPosting jobPosting = TestData.CreateJobPosting(status: JobPostingStatus.Draft);
        _jobPostingRepo.GetByIdForUpdateAsync(jobPosting.Id, Arg.Any<CancellationToken>()).Returns(jobPosting);

        // Act
        Func<Task> act = () => _sut.CloseAsync(jobPosting.Id, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("UNPROCESSABLE_ENTITY");
    }

    // ── ListAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_ValidParameters_DelegatesToRepository()
    {
        // Arrange
        JobPostingQueryParameters parameters = new() { PageSize = 10 };
        JobPostingListResponse expected = new() { Items = [], NextCursor = null };
        _jobPostingRepo.ListAsync(parameters, Arg.Any<CancellationToken>()).Returns(expected);

        // Act
        JobPostingListResponse result = await _sut.ListAsync(parameters, CancellationToken.None);

        // Assert
        await _jobPostingRepo.Received(1).ListAsync(parameters, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_ReturnsPaginatedResponse()
    {
        // Arrange
        JobPostingQueryParameters parameters = new() { PageSize = 20 };
        JobPostingListResponse expected = new()
        {
            Items = [new JobPostingResponse
            {
                Id = Guid.NewGuid(),
                Title = "Test Job",
                Description = "Desc",
                LocationType = "Remote",
                EmploymentType = "FullTime",
                Status = "Draft",
                PostedBy = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }],
            NextCursor = "cursor123"
        };
        _jobPostingRepo.ListAsync(parameters, Arg.Any<CancellationToken>()).Returns(expected);

        // Act
        JobPostingListResponse result = await _sut.ListAsync(parameters, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.NextCursor.Should().Be("cursor123");
    }
}

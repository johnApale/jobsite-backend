using FluentAssertions;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Application.Services;
using Jobsite.Modules.Recruitment.Domain.Constants;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using NSubstitute;
using ApplicationEntity = Jobsite.Modules.Recruitment.Domain.Entities.Application;

namespace Jobsite.UnitTests.Recruitment;

public sealed class ApplicationServiceTests
{
    private readonly IApplicationRepository _applicationRepo = Substitute.For<IApplicationRepository>();
    private readonly IJobPostingRepository _jobPostingRepo = Substitute.For<IJobPostingRepository>();
    private readonly IResumeOwnershipVerifier _resumeVerifier = Substitute.For<IResumeOwnershipVerifier>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ApplicationService _sut;

    public ApplicationServiceTests()
    {
        _sut = new ApplicationService(_applicationRepo, _jobPostingRepo, _resumeVerifier, _unitOfWork);
    }

    // ── SubmitAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitAsync_ValidRequest_ReturnsResponseWithSubmittedStatus()
    {
        // Arrange
        Guid applicantId = Guid.NewGuid();
        JobPosting jobPosting = TestData.CreateJobPosting(status: JobPostingStatus.Published);
        SubmitApplicationRequest request = TestData.CreateSubmitApplicationRequest();

        _jobPostingRepo.GetByIdAsync(jobPosting.Id, Arg.Any<CancellationToken>()).Returns(jobPosting);
        _applicationRepo.ExistsByApplicantAndJobAsync(applicantId, jobPosting.Id, Arg.Any<CancellationToken>()).Returns(false);
        _resumeVerifier.IsOwnedByUserAsync(request.ResumeId, applicantId, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        ApplicationResponse response = await _sut.SubmitAsync(jobPosting.Id, request, applicantId, CancellationToken.None);

        // Assert
        response.Status.Should().Be(ApplicationStatus.Submitted);
        response.JobPostingId.Should().Be(jobPosting.Id);
        response.ApplicantId.Should().Be(applicantId);
        response.ResumeId.Should().Be(request.ResumeId);
        _applicationRepo.Received(1).Add(Arg.Any<ApplicationEntity>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitAsync_NonPublishedJob_ThrowsUnprocessableEntity()
    {
        // Arrange
        JobPosting jobPosting = TestData.CreateJobPosting(status: JobPostingStatus.Draft);
        _jobPostingRepo.GetByIdAsync(jobPosting.Id, Arg.Any<CancellationToken>()).Returns(jobPosting);

        // Act
        Func<Task> act = () => _sut.SubmitAsync(jobPosting.Id, TestData.CreateSubmitApplicationRequest(), Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("UNPROCESSABLE_ENTITY");
    }

    [Fact]
    public async Task SubmitAsync_NonExistentJob_ThrowsJobPostingNotFound()
    {
        // Arrange
        _jobPostingRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((JobPosting?)null);

        // Act
        Func<Task> act = () => _sut.SubmitAsync(Guid.NewGuid(), TestData.CreateSubmitApplicationRequest(), Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("JOB_POSTING_NOT_FOUND");
    }

    [Fact]
    public async Task SubmitAsync_DuplicateApplication_ThrowsDuplicateApplication()
    {
        // Arrange
        Guid applicantId = Guid.NewGuid();
        JobPosting jobPosting = TestData.CreateJobPosting(status: JobPostingStatus.Published);
        _jobPostingRepo.GetByIdAsync(jobPosting.Id, Arg.Any<CancellationToken>()).Returns(jobPosting);
        _applicationRepo.ExistsByApplicantAndJobAsync(applicantId, jobPosting.Id, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        Func<Task> act = () => _sut.SubmitAsync(jobPosting.Id, TestData.CreateSubmitApplicationRequest(), applicantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("DUPLICATE_APPLICATION");
    }

    [Fact]
    public async Task SubmitAsync_ResumeNotOwned_ThrowsResumeNotFound()
    {
        // Arrange
        Guid applicantId = Guid.NewGuid();
        JobPosting jobPosting = TestData.CreateJobPosting(status: JobPostingStatus.Published);
        SubmitApplicationRequest request = TestData.CreateSubmitApplicationRequest();

        _jobPostingRepo.GetByIdAsync(jobPosting.Id, Arg.Any<CancellationToken>()).Returns(jobPosting);
        _applicationRepo.ExistsByApplicantAndJobAsync(applicantId, jobPosting.Id, Arg.Any<CancellationToken>()).Returns(false);
        _resumeVerifier.IsOwnedByUserAsync(request.ResumeId, applicantId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        Func<Task> act = () => _sut.SubmitAsync(jobPosting.Id, request, applicantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("RESUME_NOT_FOUND");
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsResponse()
    {
        // Arrange
        ApplicationEntity application = TestData.CreateApplication();
        _applicationRepo.GetByIdAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        // Act
        ApplicationResponse response = await _sut.GetByIdAsync(application.Id, CancellationToken.None);

        // Assert
        response.Id.Should().Be(application.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ThrowsApplicationNotFound()
    {
        // Arrange
        _applicationRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ApplicationEntity?)null);

        // Act
        Func<Task> act = () => _sut.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("APPLICATION_NOT_FOUND");
    }

    // ── WithdrawAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task WithdrawAsync_OwnApplication_TransitionsToWithdrawn()
    {
        // Arrange
        Guid applicantId = Guid.NewGuid();
        ApplicationEntity application = TestData.CreateApplication(
            applicantId: applicantId, status: ApplicationStatus.Submitted);
        _applicationRepo.GetByIdForUpdateAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        // Act
        ApplicationResponse response = await _sut.WithdrawAsync(application.Id, applicantId, CancellationToken.None);

        // Assert
        response.Status.Should().Be(ApplicationStatus.Withdrawn);
        response.WithdrawnAt.Should().NotBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WithdrawAsync_OtherUsersApplication_ThrowsForbidden()
    {
        // Arrange
        ApplicationEntity application = TestData.CreateApplication();
        _applicationRepo.GetByIdForUpdateAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);
        Guid differentUserId = Guid.NewGuid();

        // Act
        Func<Task> act = () => _sut.WithdrawAsync(application.Id, differentUserId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task WithdrawAsync_AlreadyWithdrawn_ThrowsAlreadyWithdrawn()
    {
        // Arrange
        Guid applicantId = Guid.NewGuid();
        ApplicationEntity application = TestData.CreateApplication(
            applicantId: applicantId, status: ApplicationStatus.Withdrawn);
        _applicationRepo.GetByIdForUpdateAsync(application.Id, Arg.Any<CancellationToken>()).Returns(application);

        // Act
        Func<Task> act = () => _sut.WithdrawAsync(application.Id, applicantId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("APPLICATION_ALREADY_WITHDRAWN");
    }

    [Fact]
    public async Task WithdrawAsync_NonExistentId_ThrowsApplicationNotFound()
    {
        // Arrange
        _applicationRepo.GetByIdForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ApplicationEntity?)null);

        // Act
        Func<Task> act = () => _sut.WithdrawAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("APPLICATION_NOT_FOUND");
    }
}

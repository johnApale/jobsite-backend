using FluentAssertions;
using Jobsite.Modules.Profiles.Application.DTOs;
using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Application.Services;
using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using NSubstitute;

namespace Jobsite.UnitTests.Profiles;

public sealed class ResumeServiceTests
{
    private readonly IResumeRepository _resumeRepository = Substitute.For<IResumeRepository>();
    private readonly IFileStorage _fileStorage = Substitute.For<IFileStorage>();
    private readonly IEventPublisher _eventPublisher = Substitute.For<IEventPublisher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ResumeService _sut;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public ResumeServiceTests()
    {
        _sut = new ResumeService(_resumeRepository, _fileStorage, _eventPublisher, _unitOfWork);
    }

    // ── UploadResumeAsync ────────────────────────────────────────────────

    [Fact]
    public async Task UploadResumeAsync_ValidPdf_UploadsAndPublishesEvent()
    {
        // Arrange
        using MemoryStream stream = new([0x01, 0x02]);
        string fileName = "resume.pdf";
        long fileSize = 1024;
        _fileStorage.UploadAsync(stream, fileName, "application/pdf", Arg.Any<CancellationToken>())
            .Returns("/uploads/resumes/resume.pdf");

        // Act
        ResumeResponse result = await _sut.UploadResumeAsync(
            _userId, _tenantId, stream, fileName, fileSize, CancellationToken.None);

        // Assert
        result.UserId.Should().Be(_userId);
        result.FileType.Should().Be("PDF");
        result.IsLatest.Should().BeTrue();
        result.IsParsed.Should().BeFalse();
        _resumeRepository.Received(1).Add(Arg.Is<Resume>(r =>
            r.UserId == _userId && r.FileType == "PDF" && r.IsLatest));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _eventPublisher.Received(1).PublishAsync(
            Arg.Is<ResumeUploadedEvent>(e => e.UserId == _userId && e.TenantId == _tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UploadResumeAsync_ValidDocx_UploadsSuccessfully()
    {
        // Arrange
        using MemoryStream stream = new([0x01, 0x02]);
        string fileName = "resume.docx";
        long fileSize = 2048;
        _fileStorage.UploadAsync(stream, fileName,
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            Arg.Any<CancellationToken>())
            .Returns("/uploads/resumes/resume.docx");

        // Act
        ResumeResponse result = await _sut.UploadResumeAsync(
            _userId, _tenantId, stream, fileName, fileSize, CancellationToken.None);

        // Assert
        result.FileType.Should().Be("DOCX");
    }

    [Fact]
    public async Task UploadResumeAsync_InvalidFileType_ThrowsAppError()
    {
        // Arrange
        using MemoryStream stream = new([0x01, 0x02]);
        string fileName = "resume.txt";
        long fileSize = 1024;

        // Act
        Func<Task> act = () => _sut.UploadResumeAsync(
            _userId, _tenantId, stream, fileName, fileSize, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_REQUEST");
    }

    [Fact]
    public async Task UploadResumeAsync_FileTooLarge_ThrowsAppError()
    {
        // Arrange
        using MemoryStream stream = new([0x01, 0x02]);
        string fileName = "resume.pdf";
        long fileSize = 26 * 1024 * 1024; // 26 MB — exceeds 25 MB limit

        // Act
        Func<Task> act = () => _sut.UploadResumeAsync(
            _userId, _tenantId, stream, fileName, fileSize, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_REQUEST");
    }

    [Fact]
    public async Task UploadResumeAsync_MarksPreviousResumesAsNotLatest()
    {
        // Arrange
        using MemoryStream stream = new([0x01, 0x02]);
        string fileName = "resume.pdf";
        long fileSize = 1024;
        _fileStorage.UploadAsync(stream, fileName, "application/pdf", Arg.Any<CancellationToken>())
            .Returns("/uploads/resumes/resume.pdf");

        // Act
        await _sut.UploadResumeAsync(
            _userId, _tenantId, stream, fileName, fileSize, CancellationToken.None);

        // Assert
        await _resumeRepository.Received(1)
            .MarkPreviousAsNotLatestAsync(_userId, Arg.Any<CancellationToken>());
    }

    // ── GetResumesAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetResumesAsync_ReturnsAllResumesForUser()
    {
        // Arrange
        List<Resume> resumes =
        [
            TestData.CreateResume(userId: _userId),
            TestData.CreateResume(userId: _userId, isLatest: false)
        ];
        _resumeRepository.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(resumes);

        // Act
        List<ResumeResponse> result = await _sut.GetResumesAsync(_userId, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
    }

    // ── GetResumeByIdAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetResumeByIdAsync_ResumeExistsAndBelongsToUser_ReturnsResume()
    {
        // Arrange
        Guid resumeId = Guid.NewGuid();
        Resume resume = TestData.CreateResume(id: resumeId, userId: _userId);
        _resumeRepository.GetByIdAsync(resumeId, Arg.Any<CancellationToken>())
            .Returns(resume);

        // Act
        ResumeResponse result = await _sut.GetResumeByIdAsync(resumeId, _userId, CancellationToken.None);

        // Assert
        result.Id.Should().Be(resumeId);
    }

    [Fact]
    public async Task GetResumeByIdAsync_ResumeNotFound_ThrowsAppError()
    {
        // Arrange
        Guid resumeId = Guid.NewGuid();
        _resumeRepository.GetByIdAsync(resumeId, Arg.Any<CancellationToken>())
            .Returns((Resume?)null);

        // Act
        Func<Task> act = () => _sut.GetResumeByIdAsync(resumeId, _userId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("RESUME_NOT_FOUND");
    }

    [Fact]
    public async Task GetResumeByIdAsync_ResumeBelongsToDifferentUser_ThrowsAppError()
    {
        // Arrange
        Guid resumeId = Guid.NewGuid();
        Guid otherUserId = Guid.NewGuid();
        Resume resume = TestData.CreateResume(id: resumeId, userId: otherUserId);
        _resumeRepository.GetByIdAsync(resumeId, Arg.Any<CancellationToken>())
            .Returns(resume);

        // Act
        Func<Task> act = () => _sut.GetResumeByIdAsync(resumeId, _userId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("RESUME_NOT_FOUND");
    }
}

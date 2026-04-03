using Jobsite.Modules.Profiles.Application.DTOs;
using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Domain.Constants;
using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.Profiles.Application.Services;

public sealed class ResumeService : IResumeService
{
    private const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB

    private readonly IResumeRepository _resumeRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;

    public ResumeService(
        IResumeRepository resumeRepository,
        IFileStorage fileStorage,
        IEventPublisher eventPublisher,
        [FromKeyedServices("profiles")] IUnitOfWork unitOfWork)
    {
        _resumeRepository = resumeRepository;
        _fileStorage = fileStorage;
        _eventPublisher = eventPublisher;
        _unitOfWork = unitOfWork;
    }

    public async Task<ResumeResponse> UploadResumeAsync(
        Guid userId, Guid tenantId, Stream file, string fileName, long fileSize,
        CancellationToken ct = default)
    {
        string fileType = ResolveFileType(fileName);

        if (!FileType.IsValid(fileType))
            throw AppErrors.InvalidRequest.WithMessage(
                $"Unsupported file type. Allowed: {FileType.Pdf}, {FileType.Docx}");

        if (fileSize > MaxFileSizeBytes)
            throw AppErrors.InvalidRequest.WithMessage(
                $"File size exceeds the maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB");

        string contentType = fileType == FileType.Pdf
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        string fileUrl = await _fileStorage.UploadAsync(file, fileName, contentType, ct);

        await _resumeRepository.MarkPreviousAsNotLatestAsync(userId, ct);

        Resume resume = new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FileUrl = fileUrl,
            OriginalFilename = fileName,
            FileSizeBytes = fileSize,
            FileType = fileType,
            IsLatest = true,
            IsParsed = false
        };

        _resumeRepository.Add(resume);
        await _unitOfWork.SaveChangesAsync(ct);

        await _eventPublisher.PublishAsync(new ResumeUploadedEvent
        {
            EventId = Guid.NewGuid(),
            ResumeId = resume.Id,
            UserId = userId,
            TenantId = tenantId,
            FileUrl = fileUrl,
            FileType = fileType,
            CorrelationId = Guid.NewGuid().ToString(),
            OccurredAt = DateTime.UtcNow
        }, ct);

        return MapToResponse(resume);
    }

    public async Task<List<ResumeResponse>> GetResumesAsync(
        Guid userId, CancellationToken ct = default)
    {
        List<Resume> resumes = await _resumeRepository.GetByUserIdAsync(userId, ct);
        return resumes.ConvertAll(MapToResponse);
    }

    public async Task<ResumeResponse> GetResumeByIdAsync(
        Guid resumeId, Guid userId, CancellationToken ct = default)
    {
        Resume? resume = await _resumeRepository.GetByIdAsync(resumeId, ct);

        if (resume is null || resume.UserId != userId)
            throw AppErrors.ResumeNotFound;

        return MapToResponse(resume);
    }

    private static ResumeResponse MapToResponse(Resume resume)
    {
        return new ResumeResponse
        {
            Id = resume.Id,
            UserId = resume.UserId,
            FileUrl = resume.FileUrl,
            OriginalFilename = resume.OriginalFilename,
            FileSizeBytes = resume.FileSizeBytes,
            FileType = resume.FileType,
            IsLatest = resume.IsLatest,
            IsParsed = resume.IsParsed,
            ParseError = resume.ParseError,
            ParsedAt = resume.ParsedAt,
            CreatedAt = resume.CreatedAt,
            UpdatedAt = resume.UpdatedAt
        };
    }

    private static string ResolveFileType(string fileName)
    {
        string extension = Path.GetExtension(fileName)?.ToUpperInvariant() ?? string.Empty;
        return extension switch
        {
            ".PDF" => FileType.Pdf,
            ".DOCX" => FileType.Docx,
            _ => extension.TrimStart('.')
        };
    }
}

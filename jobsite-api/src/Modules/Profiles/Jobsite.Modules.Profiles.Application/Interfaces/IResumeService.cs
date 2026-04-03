using Jobsite.Modules.Profiles.Application.DTOs;

namespace Jobsite.Modules.Profiles.Application.Interfaces;

public interface IResumeService
{
    Task<ResumeResponse> UploadResumeAsync(
        Guid userId, Guid tenantId, Stream file, string fileName, long fileSize,
        CancellationToken ct = default);

    Task<List<ResumeResponse>> GetResumesAsync(Guid userId, CancellationToken ct = default);

    Task<ResumeResponse> GetResumeByIdAsync(Guid resumeId, Guid userId, CancellationToken ct = default);
}

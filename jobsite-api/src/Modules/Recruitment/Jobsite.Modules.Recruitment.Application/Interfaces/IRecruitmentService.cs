using Jobsite.Modules.Recruitment.Application.DTOs;

namespace Jobsite.Modules.Recruitment.Application.Interfaces;

/// <summary>Application service for job posting CRUD and lifecycle management.</summary>
public interface IRecruitmentService
{
    Task<JobPostingResponse> CreateAsync(CreateJobPostingRequest request, Guid postedBy, CancellationToken ct = default);
    Task<JobPostingResponse> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<JobPostingListResponse> ListAsync(JobPostingQueryParameters parameters, CancellationToken ct = default);
    Task<JobPostingResponse> UpdateAsync(Guid id, UpdateJobPostingRequest request, CancellationToken ct = default);
    Task<JobPostingResponse> PublishAsync(Guid id, CancellationToken ct = default);
    Task<JobPostingResponse> CloseAsync(Guid id, CancellationToken ct = default);
}

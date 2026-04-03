using Jobsite.Modules.Recruitment.Application.DTOs;
using ApplicationEntity = Jobsite.Modules.Recruitment.Domain.Entities.Application;

namespace Jobsite.Modules.Recruitment.Application.Interfaces;

/// <summary>Repository for application lookups and persistence.</summary>
public interface IApplicationRepository
{
    /// <summary>Get an application by ID (read-only).</summary>
    Task<ApplicationEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Get an application by ID with tracking enabled (for updates).</summary>
    Task<ApplicationEntity?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default);

    /// <summary>Query applications with cursor-based pagination and optional filters.</summary>
    Task<ApplicationListResponse> ListAsync(ApplicationQueryParameters parameters, CancellationToken ct = default);

    /// <summary>Check if an applicant has already applied to a specific job posting.</summary>
    Task<bool> ExistsByApplicantAndJobAsync(Guid applicantId, Guid jobPostingId, CancellationToken ct = default);

    /// <summary>Persist a new application.</summary>
    void Add(ApplicationEntity application);
}

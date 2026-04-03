using Jobsite.Modules.Profiles.Application.DTOs;

namespace Jobsite.Modules.Profiles.Application.Interfaces;

public interface IProfileService
{
    Task<ProfileResponse> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<ProfileResponse> CreateAsync(CreateProfileRequest request, Guid userId, CancellationToken ct = default);
    Task<ProfileResponse> UpdateAsync(UpdateProfileRequest request, Guid userId, CancellationToken ct = default);
}

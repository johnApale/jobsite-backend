using System.Text.Json;
using Jobsite.Modules.Profiles.Application.DTOs;
using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.Profiles.Application.Services;

public sealed class ProfileService : IProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly IApplicantProfileRepository _profileRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProfileService(
        IApplicantProfileRepository profileRepository,
        [FromKeyedServices("profiles")] IUnitOfWork unitOfWork)
    {
        _profileRepository = profileRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProfileResponse> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        ApplicantProfile? profile = await _profileRepository.GetByUserIdAsync(userId, ct);

        if (profile is null)
            throw AppErrors.ProfileNotFound;

        return MapToResponse(profile);
    }

    public async Task<ProfileResponse> CreateAsync(
        CreateProfileRequest request, Guid userId, CancellationToken ct = default)
    {
        bool exists = await _profileRepository.ExistsByUserIdAsync(userId, ct);

        if (exists)
            throw AppErrors.ProfileAlreadyExists;

        ApplicantProfile profile = new()
        {
            Id = userId,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            City = request.City,
            Country = request.Country,
            Skills = SerializeJson(request.Skills),
            SocialLinks = SerializeJson(request.SocialLinks),
            Documents = SerializeJson(request.Documents)
        };

        _profileRepository.Add(profile);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(profile);
    }

    public async Task<ProfileResponse> UpdateAsync(
        UpdateProfileRequest request, Guid userId, CancellationToken ct = default)
    {
        ApplicantProfile? profile = await _profileRepository.GetByUserIdForUpdateAsync(userId, ct);

        if (profile is null)
            throw AppErrors.ProfileNotFound;

        if (request.FirstName is not null)
            profile.FirstName = request.FirstName;

        if (request.LastName is not null)
            profile.LastName = request.LastName;

        if (request.Phone is not null)
            profile.Phone = request.Phone;

        if (request.City is not null)
            profile.City = request.City;

        if (request.Country is not null)
            profile.Country = request.Country;

        if (request.Skills is not null)
            profile.Skills = SerializeJson(request.Skills);

        if (request.SocialLinks is not null)
            profile.SocialLinks = SerializeJson(request.SocialLinks);

        if (request.Documents is not null)
            profile.Documents = SerializeJson(request.Documents);

        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(profile);
    }

    private static ProfileResponse MapToResponse(ApplicantProfile profile)
    {
        return new ProfileResponse
        {
            UserId = profile.Id,
            FirstName = profile.FirstName,
            LastName = profile.LastName,
            Phone = profile.Phone,
            City = profile.City,
            Country = profile.Country,
            Skills = DeserializeJson<List<SkillDto>>(profile.Skills),
            SocialLinks = DeserializeJson<SocialLinksDto>(profile.SocialLinks),
            Documents = DeserializeJson<List<DocumentDto>>(profile.Documents),
            ProfileCompletedAt = profile.ProfileCompletedAt,
            CreatedAt = profile.CreatedAt,
            UpdatedAt = profile.UpdatedAt
        };
    }

    private static string? SerializeJson<T>(T? value) where T : class
    {
        return value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
    }

    private static T? DeserializeJson<T>(string? json) where T : class
    {
        return json is null ? null : JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}

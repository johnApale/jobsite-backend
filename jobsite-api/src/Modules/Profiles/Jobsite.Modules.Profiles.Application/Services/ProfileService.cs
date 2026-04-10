using System.Text.Json;
using Jobsite.Modules.Profiles.Application.DTOs;
using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Profiles.Application.Services;

public sealed class ProfileService : IProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly IApplicantProfileRepository _profileRepository;
    private readonly IResumeRepository _resumeRepository;
    private readonly ITenantSettingsReader _settingsReader;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProfileService> _logger;

    public ProfileService(
        IApplicantProfileRepository profileRepository,
        IResumeRepository resumeRepository,
        ITenantSettingsReader settingsReader,
        [FromKeyedServices("profiles")] IUnitOfWork unitOfWork,
        ILogger<ProfileService> logger)
    {
        _profileRepository = profileRepository;
        _resumeRepository = resumeRepository;
        _settingsReader = settingsReader;
        _unitOfWork = unitOfWork;
        _logger = logger;
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
        await EvaluateProfileCompletionAsync(profile, ct);
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

        await EvaluateProfileCompletionAsync(profile, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(profile);
    }

    internal async Task EvaluateProfileCompletionAsync(ApplicantProfile profile, CancellationToken ct)
    {
        ProfileSettings? settings =
            await _settingsReader.GetSettingAsync<ProfileSettings>("profile_settings", ct);

        if (settings is null)
            return;

        bool isComplete = CheckRequiredFields(profile, settings)
            && CheckSkillsCount(profile, settings)
            && CheckRequiredSocialLinks(profile, settings)
            && CheckRequiredDocuments(profile, settings);

        if (isComplete && settings.ResumeRequired)
            isComplete = await _resumeRepository.HasAnyByUserIdAsync(profile.Id, ct);

        if (isComplete && profile.ProfileCompletedAt is null)
        {
            profile.ProfileCompletedAt = DateTime.UtcNow;
            _logger.LogInformation("Profile completed for user {UserId}", profile.Id);
        }
        else if (!isComplete && profile.ProfileCompletedAt is not null)
        {
            profile.ProfileCompletedAt = null;
            _logger.LogInformation("Profile completion revoked for user {UserId}", profile.Id);
        }
    }

    private static bool CheckRequiredFields(ApplicantProfile profile, ProfileSettings settings)
    {
        foreach (string field in settings.RequiredProfileFields)
        {
            bool hasValue = field.ToLowerInvariant() switch
            {
                "phone" => !string.IsNullOrWhiteSpace(profile.Phone),
                "city" => !string.IsNullOrWhiteSpace(profile.City),
                "country" => !string.IsNullOrWhiteSpace(profile.Country),
                "skills" => !string.IsNullOrWhiteSpace(profile.Skills),
                "social_links" => !string.IsNullOrWhiteSpace(profile.SocialLinks),
                "documents" => !string.IsNullOrWhiteSpace(profile.Documents),
                _ => true
            };

            if (!hasValue)
                return false;
        }

        return true;
    }

    private static bool CheckSkillsCount(ApplicantProfile profile, ProfileSettings settings)
    {
        if (settings.MinimumSkillsCount <= 0)
            return true;

        if (string.IsNullOrWhiteSpace(profile.Skills))
            return false;

        List<SkillDto>? skills = DeserializeJson<List<SkillDto>>(profile.Skills);
        return skills is not null && skills.Count >= settings.MinimumSkillsCount;
    }

    private static bool CheckRequiredSocialLinks(ApplicantProfile profile, ProfileSettings settings)
    {
        if (settings.RequiredSocialLinks.Count == 0)
            return true;

        if (string.IsNullOrWhiteSpace(profile.SocialLinks))
            return false;

        SocialLinksDto? links = DeserializeJson<SocialLinksDto>(profile.SocialLinks);
        if (links is null)
            return false;

        foreach (string link in settings.RequiredSocialLinks)
        {
            bool hasValue = link.ToLowerInvariant() switch
            {
                "linkedin" => !string.IsNullOrWhiteSpace(links.LinkedIn),
                "github" => !string.IsNullOrWhiteSpace(links.GitHub),
                "portfolio" => !string.IsNullOrWhiteSpace(links.Portfolio),
                _ => true
            };

            if (!hasValue)
                return false;
        }

        return true;
    }

    private static bool CheckRequiredDocuments(ApplicantProfile profile, ProfileSettings settings)
    {
        if (settings.RequiredDocuments.Count == 0)
            return true;

        if (string.IsNullOrWhiteSpace(profile.Documents))
            return false;

        List<DocumentDto>? docs = DeserializeJson<List<DocumentDto>>(profile.Documents);
        if (docs is null)
            return false;

        HashSet<string> uploadedTypes = new(docs.Select(d => d.Type), StringComparer.OrdinalIgnoreCase);

        return settings.RequiredDocuments.All(req => uploadedTypes.Contains(req));
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

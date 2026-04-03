using System.Text.Json;
using Jobsite.Modules.Admin.Application.DTOs;
using Jobsite.Modules.Admin.Application.DTOs.Settings;
using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.Admin.Application.Services;

/// <summary>
/// Application service for reading and updating tenant-level company settings.
/// </summary>
public sealed class AdminSettingsService : IAdminSettingsService
{
    private readonly ICompanySettingsRepository _settingsRepository;
    private readonly IUnitOfWork _unitOfWork;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public AdminSettingsService(
        ICompanySettingsRepository settingsRepository,
        [FromKeyedServices("admin")] IUnitOfWork unitOfWork)
    {
        _settingsRepository = settingsRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CompanySettingsResponse> GetSettingsAsync(CancellationToken ct = default)
    {
        CompanySettings? settings = await _settingsRepository.GetAsync(ct);
        if (settings is null)
            throw AppErrors.SettingsNotFound;

        return MapToResponse(settings);
    }

    public async Task<CompanySettingsResponse> UpdateSettingsAsync(
        UpdateCompanySettingsRequest request, CancellationToken ct = default)
    {
        CompanySettings? settings = await _settingsRepository.GetForUpdateAsync(ct);
        if (settings is null)
            throw AppErrors.SettingsNotFound;

        if (request.DefaultTimezone is not null)
            settings.DefaultTimezone = request.DefaultTimezone;

        if (request.DefaultCurrency is not null)
            settings.DefaultCurrency = request.DefaultCurrency;

        if (request.AuthSettings is not null)
            settings.AuthSettings = JsonSerializer.Serialize(request.AuthSettings, JsonOptions);

        if (request.ProfileSettings is not null)
            settings.ProfileSettings = JsonSerializer.Serialize(request.ProfileSettings, JsonOptions);

        if (request.ScreeningSettings is not null)
            settings.ScreeningSettings = JsonSerializer.Serialize(request.ScreeningSettings, JsonOptions);

        if (request.MatchingSettings is not null)
            settings.MatchingSettings = JsonSerializer.Serialize(request.MatchingSettings, JsonOptions);

        if (request.AssessmentSettings is not null)
            settings.AssessmentSettings = JsonSerializer.Serialize(request.AssessmentSettings, JsonOptions);

        if (request.NotificationSettings is not null)
            settings.NotificationSettings = JsonSerializer.Serialize(request.NotificationSettings, JsonOptions);

        await _unitOfWork.SaveChangesAsync(ct);

        return MapToResponse(settings);
    }

    private static CompanySettingsResponse MapToResponse(CompanySettings settings)
    {
        return new CompanySettingsResponse
        {
            Id = settings.Id,
            DefaultTimezone = settings.DefaultTimezone,
            DefaultCurrency = settings.DefaultCurrency,
            AuthSettings = JsonSerializer.Deserialize<AuthSettingsDto>(settings.AuthSettings, JsonOptions) ?? new AuthSettingsDto(),
            ProfileSettings = JsonSerializer.Deserialize<ProfileSettingsDto>(settings.ProfileSettings, JsonOptions) ?? new ProfileSettingsDto(),
            ScreeningSettings = JsonSerializer.Deserialize<ScreeningSettingsDto>(settings.ScreeningSettings, JsonOptions) ?? new ScreeningSettingsDto(),
            MatchingSettings = JsonSerializer.Deserialize<MatchingSettingsDto>(settings.MatchingSettings, JsonOptions) ?? new MatchingSettingsDto(),
            AssessmentSettings = JsonSerializer.Deserialize<AssessmentSettingsDto>(settings.AssessmentSettings, JsonOptions) ?? new AssessmentSettingsDto(),
            NotificationSettings = JsonSerializer.Deserialize<NotificationSettingsDto>(settings.NotificationSettings, JsonOptions) ?? new NotificationSettingsDto(),
            CreatedAt = settings.CreatedAt,
            UpdatedAt = settings.UpdatedAt
        };
    }
}

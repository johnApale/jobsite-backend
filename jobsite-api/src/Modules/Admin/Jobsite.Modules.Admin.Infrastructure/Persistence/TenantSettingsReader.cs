using System.Text.Json;
using Jobsite.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jobsite.Modules.Admin.Infrastructure.Persistence;

/// <summary>
/// Reads named JSONB settings sections from the tenant's <c>admin.company_settings</c> singleton.
/// Registered as <see cref="ITenantSettingsReader"/> so other modules can check feature flags
/// without cross-module project references.
/// </summary>
public sealed class TenantSettingsReader : ITenantSettingsReader
{
    private readonly AdminDbContext _db;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public TenantSettingsReader(AdminDbContext db) => _db = db;

    public async Task<T?> GetSettingAsync<T>(string section, CancellationToken ct = default) where T : class
    {
        CompanySettingsProjection? row = await _db.CompanySettings
            .AsNoTracking()
            .Select(s => new CompanySettingsProjection
            {
                AuthSettings = s.AuthSettings,
                ProfileSettings = s.ProfileSettings,
                ScreeningSettings = s.ScreeningSettings,
                MatchingSettings = s.MatchingSettings,
                AssessmentSettings = s.AssessmentSettings,
                NotificationSettings = s.NotificationSettings,
            })
            .FirstOrDefaultAsync(ct);

        if (row is null)
            return null;

        string? json = section switch
        {
            "auth_settings" => row.AuthSettings,
            "profile_settings" => row.ProfileSettings,
            "screening_settings" => row.ScreeningSettings,
            "matching_settings" => row.MatchingSettings,
            "assessment_settings" => row.AssessmentSettings,
            "notification_settings" => row.NotificationSettings,
            _ => null
        };

        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private sealed class CompanySettingsProjection
    {
        public string AuthSettings { get; set; } = null!;
        public string ProfileSettings { get; set; } = null!;
        public string ScreeningSettings { get; set; } = null!;
        public string MatchingSettings { get; set; } = null!;
        public string AssessmentSettings { get; set; } = null!;
        public string NotificationSettings { get; set; } = null!;
    }
}

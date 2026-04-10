namespace Jobsite.SharedKernel.Persistence;

/// <summary>
/// Reads tenant-scoped settings from the <c>admin.company_settings</c> singleton.
/// Implemented by the Admin module. Consumed by other modules that need
/// feature-flag or configuration checks without cross-module project references.
/// </summary>
public interface ITenantSettingsReader
{
    /// <summary>
    /// Deserializes a named JSONB settings section from the tenant's company settings.
    /// Returns <c>null</c> if the settings row or section does not exist.
    /// </summary>
    /// <param name="section">The settings column name (e.g., "assessment_settings", "screening_settings").</param>
    /// <param name="ct">Cancellation token.</param>
    Task<T?> GetSettingAsync<T>(string section, CancellationToken ct = default) where T : class;
}

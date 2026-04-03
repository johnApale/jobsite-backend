namespace Jobsite.Modules.Admin.Application.DTOs.Settings;

/// <summary>Notification configuration settings (JSONB shape for <c>company_settings.notification_settings</c>).</summary>
public sealed class NotificationSettingsDto
{
    public bool NotifyOnNewApplication { get; set; } = true;
    public bool NotifyOnScreeningComplete { get; set; } = true;
    public bool NotifyOnAssessmentComplete { get; set; } = true;
    public bool NotifyOnManualReviewNeeded { get; set; } = true;
    public bool NotifyOnOfferResponse { get; set; } = true;
    public string? NotificationEmail { get; set; }
}

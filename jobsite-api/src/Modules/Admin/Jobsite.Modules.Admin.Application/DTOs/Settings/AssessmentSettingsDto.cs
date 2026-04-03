namespace Jobsite.Modules.Admin.Application.DTOs.Settings;

/// <summary>Assessment configuration settings (JSONB shape for <c>company_settings.assessment_settings</c>).</summary>
public sealed class AssessmentSettingsDto
{
    public bool Enabled { get; set; } = true;
    public int TimeLimitMinutes { get; set; } = 60;
    public bool AllowSkip { get; set; } = true;
    public string PartialCompletionPolicy { get; set; } = "ScorePartial";
    public string CompletionPolicy { get; set; } = "AutoAdvance";
    public bool AiAssessmentQuestionsEnabled { get; set; }
}

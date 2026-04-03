namespace Jobsite.Modules.Admin.Application.DTOs.Settings;

/// <summary>Default evaluation criterion template within screening settings.</summary>
public sealed class DefaultEvaluationCriterionDto
{
    public string Name { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string EvaluationMethod { get; set; } = null!;
    public bool IsRequired { get; set; }
    public int Weight { get; set; }
}

/// <summary>Screening configuration settings (JSONB shape for <c>company_settings.screening_settings</c>).</summary>
public sealed class ScreeningSettingsDto
{
    public double AutoAdvanceThreshold { get; set; } = 70.0;
    public double AutoRejectThreshold { get; set; } = 30.0;
    public string ManualReviewPolicy { get; set; } = "QueueForReview";
    public bool AiScoringEnabled { get; set; }
    public bool CandidateTransparencyEnabled { get; set; }
    public string CandidateTransparencyLevel { get; set; } = "Summary";
    public List<DefaultEvaluationCriterionDto> DefaultEvaluationCriteria { get; set; } = [];
}

using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.HRWorkflows.Domain.Entities;

/// <summary>
/// An interviewer assigned to a final interview — maps to <c>hr_workflows.interview_panelists</c>.
/// Unique constraint on <c>(interview_id, interviewer_id)</c>.
/// Each panelist provides independent feedback.
/// </summary>
public sealed class InterviewPanelist : Entity
{
    /// <summary>FK to <c>hr_workflows.final_interviews.application_id</c>.</summary>
    public Guid InterviewId { get; set; }

    /// <summary>Ref to <c>auth.users.id</c> (cross-module) — the staff member conducting the interview.</summary>
    public Guid InterviewerId { get; set; }

    /// <summary>Panelist's overall rating (1.0–5.0). Null until feedback is submitted.</summary>
    public decimal? Rating { get; set; }

    /// <summary>Panelist's individual recommendation: StrongHire, Hire, NoHire, StrongNoHire.</summary>
    public string? Recommendation { get; set; }

    /// <summary>What the candidate did well in this interviewer's assessment.</summary>
    public string? Strengths { get; set; }

    /// <summary>Areas of weakness or concern.</summary>
    public string? Concerns { get; set; }

    /// <summary>General interview notes, observations, questions asked.</summary>
    public string? Notes { get; set; }

    /// <summary>When this panelist submitted their feedback. Null = hasn't submitted yet.</summary>
    public DateTime? FeedbackSubmittedAt { get; set; }

    /// <summary>Navigation property to the parent interview.</summary>
    public FinalInterview Interview { get; set; } = null!;
}

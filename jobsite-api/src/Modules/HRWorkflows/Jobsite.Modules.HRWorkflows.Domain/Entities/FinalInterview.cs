using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.HRWorkflows.Domain.Entities;

/// <summary>
/// A scheduled final interview for a shortlisted candidate — maps to <c>hr_workflows.final_interviews</c>.
/// Uses shared primary key with <c>recruitment.applications</c> (ApplicationId is both PK and FK).
/// One final interview per application.
/// </summary>
public sealed class FinalInterview : Entity
{
    /// <summary>Shared PK with <c>recruitment.applications.id</c>.</summary>
    public Guid ApplicationId { get; set; }

    /// <summary>Lifecycle status: Scheduled, InProgress, Completed, Cancelled, NoShow.</summary>
    public string Status { get; set; } = null!;

    /// <summary>Interview format: InPerson, Video, Phone.</summary>
    public string InterviewType { get; set; } = null!;

    /// <summary>When the interview is scheduled to take place.</summary>
    public DateTime ScheduledAt { get; set; }

    /// <summary>Expected interview duration in minutes.</summary>
    public int DurationMinutes { get; set; } = 60;

    /// <summary>Physical address, video call URL, or phone number depending on type.</summary>
    public string? Location { get; set; }

    /// <summary>FK to <c>auth.users.id</c> — the recruiter/hiring manager who scheduled this interview.</summary>
    public Guid ScheduledBy { get; set; }

    /// <summary>Aggregated recommendation: StrongHire, Hire, NoHire, StrongNoHire. Set by hiring manager.</summary>
    public string? OverallRecommendation { get; set; }

    /// <summary>Hiring manager's summary of the interview outcome.</summary>
    public string? DecisionNotes { get; set; }

    /// <summary>FK to <c>auth.users.id</c> — the hiring manager who made the final recommendation.</summary>
    public Guid? DecidedBy { get; set; }

    /// <summary>When the final recommendation was recorded.</summary>
    public DateTime? DecidedAt { get; set; }

    /// <summary>When the interview actually finished.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>When the interview was cancelled.</summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>Reason for cancellation.</summary>
    public string? CancellationReason { get; set; }

    /// <summary>Panel of interviewers assigned to this interview.</summary>
    public ICollection<InterviewPanelist> Panelists { get; set; } = new List<InterviewPanelist>();
}

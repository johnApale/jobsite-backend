namespace Jobsite.Modules.Screening.Domain.Constants;

/// <summary>Tenant policy for handling candidates in the manual review zone (between thresholds).</summary>
public static class ManualReviewPolicy
{
    public const string QueueForReview = "QueueForReview";
    public const string AutoAdvanceAll = "AutoAdvanceAll";
    public const string AutoRejectAll = "AutoRejectAll";
    public const string NotifyAndHold = "NotifyAndHold";

    public static bool IsValid(string policy) =>
        policy is QueueForReview or AutoAdvanceAll or AutoRejectAll or NotifyAndHold;
}

namespace Jobsite.Modules.HRWorkflows.Domain.Constants;

public static class InterviewStatus
{
    public const string Scheduled = "Scheduled";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string NoShow = "NoShow";

    public static bool IsValid(string status) =>
        status is Scheduled or InProgress or Completed or Cancelled or NoShow;
}

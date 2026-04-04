namespace Jobsite.Modules.Screening.Domain.Constants;

/// <summary>Screening processing status — tracks the scoring pipeline lifecycle.</summary>
public static class ScreeningStatus
{
    public const string Pending = "Pending";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Failed = "Failed";

    public static bool IsValid(string status) =>
        status is Pending or InProgress or Completed or Failed;
}

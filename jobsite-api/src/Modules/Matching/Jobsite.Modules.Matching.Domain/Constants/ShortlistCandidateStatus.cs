namespace Jobsite.Modules.Matching.Domain.Constants;

public static class ShortlistCandidateStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static bool IsValid(string status) =>
        status is Pending or Approved or Rejected;
}

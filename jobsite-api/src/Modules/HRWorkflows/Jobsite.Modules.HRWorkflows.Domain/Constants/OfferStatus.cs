namespace Jobsite.Modules.HRWorkflows.Domain.Constants;

public static class OfferStatus
{
    public const string Draft = "Draft";
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Declined = "Declined";
    public const string Withdrawn = "Withdrawn";
    public const string Expired = "Expired";

    public static bool IsValid(string status) =>
        status is Draft or Pending or Accepted or Declined or Withdrawn or Expired;

    public static bool IsTerminal(string status) =>
        status is Accepted or Declined or Withdrawn or Expired;
}

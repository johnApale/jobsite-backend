namespace Jobsite.Modules.HRWorkflows.Domain.Constants;

public static class OfferEmploymentType
{
    public const string FullTime = "FullTime";
    public const string PartTime = "PartTime";
    public const string Contract = "Contract";
    public const string Temporary = "Temporary";

    public static bool IsValid(string type) =>
        type is FullTime or PartTime or Contract or Temporary;
}

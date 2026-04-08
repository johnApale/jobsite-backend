namespace Jobsite.Modules.HRWorkflows.Domain.Constants;

public static class InterviewType
{
    public const string InPerson = "InPerson";
    public const string Video = "Video";
    public const string Phone = "Phone";

    public static bool IsValid(string type) =>
        type is InPerson or Video or Phone;
}

namespace Jobsite.Modules.HRWorkflows.Domain.Constants;

public static class InterviewRecommendation
{
    public const string StrongHire = "StrongHire";
    public const string Hire = "Hire";
    public const string NoHire = "NoHire";
    public const string StrongNoHire = "StrongNoHire";

    public static bool IsValid(string recommendation) =>
        recommendation is StrongHire or Hire or NoHire or StrongNoHire;

    public static bool IsPositive(string recommendation) =>
        recommendation is StrongHire or Hire;
}

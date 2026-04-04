namespace Jobsite.Modules.Screening.Domain.Constants;

/// <summary>Level of detail for candidate-facing transparency feedback.</summary>
public static class TransparencyLevel
{
    public const string None = "None";
    public const string Summary = "Summary";
    public const string Detailed = "Detailed";

    public static bool IsValid(string level) =>
        level is None or Summary or Detailed;
}

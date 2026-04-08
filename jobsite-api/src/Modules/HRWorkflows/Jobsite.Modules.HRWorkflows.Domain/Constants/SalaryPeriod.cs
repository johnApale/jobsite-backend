namespace Jobsite.Modules.HRWorkflows.Domain.Constants;

public static class SalaryPeriod
{
    public const string Annual = "Annual";
    public const string Monthly = "Monthly";
    public const string Hourly = "Hourly";

    public static bool IsValid(string period) =>
        period is Annual or Monthly or Hourly;
}

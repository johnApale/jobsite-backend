namespace Jobsite.Modules.Recruitment.Domain.Constants;

/// <summary>
/// Industry categories for client companies.
/// Values must match the CHECK constraint <c>chk_client_companies_industry</c> exactly.
/// </summary>
public static class Industry
{
    public const string Technology = "Technology";
    public const string Healthcare = "Healthcare";
    public const string Finance = "Finance";
    public const string Education = "Education";
    public const string Manufacturing = "Manufacturing";
    public const string Retail = "Retail";
    public const string Construction = "Construction";
    public const string Transportation = "Transportation";
    public const string Hospitality = "Hospitality";
    public const string Media = "Media";
    public const string Energy = "Energy";
    public const string Agriculture = "Agriculture";
    public const string RealEstate = "RealEstate";
    public const string Legal = "Legal";
    public const string Consulting = "Consulting";
    public const string Telecommunications = "Telecommunications";
    public const string Pharmaceutical = "Pharmaceutical";
    public const string Automotive = "Automotive";
    public const string Aerospace = "Aerospace";
    public const string Government = "Government";
    public const string NonProfit = "NonProfit";
    public const string Other = "Other";

    public static bool IsValid(string industry) =>
        industry is Technology or Healthcare or Finance or Education or Manufacturing
            or Retail or Construction or Transportation or Hospitality or Media
            or Energy or Agriculture or RealEstate or Legal or Consulting
            or Telecommunications or Pharmaceutical or Automotive or Aerospace
            or Government or NonProfit or Other;
}

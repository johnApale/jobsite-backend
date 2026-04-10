using System.Reflection;
using FluentAssertions;
using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Entities;
using Jobsite.Modules.Admin.Infrastructure.Persistence;
using Jobsite.Modules.Auth.Application.Interfaces;
using Jobsite.Modules.Auth.Domain.Entities;
using Jobsite.Modules.Auth.Infrastructure.Persistence;
using Jobsite.Modules.Tenancy.Application.Services;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Jobsite.Modules.Tenancy.Infrastructure.Persistence;
using NetArchTest.Rules;

namespace Jobsite.ArchitectureTests;

/// <summary>
/// Validates that modules do not cross-reference each other's domain, application,
/// infrastructure, or API layers. Only SharedKernel events and interfaces are used
/// for inter-module communication.
/// </summary>
public sealed class ModuleIsolationTests
{
    private static readonly string[] AllModuleNamespaces =
    [
        "Jobsite.Modules.Tenancy",
        "Jobsite.Modules.Auth",
        "Jobsite.Modules.Admin",
        "Jobsite.Modules.Profiles",
        "Jobsite.Modules.Recruitment",
        "Jobsite.Modules.Screening",
        "Jobsite.Modules.Matching",
        "Jobsite.Modules.HRWorkflows"
    ];

    private static readonly Dictionary<string, Assembly> DomainAssemblies = new()
    {
        ["Jobsite.Modules.Tenancy"] = typeof(Tenant).Assembly,
        ["Jobsite.Modules.Auth"] = typeof(User).Assembly,
        ["Jobsite.Modules.Admin"] = typeof(CompanySettings).Assembly,
        ["Jobsite.Modules.Profiles"] = typeof(Jobsite.Modules.Profiles.Domain.Entities.ApplicantProfile).Assembly,
        ["Jobsite.Modules.Recruitment"] = typeof(Jobsite.Modules.Recruitment.Domain.Entities.ClientCompany).Assembly,
        ["Jobsite.Modules.Screening"] = typeof(Jobsite.Modules.Screening.Domain.Entities.ScreeningResult).Assembly,
        ["Jobsite.Modules.Matching"] = typeof(Jobsite.Modules.Matching.Domain.Entities.CandidateMatch).Assembly,
        ["Jobsite.Modules.HRWorkflows"] = typeof(Jobsite.Modules.HRWorkflows.Domain.Entities.FinalInterview).Assembly,
    };

    private static readonly Dictionary<string, Assembly> ApplicationAssemblies = new()
    {
        ["Jobsite.Modules.Tenancy"] = typeof(ITenantService).Assembly,
        ["Jobsite.Modules.Auth"] = typeof(IAuthService).Assembly,
        ["Jobsite.Modules.Admin"] = typeof(IAdminSettingsService).Assembly,
        ["Jobsite.Modules.Profiles"] = typeof(Jobsite.Modules.Profiles.Application.Interfaces.IProfileService).Assembly,
        ["Jobsite.Modules.Recruitment"] = typeof(Jobsite.Modules.Recruitment.Application.Interfaces.IJobPostingRepository).Assembly,
        ["Jobsite.Modules.Screening"] = typeof(Jobsite.Modules.Screening.Application.Interfaces.IAssessmentService).Assembly,
        ["Jobsite.Modules.Matching"] = typeof(Jobsite.Modules.Matching.Application.Services.IMatchingService).Assembly,
        ["Jobsite.Modules.HRWorkflows"] = typeof(Jobsite.Modules.HRWorkflows.Application.Services.IFeedbackAggregationService).Assembly,
    };

    private static readonly Dictionary<string, Assembly> InfrastructureAssemblies = new()
    {
        ["Jobsite.Modules.Tenancy"] = typeof(CatalogDbContext).Assembly,
        ["Jobsite.Modules.Auth"] = typeof(AuthDbContext).Assembly,
        ["Jobsite.Modules.Admin"] = typeof(AdminDbContext).Assembly,
        ["Jobsite.Modules.Profiles"] = typeof(Jobsite.Modules.Profiles.Infrastructure.Persistence.ProfilesDbContext).Assembly,
        ["Jobsite.Modules.Recruitment"] = typeof(Jobsite.Modules.Recruitment.Infrastructure.Persistence.RecruitmentDbContext).Assembly,
        ["Jobsite.Modules.Screening"] = typeof(Jobsite.Modules.Screening.Infrastructure.Persistence.ScreeningDbContext).Assembly,
        ["Jobsite.Modules.Matching"] = typeof(Jobsite.Modules.Matching.Infrastructure.Persistence.MatchingDbContext).Assembly,
        ["Jobsite.Modules.HRWorkflows"] = typeof(Jobsite.Modules.HRWorkflows.Infrastructure.Persistence.HRWorkflowsDbContext).Assembly,
    };

    private static readonly Dictionary<string, Assembly> ApiAssemblies = new()
    {
        ["Jobsite.Modules.Tenancy"] = typeof(Jobsite.Modules.Tenancy.Api.TenantEndpoints).Assembly,
        ["Jobsite.Modules.Auth"] = typeof(Jobsite.Modules.Auth.Api.AuthEndpoints).Assembly,
        ["Jobsite.Modules.Admin"] = typeof(Jobsite.Modules.Admin.Api.AdminEndpoints).Assembly,
        ["Jobsite.Modules.Profiles"] = typeof(Jobsite.Modules.Profiles.Api.ProfileEndpoints).Assembly,
        ["Jobsite.Modules.Recruitment"] = typeof(Jobsite.Modules.Recruitment.Api.RecruitmentEndpoints).Assembly,
        ["Jobsite.Modules.Screening"] = typeof(Jobsite.Modules.Screening.Api.ScreeningEndpoints).Assembly,
        ["Jobsite.Modules.Matching"] = typeof(Jobsite.Modules.Matching.Api.MatchingEndpoints).Assembly,
        ["Jobsite.Modules.HRWorkflows"] = typeof(Jobsite.Modules.HRWorkflows.Api.HRWorkflowsEndpoints).Assembly,
    };

    public static IEnumerable<object[]> ModuleNames()
    {
        foreach (string module in AllModuleNamespaces)
        {
            yield return [module];
        }
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void DomainLayer_ShouldNotReference_OtherModules(string moduleName)
    {
        Assembly domainAssembly = DomainAssemblies[moduleName];
        string[] otherModules = AllModuleNamespaces.Where(m => m != moduleName).ToArray();

        foreach (string other in otherModules)
        {
            TestResult result = Types.InAssembly(domainAssembly)
                .ShouldNot()
                .HaveDependencyOn(other)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"{moduleName}.Domain must not reference {other}");
        }
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void ApplicationLayer_ShouldNotReference_OtherModules(string moduleName)
    {
        Assembly applicationAssembly = ApplicationAssemblies[moduleName];
        string[] otherModules = AllModuleNamespaces.Where(m => m != moduleName).ToArray();

        foreach (string other in otherModules)
        {
            TestResult result = Types.InAssembly(applicationAssembly)
                .ShouldNot()
                .HaveDependencyOn(other)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"{moduleName}.Application must not reference {other}");
        }
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void InfrastructureLayer_ShouldNotReference_OtherModules(string moduleName)
    {
        Assembly infraAssembly = InfrastructureAssemblies[moduleName];
        string[] otherModules = AllModuleNamespaces.Where(m => m != moduleName).ToArray();

        foreach (string other in otherModules)
        {
            TestResult result = Types.InAssembly(infraAssembly)
                .ShouldNot()
                .HaveDependencyOn(other)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"{moduleName}.Infrastructure must not reference {other}");
        }
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void ApiLayer_ShouldNotReference_OtherModules(string moduleName)
    {
        Assembly apiAssembly = ApiAssemblies[moduleName];
        string[] otherModules = AllModuleNamespaces.Where(m => m != moduleName).ToArray();

        foreach (string other in otherModules)
        {
            TestResult result = Types.InAssembly(apiAssembly)
                .ShouldNot()
                .HaveDependencyOn(other)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"{moduleName}.Api must not reference {other}");
        }
    }
}

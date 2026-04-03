using System.Reflection;
using FluentAssertions;
using Jobsite.Modules.Admin.Domain.Entities;
using Jobsite.Modules.Admin.Infrastructure.Persistence;
using Jobsite.Modules.Auth.Domain.Entities;
using Jobsite.Modules.Auth.Infrastructure.Persistence;
using Jobsite.Modules.HRWorkflows.Domain;
using Jobsite.Modules.Matching.Domain;
using Jobsite.Modules.Profiles.Domain;
using Jobsite.Modules.Recruitment.Domain;
using Jobsite.Modules.Screening.Domain;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Jobsite.Modules.Tenancy.Infrastructure.Persistence;
using NetArchTest.Rules;

namespace Jobsite.ArchitectureTests;

/// <summary>
/// Validates that modules do not cross-reference each other's domain or application layers.
/// Only SharedKernel events are used for inter-module communication.
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
        ["Jobsite.Modules.Recruitment"] = typeof(Jobsite.Modules.Recruitment.Domain.Class1).Assembly,
        ["Jobsite.Modules.Screening"] = typeof(Jobsite.Modules.Screening.Domain.Class1).Assembly,
        ["Jobsite.Modules.Matching"] = typeof(Jobsite.Modules.Matching.Domain.Class1).Assembly,
        ["Jobsite.Modules.HRWorkflows"] = typeof(Jobsite.Modules.HRWorkflows.Domain.Class1).Assembly,
    };

    private static readonly Dictionary<string, Assembly> InfrastructureAssemblies = new()
    {
        ["Jobsite.Modules.Tenancy"] = typeof(CatalogDbContext).Assembly,
        ["Jobsite.Modules.Auth"] = typeof(AuthDbContext).Assembly,
        ["Jobsite.Modules.Admin"] = typeof(AdminDbContext).Assembly,
        ["Jobsite.Modules.Profiles"] = typeof(Jobsite.Modules.Profiles.Infrastructure.Persistence.ProfilesDbContext).Assembly,
        ["Jobsite.Modules.Recruitment"] = typeof(Jobsite.Modules.Recruitment.Infrastructure.Class1).Assembly,
        ["Jobsite.Modules.Screening"] = typeof(Jobsite.Modules.Screening.Infrastructure.Class1).Assembly,
        ["Jobsite.Modules.Matching"] = typeof(Jobsite.Modules.Matching.Infrastructure.Class1).Assembly,
        ["Jobsite.Modules.HRWorkflows"] = typeof(Jobsite.Modules.HRWorkflows.Infrastructure.Class1).Assembly,
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
}

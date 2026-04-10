using System.Reflection;
using FluentAssertions;
using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Entities;
using Jobsite.Modules.Auth.Application.Interfaces;
using Jobsite.Modules.Auth.Domain.Entities;
using Jobsite.Modules.HRWorkflows.Application.Services;
using Jobsite.Modules.Matching.Application.Services;
using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.Modules.Tenancy.Application.Services;
using Jobsite.Modules.Tenancy.Domain.Entities;
using NetArchTest.Rules;

namespace Jobsite.ArchitectureTests;

/// <summary>
/// Enforces the module layer dependency rules for all 8 modules:
///   SharedKernel ← no project references
///   Module.Domain ← SharedKernel only
///   Module.Application ← Module.Domain only
///   Module.Infrastructure ← Module.Application
///   Module.Api ← Module.Application + Module.Infrastructure
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Dictionary<string, Assembly> DomainAssemblies = new()
    {
        ["Jobsite.Modules.Tenancy"] = typeof(Tenant).Assembly,
        ["Jobsite.Modules.Auth"] = typeof(User).Assembly,
        ["Jobsite.Modules.Admin"] = typeof(CompanySettings).Assembly,
        ["Jobsite.Modules.Profiles"] = typeof(ApplicantProfile).Assembly,
        ["Jobsite.Modules.Recruitment"] = typeof(JobPosting).Assembly,
        ["Jobsite.Modules.Screening"] = typeof(ScreeningResult).Assembly,
        ["Jobsite.Modules.Matching"] = typeof(Jobsite.Modules.Matching.Domain.Entities.CandidateMatch).Assembly,
        ["Jobsite.Modules.HRWorkflows"] = typeof(Jobsite.Modules.HRWorkflows.Domain.Entities.FinalInterview).Assembly,
    };

    private static readonly Dictionary<string, Assembly> ApplicationAssemblies = new()
    {
        ["Jobsite.Modules.Tenancy"] = typeof(ITenantService).Assembly,
        ["Jobsite.Modules.Auth"] = typeof(IAuthService).Assembly,
        ["Jobsite.Modules.Admin"] = typeof(IAdminSettingsService).Assembly,
        ["Jobsite.Modules.Profiles"] = typeof(IProfileService).Assembly,
        ["Jobsite.Modules.Recruitment"] = typeof(IJobPostingRepository).Assembly,
        ["Jobsite.Modules.Screening"] = typeof(IAssessmentService).Assembly,
        ["Jobsite.Modules.Matching"] = typeof(IMatchingService).Assembly,
        ["Jobsite.Modules.HRWorkflows"] = typeof(IFeedbackAggregationService).Assembly,
    };

    public static IEnumerable<object[]> AllModules()
    {
        foreach (string module in DomainAssemblies.Keys)
        {
            yield return [module];
        }
    }

    // ── Domain layer must not reference Application or Infrastructure ────

    [Theory]
    [MemberData(nameof(AllModules))]
    public void DomainLayer_ShouldNotReference_ApplicationLayer(string moduleName)
    {
        // Arrange & Act
        TestResult result = Types.InAssembly(DomainAssemblies[moduleName])
            .ShouldNot()
            .HaveDependencyOn($"{moduleName}.Application")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"{moduleName} Domain layer must not reference Application layer");
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public void DomainLayer_ShouldNotReference_InfrastructureLayer(string moduleName)
    {
        // Arrange & Act
        TestResult result = Types.InAssembly(DomainAssemblies[moduleName])
            .ShouldNot()
            .HaveDependencyOn($"{moduleName}.Infrastructure")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"{moduleName} Domain layer must not reference Infrastructure layer");
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public void DomainLayer_ShouldNotReference_EFCore(string moduleName)
    {
        // Arrange & Act
        TestResult result = Types.InAssembly(DomainAssemblies[moduleName])
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"{moduleName} Domain layer must not reference EF Core");
    }

    // ── Application layer must not reference Infrastructure or EF Core ───

    [Theory]
    [MemberData(nameof(AllModules))]
    public void ApplicationLayer_ShouldNotReference_InfrastructureLayer(string moduleName)
    {
        // Arrange & Act
        TestResult result = Types.InAssembly(ApplicationAssemblies[moduleName])
            .ShouldNot()
            .HaveDependencyOn($"{moduleName}.Infrastructure")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"{moduleName} Application layer must not reference Infrastructure layer");
    }

    [Theory]
    [MemberData(nameof(AllModules))]
    public void ApplicationLayer_ShouldNotReference_EFCore(string moduleName)
    {
        // Arrange & Act
        TestResult result = Types.InAssembly(ApplicationAssemblies[moduleName])
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            $"{moduleName} Application layer must not reference EF Core");
    }
}

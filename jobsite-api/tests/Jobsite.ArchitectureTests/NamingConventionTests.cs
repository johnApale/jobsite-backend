using System.Reflection;
using FluentAssertions;
using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Entities;
using Jobsite.Modules.Admin.Infrastructure.Persistence;
using Jobsite.Modules.Auth.Application.Interfaces;
using Jobsite.Modules.Auth.Domain.Entities;
using Jobsite.Modules.Auth.Infrastructure.Persistence;
using Jobsite.Modules.HRWorkflows.Application.Services;
using Jobsite.Modules.HRWorkflows.Domain;
using Jobsite.Modules.HRWorkflows.Infrastructure.Persistence;
using Jobsite.Modules.Matching.Application.Services;
using Jobsite.Modules.Matching.Domain;
using Jobsite.Modules.Matching.Infrastructure.Persistence;
using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Domain;
using Jobsite.Modules.Profiles.Infrastructure.Persistence;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Domain;
using Jobsite.Modules.Recruitment.Infrastructure.Persistence;
using Jobsite.Modules.Screening.Application.Interfaces;
using Jobsite.Modules.Screening.Domain.Entities;
using Jobsite.Modules.Screening.Infrastructure.Persistence;
using Jobsite.Modules.Tenancy.Application.Services;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Jobsite.Modules.Tenancy.Infrastructure.Persistence;
using Jobsite.SharedKernel.Domain;
using NetArchTest.Rules;

namespace Jobsite.ArchitectureTests;

/// <summary>
/// Enforces naming and structural conventions across the codebase.
/// </summary>
public sealed class NamingConventionTests
{
    private static readonly Assembly SharedKernel = typeof(Entity).Assembly;

    private static readonly Assembly[] AllDomainAssemblies =
    [
        typeof(Tenant).Assembly,
        typeof(User).Assembly,
        typeof(CompanySettings).Assembly,
        typeof(Jobsite.Modules.Profiles.Domain.Entities.ApplicantProfile).Assembly,
        typeof(Jobsite.Modules.Recruitment.Domain.Entities.ClientCompany).Assembly,
        typeof(Jobsite.Modules.Screening.Domain.Entities.ScreeningResult).Assembly,
        typeof(Jobsite.Modules.Matching.Domain.Entities.CandidateMatch).Assembly,
        typeof(Jobsite.Modules.HRWorkflows.Domain.Entities.FinalInterview).Assembly,
    ];

    private static readonly Assembly[] AllApplicationAssemblies =
    [
        typeof(ITenantService).Assembly,
        typeof(IAuthService).Assembly,
        typeof(IAdminSettingsService).Assembly,
        typeof(IProfileService).Assembly,
        typeof(IJobPostingRepository).Assembly,
        typeof(IAssessmentService).Assembly,
        typeof(IMatchingService).Assembly,
        typeof(IFeedbackAggregationService).Assembly,
    ];

    private static readonly Assembly[] AllInfrastructureAssemblies =
    [
        typeof(CatalogDbContext).Assembly,
        typeof(AuthDbContext).Assembly,
        typeof(AdminDbContext).Assembly,
        typeof(ProfilesDbContext).Assembly,
        typeof(RecruitmentDbContext).Assembly,
        typeof(ScreeningDbContext).Assembly,
        typeof(MatchingDbContext).Assembly,
        typeof(HRWorkflowsDbContext).Assembly,
    ];

    private static readonly Assembly[] AllApiAssemblies =
    [
        typeof(Jobsite.Modules.Tenancy.Api.TenantEndpoints).Assembly,
        typeof(Jobsite.Modules.Auth.Api.AuthEndpoints).Assembly,
        typeof(Jobsite.Modules.Admin.Api.AdminEndpoints).Assembly,
        typeof(Jobsite.Modules.Profiles.Api.ProfileEndpoints).Assembly,
        typeof(Jobsite.Modules.Recruitment.Api.RecruitmentEndpoints).Assembly,
        typeof(Jobsite.Modules.Screening.Api.ScreeningEndpoints).Assembly,
        typeof(Jobsite.Modules.Matching.Api.MatchingEndpoints).Assembly,
        typeof(Jobsite.Modules.HRWorkflows.Api.HRWorkflowsEndpoints).Assembly,
    ];

    [Fact]
    public void SharedKernel_ConcreteClasses_ShouldBeSealed()
    {
        TestResult result = Types.InAssembly(SharedKernel)
            .That()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "All concrete classes in SharedKernel must be sealed. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void AllDomainLayers_ConcreteClasses_ShouldBeSealed()
    {
        foreach (Assembly assembly in AllDomainAssemblies)
        {
            TestResult result = Types.InAssembly(assembly)
                .That()
                .AreClasses()
                .And()
                .AreNotAbstract()
                .Should()
                .BeSealed()
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"All concrete classes in {assembly.GetName().Name} must be sealed. " +
                $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Fact]
    public void AllInfrastructureLayers_ConcreteClasses_ShouldBeSealed()
    {
        foreach (Assembly assembly in AllInfrastructureAssemblies)
        {
            // Exclude EF Core generated migration classes
            TestResult result = Types.InAssembly(assembly)
                .That()
                .AreClasses()
                .And()
                .AreNotAbstract()
                .And()
                .DoNotResideInNamespaceContaining("Migrations")
                .Should()
                .BeSealed()
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"All concrete classes in {assembly.GetName().Name} must be sealed (excluding EF migrations). " +
                $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Fact]
    public void AllApplicationLayers_ConcreteClasses_ShouldBeSealed()
    {
        foreach (Assembly assembly in AllApplicationAssemblies)
        {
            TestResult result = Types.InAssembly(assembly)
                .That()
                .AreClasses()
                .And()
                .AreNotAbstract()
                .Should()
                .BeSealed()
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"All concrete classes in {assembly.GetName().Name} must be sealed. " +
                $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Fact]
    public void AllApiLayers_ConcreteClasses_ShouldBeSealed()
    {
        foreach (Assembly assembly in AllApiAssemblies)
        {
            TestResult result = Types.InAssembly(assembly)
                .That()
                .AreClasses()
                .And()
                .AreNotAbstract()
                .Should()
                .BeSealed()
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"All concrete classes in {assembly.GetName().Name} must be sealed. " +
                $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Fact]
    public void AllDomainLayers_Interfaces_ShouldStartWithI()
    {
        foreach (Assembly assembly in AllDomainAssemblies)
        {
            TestResult result = Types.InAssembly(assembly)
                .That()
                .AreInterfaces()
                .Should()
                .HaveNameStartingWith("I")
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"All interfaces in {assembly.GetName().Name} must start with 'I'. " +
                $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }
}

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
        typeof(Jobsite.Modules.Profiles.Domain.Class1).Assembly,
        typeof(Jobsite.Modules.Recruitment.Domain.Class1).Assembly,
        typeof(Jobsite.Modules.Screening.Domain.Class1).Assembly,
        typeof(Jobsite.Modules.Matching.Domain.Class1).Assembly,
        typeof(Jobsite.Modules.HRWorkflows.Domain.Class1).Assembly,
    ];

    private static readonly Assembly[] AllInfrastructureAssemblies =
    [
        typeof(CatalogDbContext).Assembly,
        typeof(AuthDbContext).Assembly,
        typeof(AdminDbContext).Assembly,
        typeof(Jobsite.Modules.Profiles.Infrastructure.Class1).Assembly,
        typeof(Jobsite.Modules.Recruitment.Infrastructure.Class1).Assembly,
        typeof(Jobsite.Modules.Screening.Infrastructure.Class1).Assembly,
        typeof(Jobsite.Modules.Matching.Infrastructure.Class1).Assembly,
        typeof(Jobsite.Modules.HRWorkflows.Infrastructure.Class1).Assembly,
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

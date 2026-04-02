using System.Reflection;
using FluentAssertions;
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
    private static readonly Assembly TenancyDomain = typeof(Tenant).Assembly;
    private static readonly Assembly TenancyInfrastructure = typeof(CatalogDbContext).Assembly;

    [Fact]
    public void TenancyDomain_ShouldNotReference_OtherModules()
    {
        // Arrange
        string[] otherModules =
        [
            "Jobsite.Modules.Auth",
            "Jobsite.Modules.Profiles",
            "Jobsite.Modules.Recruitment",
            "Jobsite.Modules.Screening",
            "Jobsite.Modules.HRWorkflows",
            "Jobsite.Modules.Matching",
            "Jobsite.Modules.Admin"
        ];

        // Act & Assert
        foreach (string module in otherModules)
        {
            TestResult result = Types.InAssembly(TenancyDomain)
                .ShouldNot()
                .HaveDependencyOn(module)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"Tenancy.Domain must not reference {module}");
        }
    }

    [Fact]
    public void TenancyInfrastructure_ShouldNotReference_OtherModules()
    {
        // Arrange
        string[] otherModules =
        [
            "Jobsite.Modules.Auth",
            "Jobsite.Modules.Profiles",
            "Jobsite.Modules.Recruitment",
            "Jobsite.Modules.Screening",
            "Jobsite.Modules.HRWorkflows",
            "Jobsite.Modules.Matching",
            "Jobsite.Modules.Admin"
        ];

        // Act & Assert
        foreach (string module in otherModules)
        {
            TestResult result = Types.InAssembly(TenancyInfrastructure)
                .ShouldNot()
                .HaveDependencyOn(module)
                .GetResult();

            result.IsSuccessful.Should().BeTrue(
                $"Tenancy.Infrastructure must not reference {module}");
        }
    }
}

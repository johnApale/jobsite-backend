using System.Reflection;
using FluentAssertions;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Jobsite.Modules.Tenancy.Infrastructure.Persistence;
using NetArchTest.Rules;

namespace Jobsite.ArchitectureTests;

/// <summary>
/// Enforces naming and structural conventions across the codebase.
/// </summary>
public sealed class NamingConventionTests
{
    private static readonly Assembly TenancyDomain = typeof(Tenant).Assembly;
    private static readonly Assembly TenancyInfrastructure = typeof(CatalogDbContext).Assembly;

    [Fact]
    public void ConcreteClasses_ShouldBeSealed()
    {
        // Arrange & Act
        TestResult result = Types.InAssembly(TenancyDomain)
            .That()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .Should()
            .BeSealed()
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "All concrete classes must be sealed unless inheritance is explicitly needed. " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void InfrastructureConcreteClasses_ShouldBeSealed()
    {
        // Arrange & Act — exclude EF Core generated migration classes
        TestResult result = Types.InAssembly(TenancyInfrastructure)
            .That()
            .AreClasses()
            .And()
            .AreNotAbstract()
            .And()
            .DoNotResideInNamespaceContaining("Migrations")
            .Should()
            .BeSealed()
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "All concrete classes must be sealed (excluding EF migrations). " +
            $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Interfaces_ShouldStartWithI()
    {
        // Arrange & Act
        TestResult result = Types.InAssembly(TenancyDomain)
            .That()
            .AreInterfaces()
            .Should()
            .HaveNameStartingWith("I")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }
}

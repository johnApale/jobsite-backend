using System.Reflection;
using FluentAssertions;
using Jobsite.Modules.Tenancy.Application.Services;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Jobsite.Modules.Tenancy.Infrastructure.Persistence;
using NetArchTest.Rules;

namespace Jobsite.ArchitectureTests;

/// <summary>
/// Enforces the module layer dependency rules:
///   SharedKernel ← no project references
///   Module.Domain ← SharedKernel only
///   Module.Application ← Module.Domain only
///   Module.Infrastructure ← Module.Application
///   Module.Api ← Module.Application + Module.Infrastructure
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly TenancyDomain = typeof(Tenant).Assembly;
    private static readonly Assembly TenancyApplication = typeof(ITenantService).Assembly;
    private static readonly Assembly TenancyInfrastructure = typeof(CatalogDbContext).Assembly;

    [Fact]
    public void DomainLayer_ShouldNotReference_ApplicationLayer()
    {
        // Arrange & Act
        TestResult result = Types.InAssembly(TenancyDomain)
            .ShouldNot()
            .HaveDependencyOn("Jobsite.Modules.Tenancy.Application")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer must not reference Application layer");
    }

    [Fact]
    public void DomainLayer_ShouldNotReference_InfrastructureLayer()
    {
        // Arrange & Act
        TestResult result = Types.InAssembly(TenancyDomain)
            .ShouldNot()
            .HaveDependencyOn("Jobsite.Modules.Tenancy.Infrastructure")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer must not reference Infrastructure layer");
    }

    [Fact]
    public void DomainLayer_ShouldNotReference_EFCore()
    {
        // Arrange & Act
        TestResult result = Types.InAssembly(TenancyDomain)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Domain layer must not reference EF Core");
    }

    [Fact]
    public void ApplicationLayer_ShouldNotReference_InfrastructureLayer()
    {
        // Arrange & Act
        TestResult result = Types.InAssembly(TenancyApplication)
            .ShouldNot()
            .HaveDependencyOn("Jobsite.Modules.Tenancy.Infrastructure")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Application layer must not reference Infrastructure layer");
    }

    [Fact]
    public void ApplicationLayer_ShouldNotReference_EFCore()
    {
        // Arrange & Act
        TestResult result = Types.InAssembly(TenancyApplication)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue(
            "Application layer must not reference EF Core");
    }
}

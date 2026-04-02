using FluentAssertions;
using Jobsite.Api.Middleware;
using Jobsite.Modules.Tenancy.Application.Interfaces;
using Jobsite.Modules.Tenancy.Domain.Constants;
using Jobsite.Modules.Tenancy.Domain.Entities;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace Jobsite.UnitTests.Middleware;

/// <summary>
/// Tests for TenantResolutionMiddleware — verifies subdomain extraction,
/// tenant lookup, status checks, and non-tenant route bypass.
/// </summary>
public sealed class TenantResolutionMiddlewareTests
{
    private readonly ITenantRepository _tenantRepository;
    private readonly ITenantCache _tenantCache;

    public TenantResolutionMiddlewareTests()
    {
        _tenantRepository = Substitute.For<ITenantRepository>();
        _tenantCache = Substitute.For<ITenantCache>();
    }

    [Fact]
    public async Task InvokeAsync_HealthRoute_BypassesTenantResolution()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Path = "/health";
        bool nextCalled = false;

        TenantResolutionMiddleware middleware = new(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, _tenantRepository, _tenantCache);

        // Assert
        nextCalled.Should().BeTrue();
        await _tenantRepository.DidNotReceive().GetBySubdomainAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_TenantsApiRoute_BypassesTenantResolution()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Path = "/api/v1/tenants/register";
        bool nextCalled = false;

        TenantResolutionMiddleware middleware = new(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, _tenantRepository, _tenantCache);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_LocalhostWithoutSubdomain_Returns400()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        context.Request.Host = new HostString("localhost", 5000);
        context.Request.Path = "/api/v1/jobs";

        TenantResolutionMiddleware middleware = new(ctx => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, _tenantRepository, _tenantCache);

        // Assert
        context.Response.StatusCode.Should().Be(400);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().Contain("INVALID_REQUEST");
    }

    [Fact]
    public async Task InvokeAsync_ValidSubdomainNotFound_Returns404()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        context.Request.Host = new HostString("unknown.djobsite.com");
        context.Request.Path = "/api/v1/jobs";

        _tenantRepository.GetBySubdomainAsync("unknown", Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        TenantResolutionMiddleware middleware = new(ctx => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, _tenantRepository, _tenantCache);

        // Assert
        context.Response.StatusCode.Should().Be(404);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().Contain("TENANT_NOT_FOUND");
    }

    [Fact]
    public async Task InvokeAsync_SuspendedTenant_Returns403()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        context.Request.Host = new HostString("suspended.djobsite.com");
        context.Request.Path = "/api/v1/jobs";

        Tenant suspendedTenant = TestData.CreateTenant(subdomain: "suspended", status: TenantStatus.Suspended);
        _tenantRepository.GetBySubdomainAsync("suspended", Arg.Any<CancellationToken>())
            .Returns(suspendedTenant);

        TenantResolutionMiddleware middleware = new(ctx => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, _tenantRepository, _tenantCache);

        // Assert
        context.Response.StatusCode.Should().Be(403);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().Contain("FORBIDDEN");
        body.Should().Contain("Suspended");
    }

    [Fact]
    public async Task InvokeAsync_ActiveTenant_StoresInContextAndCallsNext()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("acme.djobsite.com");
        context.Request.Path = "/api/v1/jobs";
        bool nextCalled = false;

        Tenant activeTenant = TestData.CreateTenant(subdomain: "acme", status: TenantStatus.Active);
        _tenantRepository.GetBySubdomainAsync("acme", Arg.Any<CancellationToken>())
            .Returns(activeTenant);

        TenantResolutionMiddleware middleware = new(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, _tenantRepository, _tenantCache);

        // Assert
        nextCalled.Should().BeTrue();
        context.Items["Tenant"].Should().BeSameAs(activeTenant);
        context.Items["TenantConnectionString"].Should().Be(activeTenant.ConnectionString);
    }

    [Fact]
    public async Task InvokeAsync_SubdomainWithPort_ExtractsCorrectly()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("acme.djobsite.com", 443);
        context.Request.Path = "/api/v1/jobs";

        Tenant activeTenant = TestData.CreateTenant(subdomain: "acme", status: TenantStatus.Active);
        _tenantRepository.GetBySubdomainAsync("acme", Arg.Any<CancellationToken>())
            .Returns(activeTenant);

        TenantResolutionMiddleware middleware = new(ctx => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, _tenantRepository, _tenantCache);

        // Assert
        context.Items["Tenant"].Should().BeSameAs(activeTenant);
    }

    [Fact]
    public async Task InvokeAsync_OpenApiRoute_BypassesTenantResolution()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Path = "/openapi/v1.json";
        bool nextCalled = false;

        TenantResolutionMiddleware middleware = new(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, _tenantRepository, _tenantCache);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_TenantInCache_ReturnsCachedWithoutHittingRepository()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("cached.djobsite.com");
        context.Request.Path = "/api/v1/jobs";

        Tenant cachedTenant = TestData.CreateTenant(subdomain: "cached", status: TenantStatus.Active);
        _tenantCache.GetBySubdomainAsync("cached", Arg.Any<CancellationToken>())
            .Returns(cachedTenant);

        TenantResolutionMiddleware middleware = new(ctx => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, _tenantRepository, _tenantCache);

        // Assert
        context.Items["Tenant"].Should().BeSameAs(cachedTenant);
        await _tenantRepository.DidNotReceive().GetBySubdomainAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_CacheMiss_QueriesRepositoryAndPopulatesCache()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("fresh.djobsite.com");
        context.Request.Path = "/api/v1/jobs";

        _tenantCache.GetBySubdomainAsync("fresh", Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        Tenant freshTenant = TestData.CreateTenant(subdomain: "fresh", status: TenantStatus.Active);
        _tenantRepository.GetBySubdomainAsync("fresh", Arg.Any<CancellationToken>())
            .Returns(freshTenant);

        TenantResolutionMiddleware middleware = new(ctx => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, _tenantRepository, _tenantCache);

        // Assert
        context.Items["Tenant"].Should().BeSameAs(freshTenant);
        await _tenantCache.Received(1).SetAsync("fresh", freshTenant, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_SuspendedTenantFromCache_Returns403()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        context.Request.Host = new HostString("suspended.djobsite.com");
        context.Request.Path = "/api/v1/jobs";

        Tenant suspendedTenant = TestData.CreateTenant(subdomain: "suspended", status: TenantStatus.Suspended);
        _tenantCache.GetBySubdomainAsync("suspended", Arg.Any<CancellationToken>())
            .Returns(suspendedTenant);

        TenantResolutionMiddleware middleware = new(ctx => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, _tenantRepository, _tenantCache);

        // Assert
        context.Response.StatusCode.Should().Be(403);
        await _tenantRepository.DidNotReceive().GetBySubdomainAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_ErrorResponse_IncludesCorrelationIdAsRequestId()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        context.Request.Host = new HostString("unknown.djobsite.com");
        context.Request.Path = "/api/v1/jobs";
        string correlationId = Guid.NewGuid().ToString();
        context.Items["CorrelationId"] = correlationId;

        _tenantCache.GetBySubdomainAsync("unknown", Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);
        _tenantRepository.GetBySubdomainAsync("unknown", Arg.Any<CancellationToken>())
            .Returns((Tenant?)null);

        TenantResolutionMiddleware middleware = new(ctx => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context, _tenantRepository, _tenantCache);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        body.Should().Contain(correlationId);
    }
}

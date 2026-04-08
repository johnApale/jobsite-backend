using FluentAssertions;
using Jobsite.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace Jobsite.UnitTests.Middleware;

/// <summary>
/// Tests for SecurityHeadersMiddleware — verifies all security headers
/// are present on every response.
/// </summary>
public sealed class SecurityHeadersMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AnyRequest_SetsXContentTypeOptions()
    {
        // Arrange
        DefaultHttpContext context = new();
        SecurityHeadersMiddleware middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Content-Type-Options"].ToString().Should().Be("nosniff");
    }

    [Fact]
    public async Task InvokeAsync_AnyRequest_SetsXFrameOptions()
    {
        // Arrange
        DefaultHttpContext context = new();
        SecurityHeadersMiddleware middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-Frame-Options"].ToString().Should().Be("DENY");
    }

    [Fact]
    public async Task InvokeAsync_AnyRequest_SetsReferrerPolicy()
    {
        // Arrange
        DefaultHttpContext context = new();
        SecurityHeadersMiddleware middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["Referrer-Policy"].ToString().Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task InvokeAsync_AnyRequest_SetsPermissionsPolicy()
    {
        // Arrange
        DefaultHttpContext context = new();
        SecurityHeadersMiddleware middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["Permissions-Policy"].ToString()
            .Should().Be("camera=(), microphone=(), geolocation=()");
    }

    [Fact]
    public async Task InvokeAsync_AnyRequest_SetsXXssProtectionToZero()
    {
        // Arrange
        DefaultHttpContext context = new();
        SecurityHeadersMiddleware middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["X-XSS-Protection"].ToString().Should().Be("0");
    }

    [Fact]
    public async Task InvokeAsync_NonLocalhostRequest_SetsStrictTransportSecurity()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("acme.djobsite.com");
        SecurityHeadersMiddleware middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers["Strict-Transport-Security"].ToString()
            .Should().Be("max-age=31536000; includeSubDomains");
    }

    [Fact]
    public async Task InvokeAsync_LocalhostRequest_DoesNotSetHsts()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Request.Host = new HostString("localhost", 5000);
        SecurityHeadersMiddleware middleware = CreateMiddleware();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Headers.ContainsKey("Strict-Transport-Security").Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_AnyRequest_CallsNextMiddleware()
    {
        // Arrange
        DefaultHttpContext context = new();
        bool nextCalled = false;

        SecurityHeadersMiddleware middleware = new(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    private static SecurityHeadersMiddleware CreateMiddleware()
    {
        return new SecurityHeadersMiddleware(ctx => Task.CompletedTask);
    }
}

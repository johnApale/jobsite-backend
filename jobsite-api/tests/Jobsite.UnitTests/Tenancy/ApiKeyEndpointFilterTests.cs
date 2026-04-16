using FluentAssertions;
using Jobsite.Modules.Tenancy.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Jobsite.UnitTests.Tenancy;

/// <summary>
/// Tests for <see cref="ApiKeyEndpointFilter"/> — validates X-Api-Key header checking,
/// missing key rejection, invalid key rejection, and unconfigured key handling.
/// </summary>
public sealed class ApiKeyEndpointFilterTests
{
    private const string ValidApiKey = "test-platform-api-key-secret";

    [Fact]
    public async Task InvokeAsync_ValidApiKey_CallsNext()
    {
        // Arrange
        ApiKeyEndpointFilter filter = CreateFilter(ValidApiKey);
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers["X-Api-Key"] = ValidApiKey;
        bool nextCalled = false;

        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context = CreateFilterContext(httpContext);

        // Act
        await filter.InvokeAsync(context, next);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_MissingApiKey_Returns401()
    {
        // Arrange
        ApiKeyEndpointFilter filter = CreateFilter(ValidApiKey);
        DefaultHttpContext httpContext = new();
        bool nextCalled = false;

        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context = CreateFilterContext(httpContext);

        // Act
        object? result = await filter.InvokeAsync(context, next);

        // Assert
        nextCalled.Should().BeFalse();
        IStatusCodeHttpResult statusResult = result.Should().BeAssignableTo<IStatusCodeHttpResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_InvalidApiKey_Returns403()
    {
        // Arrange
        ApiKeyEndpointFilter filter = CreateFilter(ValidApiKey);
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers["X-Api-Key"] = "wrong-key";
        bool nextCalled = false;

        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context = CreateFilterContext(httpContext);

        // Act
        object? result = await filter.InvokeAsync(context, next);

        // Assert
        nextCalled.Should().BeFalse();
        IStatusCodeHttpResult statusResult = result.Should().BeAssignableTo<IStatusCodeHttpResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_EmptyApiKeyHeader_Returns401()
    {
        // Arrange
        ApiKeyEndpointFilter filter = CreateFilter(ValidApiKey);
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers["X-Api-Key"] = "";
        bool nextCalled = false;

        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context = CreateFilterContext(httpContext);

        // Act
        object? result = await filter.InvokeAsync(context, next);

        // Assert
        nextCalled.Should().BeFalse();
        IStatusCodeHttpResult statusResult = result.Should().BeAssignableTo<IStatusCodeHttpResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Fact]
    public async Task InvokeAsync_UnconfiguredApiKey_Returns500()
    {
        // Arrange — empty config means API key was never set
        ApiKeyEndpointFilter filter = CreateFilter("");
        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers["X-Api-Key"] = "any-key";
        bool nextCalled = false;

        EndpointFilterDelegate next = _ =>
        {
            nextCalled = true;
            return ValueTask.FromResult<object?>(Results.Ok());
        };

        DefaultEndpointFilterInvocationContext context = CreateFilterContext(httpContext);

        // Act
        object? result = await filter.InvokeAsync(context, next);

        // Assert
        nextCalled.Should().BeFalse();
        IStatusCodeHttpResult statusResult = result.Should().BeAssignableTo<IStatusCodeHttpResult>().Subject;
        statusResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static ApiKeyEndpointFilter CreateFilter(string apiKey)
    {
        Dictionary<string, string?> configData = new()
        {
            ["App:PlatformApiKey"] = apiKey
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        return new ApiKeyEndpointFilter(configuration);
    }

    private static DefaultEndpointFilterInvocationContext CreateFilterContext(HttpContext httpContext)
    {
        return new DefaultEndpointFilterInvocationContext(httpContext);
    }
}

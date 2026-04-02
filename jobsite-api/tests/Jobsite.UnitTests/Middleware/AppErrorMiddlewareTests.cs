using System.Text.Json;
using FluentAssertions;
using Jobsite.Api.Middleware;
using Jobsite.SharedKernel.Errors;
using Microsoft.AspNetCore.Http;

namespace Jobsite.UnitTests.Middleware;

/// <summary>
/// Tests for AppErrorMiddleware — verifies error envelope serialization
/// for both AppError exceptions and unhandled exceptions.
/// </summary>
public sealed class AppErrorMiddlewareTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [Fact]
    public async Task InvokeAsync_AppErrorThrown_ReturnsCorrectStatusAndEnvelope()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        context.Items["CorrelationId"] = "test-correlation-id";

        AppErrorMiddleware middleware = new(ctx => throw AppErrors.TenantNotFound);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(404);
        context.Response.ContentType.Should().Be("application/json");

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("code").GetString().Should().Be("TENANT_NOT_FOUND");
        doc.RootElement.GetProperty("message").GetString().Should().Be("Tenant not found");
        doc.RootElement.GetProperty("request_id").GetString().Should().Be("test-correlation-id");
    }

    [Fact]
    public async Task InvokeAsync_AppErrorWithDetails_IncludesDetailsInEnvelope()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        context.Items["CorrelationId"] = "corr-123";

        Dictionary<string, string> details = new()
        {
            { "email", "Required" },
            { "name", "Too short" }
        };
        AppError error = AppErrors.Validation.WithDetails(details);
        AppErrorMiddleware middleware = new(ctx => throw error);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(400);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("code").GetString().Should().Be("VALIDATION_ERROR");
        doc.RootElement.GetProperty("details").GetProperty("email").GetString().Should().Be("Required");
        doc.RootElement.GetProperty("details").GetProperty("name").GetString().Should().Be("Too short");
    }

    [Fact]
    public async Task InvokeAsync_UnhandledException_Returns500WithSafeMessage()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        context.Items["CorrelationId"] = "corr-456";

        AppErrorMiddleware middleware = new(ctx => throw new InvalidOperationException("sensitive details"));

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be(500);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("code").GetString().Should().Be("INTERNAL_ERROR");
        doc.RootElement.GetProperty("message").GetString().Should().Be("An unexpected error occurred");
        body.Should().NotContain("sensitive details", "internal details must not leak");
    }

    [Fact]
    public async Task InvokeAsync_NoException_PassesThrough()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        bool nextCalled = false;

        AppErrorMiddleware middleware = new(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_NoCorrelationId_FallsBackToTraceIdentifier()
    {
        // Arrange
        DefaultHttpContext context = new();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "trace-abc";
        // Deliberately not setting context.Items["CorrelationId"]

        AppErrorMiddleware middleware = new(ctx => throw AppErrors.Unauthorized);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        string body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        JsonDocument doc = JsonDocument.Parse(body);

        doc.RootElement.GetProperty("request_id").GetString().Should().Be("trace-abc");
    }
}

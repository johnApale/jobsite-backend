using System.Text.Json;
using Jobsite.SharedKernel.Errors;

namespace Jobsite.Api.Middleware;

/// <summary>
/// Catches <see cref="AppError"/> exceptions and serializes them into the canonical error envelope.
/// Catches unhandled exceptions and returns a safe 500 response.
/// </summary>
public sealed class AppErrorMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RequestDelegate _next;

    public AppErrorMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (AppError ex)
        {
            string requestId = context.Items["CorrelationId"]?.ToString()
                ?? context.TraceIdentifier;

            ErrorEnvelope body = new()
            {
                Code = ex.Code,
                Message = ex.Message,
                Details = ex.Details,
                RequestId = requestId
            };

            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
        }
        catch (Exception)
        {
            string requestId = context.Items["CorrelationId"]?.ToString()
                ?? context.TraceIdentifier;

            ErrorEnvelope body = new()
            {
                Code = "INTERNAL_ERROR",
                Message = "An unexpected error occurred",
                RequestId = requestId
            };

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOptions));
        }
    }

    /// <summary>Canonical error envelope shape.</summary>
    private sealed class ErrorEnvelope
    {
        public required string Code { get; init; }
        public required string Message { get; init; }
        public Dictionary<string, string>? Details { get; init; }
        public required string RequestId { get; init; }
    }
}

using Serilog;

namespace Jobsite.Api.Middleware;

/// <summary>
/// Logs inbound request method, path, and correlation ID.
/// Logs response status code and elapsed time.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;

    public RequestLoggingMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = context.Items["CorrelationId"]?.ToString() ?? "-";
        long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

        Log.Information(
            "HTTP {Method} {Path} started [CorrelationId={CorrelationId}]",
            context.Request.Method,
            context.Request.Path,
            correlationId);

        await _next(context);

        TimeSpan elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp);

        Log.Information(
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0.00}ms [CorrelationId={CorrelationId}]",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            elapsed.TotalMilliseconds,
            correlationId);
    }
}

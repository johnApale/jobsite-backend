namespace Jobsite.Api.Middleware;

/// <summary>
/// Adds security-related response headers to every HTTP response.
/// Mitigates clickjacking, MIME-sniffing, and other common web attacks.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        context.Response.Headers["X-XSS-Protection"] = "0";

        // HSTS only for non-development (localhost has no TLS)
        if (!context.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        await _next(context);
    }
}

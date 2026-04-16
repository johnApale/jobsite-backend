using Jobsite.Modules.Tenancy.Application.Interfaces;
using Jobsite.Modules.Tenancy.Domain.Constants;
using Jobsite.Modules.Tenancy.Domain.Entities;

namespace Jobsite.Api.Middleware;

/// <summary>
/// Resolves the tenant from the request's <c>Host</c> header subdomain.
/// Stores the resolved <see cref="Tenant"/> in <c>HttpContext.Items["Tenant"]</c>
/// and the tenant's connection string in <c>HttpContext.Items["TenantConnectionString"]</c>.
/// Runs before authentication — the tenant DB context is needed for user lookup.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantRepository tenantRepository, ITenantCache tenantCache)
    {
        // Skip tenant resolution for non-tenant routes
        string path = context.Request.Path.Value ?? string.Empty;
        if (IsNonTenantRoute(path))
        {
            await _next(context);
            return;
        }

        string requestId = context.Items["CorrelationId"]?.ToString() ?? context.TraceIdentifier;

        string? subdomain = ExtractSubdomain(context.Request.Host.Host);
        if (subdomain is null)
        {
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                $$"""{"code":"INVALID_REQUEST","message":"Unable to resolve tenant from hostname","request_id":"{{requestId}}"}""");
            return;
        }

        // Cache-first lookup: check cache before hitting the database
        Tenant? tenant = await tenantCache.GetBySubdomainAsync(subdomain, context.RequestAborted);
        if (tenant is null)
        {
            tenant = await tenantRepository.GetBySubdomainAsync(subdomain, context.RequestAborted);
            if (tenant is not null)
            {
                await tenantCache.SetAsync(subdomain, tenant, context.RequestAborted);
            }
        }

        if (tenant is null)
        {
            context.Response.StatusCode = 404;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                $$"""{"code":"TENANT_NOT_FOUND","message":"Tenant not found","request_id":"{{requestId}}"}""");
            return;
        }

        if (tenant.Status != TenantStatus.Active)
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                $$"""{"code":"FORBIDDEN","message":"Tenant is {{tenant.Status}}","request_id":"{{requestId}}"}""");
            return;
        }

        context.Items["Tenant"] = tenant;
        context.Items["TenantId"] = tenant.Id;
        context.Items["TenantConnectionString"] = tenant.ConnectionString;

        await _next(context);
    }

    private static bool IsNonTenantRoute(string path)
    {
        return path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/ready", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/tenants", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/platform", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/docs", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the subdomain from a hostname.
    /// <c>acme.jobsite.com</c> → <c>acme</c>.
    /// <c>localhost</c> / bare IPs → null (no tenant context).
    /// </summary>
    private static string? ExtractSubdomain(string host)
    {
        // Strip port if present
        int portIndex = host.IndexOf(':');
        if (portIndex >= 0)
            host = host[..portIndex];

        // localhost / IP — no subdomain in development without a custom host
        if (host == "localhost" || System.Net.IPAddress.TryParse(host, out _))
            return null;

        string[] parts = host.Split('.');
        // Need at least subdomain.domain.tld
        if (parts.Length < 3)
            return null;

        return parts[0].ToLowerInvariant();
    }
}

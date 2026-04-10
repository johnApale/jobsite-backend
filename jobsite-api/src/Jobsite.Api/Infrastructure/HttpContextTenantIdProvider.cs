using Jobsite.SharedKernel.Persistence;
using Microsoft.AspNetCore.Http;

namespace Jobsite.Api.Infrastructure;

/// <summary>
/// Resolves the current tenant ID from <c>HttpContext.Items["TenantId"]</c>,
/// set by <c>TenantResolutionMiddleware</c>.
/// </summary>
public sealed class HttpContextTenantIdProvider : ITenantIdProvider
{
    public Guid TenantId { get; }

    public HttpContextTenantIdProvider(IHttpContextAccessor httpContextAccessor)
    {
        object? tenantId = httpContextAccessor.HttpContext?.Items["TenantId"];
        TenantId = tenantId is Guid id
            ? id
            : throw new InvalidOperationException(
                "TenantId not found in request context. Ensure TenantResolutionMiddleware is configured.");
    }
}

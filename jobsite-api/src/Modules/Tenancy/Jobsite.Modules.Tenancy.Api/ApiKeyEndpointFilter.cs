using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Jobsite.Modules.Tenancy.Api;

/// <summary>
/// Endpoint filter that validates the <c>X-Api-Key</c> header against the configured platform API key.
/// Used for provisioning endpoints that must be callable before any tenant or user exists.
/// </summary>
public sealed class ApiKeyEndpointFilter : IEndpointFilter
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private const string ConfigKey = "App:PlatformApiKey";

    private readonly string _expectedApiKey;

    public ApiKeyEndpointFilter(IConfiguration configuration)
    {
        _expectedApiKey = configuration[ConfigKey] ?? string.Empty;
    }

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (string.IsNullOrWhiteSpace(_expectedApiKey))
        {
            return Results.Json(
                new { code = "SERVER_ERROR", message = "Platform API key is not configured." },
                statusCode: StatusCodes.Status500InternalServerError);
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(ApiKeyHeaderName, out StringValues apiKeyValues)
            || string.IsNullOrWhiteSpace(apiKeyValues.ToString()))
        {
            return Results.Json(
                new { code = "UNAUTHORIZED", message = "Missing X-Api-Key header." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        string providedKey = apiKeyValues.ToString();

        if (!CryptographicEquals(_expectedApiKey, providedKey))
        {
            return Results.Json(
                new { code = "FORBIDDEN", message = "Invalid API key." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        return await next(context);
    }

    /// <summary>
    /// Constant-time comparison to prevent timing attacks on the API key.
    /// </summary>
    private static bool CryptographicEquals(string expected, string actual)
    {
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expected);
        byte[] actualBytes = Encoding.UTF8.GetBytes(actual);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}

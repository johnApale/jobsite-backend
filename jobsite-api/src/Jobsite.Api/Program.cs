using Jobsite.Api.Endpoints;
using Jobsite.Api.Extensions;
using Jobsite.Api.Middleware;
using Jobsite.Api.OpenApi;
using Jobsite.Modules.Admin.Api;
using Jobsite.Modules.Auth.Api;
using Jobsite.Modules.Profiles.Api;
using Jobsite.Modules.Recruitment.Api;
using Jobsite.Modules.Screening.Api;
using Jobsite.Modules.Matching.Api;
using Jobsite.Modules.HRWorkflows.Api;
using Jobsite.Modules.Tenancy.Api;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;
using System.Text.Json;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, loggerConfig) =>
        loggerConfig
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Application", "djobsite-api")
            .WriteTo.Console());

    // Kestrel request size limit (10 MB default)
    builder.WebHost.ConfigureKestrel(options =>
        options.Limits.MaxRequestBodySize = 10 * 1024 * 1024);

    builder.Services.AddJobsiteModules(builder.Configuration);
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
        options.AddOperationTransformer<ErrorSchemaTransformer>();
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new Microsoft.OpenApi.OpenApiInfo
            {
                Title = "D'Jobsite iConnect API",
                Version = "v1",
                Description = """
                    REST API for the D'Jobsite iConnect recruitment platform.

                    **Architecture:** Modular monolith (C#/.NET 10) with a standalone AI Interview microservice (Python/FastAPI).

                    **Authentication:** JWT Bearer (HS256) with tenant-scoped claims.

                    **Multi-tenancy:** Tenant resolved from subdomain in the Host header.
                    """,
                Contact = new Microsoft.OpenApi.OpenApiContact
                {
                    Name = "D'Jobsite iConnect",
                    Email = "dev@djobsite.com"
                }
            };

            return Task.CompletedTask;
        });
    });

    WebApplication app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("D'Jobsite iConnect API");
            options.WithTheme(ScalarTheme.DeepSpace);
            options.WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
        });
    }

    // Middleware pipeline (order matters)
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<AppErrorMiddleware>();
    app.UseCors("TenantPolicy");
    app.UseMiddleware<TenantResolutionMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();
    app.UseSerilogRequestLogging();

    // Health checks
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = _ => false, // Liveness: always healthy if app is running
        ResponseWriter = WriteHealthResponse
    }).ExcludeFromDescription();

    app.MapHealthChecks("/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("readiness"),
        ResponseWriter = WriteHealthResponse
    }).ExcludeFromDescription();

    // Module endpoints
    app.MapTenancyEndpoints();
    app.MapPlatformAdminEndpoints();
    app.MapAuthEndpoints();
    app.MapAdminEndpoints();
    app.MapProfilesEndpoints();
    app.MapRecruitmentEndpoints();
    app.MapScreeningEndpoints();
    app.MapMatchingEndpoints();
    app.MapHRWorkflowsEndpoints();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>
/// Partial class declaration for <c>WebApplicationFactory</c> test support.
/// </summary>
public partial class Program
{
    /// <summary>
    /// Writes a structured JSON health check response with per-component status.
    /// </summary>
    private static async Task WriteHealthResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        Dictionary<string, string> checks = new();
        foreach (KeyValuePair<string, HealthReportEntry> entry in report.Entries)
        {
            checks[entry.Key] = entry.Value.Status.ToString();
        }

        object response = new
        {
            status = report.Status.ToString(),
            checks,
            duration = report.TotalDuration.TotalMilliseconds
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
                WriteIndented = false
            }));
    }
}

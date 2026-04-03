using Jobsite.Api.Endpoints;
using Jobsite.Api.Extensions;
using Jobsite.Api.Middleware;
using Jobsite.Api.OpenApi;
using Jobsite.Modules.Admin.Api;
using Jobsite.Modules.Auth.Api;
using Jobsite.Modules.Tenancy.Api;
using Scalar.AspNetCore;
using Serilog;

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
            .WriteTo.Console());

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
    app.UseMiddleware<RequestLoggingMiddleware>();
    app.UseMiddleware<AppErrorMiddleware>();
    app.UseMiddleware<TenantResolutionMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseSerilogRequestLogging();

    // Health
    app.MapHealthEndpoints();

    // Module endpoints
    app.MapTenancyEndpoints();
    app.MapAuthEndpoints();
    app.MapAdminEndpoints();

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
public partial class Program;

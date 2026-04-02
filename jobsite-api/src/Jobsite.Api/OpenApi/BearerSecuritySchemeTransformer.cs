using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Jobsite.Api.OpenApi;

/// <summary>
/// Adds the JWT Bearer security scheme to the OpenAPI document and applies it globally to all operations.
/// </summary>
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken ct)
    {
        OpenApiSecurityScheme bearerScheme = new()
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT access token issued by the Auth module. Format: `Bearer <token>`"
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = bearerScheme;

        OpenApiSecuritySchemeReference schemeReference = new("Bearer", document);

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [schemeReference] = []
        });

        return Task.CompletedTask;
    }
}

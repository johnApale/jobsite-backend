using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Jobsite.Api.OpenApi;

/// <summary>
/// Adds the canonical <c>ErrorEnvelope</c> schema to all error responses (400, 401, 403, 404, 409, 422, 429, 500, 503)
/// on every operation in the OpenAPI document.
/// </summary>
public sealed class ErrorSchemaTransformer : IOpenApiOperationTransformer
{
    private static readonly string[] ErrorStatusCodes =
        ["400", "401", "403", "404", "409", "422", "429", "500", "503"];

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken ct)
    {
        if (operation.Responses is null)
            return Task.CompletedTask;

        foreach (string statusCode in ErrorStatusCodes)
        {
            if (!operation.Responses.ContainsKey(statusCode))
                continue;

            IOpenApiResponse response = operation.Responses[statusCode];
            if (response is OpenApiResponse concreteResponse)
            {
                concreteResponse.Content ??= new Dictionary<string, OpenApiMediaType>();
                concreteResponse.Content["application/json"] = new OpenApiMediaType
                {
                    Schema = CreateErrorEnvelopeSchema()
                };
            }
        }

        return Task.CompletedTask;
    }

    private static OpenApiSchema CreateErrorEnvelopeSchema()
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["code"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Description = "Machine-readable error code in SCREAMING_SNAKE_CASE",
                    Example = "TENANT_NOT_FOUND"
                },
                ["message"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Description = "Human-readable error description",
                    Example = "Tenant not found"
                },
                ["details"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Description = "Per-field validation errors. Omitted when not applicable.",
                    AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.String }
                },
                ["request_id"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Description = "Correlation ID for tracing",
                    Example = "550e8400-e29b-41d4-a716-446655440000"
                }
            },
            Required = new HashSet<string> { "code", "message", "request_id" }
        };
    }
}

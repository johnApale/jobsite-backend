using System.Security.Claims;
using Jobsite.Modules.Admin.Application.DTOs;
using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Jobsite.Modules.Admin.Api;

/// <summary>
/// Minimal API endpoint definitions for the Admin module.
/// Route prefix: <c>/api/v1/admin</c>.
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/admin")
            .WithTags("Admin")
            .RequireAuthorization("RequireAgencyAdmin")
            .RequireRateLimiting("global");

        group.MapGet("/settings", async (IAdminSettingsService service, CancellationToken ct) =>
            {
                CompanySettingsResponse response = await service.GetSettingsAsync(ct);
                return Results.Ok(response);
            })
            .WithName("GetCompanySettings")
            .WithSummary("Get tenant company settings")
            .WithDescription("Returns the full company settings for the current tenant, including auth, profile, screening, matching, assessment, and notification configurations.")
            .Produces<CompanySettingsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPatch("/settings", async (
                UpdateCompanySettingsRequest request,
                IAdminSettingsService settingsService,
                IAuditLogService auditLogService,
                HttpContext http,
                CancellationToken ct) =>
            {
                CompanySettingsResponse response = await settingsService.UpdateSettingsAsync(request, ct);

                // Audit the settings change
                (Guid actorId, string actorEmail, string actorRole) = GetActorInfo(http);
                await auditLogService.LogAsync(
                    actorId: actorId,
                    actorEmail: actorEmail,
                    actorRole: actorRole,
                    action: AuditAction.SettingsUpdated,
                    entityType: AuditEntityType.CompanySettings,
                    entityId: response.Id,
                    details: new { updated_fields = GetUpdatedFields(request) },
                    ipAddress: http.Connection.RemoteIpAddress?.ToString(),
                    userAgent: http.Request.Headers.UserAgent.ToString(),
                    ct);

                return Results.Ok(response);
            })
            .WithName("UpdateCompanySettings")
            .WithSummary("Update tenant company settings")
            .WithDescription("Partially updates company settings using JSON merge patch semantics. Only non-null fields in the request body are applied.")
            .Produces<CompanySettingsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/audit-logs", async (
                string? action,
                Guid? actorId,
                string? entityType,
                DateTime? dateFrom,
                DateTime? dateTo,
                string? cursor,
                int? pageSize,
                IAuditLogService service,
                CancellationToken ct) =>
            {
                AuditLogQueryParameters parameters = new()
                {
                    Action = action,
                    ActorId = actorId,
                    EntityType = entityType,
                    DateFrom = dateFrom,
                    DateTo = dateTo,
                    Cursor = cursor,
                    PageSize = pageSize ?? 20
                };

                AuditLogPageResponse response = await service.QueryAsync(parameters, ct);
                return Results.Ok(response);
            })
            .WithName("GetAuditLogs")
            .WithSummary("Query audit logs")
            .WithDescription("Returns paginated audit log entries with optional filters by action, actor, entity type, and date range. Uses cursor-based pagination.")
            .Produces<AuditLogPageResponse>(StatusCodes.Status200OK);
    }

    private static (Guid UserId, string Email, string Role) GetActorInfo(HttpContext http)
    {
        string? sub = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? http.User.FindFirstValue("sub");

        Guid userId = sub is not null && Guid.TryParse(sub, out Guid id) ? id : Guid.Empty;
        string email = http.User.FindFirstValue(ClaimTypes.Email)
            ?? http.User.FindFirstValue("email")
            ?? "unknown";
        string role = http.User.FindFirstValue("role")
            ?? http.User.FindFirstValue(ClaimTypes.Role)
            ?? "unknown";

        return (userId, email, role);
    }

    private static List<string> GetUpdatedFields(UpdateCompanySettingsRequest request)
    {
        List<string> fields = [];
        if (request.DefaultTimezone is not null) fields.Add("default_timezone");
        if (request.DefaultCurrency is not null) fields.Add("default_currency");
        if (request.AuthSettings is not null) fields.Add("auth_settings");
        if (request.ProfileSettings is not null) fields.Add("profile_settings");
        if (request.ScreeningSettings is not null) fields.Add("screening_settings");
        if (request.MatchingSettings is not null) fields.Add("matching_settings");
        if (request.AssessmentSettings is not null) fields.Add("assessment_settings");
        if (request.NotificationSettings is not null) fields.Add("notification_settings");
        return fields;
    }
}

using System.Text.Json;
using Jobsite.Modules.Admin.Application.DTOs.Settings;
using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.Modules.Admin.Domain.Entities;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Jobsite.Modules.Admin.Application.EventHandlers;

/// <summary>
/// Seeds default <see cref="CompanySettings"/> when a new tenant is provisioned.
/// Uses the tenant's connection string from the event to create a scoped DbContext.
/// </summary>
public sealed class TenantProvisionedHandler : IDomainEventHandler<TenantProvisionedEvent>
{
    private readonly ICompanySettingsRepository _settingsRepository;
    private readonly IAuditLogService _auditLogService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TenantProvisionedHandler> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public TenantProvisionedHandler(
        ICompanySettingsRepository settingsRepository,
        IAuditLogService auditLogService,
        [FromKeyedServices("admin")] IUnitOfWork unitOfWork,
        ILogger<TenantProvisionedHandler> logger)
    {
        _settingsRepository = settingsRepository;
        _auditLogService = auditLogService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task HandleAsync(TenantProvisionedEvent domainEvent, CancellationToken ct)
    {
        _logger.LogInformation("Seeding default company settings for tenant {TenantId}", domainEvent.TenantId);

        CompanySettings settings = new()
        {
            DefaultTimezone = "UTC",
            DefaultCurrency = "USD",
            AuthSettings = JsonSerializer.Serialize(new AuthSettingsDto(), JsonOptions),
            ProfileSettings = JsonSerializer.Serialize(new ProfileSettingsDto(), JsonOptions),
            ScreeningSettings = JsonSerializer.Serialize(new ScreeningSettingsDto
            {
                DefaultEvaluationCriteria =
                [
                    new() { Name = "Skills Match", Category = "Skill", EvaluationMethod = "SemanticSimilarity", IsRequired = true, Weight = 40 },
                    new() { Name = "Experience Level", Category = "Experience", EvaluationMethod = "RangeMatch", IsRequired = true, Weight = 30 },
                    new() { Name = "Education", Category = "Education", EvaluationMethod = "ExactMatch", IsRequired = false, Weight = 15 },
                    new() { Name = "Resume Quality", Category = "Custom", EvaluationMethod = "SemanticSimilarity", IsRequired = false, Weight = 15 }
                ]
            }, JsonOptions),
            MatchingSettings = JsonSerializer.Serialize(new MatchingSettingsDto(), JsonOptions),
            AssessmentSettings = JsonSerializer.Serialize(new AssessmentSettingsDto(), JsonOptions),
            NotificationSettings = JsonSerializer.Serialize(new NotificationSettingsDto(), JsonOptions)
        };

        _settingsRepository.Add(settings);
        await _unitOfWork.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            actorId: Guid.Empty,
            actorEmail: "system",
            actorRole: "System",
            action: AuditAction.TenantProvisioned,
            entityType: AuditEntityType.Tenant,
            entityId: domainEvent.TenantId,
            details: new { tenant_name = domainEvent.TenantName },
            ipAddress: null,
            userAgent: null,
            ct);

        _logger.LogInformation("Default company settings seeded for tenant {TenantId}", domainEvent.TenantId);
    }
}

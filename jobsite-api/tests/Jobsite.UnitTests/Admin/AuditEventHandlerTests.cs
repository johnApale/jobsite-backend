using FluentAssertions;
using Jobsite.Modules.Admin.Application.EventHandlers;
using Jobsite.Modules.Admin.Application.Interfaces;
using Jobsite.Modules.Admin.Domain.Constants;
using Jobsite.SharedKernel.Events;
using NSubstitute;

namespace Jobsite.UnitTests.Admin;

public sealed class AuditEventHandlerTests
{
    private readonly IAuditLogService _auditLogService = Substitute.For<IAuditLogService>();

    [Fact]
    public async Task Handle_UserRegisteredEvent_CreatesAuditLog()
    {
        // Arrange
        UserRegisteredAuditHandler handler = new(_auditLogService);
        Guid userId = Guid.NewGuid();
        UserRegisteredEvent @event = new()
        {
            UserId = userId,
            Email = "user@example.com",
            Role = "Applicant",
            RegisteredAt = DateTime.UtcNow
        };

        // Act
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        await _auditLogService.Received(1).LogAsync(
            userId, "user@example.com", "Applicant",
            AuditAction.UserRegistered, AuditEntityType.User,
            userId, Arg.Any<object?>(), null, null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ApplicationSubmittedEvent_CreatesAuditLog()
    {
        // Arrange
        ApplicationSubmittedAuditHandler handler = new(_auditLogService);
        Guid applicationId = Guid.NewGuid();
        Guid applicantId = Guid.NewGuid();
        ApplicationSubmittedEvent @event = new()
        {
            ApplicationId = applicationId,
            JobPostingId = Guid.NewGuid(),
            ApplicantUserId = applicantId,
            SubmittedAt = DateTime.UtcNow
        };

        // Act
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        await _auditLogService.Received(1).LogAsync(
            applicantId, "system", "Applicant",
            AuditAction.ApplicationSubmitted, AuditEntityType.Application,
            applicationId, Arg.Any<object?>(), null, null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CvScreeningCompletedEvent_CreatesAuditLog()
    {
        // Arrange
        CvScreeningCompletedAuditHandler handler = new(_auditLogService);
        Guid screeningResultId = Guid.NewGuid();
        CvScreeningCompletedEvent @event = new()
        {
            ApplicationId = Guid.NewGuid(),
            ScreeningResultId = screeningResultId,
            PassedScreening = true,
            CompletedAt = DateTime.UtcNow
        };

        // Act
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        await _auditLogService.Received(1).LogAsync(
            Guid.Empty, "system", "System",
            AuditAction.CvScreeningCompleted, AuditEntityType.ScreeningResult,
            screeningResultId, Arg.Any<object?>(), null, null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CandidateShortlistedEvent_CreatesAuditLog()
    {
        // Arrange
        CandidateShortlistedAuditHandler handler = new(_auditLogService);
        Guid applicationId = Guid.NewGuid();
        CandidateShortlistedEvent @event = new()
        {
            ApplicationId = applicationId,
            JobPostingId = Guid.NewGuid(),
            ApplicantUserId = Guid.NewGuid(),
            ShortlistedAt = DateTime.UtcNow
        };

        // Act
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        await _auditLogService.Received(1).LogAsync(
            Guid.Empty, "system", "System",
            AuditAction.CandidateShortlisted, AuditEntityType.Application,
            applicationId, Arg.Any<object?>(), null, null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FinalInterviewScheduledEvent_CreatesAuditLog()
    {
        // Arrange
        FinalInterviewScheduledAuditHandler handler = new(_auditLogService);
        Guid interviewId = Guid.NewGuid();
        FinalInterviewScheduledEvent @event = new()
        {
            ApplicationId = Guid.NewGuid(),
            InterviewId = interviewId,
            ScheduledAt = DateTime.UtcNow
        };

        // Act
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        await _auditLogService.Received(1).LogAsync(
            Guid.Empty, "system", "System",
            AuditAction.FinalInterviewScheduled, AuditEntityType.FinalInterview,
            interviewId, Arg.Any<object?>(), null, null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OfferExtendedEvent_CreatesAuditLog()
    {
        // Arrange
        OfferExtendedAuditHandler handler = new(_auditLogService);
        Guid offerId = Guid.NewGuid();
        OfferExtendedEvent @event = new()
        {
            ApplicationId = Guid.NewGuid(),
            OfferId = offerId,
            OfferedAt = DateTime.UtcNow
        };

        // Act
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        await _auditLogService.Received(1).LogAsync(
            Guid.Empty, "system", "System",
            AuditAction.OfferExtended, AuditEntityType.JobOffer,
            offerId, Arg.Any<object?>(), null, null,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AssessmentCompletedEvent_CreatesAuditLog()
    {
        // Arrange
        AssessmentCompletedAuditHandler handler = new(_auditLogService);
        Guid applicationId = Guid.NewGuid();
        Guid applicantId = Guid.NewGuid();
        AssessmentCompletedEvent @event = new()
        {
            ApplicationId = applicationId,
            JobPostingId = Guid.NewGuid(),
            ApplicantUserId = applicantId,
            AssessmentScore = 85.5m,
            CompletedAt = DateTime.UtcNow
        };

        // Act
        await handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        await _auditLogService.Received(1).LogAsync(
            applicantId, "system", "Applicant",
            AuditAction.AssessmentCompleted, AuditEntityType.ScreeningResult,
            applicationId, Arg.Any<object?>(), null, null,
            Arg.Any<CancellationToken>());
    }
}

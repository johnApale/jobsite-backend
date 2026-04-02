using System.Text.Json;
using FluentAssertions;
using Jobsite.SharedKernel.Events;

namespace Jobsite.UnitTests.SharedKernel;

/// <summary>
/// Verifies that integration events (which cross the C# → Python boundary via
/// the message broker) serialize to the expected snake_case JSON and round-trip
/// without data loss. The AI Interview Service (Python/FastAPI) deserializes
/// these events, so the contract must remain stable.
/// </summary>
public sealed class IntegrationEventSerializationTests
{
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    // ──────────────────────────────────────────────────────────────────
    // CandidateReadyForInterviewEvent
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void CandidateReadyForInterviewEvent_SerializesToSnakeCaseJson()
    {
        // Arrange
        Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        DateTime occurredAt = new(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        CandidateReadyForInterviewEvent evt = new()
        {
            EventId = eventId,
            ApplicationId = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            TenantId = Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa"),
            ApplicantUserId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
            JobPostingId = Guid.Parse("12345678-1234-1234-1234-123456789abc"),
            CorrelationId = "corr-001",
            OccurredAt = occurredAt
        };

        // Act
        string json = JsonSerializer.Serialize(evt, SnakeCaseOptions);

        // Assert
        json.Should().Contain("\"event_id\":");
        json.Should().Contain("\"application_id\":");
        json.Should().Contain("\"tenant_id\":");
        json.Should().Contain("\"applicant_user_id\":");
        json.Should().Contain("\"job_posting_id\":");
        json.Should().Contain("\"correlation_id\":");
        json.Should().Contain("\"occurred_at\":");
    }

    [Fact]
    public void CandidateReadyForInterviewEvent_RoundTripsWithoutDataLoss()
    {
        // Arrange
        CandidateReadyForInterviewEvent original = new()
        {
            EventId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicantUserId = Guid.NewGuid(),
            JobPostingId = Guid.NewGuid(),
            CorrelationId = "round-trip-001",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(original, SnakeCaseOptions);
        CandidateReadyForInterviewEvent? deserialized =
            JsonSerializer.Deserialize<CandidateReadyForInterviewEvent>(json, SnakeCaseOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.EventId.Should().Be(original.EventId);
        deserialized.ApplicationId.Should().Be(original.ApplicationId);
        deserialized.TenantId.Should().Be(original.TenantId);
        deserialized.ApplicantUserId.Should().Be(original.ApplicantUserId);
        deserialized.JobPostingId.Should().Be(original.JobPostingId);
        deserialized.CorrelationId.Should().Be(original.CorrelationId);
        deserialized.OccurredAt.Should().Be(original.OccurredAt);
    }

    [Fact]
    public void CandidateReadyForInterviewEvent_NoPascalCaseKeysInOutput()
    {
        // Arrange
        CandidateReadyForInterviewEvent evt = new()
        {
            EventId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicantUserId = Guid.NewGuid(),
            JobPostingId = Guid.NewGuid(),
            CorrelationId = "no-pascal",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(evt, SnakeCaseOptions);

        // Assert — none of the PascalCase property names should appear
        json.Should().NotContain("\"EventId\"");
        json.Should().NotContain("\"ApplicationId\"");
        json.Should().NotContain("\"TenantId\"");
        json.Should().NotContain("\"ApplicantUserId\"");
        json.Should().NotContain("\"JobPostingId\"");
        json.Should().NotContain("\"CorrelationId\"");
        json.Should().NotContain("\"OccurredAt\"");
    }

    // ──────────────────────────────────────────────────────────────────
    // InterviewCompletedEvent
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void InterviewCompletedEvent_SerializesToSnakeCaseJson()
    {
        // Arrange
        InterviewCompletedEvent evt = new()
        {
            EventId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            InterviewSessionId = Guid.NewGuid(),
            OverallScore = 85,
            CorrelationId = "corr-002",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(evt, SnakeCaseOptions);

        // Assert
        json.Should().Contain("\"event_id\":");
        json.Should().Contain("\"application_id\":");
        json.Should().Contain("\"tenant_id\":");
        json.Should().Contain("\"interview_session_id\":");
        json.Should().Contain("\"overall_score\":");
        json.Should().Contain("\"correlation_id\":");
        json.Should().Contain("\"occurred_at\":");
    }

    [Fact]
    public void InterviewCompletedEvent_RoundTripsWithoutDataLoss()
    {
        // Arrange
        InterviewCompletedEvent original = new()
        {
            EventId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            InterviewSessionId = Guid.NewGuid(),
            OverallScore = 92,
            CorrelationId = "round-trip-002",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(original, SnakeCaseOptions);
        InterviewCompletedEvent? deserialized =
            JsonSerializer.Deserialize<InterviewCompletedEvent>(json, SnakeCaseOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.EventId.Should().Be(original.EventId);
        deserialized.ApplicationId.Should().Be(original.ApplicationId);
        deserialized.TenantId.Should().Be(original.TenantId);
        deserialized.InterviewSessionId.Should().Be(original.InterviewSessionId);
        deserialized.OverallScore.Should().Be(original.OverallScore);
        deserialized.CorrelationId.Should().Be(original.CorrelationId);
        deserialized.OccurredAt.Should().Be(original.OccurredAt);
    }

    [Fact]
    public void InterviewCompletedEvent_OverallScoreSerializesAsNumber()
    {
        // Arrange
        InterviewCompletedEvent evt = new()
        {
            EventId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            InterviewSessionId = Guid.NewGuid(),
            OverallScore = 75,
            CorrelationId = "score-check",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(evt, SnakeCaseOptions);

        // Assert — score should be a raw number, not a quoted string
        json.Should().Contain("\"overall_score\":75");
    }
}

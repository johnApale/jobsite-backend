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

    // ──────────────────────────────────────────────────────────────────
    // ResumeParseRequested
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void ResumeParseRequested_SerializesToSnakeCaseJson()
    {
        // Arrange
        ResumeParseRequested evt = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ResumeId = Guid.NewGuid(),
            ParsedText = "Experienced .NET developer…",
            CorrelationId = "corr-rpr-001",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(evt, SnakeCaseOptions);

        // Assert
        json.Should().Contain("\"event_id\":");
        json.Should().Contain("\"tenant_id\":");
        json.Should().Contain("\"resume_id\":");
        json.Should().Contain("\"parsed_text\":");
        json.Should().Contain("\"correlation_id\":");
        json.Should().Contain("\"occurred_at\":");
    }

    [Fact]
    public void ResumeParseRequested_RoundTripsWithoutDataLoss()
    {
        // Arrange
        ResumeParseRequested original = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ResumeId = Guid.NewGuid(),
            ParsedText = "Round trip text",
            CorrelationId = "rt-rpr-001",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(original, SnakeCaseOptions);
        ResumeParseRequested? deserialized =
            JsonSerializer.Deserialize<ResumeParseRequested>(json, SnakeCaseOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.EventId.Should().Be(original.EventId);
        deserialized.TenantId.Should().Be(original.TenantId);
        deserialized.ResumeId.Should().Be(original.ResumeId);
        deserialized.ParsedText.Should().Be(original.ParsedText);
        deserialized.CorrelationId.Should().Be(original.CorrelationId);
        deserialized.OccurredAt.Should().Be(original.OccurredAt);
    }

    [Fact]
    public void ResumeParseRequested_NoPascalCaseKeysInOutput()
    {
        // Arrange
        ResumeParseRequested evt = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ResumeId = Guid.NewGuid(),
            ParsedText = "text",
            CorrelationId = "no-pascal",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(evt, SnakeCaseOptions);

        // Assert
        json.Should().NotContain("\"EventId\"");
        json.Should().NotContain("\"TenantId\"");
        json.Should().NotContain("\"ResumeId\"");
        json.Should().NotContain("\"ParsedText\"");
        json.Should().NotContain("\"CorrelationId\"");
        json.Should().NotContain("\"OccurredAt\"");
    }

    // ──────────────────────────────────────────────────────────────────
    // ResumeParsed
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void ResumeParsed_SerializesToSnakeCaseJson()
    {
        // Arrange
        ResumeParsed evt = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ResumeId = Guid.NewGuid(),
            AiParsedContent = "{\"skills\":[\"C#\"]}",
            CorrelationId = "corr-rp-001",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(evt, SnakeCaseOptions);

        // Assert
        json.Should().Contain("\"event_id\":");
        json.Should().Contain("\"tenant_id\":");
        json.Should().Contain("\"resume_id\":");
        json.Should().Contain("\"ai_parsed_content\":");
        json.Should().Contain("\"correlation_id\":");
        json.Should().Contain("\"occurred_at\":");
    }

    [Fact]
    public void ResumeParsed_RoundTripsWithoutDataLoss()
    {
        // Arrange
        ResumeParsed original = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ResumeId = Guid.NewGuid(),
            AiParsedContent = "{\"skills\":[\"Python\",\"SQL\"]}",
            CorrelationId = "rt-rp-001",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(original, SnakeCaseOptions);
        ResumeParsed? deserialized =
            JsonSerializer.Deserialize<ResumeParsed>(json, SnakeCaseOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.EventId.Should().Be(original.EventId);
        deserialized.TenantId.Should().Be(original.TenantId);
        deserialized.ResumeId.Should().Be(original.ResumeId);
        deserialized.AiParsedContent.Should().Be(original.AiParsedContent);
        deserialized.CorrelationId.Should().Be(original.CorrelationId);
        deserialized.OccurredAt.Should().Be(original.OccurredAt);
    }

    // ──────────────────────────────────────────────────────────────────
    // ScreeningEvaluationRequested
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void ScreeningEvaluationRequested_SerializesToSnakeCaseJson()
    {
        // Arrange
        ScreeningEvaluationRequested evt = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            CriteriaJson = "[{\"name\":\"C#\"}]",
            ApplicantDataJson = "{\"skills\":[\"C#\"]}",
            CorrelationId = "corr-ser-001",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(evt, SnakeCaseOptions);

        // Assert
        json.Should().Contain("\"event_id\":");
        json.Should().Contain("\"tenant_id\":");
        json.Should().Contain("\"application_id\":");
        json.Should().Contain("\"criteria_json\":");
        json.Should().Contain("\"applicant_data_json\":");
        json.Should().Contain("\"correlation_id\":");
        json.Should().Contain("\"occurred_at\":");
    }

    [Fact]
    public void ScreeningEvaluationRequested_RoundTripsWithoutDataLoss()
    {
        // Arrange
        ScreeningEvaluationRequested original = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            CriteriaJson = "[{\"name\":\"Python\"}]",
            ApplicantDataJson = "{\"skills\":[\"Python\"]}",
            CorrelationId = "rt-ser-001",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(original, SnakeCaseOptions);
        ScreeningEvaluationRequested? deserialized =
            JsonSerializer.Deserialize<ScreeningEvaluationRequested>(json, SnakeCaseOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.EventId.Should().Be(original.EventId);
        deserialized.TenantId.Should().Be(original.TenantId);
        deserialized.ApplicationId.Should().Be(original.ApplicationId);
        deserialized.CriteriaJson.Should().Be(original.CriteriaJson);
        deserialized.ApplicantDataJson.Should().Be(original.ApplicantDataJson);
        deserialized.CorrelationId.Should().Be(original.CorrelationId);
        deserialized.OccurredAt.Should().Be(original.OccurredAt);
    }

    // ──────────────────────────────────────────────────────────────────
    // ScreeningEvaluated
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void ScreeningEvaluated_SerializesToSnakeCaseJson()
    {
        // Arrange
        ScreeningEvaluated evt = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            BreakdownJson = "[{\"criterion\":\"C#\",\"score\":90}]",
            OverallScore = 85.5m,
            CorrelationId = "corr-se-001",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(evt, SnakeCaseOptions);

        // Assert
        json.Should().Contain("\"event_id\":");
        json.Should().Contain("\"tenant_id\":");
        json.Should().Contain("\"application_id\":");
        json.Should().Contain("\"breakdown_json\":");
        json.Should().Contain("\"overall_score\":");
        json.Should().Contain("\"correlation_id\":");
        json.Should().Contain("\"occurred_at\":");
    }

    [Fact]
    public void ScreeningEvaluated_RoundTripsWithoutDataLoss()
    {
        // Arrange
        ScreeningEvaluated original = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            BreakdownJson = "[{\"criterion\":\"SQL\",\"score\":75}]",
            OverallScore = 78.25m,
            CorrelationId = "rt-se-001",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(original, SnakeCaseOptions);
        ScreeningEvaluated? deserialized =
            JsonSerializer.Deserialize<ScreeningEvaluated>(json, SnakeCaseOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.EventId.Should().Be(original.EventId);
        deserialized.TenantId.Should().Be(original.TenantId);
        deserialized.ApplicationId.Should().Be(original.ApplicationId);
        deserialized.BreakdownJson.Should().Be(original.BreakdownJson);
        deserialized.OverallScore.Should().Be(original.OverallScore);
        deserialized.CorrelationId.Should().Be(original.CorrelationId);
        deserialized.OccurredAt.Should().Be(original.OccurredAt);
    }

    [Fact]
    public void ScreeningEvaluated_OverallScoreSerializesAsNumber()
    {
        // Arrange
        ScreeningEvaluated evt = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            BreakdownJson = "[]",
            OverallScore = 92.5m,
            CorrelationId = "score-check",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(evt, SnakeCaseOptions);

        // Assert
        json.Should().Contain("\"overall_score\":92.5");
    }

    // ──────────────────────────────────────────────────────────────────
    // AnswerScoringRequested
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void AnswerScoringRequested_SerializesToSnakeCaseJson()
    {
        // Arrange
        AnswerScoringRequested evt = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            AnswersJson = "[{\"question\":\"Why?\",\"answer\":\"Because.\"}]",
            CorrelationId = "corr-asr-001",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(evt, SnakeCaseOptions);

        // Assert
        json.Should().Contain("\"event_id\":");
        json.Should().Contain("\"tenant_id\":");
        json.Should().Contain("\"application_id\":");
        json.Should().Contain("\"answers_json\":");
        json.Should().Contain("\"correlation_id\":");
        json.Should().Contain("\"occurred_at\":");
    }

    [Fact]
    public void AnswerScoringRequested_RoundTripsWithoutDataLoss()
    {
        // Arrange
        AnswerScoringRequested original = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            AnswersJson = "[{\"q\":\"Describe\",\"a\":\"I did X\"}]",
            CorrelationId = "rt-asr-001",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(original, SnakeCaseOptions);
        AnswerScoringRequested? deserialized =
            JsonSerializer.Deserialize<AnswerScoringRequested>(json, SnakeCaseOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.EventId.Should().Be(original.EventId);
        deserialized.TenantId.Should().Be(original.TenantId);
        deserialized.ApplicationId.Should().Be(original.ApplicationId);
        deserialized.AnswersJson.Should().Be(original.AnswersJson);
        deserialized.CorrelationId.Should().Be(original.CorrelationId);
        deserialized.OccurredAt.Should().Be(original.OccurredAt);
    }

    // ──────────────────────────────────────────────────────────────────
    // AnswersScored
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void AnswersScored_SerializesToSnakeCaseJson()
    {
        // Arrange
        AnswersScored evt = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            ScoresJson = "[{\"question_id\":\"abc\",\"score\":80}]",
            CorrelationId = "corr-as-001",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(evt, SnakeCaseOptions);

        // Assert
        json.Should().Contain("\"event_id\":");
        json.Should().Contain("\"tenant_id\":");
        json.Should().Contain("\"application_id\":");
        json.Should().Contain("\"scores_json\":");
        json.Should().Contain("\"correlation_id\":");
        json.Should().Contain("\"occurred_at\":");
    }

    [Fact]
    public void AnswersScored_RoundTripsWithoutDataLoss()
    {
        // Arrange
        AnswersScored original = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            ScoresJson = "[{\"question_id\":\"xyz\",\"score\":95}]",
            CorrelationId = "rt-as-001",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(original, SnakeCaseOptions);
        AnswersScored? deserialized =
            JsonSerializer.Deserialize<AnswersScored>(json, SnakeCaseOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.EventId.Should().Be(original.EventId);
        deserialized.TenantId.Should().Be(original.TenantId);
        deserialized.ApplicationId.Should().Be(original.ApplicationId);
        deserialized.ScoresJson.Should().Be(original.ScoresJson);
        deserialized.CorrelationId.Should().Be(original.CorrelationId);
        deserialized.OccurredAt.Should().Be(original.OccurredAt);
    }

    // ──────────────────────────────────────────────────────────────────
    // FeedbackGenerationRequested
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void FeedbackGenerationRequested_SerializesToSnakeCaseJson()
    {
        // Arrange
        FeedbackGenerationRequested evt = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            CriteriaBreakdown = "[{\"criterion\":\"C#\",\"score\":90}]",
            OverallScore = 87.0m,
            TransparencyLevel = "Detailed",
            CorrelationId = "corr-fgr-001",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(evt, SnakeCaseOptions);

        // Assert
        json.Should().Contain("\"event_id\":");
        json.Should().Contain("\"tenant_id\":");
        json.Should().Contain("\"application_id\":");
        json.Should().Contain("\"criteria_breakdown\":");
        json.Should().Contain("\"overall_score\":");
        json.Should().Contain("\"transparency_level\":");
        json.Should().Contain("\"correlation_id\":");
        json.Should().Contain("\"occurred_at\":");
    }

    [Fact]
    public void FeedbackGenerationRequested_RoundTripsWithoutDataLoss()
    {
        // Arrange
        FeedbackGenerationRequested original = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            CriteriaBreakdown = "[{\"criterion\":\"SQL\",\"score\":70}]",
            OverallScore = 72.5m,
            TransparencyLevel = "Summary",
            CorrelationId = "rt-fgr-001",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(original, SnakeCaseOptions);
        FeedbackGenerationRequested? deserialized =
            JsonSerializer.Deserialize<FeedbackGenerationRequested>(json, SnakeCaseOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.EventId.Should().Be(original.EventId);
        deserialized.TenantId.Should().Be(original.TenantId);
        deserialized.ApplicationId.Should().Be(original.ApplicationId);
        deserialized.CriteriaBreakdown.Should().Be(original.CriteriaBreakdown);
        deserialized.OverallScore.Should().Be(original.OverallScore);
        deserialized.TransparencyLevel.Should().Be(original.TransparencyLevel);
        deserialized.CorrelationId.Should().Be(original.CorrelationId);
        deserialized.OccurredAt.Should().Be(original.OccurredAt);
    }

    [Fact]
    public void FeedbackGenerationRequested_OverallScoreSerializesAsNumber()
    {
        // Arrange
        FeedbackGenerationRequested evt = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            CriteriaBreakdown = "[]",
            OverallScore = 65.0m,
            TransparencyLevel = "None",
            CorrelationId = "score-check",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(evt, SnakeCaseOptions);

        // Assert
        json.Should().Contain("\"overall_score\":65.0");
    }

    // ──────────────────────────────────────────────────────────────────
    // FeedbackGenerated
    // ──────────────────────────────────────────────────────────────────

    [Fact]
    public void FeedbackGenerated_SerializesToSnakeCaseJson()
    {
        // Arrange
        FeedbackGenerated evt = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            Feedback = "You demonstrated strong skills in C# and SQL.",
            CorrelationId = "corr-fg-001",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(evt, SnakeCaseOptions);

        // Assert
        json.Should().Contain("\"event_id\":");
        json.Should().Contain("\"tenant_id\":");
        json.Should().Contain("\"application_id\":");
        json.Should().Contain("\"feedback\":");
        json.Should().Contain("\"correlation_id\":");
        json.Should().Contain("\"occurred_at\":");
    }

    [Fact]
    public void FeedbackGenerated_RoundTripsWithoutDataLoss()
    {
        // Arrange
        FeedbackGenerated original = new()
        {
            EventId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ApplicationId = Guid.NewGuid(),
            Feedback = "Good overall match for the role.",
            CorrelationId = "rt-fg-001",
            OccurredAt = DateTime.UtcNow
        };

        // Act
        string json = JsonSerializer.Serialize(original, SnakeCaseOptions);
        FeedbackGenerated? deserialized =
            JsonSerializer.Deserialize<FeedbackGenerated>(json, SnakeCaseOptions);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.EventId.Should().Be(original.EventId);
        deserialized.TenantId.Should().Be(original.TenantId);
        deserialized.ApplicationId.Should().Be(original.ApplicationId);
        deserialized.Feedback.Should().Be(original.Feedback);
        deserialized.CorrelationId.Should().Be(original.CorrelationId);
        deserialized.OccurredAt.Should().Be(original.OccurredAt);
    }
}

using System.Text.Json;
using FluentAssertions;
using Jobsite.Modules.Screening.Infrastructure.AiIntegration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Jobsite.IntegrationTests.Contracts;

/// <summary>
/// Contract tests for <see cref="AiCandidateFeedbackClient"/> → POST /api/v1/ai/screening/feedback.
/// Verifies request body shape, endpoint path, and response deserialization
/// over a real HTTP connection via WireMock.
/// </summary>
[Collection("AiServiceContract")]
public sealed class AiCandidateFeedbackContractTests
{
    private readonly AiServiceContractFixture _fixture;

    public AiCandidateFeedbackContractTests(AiServiceContractFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private AiCandidateFeedbackClient CreateClient()
    {
        HttpClient httpClient = new() { BaseAddress = new Uri(_fixture.BaseUrl) };
        return new AiCandidateFeedbackClient(httpClient, Substitute.For<ILogger<AiCandidateFeedbackClient>>());
    }

    [Fact]
    public async Task GenerateFeedbackAsync_SendsCorrectRequestBody_WithSnakeCaseFields()
    {
        // Arrange
        string criteriaBreakdown = """[{"criterion_name": "C#", "score": 85, "result": "MeetsRequirement"}]""";
        decimal overallScore = 85m;
        string transparencyLevel = "Detailed";

        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/screening/feedback").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"feedback": "Your application demonstrates strong technical skills, particularly in C#. You scored 85 out of 100 on our evaluation criteria."}"""));

        AiCandidateFeedbackClient client = CreateClient();

        // Act
        string? result = await client.GenerateFeedbackAsync(
            criteriaBreakdown, overallScore, transparencyLevel, CancellationToken.None);

        // Assert — verify response deserialization
        result.Should().NotBeNull();
        result.Should().Contain("strong technical skills");

        // Assert — verify request body structure
        WireMock.Logging.ILogEntry entry = _fixture.Server.LogEntries.First();
        entry.RequestMessage.Method.Should().Be("POST");
        entry.RequestMessage.Path.Should().Be("/api/v1/ai/screening/feedback");

        string requestBody = entry.RequestMessage.Body!;
        JsonDocument doc = JsonDocument.Parse(requestBody);
        JsonElement root = doc.RootElement;
        root.TryGetProperty("criteria_breakdown", out _).Should().BeTrue();
        root.TryGetProperty("overall_score", out _).Should().BeTrue();
        root.TryGetProperty("transparency_level", out _).Should().BeTrue();

        root.GetProperty("overall_score").GetDecimal().Should().Be(85m);
        root.GetProperty("transparency_level").GetString().Should().Be("Detailed");
    }

    [Fact]
    public async Task GenerateFeedbackAsync_SummaryLevel_SendsCorrectLevel()
    {
        // Arrange
        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/screening/feedback").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"feedback": "Brief summary: you scored well."}"""));

        AiCandidateFeedbackClient client = CreateClient();

        // Act
        string? result = await client.GenerateFeedbackAsync(
            """[{"score": 70}]""", 70m, "Summary", CancellationToken.None);

        // Assert
        result.Should().Be("Brief summary: you scored well.");

        WireMock.Logging.ILogEntry entry = _fixture.Server.LogEntries.First();
        string requestBody = entry.RequestMessage.Body!;
        JsonDocument doc = JsonDocument.Parse(requestBody);
        doc.RootElement.GetProperty("transparency_level").GetString().Should().Be("Summary");
    }

    [Fact]
    public async Task GenerateFeedbackAsync_NullFeedbackField_ReturnsNull()
    {
        // Arrange
        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/screening/feedback").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"feedback": null}"""));

        AiCandidateFeedbackClient client = CreateClient();

        // Act
        string? result = await client.GenerateFeedbackAsync(
            """[{"score": 50}]""", 50m, "Detailed", CancellationToken.None);

        // Assert — null feedback field should return null
        result.Should().BeNull();
    }

    [Fact]
    public async Task GenerateFeedbackAsync_ServerReturns500_ReturnsNull()
    {
        // Arrange
        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/screening/feedback").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500));

        AiCandidateFeedbackClient client = CreateClient();

        // Act
        string? result = await client.GenerateFeedbackAsync(
            """[{"score": 80}]""", 80m, "Summary", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}

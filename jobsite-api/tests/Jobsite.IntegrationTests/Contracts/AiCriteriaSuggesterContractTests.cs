using System.Text.Json;
using FluentAssertions;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Infrastructure.AiIntegration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Jobsite.IntegrationTests.Contracts;

/// <summary>
/// Contract tests for <see cref="AiCriteriaSuggesterClient"/> → POST /api/v1/ai/criteria/suggest.
/// Verifies request body shape, endpoint path, and response deserialization
/// over a real HTTP connection via WireMock.
/// </summary>
[Collection("AiServiceContract")]
public sealed class AiCriteriaSuggesterContractTests
{
    private readonly AiServiceContractFixture _fixture;

    public AiCriteriaSuggesterContractTests(AiServiceContractFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private AiCriteriaSuggesterClient CreateClient()
    {
        HttpClient httpClient = new() { BaseAddress = new Uri(_fixture.BaseUrl) };
        return new AiCriteriaSuggesterClient(httpClient, Substitute.For<ILogger<AiCriteriaSuggesterClient>>());
    }

    [Fact]
    public async Task SuggestAsync_SendsCorrectRequestBody_WithSnakeCaseFields()
    {
        // Arrange
        string jobTitle = "Senior .NET Developer";
        string jobDescription = "Building microservices with C# and .NET, Azure deployment, SQL Server.";

        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/criteria/suggest").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                [
                    {
                        "name": "C# Proficiency",
                        "category": "Skill",
                        "evaluation_method": "ExactMatch",
                        "is_required": true,
                        "weight": 30.0,
                        "configuration": "{\"skill_name\": \"C#\"}"
                    },
                    {
                        "name": "Azure Experience",
                        "category": "Skill",
                        "evaluation_method": "SemanticSimilarity",
                        "is_required": false,
                        "weight": 20.0,
                        "configuration": "{\"keywords\": [\"Azure\", \"cloud\"]}"
                    }
                ]
                """));

        AiCriteriaSuggesterClient client = CreateClient();

        // Act
        List<AiCriteriaSuggestion>? result = await client.SuggestAsync(
            jobTitle, jobDescription, CancellationToken.None);

        // Assert — verify response deserialization
        result.Should().NotBeNull();
        result.Should().HaveCount(2);

        result![0].Name.Should().Be("C# Proficiency");
        result[0].Category.Should().Be("Skill");
        result[0].EvaluationMethod.Should().Be("ExactMatch");
        result[0].IsRequired.Should().BeTrue();
        result[0].Weight.Should().Be(30.0m);
        result[0].Configuration.Should().Contain("C#");

        result[1].Name.Should().Be("Azure Experience");
        result[1].IsRequired.Should().BeFalse();

        // Assert — verify request body structure
        WireMock.Logging.ILogEntry entry = _fixture.Server.LogEntries.First();
        entry.RequestMessage.Method.Should().Be("POST");
        entry.RequestMessage.Path.Should().Be("/api/v1/ai/criteria/suggest");

        string requestBody = entry.RequestMessage.Body!;
        JsonDocument doc = JsonDocument.Parse(requestBody);
        JsonElement root = doc.RootElement;
        root.TryGetProperty("job_title", out JsonElement titleElement).Should().BeTrue();
        root.TryGetProperty("job_description", out JsonElement descElement).Should().BeTrue();
        titleElement.GetString().Should().Be(jobTitle);
        descElement.GetString().Should().Be(jobDescription);
    }

    [Fact]
    public async Task SuggestAsync_EmptyList_DeserializesCorrectly()
    {
        // Arrange
        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/criteria/suggest").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("[]"));

        AiCriteriaSuggesterClient client = CreateClient();

        // Act
        List<AiCriteriaSuggestion>? result = await client.SuggestAsync(
            "Intern", "Entry level role", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SuggestAsync_ServerReturns500_ReturnsNull()
    {
        // Arrange
        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/criteria/suggest").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500));

        AiCriteriaSuggesterClient client = CreateClient();

        // Act
        List<AiCriteriaSuggestion>? result = await client.SuggestAsync(
            "Dev", "Build things", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SuggestAsync_MalformedJson_ReturnsNull()
    {
        // Arrange
        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/criteria/suggest").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("not a list"));

        AiCriteriaSuggesterClient client = CreateClient();

        // Act
        List<AiCriteriaSuggestion>? result = await client.SuggestAsync(
            "Dev", "Build", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}

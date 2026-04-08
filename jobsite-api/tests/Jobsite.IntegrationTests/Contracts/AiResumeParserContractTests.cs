using System.Text.Json;
using FluentAssertions;
using Jobsite.Modules.Profiles.Application.DTOs;
using Jobsite.Modules.Profiles.Infrastructure.AiIntegration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Jobsite.IntegrationTests.Contracts;

/// <summary>
/// Contract tests for <see cref="AiResumeParserClient"/> → POST /api/v1/ai/resumes/parse.
/// Verifies request body shape, endpoint path, and response deserialization
/// over a real HTTP connection via WireMock.
/// </summary>
[Collection("AiServiceContract")]
public sealed class AiResumeParserContractTests
{
    private readonly AiServiceContractFixture _fixture;

    public AiResumeParserContractTests(AiServiceContractFixture fixture)
    {
        _fixture = fixture;
        _fixture.Reset();
    }

    private AiResumeParserClient CreateClient()
    {
        HttpClient httpClient = new() { BaseAddress = new Uri(_fixture.BaseUrl) };
        return new AiResumeParserClient(httpClient, Substitute.For<ILogger<AiResumeParserClient>>());
    }

    [Fact]
    public async Task ParseAsync_SendsCorrectRequestBody_WithSnakeCaseFields()
    {
        // Arrange
        string parsedText = "John Doe — Senior Software Engineer with 10 years of C# and .NET experience. AWS certified.";

        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/resumes/parse").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                    "skills": [
                        {"name": "C#", "level": "Expert", "years": 10},
                        {"name": ".NET", "level": "Expert", "years": 10}
                    ],
                    "experience": [
                        {
                            "title": "Senior Software Engineer",
                            "company": "Acme Corp",
                            "start_date": "2015-01-01",
                            "end_date": "2024-12-31",
                            "description": "Built enterprise .NET systems"
                        }
                    ],
                    "education": [
                        {
                            "degree": "BSc Computer Science",
                            "institution": "MIT",
                            "start_date": "2010-09-01",
                            "end_date": "2014-06-01",
                            "field": "Computer Science"
                        }
                    ],
                    "certifications": ["AWS Solutions Architect"],
                    "summary": "Experienced senior engineer specializing in .NET backend systems."
                }
                """));

        AiResumeParserClient client = CreateClient();

        // Act
        AiResumeParseResult? result = await client.ParseAsync(parsedText, CancellationToken.None);

        // Assert — verify response deserialization
        result.Should().NotBeNull();
        result!.Skills.Should().HaveCount(2);
        result.Skills![0].Name.Should().Be("C#");
        result.Skills[0].Level.Should().Be("Expert");
        result.Skills[0].Years.Should().Be(10);

        result.Experience.Should().HaveCount(1);
        result.Experience![0].Title.Should().Be("Senior Software Engineer");
        result.Experience[0].Company.Should().Be("Acme Corp");
        result.Experience[0].StartDate.Should().Be("2015-01-01");

        result.Education.Should().HaveCount(1);
        result.Education![0].Degree.Should().Be("BSc Computer Science");
        result.Education[0].Field.Should().Be("Computer Science");

        result.Certifications.Should().Contain("AWS Solutions Architect");
        result.Summary.Should().Contain("senior engineer");

        // Assert — verify request body structure
        WireMock.Logging.ILogEntry entry = _fixture.Server.LogEntries.First();
        entry.RequestMessage.Method.Should().Be("POST");
        entry.RequestMessage.Path.Should().Be("/api/v1/ai/resumes/parse");

        string requestBody = entry.RequestMessage.Body!;
        JsonDocument doc = JsonDocument.Parse(requestBody);
        JsonElement root = doc.RootElement;
        root.TryGetProperty("parsed_text", out JsonElement parsedTextElement).Should().BeTrue();
        parsedTextElement.GetString().Should().Be(parsedText);
    }

    [Fact]
    public async Task ParseAsync_MinimalResponse_DeserializesNullableFields()
    {
        // Arrange
        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/resumes/parse").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                    "skills": null,
                    "experience": null,
                    "education": null,
                    "certifications": null,
                    "summary": null
                }
                """));

        AiResumeParserClient client = CreateClient();

        // Act
        AiResumeParseResult? result = await client.ParseAsync("sparse resume text", CancellationToken.None);

        // Assert — all nullable fields should be null
        result.Should().NotBeNull();
        result!.Skills.Should().BeNull();
        result.Experience.Should().BeNull();
        result.Education.Should().BeNull();
        result.Certifications.Should().BeNull();
        result.Summary.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_ServerReturns500_ReturnsNull()
    {
        // Arrange
        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/resumes/parse").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500));

        AiResumeParserClient client = CreateClient();

        // Act
        AiResumeParseResult? result = await client.ParseAsync("text", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_MalformedJson_ReturnsNull()
    {
        // Arrange
        _fixture.Server
            .Given(Request.Create().WithPath("/api/v1/ai/resumes/parse").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("<<<not json>>>"));

        AiResumeParserClient client = CreateClient();

        // Act
        AiResumeParseResult? result = await client.ParseAsync("text", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}

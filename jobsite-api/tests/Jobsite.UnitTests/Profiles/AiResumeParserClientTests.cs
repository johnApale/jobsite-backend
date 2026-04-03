using System.Net;
using System.Text.Json;
using FluentAssertions;
using Jobsite.Modules.Profiles.Application.DTOs;
using Jobsite.Modules.Profiles.Infrastructure.AiIntegration;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Profiles;

public sealed class AiResumeParserClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly ILogger<AiResumeParserClient> _logger = Substitute.For<ILogger<AiResumeParserClient>>();

    [Fact]
    public async Task ParseAsync_SuccessResponse_ReturnsResult()
    {
        // Arrange
        AiResumeParseResult expected = new()
        {
            Skills = [new AiExtractedSkill { Name = "C#", Level = "Advanced", Years = 5 }],
            Summary = "Senior .NET developer"
        };
        string json = JsonSerializer.Serialize(expected, JsonOptions);

        HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        AiResumeParserClient sut = new(httpClient, _logger);

        // Act
        AiResumeParseResult? result = await sut.ParseAsync("Resume text content", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Skills.Should().HaveCount(1);
        result.Skills![0].Name.Should().Be("C#");
        result.Summary.Should().Be("Senior .NET developer");
    }

    [Fact]
    public async Task ParseAsync_ErrorResponse_ReturnsNull()
    {
        // Arrange
        HttpClient httpClient = CreateHttpClient(HttpStatusCode.InternalServerError, "");
        AiResumeParserClient sut = new(httpClient, _logger);

        // Act
        AiResumeParseResult? result = await sut.ParseAsync("Resume text content", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_HttpRequestException_ReturnsNull()
    {
        // Arrange
        FakeMessageHandler handler = new(() =>
            throw new HttpRequestException("Connection refused"));
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:8000") };
        AiResumeParserClient sut = new(httpClient, _logger);

        // Act
        AiResumeParseResult? result = await sut.ParseAsync("Resume text content", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_TaskCanceled_ReturnsNull()
    {
        // Arrange
        FakeMessageHandler handler = new(() =>
            throw new TaskCanceledException("Request timed out"));
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:8000") };
        AiResumeParserClient sut = new(httpClient, _logger);

        // Act
        AiResumeParseResult? result = await sut.ParseAsync("Resume text content", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_InvalidJson_ReturnsNull()
    {
        // Arrange
        HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, "not-json");
        AiResumeParserClient sut = new(httpClient, _logger);

        // Act
        AiResumeParseResult? result = await sut.ParseAsync("Resume text content", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    private static HttpClient CreateHttpClient(HttpStatusCode statusCode, string content)
    {
        FakeMessageHandler handler = new(() => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        });
        return new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8000") };
    }

    /// <summary>Fake HTTP message handler for unit testing HttpClient calls.</summary>
    private sealed class FakeMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _factory;

        public FakeMessageHandler(Func<HttpResponseMessage> factory) => _factory = factory;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_factory());
        }
    }
}

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Infrastructure.AiIntegration;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Recruitment;

public sealed class AiCriteriaSuggesterClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly ILogger<AiCriteriaSuggesterClient> _logger = Substitute.For<ILogger<AiCriteriaSuggesterClient>>();

    [Fact]
    public async Task SuggestAsync_SuccessResponse_ReturnsSuggestions()
    {
        // Arrange
        List<AiCriteriaSuggestion> expected =
        [
            new() { Name = "C# Proficiency", Category = "Skill", EvaluationMethod = "SemanticSimilarity", IsRequired = true, Weight = 25.0m, Configuration = "{}" }
        ];
        string json = JsonSerializer.Serialize(expected, JsonOptions);
        HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        AiCriteriaSuggesterClient sut = new(httpClient, _logger);

        // Act
        List<AiCriteriaSuggestion>? result = await sut.SuggestAsync("Senior .NET Developer", "Job description", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].Name.Should().Be("C# Proficiency");
    }

    [Fact]
    public async Task SuggestAsync_ErrorResponse_ReturnsNull()
    {
        // Arrange
        HttpClient httpClient = CreateHttpClient(HttpStatusCode.InternalServerError, "");
        AiCriteriaSuggesterClient sut = new(httpClient, _logger);

        // Act
        List<AiCriteriaSuggestion>? result = await sut.SuggestAsync("Title", "Description", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SuggestAsync_HttpRequestException_ReturnsNull()
    {
        // Arrange
        FakeMessageHandler handler = new(() => throw new HttpRequestException("Connection refused"));
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:8000") };
        AiCriteriaSuggesterClient sut = new(httpClient, _logger);

        // Act
        List<AiCriteriaSuggestion>? result = await sut.SuggestAsync("Title", "Description", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SuggestAsync_TaskCanceled_ReturnsNull()
    {
        // Arrange
        FakeMessageHandler handler = new(() => throw new TaskCanceledException("Timeout"));
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:8000") };
        AiCriteriaSuggesterClient sut = new(httpClient, _logger);

        // Act
        List<AiCriteriaSuggestion>? result = await sut.SuggestAsync("Title", "Description", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SuggestAsync_InvalidJson_ReturnsNull()
    {
        // Arrange
        HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, "not-json");
        AiCriteriaSuggesterClient sut = new(httpClient, _logger);

        // Act
        List<AiCriteriaSuggestion>? result = await sut.SuggestAsync("Title", "Description", CancellationToken.None);

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

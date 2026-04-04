using System.Net;
using System.Text.Json;
using FluentAssertions;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Infrastructure.AiIntegration;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Recruitment;

public sealed class AiQuestionSuggesterClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly ILogger<AiQuestionSuggesterClient> _logger = Substitute.For<ILogger<AiQuestionSuggesterClient>>();

    [Fact]
    public async Task SuggestAsync_SuccessResponse_ReturnsSuggestions()
    {
        // Arrange
        List<AiQuestionSuggestion> expected =
        [
            new() { QuestionText = "Do you have C# experience?", QuestionType = "YesNo", Timing = "AtApplication", IsRequired = true, Weight = 10.0m }
        ];
        string json = JsonSerializer.Serialize(expected, JsonOptions);
        HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, json);
        AiQuestionSuggesterClient sut = new(httpClient, _logger);

        List<CriteriaResponse> criteria = [];

        // Act
        List<AiQuestionSuggestion>? result = await sut.SuggestAsync("Job description", criteria, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result![0].QuestionText.Should().Be("Do you have C# experience?");
    }

    [Fact]
    public async Task SuggestAsync_ErrorResponse_ReturnsNull()
    {
        // Arrange
        HttpClient httpClient = CreateHttpClient(HttpStatusCode.ServiceUnavailable, "");
        AiQuestionSuggesterClient sut = new(httpClient, _logger);

        // Act
        List<AiQuestionSuggestion>? result = await sut.SuggestAsync("Description", [], CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SuggestAsync_HttpRequestException_ReturnsNull()
    {
        // Arrange
        FakeMessageHandler handler = new(() => throw new HttpRequestException("Connection refused"));
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:8000") };
        AiQuestionSuggesterClient sut = new(httpClient, _logger);

        // Act
        List<AiQuestionSuggestion>? result = await sut.SuggestAsync("Description", [], CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SuggestAsync_TaskCanceled_ReturnsNull()
    {
        // Arrange
        FakeMessageHandler handler = new(() => throw new TaskCanceledException("Timeout"));
        HttpClient httpClient = new(handler) { BaseAddress = new Uri("http://localhost:8000") };
        AiQuestionSuggesterClient sut = new(httpClient, _logger);

        // Act
        List<AiQuestionSuggestion>? result = await sut.SuggestAsync("Description", [], CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SuggestAsync_InvalidJson_ReturnsNull()
    {
        // Arrange
        HttpClient httpClient = CreateHttpClient(HttpStatusCode.OK, "invalid-json");
        AiQuestionSuggesterClient sut = new(httpClient, _logger);

        // Act
        List<AiQuestionSuggestion>? result = await sut.SuggestAsync("Description", [], CancellationToken.None);

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

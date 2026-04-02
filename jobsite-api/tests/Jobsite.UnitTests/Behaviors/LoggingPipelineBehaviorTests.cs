using FluentAssertions;
using Jobsite.Api.Behaviors;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Behaviors;

public sealed class LoggingPipelineBehaviorTests
{
    private readonly ILogger<LoggingPipelineBehavior<TestRequest, TestResponse>> _logger;
    private readonly LoggingPipelineBehavior<TestRequest, TestResponse> _sut;

    public LoggingPipelineBehaviorTests()
    {
        _logger = Substitute.For<ILogger<LoggingPipelineBehavior<TestRequest, TestResponse>>>();
        _sut = new LoggingPipelineBehavior<TestRequest, TestResponse>(_logger);
    }

    [Fact]
    public async Task Handle_LogsStartAndCompletion()
    {
        // Arrange
        TestRequest request = new("test");
        TestResponse expected = new("ok");
        RequestHandlerDelegate<TestResponse> next = _ => Task.FromResult(expected);

        // Act
        TestResponse result = await _sut.Handle(request, next, CancellationToken.None);

        // Assert
        result.Should().Be(expected);
        _logger.ReceivedWithAnyArgs(2).Log(
            default, default, default!, default!, default!);
    }

    [Fact]
    public async Task Handle_ReturnsResponseFromNext()
    {
        // Arrange
        TestRequest request = new("test");
        TestResponse expected = new("result");
        RequestHandlerDelegate<TestResponse> next = _ => Task.FromResult(expected);

        // Act
        TestResponse result = await _sut.Handle(request, next, CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_WhenNextThrows_PropagatesException()
    {
        // Arrange
        TestRequest request = new("test");
        RequestHandlerDelegate<TestResponse> next = _ => throw new InvalidOperationException("boom");

        // Act
        Func<Task> act = async () => await _sut.Handle(request, next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    public sealed record TestRequest(string Value) : IRequest<TestResponse>;
    public sealed record TestResponse(string Value);
}

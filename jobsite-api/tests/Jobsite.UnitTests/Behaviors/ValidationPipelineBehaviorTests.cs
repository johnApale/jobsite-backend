using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Jobsite.Api.Behaviors;
using Jobsite.SharedKernel.Errors;
using MediatR;
using NSubstitute;

namespace Jobsite.UnitTests.Behaviors;

public sealed class ValidationPipelineBehaviorTests
{
    [Fact]
    public async Task Handle_NoValidators_PassesThrough()
    {
        // Arrange
        List<IValidator<TestRequest>> validators = [];
        ValidationPipelineBehavior<TestRequest, TestResponse> sut = new(validators);
        TestResponse expected = new("ok");
        RequestHandlerDelegate<TestResponse> next = _ => Task.FromResult(expected);

        // Act
        TestResponse result = await sut.Handle(new TestRequest("test"), next, CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_AllValidatorsPass_PassesThrough()
    {
        // Arrange
        IValidator<TestRequest> validator = Substitute.For<IValidator<TestRequest>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<TestRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        List<IValidator<TestRequest>> validators = [validator];
        ValidationPipelineBehavior<TestRequest, TestResponse> sut = new(validators);
        TestResponse expected = new("ok");
        RequestHandlerDelegate<TestResponse> next = _ => Task.FromResult(expected);

        // Act
        TestResponse result = await sut.Handle(new TestRequest("test"), next, CancellationToken.None);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ValidationFails_ThrowsValidationError()
    {
        // Arrange
        IValidator<TestRequest> validator = Substitute.For<IValidator<TestRequest>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<TestRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new List<ValidationFailure>
            {
                new("Name", "Name is required"),
                new("Email", "Email is invalid")
            }));

        List<IValidator<TestRequest>> validators = [validator];
        ValidationPipelineBehavior<TestRequest, TestResponse> sut = new(validators);
        RequestHandlerDelegate<TestResponse> next = _ => Task.FromResult(new TestResponse("ok"));

        // Act
        Func<Task> act = async () => await sut.Handle(new TestRequest(""), next, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("VALIDATION_ERROR");
        error.StatusCode.Should().Be(400);
        error.Details.Should().ContainKey("Name").WhoseValue.Should().Be("Name is required");
        error.Details.Should().ContainKey("Email").WhoseValue.Should().Be("Email is invalid");
    }

    [Fact]
    public async Task Handle_ValidationFails_DoesNotCallNext()
    {
        // Arrange
        IValidator<TestRequest> validator = Substitute.For<IValidator<TestRequest>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<TestRequest>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new List<ValidationFailure>
            {
                new("Name", "Required")
            }));

        List<IValidator<TestRequest>> validators = [validator];
        ValidationPipelineBehavior<TestRequest, TestResponse> sut = new(validators);
        bool nextCalled = false;
        RequestHandlerDelegate<TestResponse> next = _ =>
        {
            nextCalled = true;
            return Task.FromResult(new TestResponse("ok"));
        };

        // Act
        Func<Task> act = async () => await sut.Handle(new TestRequest(""), next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AppError>();
        nextCalled.Should().BeFalse();
    }

    public sealed record TestRequest(string Value) : IRequest<TestResponse>;
    public sealed record TestResponse(string Value);
}

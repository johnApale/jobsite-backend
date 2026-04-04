using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Jobsite.Api.Behaviors;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Errors;
using NSubstitute;

namespace Jobsite.UnitTests.Behaviors;

public sealed class ValidationEventBehaviorTests
{
    [Fact]
    public async Task HandleAsync_NoValidators_CallsNext()
    {
        // Arrange
        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IEnumerable<IValidator<TestEvent>>))
            .Returns(Enumerable.Empty<IValidator<TestEvent>>());

        ValidationEventBehavior sut = new(serviceProvider);
        TestEvent domainEvent = new("test");
        bool nextCalled = false;
        Func<Task> next = () => { nextCalled = true; return Task.CompletedTask; };

        // Act
        await sut.HandleAsync(domainEvent, next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_AllValidatorsPass_CallsNext()
    {
        // Arrange
        IValidator<TestEvent> validator = Substitute.For<IValidator<TestEvent>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<TestEvent>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IEnumerable<IValidator<TestEvent>>))
            .Returns(new List<IValidator<TestEvent>> { validator });

        ValidationEventBehavior sut = new(serviceProvider);
        TestEvent domainEvent = new("test");
        bool nextCalled = false;
        Func<Task> next = () => { nextCalled = true; return Task.CompletedTask; };

        // Act
        await sut.HandleAsync(domainEvent, next, CancellationToken.None);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_ValidationFails_ThrowsValidationError()
    {
        // Arrange
        IValidator<TestEvent> validator = Substitute.For<IValidator<TestEvent>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<TestEvent>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new List<ValidationFailure>
            {
                new("Name", "Name is required"),
                new("Email", "Email is invalid")
            }));

        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IEnumerable<IValidator<TestEvent>>))
            .Returns(new List<IValidator<TestEvent>> { validator });

        ValidationEventBehavior sut = new(serviceProvider);
        TestEvent domainEvent = new("");
        Func<Task> next = () => Task.CompletedTask;

        // Act
        Func<Task> act = async () => await sut.HandleAsync(domainEvent, next, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("VALIDATION_ERROR");
        error.StatusCode.Should().Be(400);
        error.Details.Should().ContainKey("Name").WhoseValue.Should().Be("Name is required");
        error.Details.Should().ContainKey("Email").WhoseValue.Should().Be("Email is invalid");
    }

    [Fact]
    public async Task HandleAsync_ValidationFails_DoesNotCallNext()
    {
        // Arrange
        IValidator<TestEvent> validator = Substitute.For<IValidator<TestEvent>>();
        validator.ValidateAsync(Arg.Any<ValidationContext<TestEvent>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(new List<ValidationFailure>
            {
                new("Name", "Required")
            }));

        IServiceProvider serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IEnumerable<IValidator<TestEvent>>))
            .Returns(new List<IValidator<TestEvent>> { validator });

        ValidationEventBehavior sut = new(serviceProvider);
        bool nextCalled = false;
        Func<Task> next = () => { nextCalled = true; return Task.CompletedTask; };

        // Act
        Func<Task> act = async () => await sut.HandleAsync(new TestEvent(""), next, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AppError>();
        nextCalled.Should().BeFalse();
    }

    public sealed record TestEvent(string Value) : IDomainEvent;
}

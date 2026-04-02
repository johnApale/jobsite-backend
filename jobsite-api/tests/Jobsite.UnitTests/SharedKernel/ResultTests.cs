using FluentAssertions;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Results;

namespace Jobsite.UnitTests.SharedKernel;

/// <summary>Tests for Result&lt;T&gt; monad.</summary>
public sealed class ResultTests
{
    [Fact]
    public void Success_WithValue_IsSuccessTrue()
    {
        // Arrange & Act
        Result<string> result = Result<string>.Success("hello");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Value.Should().Be("hello");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_WithError_IsFailureTrue()
    {
        // Arrange
        AppError error = AppErrors.TenantNotFound;

        // Act
        Result<string> result = Result<string>.Failure(error);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("TENANT_NOT_FOUND");
    }

    [Fact]
    public void ImplicitConversion_FromValue_CreatesSuccess()
    {
        // Arrange & Act
        Result<int> result = 42;

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void ImplicitConversion_FromError_CreatesFailure()
    {
        // Arrange & Act
        Result<int> result = AppErrors.InternalError;

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("INTERNAL_ERROR");
    }
}

using FluentAssertions;
using Jobsite.SharedKernel.Errors;

namespace Jobsite.UnitTests.SharedKernel;

/// <summary>Tests for AppError and AppErrors sentinel catalog.</summary>
public sealed class AppErrorTests
{
    [Fact]
    public void AppError_Constructor_SetsProperties()
    {
        // Arrange & Act
        AppError error = new("TEST_CODE", 400, "Test message");

        // Assert
        error.Code.Should().Be("TEST_CODE");
        error.StatusCode.Should().Be(400);
        error.Message.Should().Be("Test message");
        error.Details.Should().BeNull();
    }

    [Fact]
    public void WithMessage_ReturnsNewInstanceWithCustomMessage()
    {
        // Arrange
        AppError original = AppErrors.TenantNotFound;

        // Act
        AppError customized = original.WithMessage("Tenant 'acme' not found");

        // Assert
        customized.Code.Should().Be("TENANT_NOT_FOUND");
        customized.StatusCode.Should().Be(404);
        customized.Message.Should().Be("Tenant 'acme' not found");
        customized.Should().NotBeSameAs(original);
    }

    [Fact]
    public void WithDetails_ReturnsNewInstanceWithValidationDetails()
    {
        // Arrange
        AppError original = AppErrors.Validation;
        Dictionary<string, string> details = new()
        {
            { "email", "Must be a valid email address" },
            { "name", "Required" }
        };

        // Act
        AppError withDetails = original.WithDetails(details);

        // Assert
        withDetails.Code.Should().Be("VALIDATION_ERROR");
        withDetails.Details.Should().HaveCount(2);
        withDetails.Details!["email"].Should().Be("Must be a valid email address");
        withDetails.Should().NotBeSameAs(original);
    }

    [Fact]
    public void AppErrors_TenantNotFound_Has404Status()
    {
        // Arrange & Act
        AppError error = AppErrors.TenantNotFound;

        // Assert
        error.Code.Should().Be("TENANT_NOT_FOUND");
        error.StatusCode.Should().Be(404);
    }

    [Fact]
    public void AppErrors_Unauthorized_Has401Status()
    {
        // Arrange & Act
        AppError error = AppErrors.Unauthorized;

        // Assert
        error.Code.Should().Be("UNAUTHORIZED");
        error.StatusCode.Should().Be(401);
    }

    [Fact]
    public void AppErrors_SentinelProperties_ReturnNewInstances()
    {
        // Arrange & Act
        AppError first = AppErrors.TenantNotFound;
        AppError second = AppErrors.TenantNotFound;

        // Assert — sentinels are new instances each time (safe to customize)
        first.Should().NotBeSameAs(second);
    }
}

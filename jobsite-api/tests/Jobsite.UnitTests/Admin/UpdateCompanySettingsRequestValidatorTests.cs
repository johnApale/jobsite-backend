using FluentAssertions;
using FluentValidation.Results;
using Jobsite.Modules.Admin.Application.DTOs;
using Jobsite.Modules.Admin.Application.DTOs.Settings;
using Jobsite.Modules.Admin.Application.Validators;

namespace Jobsite.UnitTests.Admin;

public sealed class UpdateCompanySettingsRequestValidatorTests
{
    private readonly UpdateCompanySettingsRequestValidator _sut = new();

    [Fact]
    public void Validate_WithValidRequest_Passes()
    {
        // Arrange
        UpdateCompanySettingsRequest request = new()
        {
            DefaultTimezone = "America/New_York",
            DefaultCurrency = "USD",
            ScreeningSettings = new ScreeningSettingsDto
            {
                AutoAdvanceThreshold = 70,
                AutoRejectThreshold = 30,
                ManualReviewPolicy = "QueueForReview"
            }
        };

        // Act
        ValidationResult result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithAllNullFields_Passes()
    {
        // Arrange
        UpdateCompanySettingsRequest request = new();

        // Act
        ValidationResult result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithThresholdAbove100_Fails()
    {
        // Arrange
        UpdateCompanySettingsRequest request = new()
        {
            ScreeningSettings = new ScreeningSettingsDto
            {
                AutoAdvanceThreshold = 101
            }
        };

        // Act
        ValidationResult result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName.Contains("AutoAdvanceThreshold"));
    }

    [Fact]
    public void Validate_WithNegativeWeight_Fails()
    {
        // Arrange
        UpdateCompanySettingsRequest request = new()
        {
            MatchingSettings = new MatchingSettingsDto
            {
                ScreeningWeight = -1
            }
        };

        // Act
        ValidationResult result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName.Contains("ScreeningWeight"));
    }

    [Fact]
    public void Validate_WithInvalidManualReviewPolicy_Fails()
    {
        // Arrange
        UpdateCompanySettingsRequest request = new()
        {
            ScreeningSettings = new ScreeningSettingsDto
            {
                ManualReviewPolicy = "Invalid"
            }
        };

        // Act
        ValidationResult result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName.Contains("ManualReviewPolicy"));
    }

    [Fact]
    public void Validate_WithCurrencyNotThreeChars_Fails()
    {
        // Arrange
        UpdateCompanySettingsRequest request = new()
        {
            DefaultCurrency = "US"
        };

        // Act
        ValidationResult result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "DefaultCurrency");
    }

    [Fact]
    public void Validate_WithTimezoneTooLong_Fails()
    {
        // Arrange
        UpdateCompanySettingsRequest request = new()
        {
            DefaultTimezone = new string('x', 51)
        };

        // Act
        ValidationResult result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == "DefaultTimezone");
    }

    [Fact]
    public void Validate_WithPasswordMinLengthBelowMinimum_Fails()
    {
        // Arrange
        UpdateCompanySettingsRequest request = new()
        {
            AuthSettings = new AuthSettingsDto
            {
                PasswordMinLength = 5
            }
        };

        // Act
        ValidationResult result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName.Contains("PasswordMinLength"));
    }

    [Fact]
    public void Validate_WithInvalidAiParsingProvider_Fails()
    {
        // Arrange
        UpdateCompanySettingsRequest request = new()
        {
            ProfileSettings = new ProfileSettingsDto
            {
                AiParsingProvider = "InvalidProvider"
            }
        };

        // Act
        ValidationResult result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName.Contains("AiParsingProvider"));
    }

    [Fact]
    public void Validate_WithInvalidPartialCompletionPolicy_Fails()
    {
        // Arrange
        UpdateCompanySettingsRequest request = new()
        {
            AssessmentSettings = new AssessmentSettingsDto
            {
                PartialCompletionPolicy = "Invalid"
            }
        };

        // Act
        ValidationResult result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName.Contains("PartialCompletionPolicy"));
    }

    [Fact]
    public void Validate_WithZeroShortlistSize_Fails()
    {
        // Arrange
        UpdateCompanySettingsRequest request = new()
        {
            MatchingSettings = new MatchingSettingsDto
            {
                ShortlistSize = 0
            }
        };

        // Act
        ValidationResult result = _sut.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName.Contains("ShortlistSize"));
    }
}

using FluentAssertions;
using FluentValidation.Results;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Validators;
using Jobsite.Modules.Recruitment.Domain.Constants;

namespace Jobsite.UnitTests.Recruitment;

public sealed class CreateJobPostingRequestValidatorTests
{
    private readonly CreateJobPostingRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        // Arrange
        CreateJobPostingRequest request = TestData.CreateJobPostingRequest();

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyTitle_Fails()
    {
        // Arrange
        CreateJobPostingRequest request = TestData.CreateJobPostingRequest(title: "");

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void Validate_TitleTooLong_Fails()
    {
        // Arrange
        CreateJobPostingRequest request = TestData.CreateJobPostingRequest(title: new string('A', 201));

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Title");
    }

    [Fact]
    public void Validate_InvalidLocationType_Fails()
    {
        // Arrange
        CreateJobPostingRequest request = TestData.CreateJobPostingRequest(locationType: "InvalidType");

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LocationType");
    }

    [Fact]
    public void Validate_OnSiteWithoutCity_Fails()
    {
        // Arrange
        CreateJobPostingRequest request = TestData.CreateJobPostingRequest(
            locationType: LocationType.OnSite, city: null, country: "Philippines");

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "City");
    }

    [Fact]
    public void Validate_RemoteWithoutCity_Passes()
    {
        // Arrange
        CreateJobPostingRequest request = TestData.CreateJobPostingRequest(
            locationType: LocationType.Remote, city: null);

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_InvalidEmploymentType_Fails()
    {
        // Arrange
        CreateJobPostingRequest request = TestData.CreateJobPostingRequest(employmentType: "InvalidType");

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "EmploymentType");
    }

    [Fact]
    public void Validate_SalaryMinGreaterThanMax_Fails()
    {
        // Arrange
        CreateJobPostingRequest request = TestData.CreateJobPostingRequest(
            salaryMin: 100000, salaryMax: 50000, salaryCurrency: "USD");

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SalaryMax");
    }

    [Fact]
    public void Validate_SalaryWithoutCurrency_Fails()
    {
        // Arrange
        CreateJobPostingRequest request = TestData.CreateJobPostingRequest(
            salaryMin: 50000, salaryCurrency: null);

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SalaryCurrency");
    }

    [Fact]
    public void Validate_NegativeSalaryMin_Fails()
    {
        // Arrange
        CreateJobPostingRequest request = TestData.CreateJobPostingRequest(
            salaryMin: -1, salaryCurrency: "USD");

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SalaryMin");
    }
}

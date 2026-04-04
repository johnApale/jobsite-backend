using FluentAssertions;
using FluentValidation.Results;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Validators;

namespace Jobsite.UnitTests.Recruitment;

public sealed class SubmitApplicationRequestValidatorTests
{
    private readonly SubmitApplicationRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_Passes()
    {
        // Arrange
        SubmitApplicationRequest request = TestData.CreateSubmitApplicationRequest();

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyResumeId_Fails()
    {
        // Arrange
        SubmitApplicationRequest request = TestData.CreateSubmitApplicationRequest(resumeId: Guid.Empty);

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ResumeId");
    }

    [Fact]
    public void Validate_QuestionAnswerWithEmptyQuestionId_Fails()
    {
        // Arrange
        SubmitApplicationRequest request = new()
        {
            ResumeId = Guid.NewGuid(),
            QuestionAnswers =
            [
                new QuestionAnswerDto
                {
                    QuestionId = Guid.Empty,
                    ResponseText = "Yes"
                }
            ]
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("QuestionId"));
    }

    [Fact]
    public void Validate_NullQuestionAnswers_Passes()
    {
        // Arrange
        SubmitApplicationRequest request = new()
        {
            ResumeId = Guid.NewGuid(),
            QuestionAnswers = null
        };

        // Act
        ValidationResult result = _validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}

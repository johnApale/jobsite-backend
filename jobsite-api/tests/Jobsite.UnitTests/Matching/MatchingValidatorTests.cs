using FluentAssertions;
using FluentValidation.Results;
using Jobsite.Modules.Matching.Application.DTOs;
using Jobsite.Modules.Matching.Application.Validators;

namespace Jobsite.UnitTests.Matching;

public sealed class MatchingValidatorTests
{
    // ── GenerateShortlistRequestValidator ─────────────────────────────────

    [Fact]
    public void GenerateShortlistRequest_ValidJobPostingId_Passes()
    {
        // Arrange
        GenerateShortlistRequestValidator validator = new();
        GenerateShortlistRequest request = new() { JobPostingId = Guid.NewGuid() };

        // Act
        ValidationResult result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GenerateShortlistRequest_EmptyJobPostingId_Fails()
    {
        // Arrange
        GenerateShortlistRequestValidator validator = new();
        GenerateShortlistRequest request = new() { JobPostingId = Guid.Empty };

        // Act
        ValidationResult result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "JobPostingId");
    }

    // ── AddCandidateToShortlistRequestValidator ──────────────────────────

    [Fact]
    public void AddCandidateToShortlistRequest_ValidApplicationId_Passes()
    {
        // Arrange
        AddCandidateToShortlistRequestValidator validator = new();
        AddCandidateToShortlistRequest request = new() { ApplicationId = Guid.NewGuid() };

        // Act
        ValidationResult result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void AddCandidateToShortlistRequest_EmptyApplicationId_Fails()
    {
        // Arrange
        AddCandidateToShortlistRequestValidator validator = new();
        AddCandidateToShortlistRequest request = new() { ApplicationId = Guid.Empty };

        // Act
        ValidationResult result = validator.Validate(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "ApplicationId");
    }
}

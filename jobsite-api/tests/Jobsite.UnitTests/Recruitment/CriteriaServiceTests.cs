using FluentAssertions;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Application.Services;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using NSubstitute;

namespace Jobsite.UnitTests.Recruitment;

public sealed class CriteriaServiceTests
{
    private readonly ICriteriaRepository _criteriaRepo = Substitute.For<ICriteriaRepository>();
    private readonly IJobPostingRepository _jobPostingRepo = Substitute.For<IJobPostingRepository>();
    private readonly IAiCriteriaSuggester _aiSuggester = Substitute.For<IAiCriteriaSuggester>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CriteriaService _sut;

    public CriteriaServiceTests()
    {
        _sut = new CriteriaService(_criteriaRepo, _jobPostingRepo, _aiSuggester, _unitOfWork);
    }

    // ── AddAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ValidRequest_ReturnsResponse()
    {
        // Arrange
        Guid jobPostingId = Guid.NewGuid();
        _jobPostingRepo.ExistsByIdAsync(jobPostingId, Arg.Any<CancellationToken>()).Returns(true);
        CreateCriteriaRequest request = TestData.CreateCriteriaRequest();

        // Act
        CriteriaResponse response = await _sut.AddAsync(jobPostingId, request, CancellationToken.None);

        // Assert
        response.Name.Should().Be(request.Name);
        response.JobPostingId.Should().Be(jobPostingId);
        _criteriaRepo.Received(1).Add(Arg.Any<JobEvaluationCriteria>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_NonExistentJob_ThrowsJobPostingNotFound()
    {
        // Arrange
        _jobPostingRepo.ExistsByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        // Act
        Func<Task> act = () => _sut.AddAsync(Guid.NewGuid(), TestData.CreateCriteriaRequest(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("JOB_POSTING_NOT_FOUND");
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesFields()
    {
        // Arrange
        Guid jobPostingId = Guid.NewGuid();
        JobEvaluationCriteria criteria = TestData.CreateCriteria(jobPostingId: jobPostingId);
        _criteriaRepo.GetByIdForUpdateAsync(criteria.Id, Arg.Any<CancellationToken>()).Returns(criteria);

        UpdateCriteriaRequest request = new() { Name = "Updated Criterion" };

        // Act
        CriteriaResponse response = await _sut.UpdateAsync(jobPostingId, criteria.Id, request, CancellationToken.None);

        // Assert
        response.Name.Should().Be("Updated Criterion");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WrongJobPostingId_ThrowsCriteriaNotFound()
    {
        // Arrange
        JobEvaluationCriteria criteria = TestData.CreateCriteria(jobPostingId: Guid.NewGuid());
        _criteriaRepo.GetByIdForUpdateAsync(criteria.Id, Arg.Any<CancellationToken>()).Returns(criteria);
        Guid differentJobId = Guid.NewGuid();

        // Act
        Func<Task> act = () => _sut.UpdateAsync(differentJobId, criteria.Id, new UpdateCriteriaRequest(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("CRITERIA_NOT_FOUND");
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingCriteria_RemovesAndSaves()
    {
        // Arrange
        Guid jobPostingId = Guid.NewGuid();
        JobEvaluationCriteria criteria = TestData.CreateCriteria(jobPostingId: jobPostingId);
        _criteriaRepo.GetByIdForUpdateAsync(criteria.Id, Arg.Any<CancellationToken>()).Returns(criteria);

        // Act
        await _sut.DeleteAsync(jobPostingId, criteria.Id, CancellationToken.None);

        // Assert
        _criteriaRepo.Received(1).Remove(criteria);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_NonExistentCriteria_ThrowsCriteriaNotFound()
    {
        // Arrange
        _criteriaRepo.GetByIdForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((JobEvaluationCriteria?)null);

        // Act
        Func<Task> act = () => _sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("CRITERIA_NOT_FOUND");
    }

    // ── SuggestAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SuggestAsync_AiAvailable_ReturnsSuggestions()
    {
        // Arrange
        JobPosting jobPosting = TestData.CreateJobPosting();
        _jobPostingRepo.GetByIdAsync(jobPosting.Id, Arg.Any<CancellationToken>()).Returns(jobPosting);
        List<AiCriteriaSuggestion> expected = [new() { Name = "C#", Category = "Skill", EvaluationMethod = "SemanticSimilarity", IsRequired = true, Weight = 25.0m, Configuration = "{}" }];
        _aiSuggester.SuggestAsync(jobPosting.Title, jobPosting.Description, Arg.Any<CancellationToken>()).Returns(expected);

        // Act
        List<AiCriteriaSuggestion>? result = await _sut.SuggestAsync(jobPosting.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task SuggestAsync_AiUnavailable_ReturnsNull()
    {
        // Arrange
        JobPosting jobPosting = TestData.CreateJobPosting();
        _jobPostingRepo.GetByIdAsync(jobPosting.Id, Arg.Any<CancellationToken>()).Returns(jobPosting);
        _aiSuggester.SuggestAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((List<AiCriteriaSuggestion>?)null);

        // Act
        List<AiCriteriaSuggestion>? result = await _sut.SuggestAsync(jobPosting.Id, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SuggestAsync_NonExistentJob_ThrowsJobPostingNotFound()
    {
        // Arrange
        _jobPostingRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((JobPosting?)null);

        // Act
        Func<Task> act = () => _sut.SuggestAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("JOB_POSTING_NOT_FOUND");
    }
}

using FluentAssertions;
using Jobsite.Modules.Recruitment.Application.DTOs;
using Jobsite.Modules.Recruitment.Application.Interfaces;
using Jobsite.Modules.Recruitment.Application.Services;
using Jobsite.Modules.Recruitment.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using NSubstitute;

namespace Jobsite.UnitTests.Recruitment;

public sealed class ScreeningQuestionServiceTests
{
    private readonly IScreeningQuestionRepository _questionRepo = Substitute.For<IScreeningQuestionRepository>();
    private readonly ICriteriaRepository _criteriaRepo = Substitute.For<ICriteriaRepository>();
    private readonly IJobPostingRepository _jobPostingRepo = Substitute.For<IJobPostingRepository>();
    private readonly IAiQuestionSuggester _aiSuggester = Substitute.For<IAiQuestionSuggester>();
    private readonly ITenantSettingsReader _settingsReader = Substitute.For<ITenantSettingsReader>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ScreeningQuestionService _sut;

    public ScreeningQuestionServiceTests()
    {
        _sut = new ScreeningQuestionService(
            _questionRepo, _criteriaRepo, _jobPostingRepo, _aiSuggester, _settingsReader, _unitOfWork);
    }

    // ── AddAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ValidRequest_ReturnsResponse()
    {
        // Arrange
        Guid jobPostingId = Guid.NewGuid();
        _jobPostingRepo.ExistsByIdAsync(jobPostingId, Arg.Any<CancellationToken>()).Returns(true);
        CreateQuestionRequest request = TestData.CreateQuestionRequest();

        // Act
        QuestionResponse response = await _sut.AddAsync(jobPostingId, request, CancellationToken.None);

        // Assert
        response.QuestionText.Should().Be(request.QuestionText);
        response.JobPostingId.Should().Be(jobPostingId);
        _questionRepo.Received(1).Add(Arg.Any<JobScreeningQuestion>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddAsync_NonExistentJob_ThrowsJobPostingNotFound()
    {
        // Arrange
        _jobPostingRepo.ExistsByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        // Act
        Func<Task> act = () => _sut.AddAsync(Guid.NewGuid(), TestData.CreateQuestionRequest(), CancellationToken.None);

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
        JobScreeningQuestion question = TestData.CreateScreeningQuestion(jobPostingId: jobPostingId);
        _questionRepo.GetByIdForUpdateAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);

        UpdateQuestionRequest request = new() { QuestionText = "Updated question?" };

        // Act
        QuestionResponse response = await _sut.UpdateAsync(jobPostingId, question.Id, request, CancellationToken.None);

        // Assert
        response.QuestionText.Should().Be("Updated question?");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_WrongJobPostingId_ThrowsScreeningQuestionNotFound()
    {
        // Arrange
        JobScreeningQuestion question = TestData.CreateScreeningQuestion(jobPostingId: Guid.NewGuid());
        _questionRepo.GetByIdForUpdateAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);
        Guid differentJobId = Guid.NewGuid();

        // Act
        Func<Task> act = () => _sut.UpdateAsync(differentJobId, question.Id, new UpdateQuestionRequest(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("SCREENING_QUESTION_NOT_FOUND");
    }

    // ── DeleteAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_ExistingQuestion_RemovesAndSaves()
    {
        // Arrange
        Guid jobPostingId = Guid.NewGuid();
        JobScreeningQuestion question = TestData.CreateScreeningQuestion(jobPostingId: jobPostingId);
        _questionRepo.GetByIdForUpdateAsync(question.Id, Arg.Any<CancellationToken>()).Returns(question);

        // Act
        await _sut.DeleteAsync(jobPostingId, question.Id, CancellationToken.None);

        // Assert
        _questionRepo.Received(1).Remove(question);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_NonExistentQuestion_ThrowsScreeningQuestionNotFound()
    {
        // Arrange
        _questionRepo.GetByIdForUpdateAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((JobScreeningQuestion?)null);

        // Act
        Func<Task> act = () => _sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("SCREENING_QUESTION_NOT_FOUND");
    }

    // ── SuggestAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SuggestAsync_FeatureDisabled_ReturnsNull()
    {
        // Arrange
        _settingsReader.GetSettingAsync<AssessmentFeatureFlags>("assessment_settings", Arg.Any<CancellationToken>())
            .Returns(new AssessmentFeatureFlags { AiAssessmentQuestionsEnabled = false });

        // Act
        List<AiQuestionSuggestion>? result = await _sut.SuggestAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task SuggestAsync_FeatureEnabled_ReturnsSuggestions()
    {
        // Arrange
        JobPosting jobPosting = TestData.CreateJobPosting();
        _settingsReader.GetSettingAsync<AssessmentFeatureFlags>("assessment_settings", Arg.Any<CancellationToken>())
            .Returns(new AssessmentFeatureFlags { AiAssessmentQuestionsEnabled = true });
        _jobPostingRepo.GetByIdAsync(jobPosting.Id, Arg.Any<CancellationToken>()).Returns(jobPosting);
        _criteriaRepo.GetByJobPostingIdAsync(jobPosting.Id, Arg.Any<CancellationToken>())
            .Returns(new List<JobEvaluationCriteria>());

        List<AiQuestionSuggestion> expected = [new() { QuestionText = "Test?", QuestionType = "YesNo", Timing = "AtApplication", IsRequired = true, Weight = 10.0m }];
        _aiSuggester.SuggestAsync(Arg.Any<string>(), Arg.Any<List<CriteriaResponse>>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        List<AiQuestionSuggestion>? result = await _sut.SuggestAsync(jobPosting.Id, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task SuggestAsync_NullSettings_ReturnsNull()
    {
        // Arrange
        _settingsReader.GetSettingAsync<AssessmentFeatureFlags>("assessment_settings", Arg.Any<CancellationToken>())
            .Returns((AssessmentFeatureFlags?)null);

        // Act
        List<AiQuestionSuggestion>? result = await _sut.SuggestAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }
}

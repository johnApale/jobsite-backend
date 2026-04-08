using FluentAssertions;
using Jobsite.Modules.Matching.Application.Services;
using Jobsite.Modules.Matching.Domain.Constants;
using Jobsite.Modules.Matching.Domain.Entities;
using Jobsite.Modules.Matching.Domain.Interfaces;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.Matching;

public sealed class ShortlistServiceTests
{
    private readonly IShortlistRepository _shortlistRepo = Substitute.For<IShortlistRepository>();
    private readonly ICandidateMatchRepository _matchRepo = Substitute.For<ICandidateMatchRepository>();
    private readonly IApplicationStatusUpdater _statusUpdater = Substitute.For<IApplicationStatusUpdater>();
    private readonly IDomainEventDispatcher _dispatcher = Substitute.For<IDomainEventDispatcher>();
    private readonly ITenantSettingsReader _settingsReader = Substitute.For<ITenantSettingsReader>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ShortlistService _service;

    public ShortlistServiceTests()
    {
        _service = new ShortlistService(
            _shortlistRepo,
            _matchRepo,
            _statusUpdater,
            _dispatcher,
            _settingsReader,
            _unitOfWork,
            Substitute.For<ILogger<ShortlistService>>());
    }

    // ── GenerateShortlistAsync ───────────────────────────────────────────

    [Fact]
    public async Task GenerateShortlist_WithCandidates_CreatesShortlistWithTopN()
    {
        // Arrange
        Guid jobPostingId = Guid.NewGuid();
        List<CandidateMatch> matches =
        [
            TestData.CreateCandidateMatch(jobPostingId: jobPostingId, compositeScore: 90m),
            TestData.CreateCandidateMatch(jobPostingId: jobPostingId, compositeScore: 80m),
            TestData.CreateCandidateMatch(jobPostingId: jobPostingId, compositeScore: 70m),
        ];
        _matchRepo.GetByJobPostingIdAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(matches);

        _settingsReader.GetSettingAsync<Modules.Matching.Application.DTOs.MatchingSettings>(
            "matching_settings", Arg.Any<CancellationToken>())
            .Returns(new Modules.Matching.Application.DTOs.MatchingSettings { ShortlistSize = 2 });

        // Act
        Modules.Matching.Application.DTOs.ShortlistResponse result =
            await _service.GenerateShortlistAsync(jobPostingId, CancellationToken.None);

        // Assert
        result.JobPostingId.Should().Be(jobPostingId);
        result.Status.Should().Be(ShortlistStatus.Draft);
        result.TotalCandidates.Should().Be(2);
        result.Candidates.Should().HaveCount(2);
        result.Candidates[0].Rank.Should().Be(1);
        result.Candidates[1].Rank.Should().Be(2);
        _shortlistRepo.Received(1).Add(Arg.Any<Shortlist>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateShortlist_NoCandidates_CreatesEmptyShortlist()
    {
        // Arrange
        Guid jobPostingId = Guid.NewGuid();
        _matchRepo.GetByJobPostingIdAsync(jobPostingId, Arg.Any<CancellationToken>())
            .Returns(new List<CandidateMatch>());

        _settingsReader.GetSettingAsync<Modules.Matching.Application.DTOs.MatchingSettings>(
            "matching_settings", Arg.Any<CancellationToken>())
            .Returns((Modules.Matching.Application.DTOs.MatchingSettings?)null);

        // Act
        Modules.Matching.Application.DTOs.ShortlistResponse result =
            await _service.GenerateShortlistAsync(jobPostingId, CancellationToken.None);

        // Assert
        result.TotalCandidates.Should().Be(0);
        result.Candidates.Should().BeEmpty();
    }

    // ── GetShortlistAsync ────────────────────────────────────────────────

    [Fact]
    public async Task GetShortlist_ExistingShortlist_ReturnsResponse()
    {
        // Arrange
        Shortlist shortlist = TestData.CreateShortlist();
        _shortlistRepo.GetByIdAsync(shortlist.Id, Arg.Any<CancellationToken>())
            .Returns(shortlist);

        // Act
        Modules.Matching.Application.DTOs.ShortlistResponse result =
            await _service.GetShortlistAsync(shortlist.Id, CancellationToken.None);

        // Assert
        result.Id.Should().Be(shortlist.Id);
        result.Status.Should().Be(ShortlistStatus.Draft);
    }

    [Fact]
    public async Task GetShortlist_NonexistentShortlist_ThrowsNotFound()
    {
        // Arrange
        Guid shortlistId = Guid.NewGuid();
        _shortlistRepo.GetByIdAsync(shortlistId, Arg.Any<CancellationToken>())
            .Returns((Shortlist?)null);

        // Act
        Func<Task> act = () => _service.GetShortlistAsync(shortlistId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(404);
    }

    // ── AddCandidateAsync ────────────────────────────────────────────────

    [Fact]
    public async Task AddCandidate_DraftShortlist_AddsCandidateWithManualSource()
    {
        // Arrange
        CandidateMatch match = TestData.CreateCandidateMatch(compositeScore: 85m);
        Shortlist shortlist = TestData.CreateShortlist();
        shortlist.Candidates = new List<ShortlistCandidate>();

        _shortlistRepo.GetByIdForUpdateAsync(shortlist.Id, Arg.Any<CancellationToken>())
            .Returns(shortlist);
        _matchRepo.GetByApplicationIdAsync(match.ApplicationId, Arg.Any<CancellationToken>())
            .Returns(match);

        // Act
        Modules.Matching.Application.DTOs.ShortlistResponse result =
            await _service.AddCandidateAsync(shortlist.Id, match.ApplicationId, CancellationToken.None);

        // Assert
        result.Candidates.Should().HaveCount(1);
        result.Candidates[0].Source.Should().Be(ShortlistCandidateSource.Manual);
        result.Candidates[0].ApplicationId.Should().Be(match.ApplicationId);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AddCandidate_FinalizedShortlist_ThrowsConflict()
    {
        // Arrange
        Shortlist shortlist = TestData.CreateShortlist(status: ShortlistStatus.Finalized);
        _shortlistRepo.GetByIdForUpdateAsync(shortlist.Id, Arg.Any<CancellationToken>())
            .Returns(shortlist);

        // Act
        Func<Task> act = () => _service.AddCandidateAsync(
            shortlist.Id, Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task AddCandidate_DuplicateCandidate_ThrowsConflict()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Shortlist shortlist = TestData.CreateShortlist();
        shortlist.Candidates = new List<ShortlistCandidate>
        {
            TestData.CreateShortlistCandidate(
                shortlistId: shortlist.Id,
                applicationId: applicationId)
        };
        _shortlistRepo.GetByIdForUpdateAsync(shortlist.Id, Arg.Any<CancellationToken>())
            .Returns(shortlist);

        // Act
        Func<Task> act = () => _service.AddCandidateAsync(
            shortlist.Id, applicationId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(409);
    }

    // ── RemoveCandidateAsync ─────────────────────────────────────────────

    [Fact]
    public async Task RemoveCandidate_ExistingCandidate_SoftRemoves()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        ShortlistCandidate candidate = TestData.CreateShortlistCandidate(applicationId: applicationId);
        Shortlist shortlist = TestData.CreateShortlist();
        shortlist.Candidates = new List<ShortlistCandidate> { candidate };

        _shortlistRepo.GetByIdForUpdateAsync(shortlist.Id, Arg.Any<CancellationToken>())
            .Returns(shortlist);

        // Act
        await _service.RemoveCandidateAsync(shortlist.Id, applicationId, CancellationToken.None);

        // Assert
        candidate.RemovedAt.Should().NotBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveCandidate_FinalizedShortlist_ThrowsConflict()
    {
        // Arrange
        Shortlist shortlist = TestData.CreateShortlist(status: ShortlistStatus.Finalized);
        _shortlistRepo.GetByIdForUpdateAsync(shortlist.Id, Arg.Any<CancellationToken>())
            .Returns(shortlist);

        // Act
        Func<Task> act = () => _service.RemoveCandidateAsync(
            shortlist.Id, Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task RemoveCandidate_NotFoundOnShortlist_ThrowsNotFound()
    {
        // Arrange
        Shortlist shortlist = TestData.CreateShortlist();
        shortlist.Candidates = new List<ShortlistCandidate>();
        _shortlistRepo.GetByIdForUpdateAsync(shortlist.Id, Arg.Any<CancellationToken>())
            .Returns(shortlist);

        // Act
        Func<Task> act = () => _service.RemoveCandidateAsync(
            shortlist.Id, Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(404);
    }

    // ── FinalizeShortlistAsync ───────────────────────────────────────────

    [Fact]
    public async Task FinalizeShortlist_DraftWithApprovedCandidates_UpdatesStatusAndDispatchesEvents()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Guid app1 = Guid.NewGuid();
        Guid app2 = Guid.NewGuid();
        Shortlist shortlist = TestData.CreateShortlist();
        shortlist.Candidates = new List<ShortlistCandidate>
        {
            TestData.CreateShortlistCandidate(shortlistId: shortlist.Id, applicationId: app1, status: ShortlistCandidateStatus.Approved),
            TestData.CreateShortlistCandidate(shortlistId: shortlist.Id, applicationId: app2, status: ShortlistCandidateStatus.Approved),
        };

        _shortlistRepo.GetByIdForUpdateAsync(shortlist.Id, Arg.Any<CancellationToken>())
            .Returns(shortlist);

        // Act
        Modules.Matching.Application.DTOs.ShortlistResponse result =
            await _service.FinalizeShortlistAsync(shortlist.Id, userId, CancellationToken.None);

        // Assert
        result.Status.Should().Be(ShortlistStatus.Finalized);
        result.FinalizedBy.Should().Be(userId);
        result.FinalizedAt.Should().NotBeNull();

        // Status updates dispatched for each approved candidate
        await _statusUpdater.Received(2).UpdateStatusAsync(
            Arg.Any<Guid>(), "Shortlisted", null, null, Arg.Any<CancellationToken>());

        // Events dispatched for each approved candidate
        await _dispatcher.Received(2).DispatchAsync(
            Arg.Any<CandidateShortlistedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinalizeShortlist_NoPendingOrRejectedOnly_ThrowsInvalidRequest()
    {
        // Arrange
        Shortlist shortlist = TestData.CreateShortlist();
        shortlist.Candidates = new List<ShortlistCandidate>
        {
            TestData.CreateShortlistCandidate(shortlistId: shortlist.Id, status: ShortlistCandidateStatus.Pending),
            TestData.CreateShortlistCandidate(shortlistId: shortlist.Id, status: ShortlistCandidateStatus.Rejected),
        };

        _shortlistRepo.GetByIdForUpdateAsync(shortlist.Id, Arg.Any<CancellationToken>())
            .Returns(shortlist);

        // Act
        Func<Task> act = () => _service.FinalizeShortlistAsync(
            shortlist.Id, Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("INVALID_REQUEST");
    }

    [Fact]
    public async Task FinalizeShortlist_AlreadyFinalized_ThrowsConflict()
    {
        // Arrange
        Shortlist shortlist = TestData.CreateShortlist(status: ShortlistStatus.Finalized);
        _shortlistRepo.GetByIdForUpdateAsync(shortlist.Id, Arg.Any<CancellationToken>())
            .Returns(shortlist);

        // Act
        Func<Task> act = () => _service.FinalizeShortlistAsync(
            shortlist.Id, Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(409);
    }

    // ── ApproveCandidateAsync ────────────────────────────────────────────

    [Fact]
    public async Task ApproveCandidate_DraftShortlist_SetsStatusToApproved()
    {
        // Arrange
        ShortlistCandidate candidate = TestData.CreateShortlistCandidate();
        Shortlist shortlist = TestData.CreateShortlist();
        shortlist.Candidates = new List<ShortlistCandidate> { candidate };

        _shortlistRepo.GetByIdForUpdateAsync(shortlist.Id, Arg.Any<CancellationToken>())
            .Returns(shortlist);

        // Act
        Modules.Matching.Application.DTOs.ShortlistResponse result =
            await _service.ApproveCandidateAsync(shortlist.Id, candidate.Id, CancellationToken.None);

        // Assert
        candidate.Status.Should().Be(ShortlistCandidateStatus.Approved);
        result.Candidates[0].Status.Should().Be(ShortlistCandidateStatus.Approved);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApproveCandidate_FinalizedShortlist_ThrowsConflict()
    {
        // Arrange
        Shortlist shortlist = TestData.CreateShortlist(status: ShortlistStatus.Finalized);
        _shortlistRepo.GetByIdForUpdateAsync(shortlist.Id, Arg.Any<CancellationToken>())
            .Returns(shortlist);

        // Act
        Func<Task> act = () => _service.ApproveCandidateAsync(
            shortlist.Id, Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task ApproveCandidate_NonexistentCandidate_ThrowsNotFound()
    {
        // Arrange
        Shortlist shortlist = TestData.CreateShortlist();
        shortlist.Candidates = new List<ShortlistCandidate>();
        _shortlistRepo.GetByIdForUpdateAsync(shortlist.Id, Arg.Any<CancellationToken>())
            .Returns(shortlist);

        // Act
        Func<Task> act = () => _service.ApproveCandidateAsync(
            shortlist.Id, Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(404);
    }

    // ── RejectCandidateAsync ─────────────────────────────────────────────

    [Fact]
    public async Task RejectCandidate_DraftShortlist_SetsStatusToRejected()
    {
        // Arrange
        ShortlistCandidate candidate = TestData.CreateShortlistCandidate();
        Shortlist shortlist = TestData.CreateShortlist();
        shortlist.Candidates = new List<ShortlistCandidate> { candidate };

        _shortlistRepo.GetByIdForUpdateAsync(shortlist.Id, Arg.Any<CancellationToken>())
            .Returns(shortlist);

        // Act
        Modules.Matching.Application.DTOs.ShortlistResponse result =
            await _service.RejectCandidateAsync(shortlist.Id, candidate.Id, CancellationToken.None);

        // Assert
        candidate.Status.Should().Be(ShortlistCandidateStatus.Rejected);
        result.Candidates.Should().HaveCount(1);
        result.Candidates[0].Status.Should().Be(ShortlistCandidateStatus.Rejected);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RejectCandidate_FinalizedShortlist_ThrowsConflict()
    {
        // Arrange
        Shortlist shortlist = TestData.CreateShortlist(status: ShortlistStatus.Finalized);
        _shortlistRepo.GetByIdForUpdateAsync(shortlist.Id, Arg.Any<CancellationToken>())
            .Returns(shortlist);

        // Act
        Func<Task> act = () => _service.RejectCandidateAsync(
            shortlist.Id, Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(409);
    }
}

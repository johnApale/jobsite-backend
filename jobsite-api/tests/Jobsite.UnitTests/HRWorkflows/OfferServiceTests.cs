using FluentAssertions;
using Jobsite.Modules.HRWorkflows.Application.DTOs;
using Jobsite.Modules.HRWorkflows.Application.Services;
using Jobsite.Modules.HRWorkflows.Domain.Constants;
using Jobsite.Modules.HRWorkflows.Domain.Entities;
using Jobsite.Modules.HRWorkflows.Domain.Interfaces;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.HRWorkflows;

public sealed class OfferServiceTests
{
    private readonly IJobOfferRepository _offerRepo = Substitute.For<IJobOfferRepository>();
    private readonly IApplicationStatusUpdater _statusUpdater = Substitute.For<IApplicationStatusUpdater>();
    private readonly IDomainEventDispatcher _dispatcher = Substitute.For<IDomainEventDispatcher>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly OfferService _service;

    public OfferServiceTests()
    {
        _service = new OfferService(
            _offerRepo,
            _statusUpdater,
            _dispatcher,
            _unitOfWork,
            Substitute.For<ILogger<OfferService>>());
    }

    // ── CreateOfferAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateOffer_NewApplication_CreatesDraftOffer()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        Guid extendedBy = Guid.NewGuid();
        _offerRepo.GetByApplicationIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns((JobOffer?)null);

        CreateOfferRequest request = new()
        {
            ApplicationId = applicationId,
            Salary = 120000m,
            SalaryCurrency = "USD",
            SalaryPeriod = SalaryPeriod.Annual,
            EmploymentType = OfferEmploymentType.FullTime
        };

        // Act
        JobOfferResponse result = await _service.CreateOfferAsync(
            request, extendedBy, CancellationToken.None);

        // Assert
        result.ApplicationId.Should().Be(applicationId);
        result.Status.Should().Be(OfferStatus.Draft);
        result.Salary.Should().Be(120000m);
        result.ExtendedBy.Should().Be(extendedBy);
        _offerRepo.Received(1).Add(Arg.Any<JobOffer>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateOffer_AlreadyExists_ThrowsConflict()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        _offerRepo.GetByApplicationIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(TestData.CreateJobOffer(applicationId: applicationId));

        CreateOfferRequest request = new()
        {
            ApplicationId = applicationId,
            Salary = 100000m,
            SalaryCurrency = "USD",
            SalaryPeriod = SalaryPeriod.Annual,
            EmploymentType = OfferEmploymentType.FullTime
        };

        // Act
        Func<Task> act = () => _service.CreateOfferAsync(
            request, Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(409);
    }

    // ── GetOfferAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetOffer_Exists_ReturnsResponse()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        JobOffer offer = TestData.CreateJobOffer(applicationId: applicationId);
        _offerRepo.GetByApplicationIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(offer);

        // Act
        JobOfferResponse result = await _service.GetOfferAsync(
            applicationId, CancellationToken.None);

        // Assert
        result.ApplicationId.Should().Be(applicationId);
    }

    [Fact]
    public async Task GetOffer_NotFound_Throws404()
    {
        // Arrange
        _offerRepo.GetByApplicationIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((JobOffer?)null);

        // Act
        Func<Task> act = () => _service.GetOfferAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(404);
    }

    // ── UpdateOfferAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdateOffer_Draft_UpdatesFields()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        JobOffer offer = TestData.CreateJobOffer(applicationId: applicationId);
        _offerRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(offer);

        UpdateOfferRequest request = new()
        {
            Salary = 130000m,
            Benefits = "Full health coverage"
        };

        // Act
        JobOfferResponse result = await _service.UpdateOfferAsync(
            applicationId, request, CancellationToken.None);

        // Assert
        result.Salary.Should().Be(130000m);
        result.Benefits.Should().Be("Full health coverage");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateOffer_NotDraft_ThrowsConflict()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        JobOffer offer = TestData.CreateJobOffer(
            applicationId: applicationId, status: OfferStatus.Pending);
        _offerRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(offer);

        // Act
        Func<Task> act = () => _service.UpdateOfferAsync(
            applicationId, new UpdateOfferRequest(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(409);
    }

    // ── ExtendOfferAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ExtendOffer_Draft_MovesPendingAndPublishesEvent()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        JobOffer offer = TestData.CreateJobOffer(applicationId: applicationId);
        _offerRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(offer);

        // Act
        JobOfferResponse result = await _service.ExtendOfferAsync(
            applicationId, CancellationToken.None);

        // Assert
        result.Status.Should().Be(OfferStatus.Pending);
        result.ExtendedAt.Should().NotBeNull();
        await _dispatcher.Received(1).DispatchAsync(
            Arg.Any<OfferExtendedEvent>(), Arg.Any<CancellationToken>());
        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Offered", null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExtendOffer_NotDraft_ThrowsConflict()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        JobOffer offer = TestData.CreateJobOffer(
            applicationId: applicationId, status: OfferStatus.Pending);
        _offerRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(offer);

        // Act
        Func<Task> act = () => _service.ExtendOfferAsync(
            applicationId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(409);
    }

    // ── RespondToOfferAsync ──────────────────────────────────────────────

    [Fact]
    public async Task RespondToOffer_Accept_MovesToAcceptedAndHired()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        JobOffer offer = TestData.CreateJobOffer(
            applicationId: applicationId, status: OfferStatus.Pending);
        _offerRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(offer);

        RespondToOfferRequest request = new() { Accepted = true };

        // Act
        JobOfferResponse result = await _service.RespondToOfferAsync(
            applicationId, request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(OfferStatus.Accepted);
        result.RespondedAt.Should().NotBeNull();
        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Hired", null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RespondToOffer_Decline_MovesToDeclinedAndRejected()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        JobOffer offer = TestData.CreateJobOffer(
            applicationId: applicationId, status: OfferStatus.Pending);
        _offerRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(offer);

        RespondToOfferRequest request = new()
        {
            Accepted = false,
            DeclineReason = "Accepted another offer"
        };

        // Act
        JobOfferResponse result = await _service.RespondToOfferAsync(
            applicationId, request, CancellationToken.None);

        // Assert
        result.Status.Should().Be(OfferStatus.Declined);
        result.DeclineReason.Should().Be("Accepted another offer");
        await _statusUpdater.Received(1).UpdateStatusAsync(
            applicationId, "Rejected", "Accepted another offer", "Offered",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RespondToOffer_NotPending_ThrowsConflict()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        JobOffer offer = TestData.CreateJobOffer(
            applicationId: applicationId, status: OfferStatus.Draft);
        _offerRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(offer);

        // Act
        Func<Task> act = () => _service.RespondToOfferAsync(
            applicationId, new RespondToOfferRequest { Accepted = true }, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(409);
    }

    // ── WithdrawOfferAsync ───────────────────────────────────────────────

    [Fact]
    public async Task WithdrawOffer_DraftOrPending_SetsWithdrawn()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        JobOffer offer = TestData.CreateJobOffer(
            applicationId: applicationId, status: OfferStatus.Pending);
        _offerRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(offer);

        WithdrawOfferRequest request = new() { WithdrawalReason = "Position eliminated" };

        // Act
        await _service.WithdrawOfferAsync(applicationId, request, CancellationToken.None);

        // Assert
        offer.Status.Should().Be(OfferStatus.Withdrawn);
        offer.WithdrawnAt.Should().NotBeNull();
        offer.WithdrawalReason.Should().Be("Position eliminated");
    }

    [Fact]
    public async Task WithdrawOffer_AlreadyAccepted_ThrowsConflict()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        JobOffer offer = TestData.CreateJobOffer(
            applicationId: applicationId, status: OfferStatus.Accepted);
        _offerRepo.GetByApplicationIdForUpdateAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(offer);

        // Act
        Func<Task> act = () => _service.WithdrawOfferAsync(
            applicationId, new WithdrawOfferRequest(), CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.StatusCode.Should().Be(409);
    }
}

using Jobsite.Modules.HRWorkflows.Application.EventHandlers;
using Jobsite.Modules.HRWorkflows.Domain.Constants;
using Jobsite.Modules.HRWorkflows.Domain.Entities;
using Jobsite.Modules.HRWorkflows.Domain.Interfaces;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Jobsite.UnitTests.HRWorkflows;

public sealed class CandidateShortlistedHRWorkflowsHandlerTests
{
    private readonly IFinalInterviewRepository _interviewRepo = Substitute.For<IFinalInterviewRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CandidateShortlistedHRWorkflowsHandler _handler;

    public CandidateShortlistedHRWorkflowsHandlerTests()
    {
        _handler = new CandidateShortlistedHRWorkflowsHandler(
            _interviewRepo,
            _unitOfWork,
            Substitute.For<ILogger<CandidateShortlistedHRWorkflowsHandler>>());
    }

    [Fact]
    public async Task Handle_NewApplication_CreatesPlaceholderInterview()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        _interviewRepo.GetByApplicationIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns((FinalInterview?)null);

        CandidateShortlistedEvent @event = new()
        {
            ApplicationId = applicationId,
            JobPostingId = Guid.NewGuid(),
            ApplicantUserId = Guid.NewGuid(),
            ShortlistedAt = DateTime.UtcNow
        };

        // Act
        await _handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        _interviewRepo.Received(1).Add(Arg.Is<FinalInterview>(i =>
            i.ApplicationId == applicationId
            && i.Status == InterviewStatus.Scheduled
            && i.InterviewType == InterviewType.Video
            && i.DurationMinutes == 60));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyExists_SkipsCreation()
    {
        // Arrange
        Guid applicationId = Guid.NewGuid();
        _interviewRepo.GetByApplicationIdAsync(applicationId, Arg.Any<CancellationToken>())
            .Returns(TestData.CreateFinalInterview(applicationId: applicationId));

        CandidateShortlistedEvent @event = new()
        {
            ApplicationId = applicationId,
            JobPostingId = Guid.NewGuid(),
            ApplicantUserId = Guid.NewGuid(),
            ShortlistedAt = DateTime.UtcNow
        };

        // Act
        await _handler.HandleAsync(@event, CancellationToken.None);

        // Assert
        _interviewRepo.DidNotReceive().Add(Arg.Any<FinalInterview>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

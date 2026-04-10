using Jobsite.Modules.Profiles.Application.EventHandlers;
using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using NSubstitute;

namespace Jobsite.UnitTests.Profiles;

public sealed class UserRegisteredProfileHandlerTests
{
    private readonly IApplicantProfileRepository _profileRepository = Substitute.For<IApplicantProfileRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly UserRegisteredProfileHandler _sut;

    public UserRegisteredProfileHandlerTests()
    {
        _sut = new UserRegisteredProfileHandler(_profileRepository, _unitOfWork);
    }

    [Fact]
    public async Task Handle_ApplicantRole_CreatesEmptyProfile()
    {
        // Arrange
        UserRegisteredEvent notification = new()
        {
            UserId = Guid.NewGuid(),
            Email = "applicant@example.com",
            Role = "Applicant",
            RegisteredAt = DateTime.UtcNow
        };
        _profileRepository.ExistsByUserIdAsync(notification.UserId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        await _sut.HandleAsync(notification, CancellationToken.None);

        // Assert
        _profileRepository.Received(1).Add(Arg.Is<ApplicantProfile>(p =>
            p.Id == notification.UserId &&
            p.FirstName == string.Empty &&
            p.LastName == string.Empty));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("AgencyAdmin")]
    [InlineData("HiringManager")]
    [InlineData("Recruiter")]
    public async Task Handle_NonApplicantRole_SkipsProfileCreation(string role)
    {
        // Arrange
        UserRegisteredEvent notification = new()
        {
            UserId = Guid.NewGuid(),
            Email = "admin@example.com",
            Role = role,
            RegisteredAt = DateTime.UtcNow
        };

        // Act
        await _sut.HandleAsync(notification, CancellationToken.None);

        // Assert
        _profileRepository.DidNotReceive().Add(Arg.Any<ApplicantProfile>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ProfileAlreadyExists_SkipsCreation()
    {
        // Arrange
        UserRegisteredEvent notification = new()
        {
            UserId = Guid.NewGuid(),
            Email = "applicant@example.com",
            Role = "Applicant",
            RegisteredAt = DateTime.UtcNow
        };
        _profileRepository.ExistsByUserIdAsync(notification.UserId, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _sut.HandleAsync(notification, CancellationToken.None);

        // Assert
        _profileRepository.DidNotReceive().Add(Arg.Any<ApplicantProfile>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

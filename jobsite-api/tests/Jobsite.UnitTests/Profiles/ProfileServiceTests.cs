using FluentAssertions;
using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Application.Services;
using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.SharedKernel.Errors;
using Jobsite.SharedKernel.Persistence;
using NSubstitute;

namespace Jobsite.UnitTests.Profiles;

public sealed class ProfileServiceTests
{
    private readonly IApplicantProfileRepository _profileRepository = Substitute.For<IApplicantProfileRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ProfileService _sut;

    public ProfileServiceTests()
    {
        _sut = new ProfileService(_profileRepository, _unitOfWork);
    }

    // ── GetByUserIdAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetByUserIdAsync_ProfileExists_ReturnsProfileResponse()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ApplicantProfile profile = TestData.CreateApplicantProfile(id: userId);
        _profileRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(profile);

        // Act
        Modules.Profiles.Application.DTOs.ProfileResponse result =
            await _sut.GetByUserIdAsync(userId, CancellationToken.None);

        // Assert
        result.UserId.Should().Be(userId);
        result.FirstName.Should().Be(profile.FirstName);
        result.LastName.Should().Be(profile.LastName);
    }

    [Fact]
    public async Task GetByUserIdAsync_ProfileNotFound_ThrowsAppError()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        _profileRepository.GetByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((ApplicantProfile?)null);

        // Act
        Func<Task> act = () => _sut.GetByUserIdAsync(userId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("PROFILE_NOT_FOUND");
    }

    // ── CreateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesAndReturnsProfile()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Modules.Profiles.Application.DTOs.CreateProfileRequest request = TestData.CreateProfileRequest();
        _profileRepository.ExistsByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        Modules.Profiles.Application.DTOs.ProfileResponse result =
            await _sut.CreateAsync(request, userId, CancellationToken.None);

        // Assert
        result.UserId.Should().Be(userId);
        result.FirstName.Should().Be(request.FirstName);
        result.LastName.Should().Be(request.LastName);
        _profileRepository.Received(1).Add(Arg.Is<ApplicantProfile>(p =>
            p.Id == userId &&
            p.FirstName == request.FirstName));
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ProfileAlreadyExists_ThrowsAppError()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        Modules.Profiles.Application.DTOs.CreateProfileRequest request = TestData.CreateProfileRequest();
        _profileRepository.ExistsByUserIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        Func<Task> act = () => _sut.CreateAsync(request, userId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("PROFILE_ALREADY_EXISTS");
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ValidRequest_UpdatesOnlyProvidedFields()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        ApplicantProfile profile = TestData.CreateApplicantProfile(id: userId, firstName: "Old", lastName: "Name");
        _profileRepository.GetByUserIdForUpdateAsync(userId, Arg.Any<CancellationToken>())
            .Returns(profile);

        Modules.Profiles.Application.DTOs.UpdateProfileRequest request = new()
        {
            FirstName = "New"
        };

        // Act
        Modules.Profiles.Application.DTOs.ProfileResponse result =
            await _sut.UpdateAsync(request, userId, CancellationToken.None);

        // Assert
        result.FirstName.Should().Be("New");
        result.LastName.Should().Be("Name"); // unchanged
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ProfileNotFound_ThrowsAppError()
    {
        // Arrange
        Guid userId = Guid.NewGuid();
        _profileRepository.GetByUserIdForUpdateAsync(userId, Arg.Any<CancellationToken>())
            .Returns((ApplicantProfile?)null);

        Modules.Profiles.Application.DTOs.UpdateProfileRequest request = TestData.CreateUpdateProfileRequest(firstName: "New");

        // Act
        Func<Task> act = () => _sut.UpdateAsync(request, userId, CancellationToken.None);

        // Assert
        AppError error = (await act.Should().ThrowAsync<AppError>()).Which;
        error.Code.Should().Be("PROFILE_NOT_FOUND");
    }
}

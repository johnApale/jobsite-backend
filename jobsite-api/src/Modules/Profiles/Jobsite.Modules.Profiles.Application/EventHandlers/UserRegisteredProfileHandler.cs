using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.Profiles.Application.EventHandlers;

/// <summary>
/// Creates an empty <see cref="ApplicantProfile"/> when a user registers with the "Applicant" role.
/// Runs in the same transaction as the Auth module's SaveChangesAsync (via MediatR dispatch).
/// </summary>
public sealed class UserRegisteredProfileHandler : INotificationHandler<UserRegisteredEvent>
{
    private readonly IApplicantProfileRepository _profileRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UserRegisteredProfileHandler(
        IApplicantProfileRepository profileRepository,
        [FromKeyedServices("profiles")] IUnitOfWork unitOfWork)
    {
        _profileRepository = profileRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UserRegisteredEvent notification, CancellationToken ct)
    {
        if (notification.Role is not "Applicant")
            return;

        bool exists = await _profileRepository.ExistsByUserIdAsync(notification.UserId, ct);

        if (exists)
            return;

        ApplicantProfile profile = new()
        {
            Id = notification.UserId,
            FirstName = string.Empty,
            LastName = string.Empty
        };

        _profileRepository.Add(profile);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}

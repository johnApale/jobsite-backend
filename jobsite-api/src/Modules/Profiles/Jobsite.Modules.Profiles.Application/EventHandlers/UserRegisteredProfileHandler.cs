using Jobsite.Modules.Profiles.Application.Interfaces;
using Jobsite.Modules.Profiles.Domain.Entities;
using Jobsite.SharedKernel.Domain;
using Jobsite.SharedKernel.Events;
using Jobsite.SharedKernel.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.Profiles.Application.EventHandlers;

/// <summary>
/// Creates an empty <see cref="ApplicantProfile"/> when a user registers with the "Applicant" role.
/// Runs in the same transaction as the Auth module's SaveChangesAsync (via domain event dispatch).
/// </summary>
public sealed class UserRegisteredProfileHandler : IDomainEventHandler<UserRegisteredEvent>
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

    public async Task HandleAsync(UserRegisteredEvent domainEvent, CancellationToken ct)
    {
        if (domainEvent.Role is not "Applicant")
            return;

        bool exists = await _profileRepository.ExistsByUserIdAsync(domainEvent.UserId, ct);

        if (exists)
            return;

        ApplicantProfile profile = new()
        {
            Id = domainEvent.UserId,
            FirstName = string.Empty,
            LastName = string.Empty
        };

        _profileRepository.Add(profile);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}

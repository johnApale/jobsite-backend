using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Auth.Domain.Entities;

/// <summary>
/// Maps a user to an OAuth provider identity — maps to <c>auth.user_external_logins</c>.
/// A user can have multiple linked providers (Google + Apple, etc.).
/// The provider's subject ID is the stable identifier, not the email.
/// </summary>
public sealed class UserExternalLogin : Entity
{
    /// <summary>The local user this external identity belongs to.</summary>
    public Guid UserId { get; set; }

    /// <summary>OAuth provider: Google, Apple, Facebook.</summary>
    public string Provider { get; set; } = null!;

    /// <summary>The <c>sub</c> claim from the provider's ID token. Stable, unique per user per provider.</summary>
    public string ProviderSubjectId { get; set; } = null!;

    /// <summary>Email from the provider at time of linking. Informational only.</summary>
    public string? ProviderEmail { get; set; }

    /// <summary>Name from the provider at time of linking. Informational only.</summary>
    public string? ProviderDisplayName { get; set; }

    /// <summary>When this provider was linked to the user account.</summary>
    public DateTime LinkedAt { get; set; }

    /// <summary>Navigation property back to the owning user.</summary>
    public User User { get; set; } = null!;
}

using Jobsite.SharedKernel.Domain;

namespace Jobsite.Modules.Auth.Domain.Entities;

/// <summary>
/// Every person who can log into this tenant's portal — maps to <c>auth.users</c>.
/// Staff are created via admin invite. Applicants self-register.
/// Supports email/password and OAuth authentication.
/// </summary>
public sealed class User : AggregateRoot
{
    /// <summary>Login identifier. Unique per tenant (per database).</summary>
    public string Email { get; set; } = null!;

    /// <summary>BCrypt hash. NULL for OAuth-only users.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>True if verified via email confirmation or OAuth provider.</summary>
    public bool EmailVerified { get; set; }

    /// <summary>Exactly one role: Applicant, Recruiter, HiringManager, Interviewer, AgencyAdmin.</summary>
    public string Role { get; set; } = null!;

    /// <summary>Lifecycle status: Active, Invited, Deactivated.</summary>
    public string Status { get; set; } = null!;

    /// <summary>User's first name.</summary>
    public string FirstName { get; set; } = null!;

    /// <summary>User's last name.</summary>
    public string LastName { get; set; } = null!;

    /// <summary>Profile picture URL. Initially populated from OAuth provider if available.</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>The admin/manager who created this account. NULL for self-registered and seeded users.</summary>
    public Guid? InvitedBy { get; set; }

    /// <summary>Updated on each successful authentication.</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Set when status moves to Deactivated.</summary>
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>Number of consecutive failed login attempts. Reset on successful login.</summary>
    public int FailedLoginAttempts { get; set; }

    /// <summary>Account is locked until this time. NULL if not locked.</summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>Token for email verification. NULL after verified.</summary>
    public string? EmailVerificationToken { get; set; }

    /// <summary>When the verification token expires.</summary>
    public DateTime? EmailVerificationTokenExpiresAt { get; set; }

    /// <summary>Token for password reset. NULL when not in reset flow.</summary>
    public string? PasswordResetToken { get; set; }

    /// <summary>When the password reset token expires.</summary>
    public DateTime? PasswordResetTokenExpiresAt { get; set; }

    /// <summary>OAuth provider links for this user.</summary>
    public List<UserExternalLogin> ExternalLogins { get; set; } = [];

    /// <summary>Refresh tokens for this user's sessions.</summary>
    public List<RefreshToken> RefreshTokens { get; set; } = [];

    /// <summary>Raise a domain event from this aggregate root.</summary>
    public void Raise(IDomainEvent domainEvent) => RaiseDomainEvent(domainEvent);
}

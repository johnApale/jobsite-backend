using Jobsite.Modules.Auth.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Jobsite.Modules.Auth.Api;

/// <summary>
/// Role-based authorization policies for the Auth module.
/// </summary>
public static class AuthorizationPolicies
{
    public const string RequireApplicant = "RequireApplicant";
    public const string RequireRecruiter = "RequireRecruiter";
    public const string RequireHiringManager = "RequireHiringManager";
    public const string RequireInterviewer = "RequireInterviewer";
    public const string RequireAgencyAdmin = "RequireAgencyAdmin";

    /// <summary>
    /// Register role-based authorization policies.
    /// </summary>
    public static AuthorizationBuilder AddAuthModulePolicies(this AuthorizationBuilder builder)
    {
        builder.AddPolicy(RequireApplicant, policy =>
            policy.RequireClaim("role", UserRole.Applicant));

        builder.AddPolicy(RequireRecruiter, policy =>
            policy.RequireClaim("role", UserRole.Recruiter));

        builder.AddPolicy(RequireHiringManager, policy =>
            policy.RequireClaim("role", UserRole.HiringManager));

        builder.AddPolicy(RequireInterviewer, policy =>
            policy.RequireClaim("role", UserRole.Interviewer));

        builder.AddPolicy(RequireAgencyAdmin, policy =>
            policy.RequireClaim("role", UserRole.AgencyAdmin));

        return builder;
    }
}

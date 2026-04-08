using System.Security.Claims;
using Jobsite.Modules.Auth.Application.DTOs;
using Jobsite.Modules.Auth.Application.Interfaces;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Jobsite.Modules.Auth.Api;

/// <summary>
/// Minimal API endpoint definitions for the Auth module.
/// Route prefix: <c>/api/v1/auth</c>.
/// </summary>
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/auth")
            .WithTags("Auth")
            .RequireRateLimiting("auth");

        group.MapPost("/register", async (RegisterRequest request, IAuthService service, HttpContext http, CancellationToken ct) =>
            {
                Guid tenantId = GetTenantId(http);
                AuthTokensResponse response = await service.RegisterAsync(request, tenantId, ct);
                return Results.Created("/api/v1/auth/me", response);
            })
            .WithName("Register")
            .WithSummary("Register a new user")
            .WithDescription("Creates a new user with email/password credentials. Returns JWT access token and refresh token.")
            .Produces<AuthTokensResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/login", async (LoginRequest request, IAuthService service, HttpContext http, CancellationToken ct) =>
            {
                Guid tenantId = GetTenantId(http);
                AuthTokensResponse response = await service.LoginAsync(request, tenantId, ct);
                return Results.Ok(response);
            })
            .WithName("Login")
            .WithSummary("Login with email and password")
            .WithDescription("Validates credentials and returns JWT access token and refresh token.")
            .Produces<AuthTokensResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", async (RefreshTokenRequest request, IAuthService service, HttpContext http, CancellationToken ct) =>
            {
                Guid tenantId = GetTenantId(http);
                AuthTokensResponse response = await service.RefreshTokenAsync(request, tenantId, ct);
                return Results.Ok(response);
            })
            .WithName("RefreshToken")
            .WithSummary("Refresh access token")
            .WithDescription("Exchanges a valid refresh token for new access and refresh tokens. Implements token rotation with replay detection.")
            .Produces<AuthTokensResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/oauth/{provider}", async (string provider, OAuthLoginRequest request, IAuthService service, HttpContext http, CancellationToken ct) =>
            {
                Guid tenantId = GetTenantId(http);
                AuthTokensResponse response = await service.OAuthLoginAsync(provider, request, tenantId, ct);
                return Results.Ok(response);
            })
            .WithName("OAuthLogin")
            .WithSummary("Login or register via OAuth provider")
            .WithDescription("Authenticates via Google, Apple, or Facebook. Creates a new user if not found, or links to existing account if email matches.")
            .Produces<AuthTokensResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", async (RefreshTokenRequest request, IAuthService service, CancellationToken ct) =>
            {
                await service.LogoutAsync(request, ct);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithName("Logout")
            .WithSummary("Logout and revoke refresh token")
            .WithDescription("Revokes the specified refresh token. Idempotent — succeeds even if the token is already revoked.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", async (IAuthService service, HttpContext http, CancellationToken ct) =>
            {
                Guid userId = GetUserId(http);
                UserResponse response = await service.GetCurrentUserAsync(userId, ct);
                return Results.Ok(response);
            })
            .RequireAuthorization()
            .WithName("GetCurrentUser")
            .WithSummary("Get current authenticated user")
            .WithDescription("Returns the profile of the currently authenticated user.")
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/verify-email", async (VerifyEmailRequest request, IAuthService service, CancellationToken ct) =>
            {
                await service.VerifyEmailAsync(request, ct);
                return Results.NoContent();
            })
            .WithName("VerifyEmail")
            .WithSummary("Verify email address")
            .WithDescription("Verifies a user's email address using the token sent during registration.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/resend-verification", async (ResendVerificationRequest request, IAuthService service, CancellationToken ct) =>
            {
                await service.ResendVerificationEmailAsync(request, ct);
                return Results.NoContent();
            })
            .WithName("ResendVerificationEmail")
            .WithSummary("Resend verification email")
            .WithDescription("Resends the email verification token. Silent success if email not found or already verified.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/forgot-password", async (ForgotPasswordRequest request, IAuthService service, CancellationToken ct) =>
            {
                await service.ForgotPasswordAsync(request, ct);
                return Results.NoContent();
            })
            .WithName("ForgotPassword")
            .WithSummary("Request password reset")
            .WithDescription("Sends a password reset token via email. Silent success if email not found.")
            .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/reset-password", async (ResetPasswordRequest request, IAuthService service, CancellationToken ct) =>
            {
                await service.ResetPasswordAsync(request, ct);
                return Results.NoContent();
            })
            .WithName("ResetPassword")
            .WithSummary("Reset password")
            .WithDescription("Resets the user's password using a valid reset token. Clears lockout state.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest);
    }

    private static Guid GetTenantId(HttpContext http)
    {
        object? tenantId = http.Items["TenantId"];
        if (tenantId is Guid id)
            return id;

        throw new InvalidOperationException("TenantId not found in request context. Ensure TenantResolutionMiddleware is configured.");
    }

    private static Guid GetUserId(HttpContext http)
    {
        string? sub = http.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? http.User.FindFirstValue("sub");

        if (sub is not null && Guid.TryParse(sub, out Guid userId))
            return userId;

        throw new InvalidOperationException("User ID not found in JWT claims.");
    }
}

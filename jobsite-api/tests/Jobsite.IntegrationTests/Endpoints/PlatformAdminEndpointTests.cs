using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Jobsite.Modules.Tenancy.Application.DTOs;

namespace Jobsite.IntegrationTests.Endpoints;

/// <summary>
/// HTTP pipeline tests for the platform admin tenant creation endpoint
/// (<c>POST /api/v1/platform/tenants</c>). Requires <c>RequirePlatformAdmin</c> JWT policy.
/// </summary>
[Collection("Endpoints")]
public sealed class PlatformAdminEndpointTests
{
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly JobsiteWebApplicationFactory _factory;

    public PlatformAdminEndpointTests(JobsiteWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RegisterTenant_PlatformAdminJwt_Returns201()
    {
        // Arrange
        HttpClient client = _factory.CreateClient();
        string token = TestJwtHelper.GenerateToken(
            Guid.NewGuid(), _factory.TestTenantId, role: "PlatformAdmin");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        object request = new
        {
            name = "Platform Created Corp",
            subdomain = $"platcreate{Guid.NewGuid():N}"[..12],
            owner_name = "Platform Admin",
            owner_email = "admin@platform.com"
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/platform/tenants", request, SnakeCaseOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        TenantResponse? body = await response.Content
            .ReadFromJsonAsync<TenantResponse>(SnakeCaseOptions);
        body.Should().NotBeNull();
        body!.Name.Should().Be("Platform Created Corp");
    }

    [Fact]
    public async Task RegisterTenant_WithoutAuth_Returns401()
    {
        // Arrange — no authorization header
        HttpClient client = _factory.CreateClient();
        object request = new
        {
            name = "No Auth Corp",
            subdomain = "noauth",
            owner_name = "Owner",
            owner_email = "owner@noauth.com"
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/platform/tenants", request, SnakeCaseOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegisterTenant_NonAdminRole_Returns403()
    {
        // Arrange — authenticated as Applicant, not PlatformAdmin
        HttpClient client = _factory.CreateClient();
        string token = TestJwtHelper.GenerateToken(
            Guid.NewGuid(), _factory.TestTenantId, role: "Applicant");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        object request = new
        {
            name = "Forbidden Corp",
            subdomain = "forbidden",
            owner_name = "Owner",
            owner_email = "owner@forbidden.com"
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/platform/tenants", request, SnakeCaseOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RegisterTenant_DuplicateSubdomain_ReturnsClientError()
    {
        // Arrange — the test tenant's subdomain is already taken
        HttpClient client = _factory.CreateClient();
        string token = TestJwtHelper.GenerateToken(
            Guid.NewGuid(), _factory.TestTenantId, role: "PlatformAdmin");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        object request = new
        {
            name = "Duplicate Corp",
            subdomain = JobsiteWebApplicationFactory.TestTenantSubdomain,
            owner_name = "Owner",
            owner_email = "owner@dup.com"
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/platform/tenants", request, SnakeCaseOptions);

        // Assert — returns 400 (validation) or 409 (conflict) depending on error handling
        int statusCode = (int)response.StatusCode;
        statusCode.Should().BeOneOf(400, 409);
    }
}

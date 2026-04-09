using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace Jobsite.IntegrationTests.Endpoints;

/// <summary>
/// Smoke tests for the health endpoint — validates the <see cref="JobsiteWebApplicationFactory"/>
/// boots the full application correctly and can serve HTTP requests.
/// </summary>
[Collection("Endpoints")]
public sealed class HealthEndpointTests
{
    private readonly JobsiteWebApplicationFactory _factory;

    public HealthEndpointTests(JobsiteWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        // Arrange — /health is a non-tenant route (no Host header needed)
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_ReturnsJsonWithHealthyStatus()
    {
        // Arrange
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/health");
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonDocument doc = JsonDocument.Parse(content);
        doc.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task TenantRoute_WithoutHostHeader_Returns400()
    {
        // Arrange — request to a tenant-scoped route without a valid subdomain host
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/auth/me");

        // Assert — TenantResolutionMiddleware rejects: no subdomain on localhost
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TenantRoute_WithValidHost_DoesNotReturn400()
    {
        // Arrange — request with the test tenant subdomain
        HttpClient client = _factory.CreateTenantClient();

        // Act — /me requires auth, so we expect 401 (not 400 from tenant resolution)
        HttpResponseMessage response = await client.GetAsync("/api/v1/auth/me");

        // Assert — gets past tenant resolution (not 400), hits auth (401)
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TenantRoute_WithUnknownSubdomain_Returns404()
    {
        // Arrange — unknown tenant subdomain
        HttpClient client = _factory.CreateTenantClient("nonexistent");

        // Act
        HttpResponseMessage response = await client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        string content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("TENANT_NOT_FOUND");
    }
}

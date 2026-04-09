using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Jobsite.Modules.Tenancy.Application.DTOs;

namespace Jobsite.IntegrationTests.Endpoints;

/// <summary>
/// HTTP pipeline tests for Tenancy module endpoints via <see cref="JobsiteWebApplicationFactory"/>.
/// Tenant endpoints are non-tenant-scoped (no subdomain required).
/// </summary>
[Collection("Endpoints")]
public sealed class TenantEndpointTests
{
    private static readonly JsonSerializerOptions SnakeCaseOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly JobsiteWebApplicationFactory _factory;

    public TenantEndpointTests(JobsiteWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetTenantById_ExistingTenant_Returns200()
    {
        // Arrange — use the pre-seeded test tenant
        HttpClient client = _factory.CreateClient();
        Guid tenantId = _factory.TestTenantId;

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/v1/tenants/{tenantId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        TenantResponse? body = await response.Content
            .ReadFromJsonAsync<TenantResponse>(SnakeCaseOptions);
        body.Should().NotBeNull();
        body!.Id.Should().Be(tenantId);
        body.Name.Should().Be(JobsiteWebApplicationFactory.TestTenantName);
        body.Subdomain.Should().Be(JobsiteWebApplicationFactory.TestTenantSubdomain);
        body.Status.Should().Be("Active");
    }

    [Fact]
    public async Task GetTenantById_NonExistentTenant_Returns404()
    {
        // Arrange
        HttpClient client = _factory.CreateClient();
        Guid fakeId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/v1/tenants/{fakeId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTenantById_ResponseUsesSnakeCaseJson()
    {
        // Arrange
        HttpClient client = _factory.CreateClient();
        Guid tenantId = _factory.TestTenantId;

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/v1/tenants/{tenantId}");
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        JsonDocument doc = JsonDocument.Parse(content);
        JsonElement root = doc.RootElement;

        root.TryGetProperty("id", out _).Should().BeTrue();
        root.TryGetProperty("name", out _).Should().BeTrue();
        root.TryGetProperty("subdomain", out _).Should().BeTrue();
        root.TryGetProperty("owner_name", out _).Should().BeTrue();
        root.TryGetProperty("owner_email", out _).Should().BeTrue();
        root.TryGetProperty("contact_name", out _).Should().BeTrue();
        root.TryGetProperty("contact_email", out _).Should().BeTrue();
    }

    [Fact]
    public async Task RegisterTenant_ValidRequest_Returns201()
    {
        // Arrange
        HttpClient client = _factory.CreateClient();
        object request = new
        {
            name = "New Tenant Corp",
            subdomain = $"newtenant{Guid.NewGuid():N}"[..12],
            owner_name = "New Owner",
            owner_email = "owner@newtenant.com"
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/tenants/register", request, SnakeCaseOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        TenantResponse? body = await response.Content
            .ReadFromJsonAsync<TenantResponse>(SnakeCaseOptions);
        body.Should().NotBeNull();
        body!.Name.Should().Be("New Tenant Corp");
    }

    [Fact]
    public async Task RegisterTenant_DuplicateSubdomain_ReturnsClientError()
    {
        // Arrange — the test tenant's subdomain is already taken
        HttpClient client = _factory.CreateClient();
        object request = new
        {
            name = "Duplicate Corp",
            subdomain = JobsiteWebApplicationFactory.TestTenantSubdomain,
            owner_name = "Owner",
            owner_email = "owner@dup.com"
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/tenants/register", request, SnakeCaseOptions);

        // Assert — returns 400 (validation) or 409 (conflict) depending on error handling
        int statusCode = (int)response.StatusCode;
        statusCode.Should().BeOneOf(400, 409);
    }

    [Fact]
    public async Task TenantRoutes_AreNonTenantScoped_NoHostHeaderRequired()
    {
        // Arrange — plain localhost client, no subdomain
        HttpClient client = _factory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/api/v1/tenants/{_factory.TestTenantId}");

        // Assert — should NOT get 400 "Unable to resolve tenant"
        response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);
    }
}

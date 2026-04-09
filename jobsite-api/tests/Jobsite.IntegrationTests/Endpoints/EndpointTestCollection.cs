namespace Jobsite.IntegrationTests.Endpoints;

/// <summary>
/// xUnit collection definition for sharing a single <see cref="JobsiteWebApplicationFactory"/>
/// across all endpoint test classes. Tests share one PostgreSQL container and app instance.
/// </summary>
[CollectionDefinition("Endpoints")]
public sealed class EndpointTestCollection : ICollectionFixture<JobsiteWebApplicationFactory>;

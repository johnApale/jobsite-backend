namespace Jobsite.IntegrationTests.Auth;

/// <summary>
/// xUnit collection definition for sharing the PostgreSQL container across all
/// Auth integration test classes. Tests in this collection share a single container
/// but get data isolation via <see cref="AuthIntegrationFixture.ResetDataAsync"/>.
/// </summary>
[CollectionDefinition("Auth")]
public sealed class AuthIntegrationCollection : ICollectionFixture<AuthIntegrationFixture>;

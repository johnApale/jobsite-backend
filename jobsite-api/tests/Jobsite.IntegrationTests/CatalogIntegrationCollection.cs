namespace Jobsite.IntegrationTests;

/// <summary>
/// xUnit collection definition for sharing the PostgreSQL container across all
/// integration test classes. Tests in this collection share a single container
/// but get data isolation via <see cref="CatalogIntegrationFixture.ResetDataAsync"/>.
/// </summary>
[CollectionDefinition("Catalog")]
public sealed class CatalogIntegrationCollection : ICollectionFixture<CatalogIntegrationFixture>;

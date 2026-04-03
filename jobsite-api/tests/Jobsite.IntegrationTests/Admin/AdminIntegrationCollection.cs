namespace Jobsite.IntegrationTests.Admin;

/// <summary>
/// xUnit collection definition for sharing the PostgreSQL container across all
/// Admin integration test classes.
/// </summary>
[CollectionDefinition("Admin")]
public sealed class AdminIntegrationCollection : ICollectionFixture<AdminIntegrationFixture>;

namespace Jobsite.IntegrationTests.Matching;

/// <summary>
/// xUnit collection definition for sharing the PostgreSQL container across all
/// Matching integration test classes. Tests in this collection share a single container
/// but get data isolation via <see cref="MatchingIntegrationFixture.ResetDataAsync"/>.
/// </summary>
[CollectionDefinition("Matching")]
public sealed class MatchingIntegrationCollection : ICollectionFixture<MatchingIntegrationFixture>;

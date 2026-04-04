namespace Jobsite.IntegrationTests.Screening;

/// <summary>
/// xUnit collection definition for sharing the PostgreSQL container across all
/// Screening integration test classes. Tests in this collection share a single container
/// but get data isolation via <see cref="ScreeningIntegrationFixture.ResetDataAsync"/>.
/// </summary>
[CollectionDefinition("Screening")]
public sealed class ScreeningIntegrationCollection : ICollectionFixture<ScreeningIntegrationFixture>;

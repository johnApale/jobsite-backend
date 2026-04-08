namespace Jobsite.IntegrationTests.E2E;

/// <summary>
/// xUnit collection definition for sharing the PostgreSQL container across all
/// E2E screening pipeline test classes.
/// </summary>
[CollectionDefinition("ScreeningPipeline")]
public sealed class ScreeningPipelineCollection : ICollectionFixture<ScreeningPipelineFixture>;

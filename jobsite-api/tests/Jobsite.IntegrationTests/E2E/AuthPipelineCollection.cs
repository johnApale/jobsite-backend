namespace Jobsite.IntegrationTests.E2E;

/// <summary>
/// xUnit collection definition for sharing the PostgreSQL container across all
/// E2E auth pipeline test classes.
/// </summary>
[CollectionDefinition("AuthPipeline")]
public sealed class AuthPipelineCollection : ICollectionFixture<AuthPipelineFixture>;

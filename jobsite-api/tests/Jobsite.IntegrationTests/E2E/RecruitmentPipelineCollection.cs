namespace Jobsite.IntegrationTests.E2E;

/// <summary>
/// xUnit collection definition for sharing the PostgreSQL container across all
/// E2E recruitment pipeline test classes.
/// </summary>
[CollectionDefinition("RecruitmentPipeline")]
public sealed class RecruitmentPipelineCollection : ICollectionFixture<RecruitmentPipelineFixture>;

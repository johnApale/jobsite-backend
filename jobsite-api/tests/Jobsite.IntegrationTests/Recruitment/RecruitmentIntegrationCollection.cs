namespace Jobsite.IntegrationTests.Recruitment;

/// <summary>
/// xUnit collection definition for sharing the PostgreSQL container across all
/// Recruitment integration test classes. Tests in this collection share a single container
/// but get data isolation via <see cref="RecruitmentIntegrationFixture.ResetDataAsync"/>.
/// </summary>
[CollectionDefinition("Recruitment")]
public sealed class RecruitmentIntegrationCollection : ICollectionFixture<RecruitmentIntegrationFixture>;

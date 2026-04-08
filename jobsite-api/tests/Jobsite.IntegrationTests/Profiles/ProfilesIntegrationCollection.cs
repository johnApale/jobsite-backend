namespace Jobsite.IntegrationTests.Profiles;

/// <summary>
/// xUnit collection definition for sharing the PostgreSQL container across all
/// Profiles integration test classes. Tests in this collection share a single container
/// but get data isolation via <see cref="ProfilesIntegrationFixture.ResetDataAsync"/>.
/// </summary>
[CollectionDefinition("Profiles")]
public sealed class ProfilesIntegrationCollection : ICollectionFixture<ProfilesIntegrationFixture>;

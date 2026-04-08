namespace Jobsite.IntegrationTests.Contracts;

/// <summary>
/// xUnit collection definition for sharing the WireMock server across all
/// AI Service contract test classes.
/// </summary>
[CollectionDefinition("AiServiceContract")]
public sealed class AiServiceContractCollection : ICollectionFixture<AiServiceContractFixture>;

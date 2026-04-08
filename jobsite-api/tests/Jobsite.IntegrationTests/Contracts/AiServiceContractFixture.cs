using WireMock.Server;

namespace Jobsite.IntegrationTests.Contracts;

/// <summary>
/// Shared fixture that starts a WireMock server for AI Service contract tests.
/// Verifies that .NET HTTP clients send correct requests and handle responses
/// from the Python AI Service according to the agreed-upon API contract.
/// </summary>
public sealed class AiServiceContractFixture : IAsyncLifetime
{
    public WireMockServer Server { get; private set; } = null!;

    public string BaseUrl => Server.Url!;

    public Task InitializeAsync()
    {
        Server = WireMockServer.Start();
        return Task.CompletedTask;
    }

    /// <summary>Resets all stub mappings between test classes for isolation.</summary>
    public void Reset() => Server.Reset();

    public Task DisposeAsync()
    {
        Server.Dispose();
        return Task.CompletedTask;
    }
}

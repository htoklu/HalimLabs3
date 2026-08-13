using HalimLabs.Models;
using HalimLabs.Services.Abstractions;

namespace HalimLabs.Services.Ai;

public sealed class ConnectionTester : IConnectionTester
{
    private readonly OpenAiCompatibleClient _openAiClient;

    public ConnectionTester(OpenAiCompatibleClient openAiClient)
    {
        _openAiClient = openAiClient;
    }

    public Task<ConnectionTestResult> TestAsync(ProviderConfig provider, CancellationToken cancellationToken = default)
    {
        return provider.Type switch
        {
            ProviderType.OpenAICompatible => _openAiClient.TestConnectionAsync(provider, cancellationToken),
            _ => Task.FromResult(ConnectionTestResult.Fail($"Unsupported provider type: {provider.Type}", TimeSpan.Zero))
        };
    }
}

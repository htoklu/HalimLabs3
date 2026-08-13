using HalimLabs.Models;

namespace HalimLabs.Services.Abstractions;

public interface IConnectionTester
{
    Task<ConnectionTestResult> TestAsync(ProviderConfig provider, CancellationToken cancellationToken = default);
}

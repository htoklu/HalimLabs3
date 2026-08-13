using HalimLabs.Models;

namespace HalimLabs.Services.Abstractions;

public interface IProviderRepository
{
    Task<IReadOnlyList<ProviderConfig>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAllAsync(IEnumerable<ProviderConfig> providers, CancellationToken cancellationToken = default);
}

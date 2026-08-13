using System.IO;
using System.Text.Json;
using HalimLabs.Configuration;
using HalimLabs.Models;
using HalimLabs.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace HalimLabs.Services.Persistence;

public sealed class ProviderRepository : IProviderRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;
    private readonly ILogger<ProviderRepository> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ProviderRepository(ILogger<ProviderRepository> logger)
    {
        _logger = logger;
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HalimLabs");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "providers.json");
    }

    public async Task<IReadOnlyList<ProviderConfig>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                var defaults = DefaultProviders.Create().ToList();
                await WriteUnsafeAsync(defaults, cancellationToken).ConfigureAwait(false);
                return defaults;
            }

            await using var stream = File.OpenRead(_filePath);
            var list = await JsonSerializer.DeserializeAsync<List<ProviderConfig>>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return list ?? DefaultProviders.Create().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load providers, using defaults");
            return DefaultProviders.Create().ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAllAsync(IEnumerable<ProviderConfig> providers, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteUnsafeAsync(providers.ToList(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task WriteUnsafeAsync(List<ProviderConfig> providers, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, providers, JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}

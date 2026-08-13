using System.IO;
using System.Text.Json;
using HalimLabs.Configuration;
using HalimLabs.Models;
using HalimLabs.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace HalimLabs.Services.Persistence;

public sealed class ImageSettingsRepository : IImageSettingsRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _folder;
    private readonly string _filePath;
    private readonly string _legacyFilePath;
    private readonly ILogger<ImageSettingsRepository> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ImageSettingsRepository(ILogger<ImageSettingsRepository> logger)
    {
        _logger = logger;
        _folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HalimLabs3");
        Directory.CreateDirectory(_folder);
        // Separate file so older single-model EXE builds cannot wipe multi-model settings.
        _filePath = Path.Combine(_folder, "image-models.json");
        _legacyFilePath = Path.Combine(_folder, "image-settings.json");
    }

    public async Task<ImageSettingsStore> GetAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
                TryImportHalimLabs2Keys();

            if (File.Exists(_filePath))
            {
                var store = await ReadStoreAsync(_filePath, cancellationToken).ConfigureAwait(false);
                EnsureEditModels(store);
                store.EnsureActive();
                await WriteUnsafeAsync(store, cancellationToken).ConfigureAwait(false);
                return store;
            }

            if (File.Exists(_legacyFilePath))
            {
                var migrated = await ReadStoreAsync(_legacyFilePath, cancellationToken).ConfigureAwait(false);
                migrated.EnsureActive();
                await WriteUnsafeAsync(migrated, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Migrated image settings to image-models.json");
                return migrated;
            }

            var defaults = CreateDefaults();
            await WriteUnsafeAsync(defaults, cancellationToken).ConfigureAwait(false);
            return defaults;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load image settings");
            return CreateDefaults();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(ImageSettingsStore store, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            store.EnsureActive();
            await WriteUnsafeAsync(store, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<ImageSettingsStore> ReadStoreAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return ParseStore(doc.RootElement);
    }

    private async Task WriteUnsafeAsync(ImageSettingsStore store, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, store, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static ImageSettingsStore ParseStore(JsonElement root)
    {
        if (root.TryGetProperty("profiles", out var profilesEl) && profilesEl.ValueKind == JsonValueKind.Array)
        {
            var store = new ImageSettingsStore
            {
                ActiveProfileId = root.TryGetProperty("activeProfileId", out var active)
                    ? active.GetString() ?? string.Empty
                    : string.Empty
            };

            foreach (var item in profilesEl.EnumerateArray())
            {
                var profile = JsonSerializer.Deserialize<ImageModelProfile>(item.GetRawText(), JsonOptions);
                if (profile is not null)
                    store.Profiles.Add(profile);
            }

            return store.Profiles.Count == 0 ? CreateDefaults() : store;
        }

        var legacy = JsonSerializer.Deserialize<LegacyImageSettings>(root.GetRawText(), JsonOptions);
        if (legacy is null)
            return CreateDefaults();

        var migrated = CreateDefaults();
        var existingKey = legacy.ApiKey?.Trim() ?? string.Empty;

        if (legacy.ApiBaseUrl?.Contains("flux", StringComparison.OrdinalIgnoreCase) == true ||
            legacy.Model?.Contains("flux", StringComparison.OrdinalIgnoreCase) == true)
        {
            var flux = migrated.Profiles.First(p => p.Model.Contains("flux", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(existingKey))
                flux.ApiKey = existingKey;
            if (!string.IsNullOrWhiteSpace(legacy.ApiBaseUrl))
                flux.ApiBaseUrl = legacy.ApiBaseUrl;
            if (!string.IsNullOrWhiteSpace(legacy.Model))
                flux.Model = legacy.Model;
            flux.Steps = legacy.Steps > 0 ? legacy.Steps : flux.Steps;
            flux.CfgScale = legacy.CfgScale > 0 ? legacy.CfgScale : flux.CfgScale;
            flux.Seed = legacy.Seed;
            flux.RandomSeed = legacy.RandomSeed;
            flux.ApiMode = legacy.ApiMode;
            migrated.ActiveProfileId = flux.Id;
        }
        else if (!string.IsNullOrWhiteSpace(legacy.ApiBaseUrl) || !string.IsNullOrWhiteSpace(legacy.Model))
        {
            var custom = new ImageModelProfile
            {
                Name = string.IsNullOrWhiteSpace(legacy.Model) ? "Imported Model" : legacy.Model!,
                ApiKey = existingKey,
                ApiBaseUrl = legacy.ApiBaseUrl ?? string.Empty,
                Model = legacy.Model ?? string.Empty,
                ApiMode = legacy.ApiMode,
                Steps = legacy.Steps > 0 ? legacy.Steps : 20,
                CfgScale = legacy.CfgScale > 0 ? legacy.CfgScale : 3.5,
                Seed = legacy.Seed,
                RandomSeed = legacy.RandomSeed
            };
            migrated.Profiles.Insert(0, custom);
            migrated.ActiveProfileId = custom.Id;
        }

        return migrated;
    }

    private void TryImportHalimLabs2Keys()
    {
        try
        {
            var lab2 = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HalimLabs2",
                "image-models.json");
            if (File.Exists(lab2))
            {
                File.Copy(lab2, _filePath, overwrite: false);
                _logger.LogInformation("Imported API keys from HalimLabs2");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not import HalimLabs2 settings");
        }
    }

    private static void EnsureEditModels(ImageSettingsStore store)
    {
        var key = store.Profiles.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.ApiKey))?.ApiKey
                  ?? string.Empty;

        if (!store.Profiles.Any(p => ImageModelPresets.LooksLikeKontext(p)))
            store.Profiles.Insert(Math.Min(1, store.Profiles.Count), ImageModelPresets.CreateFluxKontext(key));

        if (!store.Profiles.Any(p => ImageModelPresets.LooksLikeKlein(p)))
            store.Profiles.Add(ImageModelPresets.CreateFluxKlein(key));

        if (!store.Profiles.Any(p => ImageModelPresets.LooksLikeQwenEdit(p)))
            store.Profiles.Add(ImageModelPresets.CreateQwenImageEdit(key));
    }

    public static ImageSettingsStore CreateDefaults()
    {
        var flux = ImageModelPresets.CreateFluxDev();
        var kontext = ImageModelPresets.CreateFluxKontext();
        var klein = ImageModelPresets.CreateFluxKlein();
        var qwen = ImageModelPresets.CreateQwenHosted();
        qwen.Name = "Qwen-Image";
        var qwenEdit = ImageModelPresets.CreateQwenImageEdit();
        var qwenLocal = ImageModelPresets.CreateQwenSelfHost();

        return new ImageSettingsStore
        {
            Profiles = [flux, kontext, klein, qwen, qwenEdit, qwenLocal],
            ActiveProfileId = flux.Id
        };
    }

    private sealed class LegacyImageSettings
    {
        public string ApiKey { get; set; } = string.Empty;
        public string ApiBaseUrl { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public ImageApiMode ApiMode { get; set; }
        public int Steps { get; set; } = 20;
        public double CfgScale { get; set; } = 3.5;
        public int Seed { get; set; }
        public bool RandomSeed { get; set; } = true;
    }
}

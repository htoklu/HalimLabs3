using HalimLabs.Models;

namespace HalimLabs.Services.Abstractions;

public interface IImageGenerationService
{
    Task<ImageGenerationResult> GenerateAsync(
        ImageModelProfile profile,
        string prompt,
        IReadOnlyList<byte[]>? inputImages = null,
        CancellationToken cancellationToken = default);
}

public interface IPromptTranslationService
{
    Task<PromptTranslationResult> ToEnglishPromptAsync(
        string prompt,
        string apiKey,
        CancellationToken cancellationToken = default);
}

public interface IImageCaptionService
{
    Task<string?> BuildTryOnPromptAsync(
        IReadOnlyList<byte[]> images,
        string englishInstruction,
        string apiKey,
        CancellationToken cancellationToken = default);
}

public interface IImageSettingsRepository
{
    Task<ImageSettingsStore> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ImageSettingsStore store, CancellationToken cancellationToken = default);
}

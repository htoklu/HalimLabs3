namespace HalimLabs.Models;

public enum ImageApiMode
{
    NvidiaGenAi = 0,
    OpenAiImages = 1
}

public sealed class ImageModelProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Model";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public ImageApiMode ApiMode { get; set; } = ImageApiMode.NvidiaGenAi;
    public int Steps { get; set; } = 20;
    public double CfgScale { get; set; } = 3.5;
    public int Seed { get; set; }
    public bool RandomSeed { get; set; } = true;

    public ImageModelProfile Clone() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = Name,
        ApiKey = ApiKey,
        ApiBaseUrl = ApiBaseUrl,
        Model = Model,
        ApiMode = ApiMode,
        Steps = Steps,
        CfgScale = CfgScale,
        Seed = Seed,
        RandomSeed = RandomSeed
    };
}

public sealed class ImageSettingsStore
{
    public string ActiveProfileId { get; set; } = string.Empty;
    public List<ImageModelProfile> Profiles { get; set; } = [];

    public ImageModelProfile? GetActiveProfile()
    {
        if (Profiles.Count == 0)
            return null;

        var active = Profiles.FirstOrDefault(p =>
            string.Equals(p.Id, ActiveProfileId, StringComparison.OrdinalIgnoreCase));
        return active ?? Profiles[0];
    }

    public void EnsureActive()
    {
        if (Profiles.Count == 0)
            return;

        if (Profiles.All(p => !string.Equals(p.Id, ActiveProfileId, StringComparison.OrdinalIgnoreCase)))
            ActiveProfileId = Profiles[0].Id;
    }
}

public sealed class ImageGenerationResult
{
    public bool Success { get; init; }
    public byte[]? ImageBytes { get; init; }
    public string? ErrorMessage { get; init; }
    public string? MimeType { get; init; } = "image/png";
    public TimeSpan Duration { get; init; }
    public bool ContentFiltered { get; init; }

    public static ImageGenerationResult Ok(byte[] bytes, TimeSpan duration, string mime = "image/png") =>
        new() { Success = true, ImageBytes = bytes, Duration = duration, MimeType = mime };

    public static ImageGenerationResult Fail(string message, TimeSpan duration) =>
        new() { Success = false, ErrorMessage = message, Duration = duration };

    public static ImageGenerationResult Filtered(string message, TimeSpan duration) =>
        new() { Success = false, ErrorMessage = message, Duration = duration, ContentFiltered = true };
}

public sealed class PromptTranslationResult
{
    public required string Prompt { get; init; }
    public bool Translated { get; init; }
    public string Engine { get; init; } = string.Empty;
    public string? Error { get; init; }

    public static PromptTranslationResult Passthrough(string prompt) =>
        new() { Prompt = prompt, Translated = false, Engine = "none" };

    public static PromptTranslationResult Ok(string prompt, string engine) =>
        new() { Prompt = prompt, Translated = true, Engine = engine };

    public static PromptTranslationResult Fail(string original, string error) =>
        new() { Prompt = original, Translated = false, Engine = "failed", Error = error };
}

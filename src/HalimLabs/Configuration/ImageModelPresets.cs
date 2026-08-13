using HalimLabs.Models;

namespace HalimLabs.Configuration;

public static class ImageModelPresets
{
    public const string FluxDevUrl = "https://ai.api.nvidia.com/v1/genai/black-forest-labs/flux.1-dev";
    public const string FluxDevModel = "black-forest-labs/flux.1-dev";

    public const string FluxKontextUrl = "https://ai.api.nvidia.com/v1/genai/black-forest-labs/flux.1-kontext-dev";
    public const string FluxKontextModel = "black-forest-labs/flux.1-kontext-dev";

    public const string QwenModel = "qwen/qwen-image";
    public const string QwenHostedUrl = "https://ai.api.nvidia.com/v1/genai/qwen/qwen-image";
    public const string QwenEditModel = "qwen/qwen-image-edit";
    public const string QwenEditUrl = "https://ai.api.nvidia.com/v1/genai/qwen/qwen-image-edit";
    public const string QwenSelfHostUrl = "http://localhost:8000/v1/images/generations";

    public const int MaxImagesPerChat = 5;

    public static ImageModelProfile CreateFluxKlein(string? apiKey = null) => new()
    {
        Name = "FLUX.2-klein-4b (çoklu görsel)",
        ApiKey = apiKey ?? string.Empty,
        ApiBaseUrl = "https://ai.api.nvidia.com/v1/genai/black-forest-labs/flux.2-klein-4b",
        Model = "black-forest-labs/flux.2-klein-4b",
        ApiMode = ImageApiMode.NvidiaGenAi,
        Steps = 4,
        CfgScale = 1,
        RandomSeed = true
    };

    public static ImageModelProfile CreateFluxDev(string? apiKey = null) => new()
    {
        Name = "FLUX.1-dev",
        ApiKey = apiKey ?? string.Empty,
        ApiBaseUrl = FluxDevUrl,
        Model = FluxDevModel,
        ApiMode = ImageApiMode.NvidiaGenAi,
        Steps = 20,
        CfgScale = 3.5,
        RandomSeed = true
    };

    public static ImageModelProfile CreateFluxKontext(string? apiKey = null) => new()
    {
        Name = "FLUX.1-Kontext (görsel düzenle)",
        ApiKey = apiKey ?? string.Empty,
        ApiBaseUrl = FluxKontextUrl,
        Model = FluxKontextModel,
        ApiMode = ImageApiMode.NvidiaGenAi,
        Steps = 30,
        CfgScale = 3.5,
        RandomSeed = true
    };

    public static ImageModelProfile CreateQwenSelfHost(string? apiKey = null) => new()
    {
        Name = "Qwen-Image (local NIM)",
        ApiKey = apiKey ?? string.Empty,
        ApiBaseUrl = QwenSelfHostUrl,
        Model = QwenModel,
        ApiMode = ImageApiMode.OpenAiImages,
        Steps = 20,
        CfgScale = 4.0,
        RandomSeed = true
    };

    public static ImageModelProfile CreateQwenHosted(string? apiKey = null) => new()
    {
        Name = "Qwen-Image (hosted)",
        ApiKey = apiKey ?? string.Empty,
        ApiBaseUrl = QwenHostedUrl,
        Model = QwenModel,
        ApiMode = ImageApiMode.NvidiaGenAi,
        Steps = 20,
        CfgScale = 4.0,
        RandomSeed = true
    };

    public static ImageModelProfile CreateQwenImageEdit(string? apiKey = null) => new()
    {
        Name = "Qwen-Image-Edit",
        ApiKey = apiKey ?? string.Empty,
        ApiBaseUrl = QwenEditUrl,
        Model = QwenEditModel,
        ApiMode = ImageApiMode.NvidiaGenAi,
        Steps = 20,
        CfgScale = 4.0,
        RandomSeed = true
    };

    public static ImageModelProfile CreateBlank() => new()
    {
        Name = "Custom Model",
        ApiKey = string.Empty,
        ApiBaseUrl = "https://ai.api.nvidia.com/v1/genai/",
        Model = "",
        ApiMode = ImageApiMode.NvidiaGenAi,
        Steps = 20,
        CfgScale = 3.5,
        RandomSeed = true
    };

    public static void ApplyFluxDev(ImageModelProfile profile)
    {
        profile.Name = string.IsNullOrWhiteSpace(profile.Name) || profile.Name is "New Model" or "Custom Model"
            ? "FLUX.1-dev"
            : profile.Name;
        profile.ApiBaseUrl = FluxDevUrl;
        profile.Model = FluxDevModel;
        profile.ApiMode = ImageApiMode.NvidiaGenAi;
        if (profile.Steps < 5)
            profile.Steps = 20;
        if (profile.CfgScale <= 0)
            profile.CfgScale = 3.5;
    }

    public static ImageModelProfile ResolveForInput(ImageModelProfile selected, int imageCount) =>
        selected;

    public static bool LooksLikeFluxDev(ImageModelProfile profile)
    {
        var blob = $"{profile.Model} {profile.ApiBaseUrl}";
        return blob.Contains("flux.1-dev", StringComparison.OrdinalIgnoreCase) &&
               !blob.Contains("kontext", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikeKontext(ImageModelProfile profile) =>
        $"{profile.Model} {profile.ApiBaseUrl}".Contains("kontext", StringComparison.OrdinalIgnoreCase);

    public static bool LooksLikeKlein(ImageModelProfile profile) =>
        $"{profile.Model} {profile.ApiBaseUrl}".Contains("klein", StringComparison.OrdinalIgnoreCase);

    public static bool LooksLikeQwenEdit(ImageModelProfile profile) =>
        $"{profile.Model} {profile.ApiBaseUrl}".Contains("image-edit", StringComparison.OrdinalIgnoreCase);

    public static bool IsHostedNvidia(ImageModelProfile profile)
    {
        var url = profile.ApiBaseUrl ?? string.Empty;
        return url.Contains("nvidia.com", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("nvcf.nvidia", StringComparison.OrdinalIgnoreCase);
    }

    public static void ApplyQwenSelfHost(ImageModelProfile profile)
    {
        profile.Name = string.IsNullOrWhiteSpace(profile.Name) || profile.Name is "New Model" or "Custom Model"
            ? "Qwen-Image (local NIM)"
            : profile.Name;
        profile.ApiBaseUrl = QwenSelfHostUrl;
        profile.Model = QwenModel;
        profile.ApiMode = ImageApiMode.OpenAiImages;
        if (profile.Steps < 1)
            profile.Steps = 20;
    }
}

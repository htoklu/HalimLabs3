using HalimLabs.Models;

namespace HalimLabs.Configuration;

public static class DefaultProviders
{
    public static IReadOnlyList<ProviderConfig> Create() =>
    [
        new ProviderConfig
        {
            Name = "DeepSeek V4 Flash",
            Type = ProviderType.OpenAICompatible,
            ApiBaseUrl = "https://integrate.api.nvidia.com/v1",
            ApiKey = string.Empty,
            Model = "deepseek-ai/deepseek-v4-flash",
            Description = "NVIDIA Build — DeepSeek V4 Flash (OpenAI compatible)",
            Enabled = true
        },
        new ProviderConfig
        {
            Name = "NVIDIA Llama 3.3",
            Type = ProviderType.OpenAICompatible,
            ApiBaseUrl = "https://integrate.api.nvidia.com/v1",
            ApiKey = string.Empty,
            Model = "meta/llama-3.3-70b-instruct",
            Description = "NVIDIA Build — Llama 3.3 70B Instruct",
            Enabled = true
        },
        new ProviderConfig
        {
            Name = "NVIDIA Kimi K2.6",
            Type = ProviderType.OpenAICompatible,
            ApiBaseUrl = "https://integrate.api.nvidia.com/v1",
            ApiKey = string.Empty,
            Model = "moonshotai/kimi-k2.6",
            Description = "NVIDIA Build — Kimi K2.6",
            Enabled = true
        },
        new ProviderConfig
        {
            Name = "OpenAI",
            Type = ProviderType.OpenAICompatible,
            ApiBaseUrl = "https://api.openai.com/v1",
            ApiKey = string.Empty,
            Model = "gpt-4o-mini",
            Description = "OpenAI Chat Completions API",
            Enabled = true
        },
        new ProviderConfig
        {
            Name = "Google Gemini",
            Type = ProviderType.OpenAICompatible,
            ApiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/openai",
            ApiKey = string.Empty,
            Model = "gemini-2.0-flash",
            Description = "Google Gemini OpenAI-compatible endpoint",
            Enabled = true
        },
        new ProviderConfig
        {
            Name = "Anthropic Claude",
            Type = ProviderType.OpenAICompatible,
            ApiBaseUrl = "https://api.anthropic.com/v1",
            ApiKey = string.Empty,
            Model = "claude-sonnet-4-20250514",
            Description = "Anthropic (use OpenAI-compatible gateway if needed)",
            Enabled = true
        }
    ];
}

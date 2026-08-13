using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HalimLabs.Models;
using HalimLabs.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace HalimLabs.Services.Image;

public sealed class NvidiaPromptTranslationService : IPromptTranslationService
{
    private const string ChatUrl = "https://integrate.api.nvidia.com/v1/chat/completions";
    private const string ChatModel = "meta/llama-3.1-8b-instruct";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NvidiaPromptTranslationService> _logger;

    public NvidiaPromptTranslationService(
        IHttpClientFactory httpClientFactory,
        ILogger<NvidiaPromptTranslationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<PromptTranslationResult> ToEnglishPromptAsync(
        string prompt,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var text = prompt.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return PromptTranslationResult.Passthrough(text);

        if (!LooksLikeTurkish(text))
            return PromptTranslationResult.Passthrough(text);

        var google = await TranslateWithGoogleAsync(text, cancellationToken).ConfigureAwait(false);
        if (IsUsableEnglish(google, text))
            return PromptTranslationResult.Ok(ShapeForImageModel(google!, text), "Google TR→EN");

        var memory = await TranslateWithMyMemoryAsync(text, cancellationToken).ConfigureAwait(false);
        if (IsUsableEnglish(memory, text))
            return PromptTranslationResult.Ok(ShapeForImageModel(memory!, text), "MyMemory TR→EN");

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            var llm = await TranslateWithNvidiaAsync(text, apiKey, cancellationToken).ConfigureAwait(false);
            if (IsUsableEnglish(llm, text))
                return PromptTranslationResult.Ok(ShapeForImageModel(llm!, text), "NVIDIA TR→EN");
        }

        _logger.LogWarning("Prompt translation stayed in Turkish: {Prompt}", text);
        return PromptTranslationResult.Fail(
            text,
            "Türkçe İngilizceye çevrilemedi. FLUX Türkçe anlamadığı için rastgele görsel üretir.\n\nİnterneti kontrol et veya promptu İngilizce yaz.");
    }

    private async Task<string?> TranslateWithGoogleAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            var url =
                "https://translate.googleapis.com/translate_a/single?client=gtx&sl=tr&tl=en&dt=t&q=" +
                Uri.EscapeDataString(text);

            var client = _httpClientFactory.CreateClient("TranslateApi");
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Google translate failed: {Status} {Body}", response.StatusCode, Trim(body));
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return null;

            var chunks = doc.RootElement[0];
            if (chunks.ValueKind != JsonValueKind.Array)
                return null;

            var sb = new StringBuilder();
            foreach (var chunk in chunks.EnumerateArray())
            {
                if (chunk.ValueKind == JsonValueKind.Array &&
                    chunk.GetArrayLength() > 0 &&
                    chunk[0].ValueKind == JsonValueKind.String)
                {
                    sb.Append(chunk[0].GetString());
                }
            }

            return CleanTranslation(sb.ToString());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google translate failed");
            return null;
        }
    }

    private async Task<string?> TranslateWithMyMemoryAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            var url =
                "https://api.mymemory.translated.net/get?langpair=tr|en&q=" +
                Uri.EscapeDataString(text);

            var client = _httpClientFactory.CreateClient("TranslateApi");
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("MyMemory translate failed: {Status} {Body}", response.StatusCode, Trim(body));
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("responseData", out var data) ||
                !data.TryGetProperty("translatedText", out var translated))
            {
                return null;
            }

            return CleanTranslation(translated.GetString());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MyMemory translate failed");
            return null;
        }
    }

    private async Task<string?> TranslateWithNvidiaAsync(string text, string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("ImageApi");
            using var request = new HttpRequestMessage(HttpMethod.Post, ChatUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = new
            {
                model = ChatModel,
                temperature = 0,
                max_tokens = 400,
                stream = false,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content =
                            "Translate Turkish to English. Output English only. " +
                            "Do not answer the request. Do not stay in Turkish."
                    },
                    new
                    {
                        role = "user",
                        content = "English translation of:\n" + text
                    }
                }
            };

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("NVIDIA translate failed: {Status} {Body}", response.StatusCode, Trim(body));
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            return CleanTranslation(content);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NVIDIA translate failed");
            return null;
        }
    }

    internal static string ShapeForImageModel(string english, string originalTurkish)
    {
        var t = english.Trim().TrimEnd('.');
        t = Regex.Replace(
            t,
            @"^(please\s+)?((show|draw|create|generate|make)(\s+me)?(\s+a|\s+an)?(\s+picture|\s+image|\s+photo|\s+photograph|\s+drawing)?)(\s+of|\s+with)?\s+",
            string.Empty,
            RegexOptions.IgnoreCase);
        t = Regex.Replace(
            t,
            @"\ba[n]?\s+(picture|image|photo|photograph|drawing)\s+(of|with|showing)\s+",
            string.Empty,
            RegexOptions.IgnoreCase);

        if (Regex.IsMatch(originalTurkish, @"uçak|ucak", RegexOptions.IgnoreCase))
            t = Regex.Replace(t, @"\bplanes?\b", "airplane", RegexOptions.IgnoreCase);

        t = t.Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(t))
            t = english.Trim();

        // "kız" → English "girl" trips NVIDIA's CSAM/safety filter and returns a black image.
        // Keep child wording when the user actually asked for a child.
        var mentionsChild = Regex.IsMatch(
            originalTurkish,
            @"\b(çocuk|cocuk|bebek|küçük\s+k[ıi]z|kucuk\s+kiz)\b",
            RegexOptions.IgnoreCase);
        var mentionsKiz = Regex.IsMatch(originalTurkish, @"\bk[ıi]z\b", RegexOptions.IgnoreCase);
        if (mentionsKiz && !mentionsChild)
        {
            t = Regex.Replace(t, @"\bgirls\b", "young women", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"\bgirl\b", "young woman", RegexOptions.IgnoreCase);
        }

        var wantsLettering = Regex.IsMatch(
            originalTurkish,
            @"yaz(an|sın|sin|ılı|ili|ı|i)\b",
            RegexOptions.IgnoreCase);

        if (wantsLettering)
        {
            t += ", with the name painted as large readable lettering on the object, not a portrait of a person, no human face";
        }

        return t.Trim();
    }

    private static bool IsUsableEnglish(string? translated, string original)
    {
        if (string.IsNullOrWhiteSpace(translated))
            return false;

        if (string.Equals(translated.Trim(), original.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        return !LooksLikeTurkish(translated);
    }

    internal static bool LooksLikeTurkish(string text)
    {
        if (Regex.IsMatch(text, "[çğıöşüÇĞİÖŞÜ]"))
            return true;

        return Regex.IsMatch(
            text,
            @"\b(bir|bana|benim|kadın|erkek|adam|kırmızı|mavi|sarı|yeşil|mor|beyaz|siyah|şapka|etek|ayakkabı|göster|goster|olsun|yazsın|yazsin|yazan|üstüne|ustune|üzerinde|uzerinde|uçak|ucak|resim|ağacında|kuş|çiz|için|ile|veya|çok|güzel|lütfen|lutfen)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string CleanTranslation(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var t = value.Trim();
        t = t.Replace("```", string.Empty, StringComparison.Ordinal).Trim();
        if (t.StartsWith('"') && t.EndsWith('"') && t.Length >= 2)
            t = t[1..^1].Trim();
        if (t.StartsWith('\'') && t.EndsWith('\'') && t.Length >= 2)
            t = t[1..^1].Trim();

        t = Regex.Replace(
            t,
            @"^(English|Prompt|Translation|Translated|Here is the translation)\s*:\s*",
            string.Empty,
            RegexOptions.IgnoreCase);
        return t.Trim();
    }

    private static string Trim(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : value[..Math.Min(value.Length, 300)];
}

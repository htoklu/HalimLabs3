using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HalimLabs.Configuration;
using HalimLabs.Localization;
using HalimLabs.Models;
using HalimLabs.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace HalimLabs.Services.Image;

public sealed class NvidiaImageGenerationService : IImageGenerationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NvidiaImageGenerationService> _logger;

    public NvidiaImageGenerationService(
        IHttpClientFactory httpClientFactory,
        ILogger<NvidiaImageGenerationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ImageGenerationResult> GenerateAsync(
        ImageModelProfile profile,
        string prompt,
        IReadOnlyList<byte[]>? inputImages = null,
        CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        try
        {
            if (string.IsNullOrWhiteSpace(profile.ApiKey))
                return ImageGenerationResult.Fail(Loc.T("ApiKeyRequired"), DateTime.UtcNow - started);

            if (string.IsNullOrWhiteSpace(prompt))
                return ImageGenerationResult.Fail(Loc.T("PromptRequired"), DateTime.UtcNow - started);

            if (string.IsNullOrWhiteSpace(profile.ApiBaseUrl))
                return ImageGenerationResult.Fail(Loc.T("ApiBaseUrlRequired"), DateTime.UtcNow - started);

            var seed = profile.RandomSeed
                ? Random.Shared.Next(0, int.MaxValue)
                : profile.Seed;

            var prepared = PrepareInputImages(profile, inputImages);
            var dataUris = EncodeInputImages(prepared);

            var client = _httpClientFactory.CreateClient("ImageApi");
            using var request = profile.ApiMode == ImageApiMode.OpenAiImages
                ? BuildOpenAiRequest(profile, prompt, seed, dataUris)
                : BuildNvidiaGenAiRequest(profile, prompt, seed, dataUris);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var duration = DateTime.UtcNow - started;
            WriteLastResponse((int)response.StatusCode, request.RequestUri?.ToString(), body);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Image API failed: {Status} {Body}", response.StatusCode, Trim(body));
                if (IsContentFilteredPayload(body) || (int)response.StatusCode == 451)
                    return ImageGenerationResult.Filtered(Loc.T("ContentFilterMessage"), duration);
                return ImageGenerationResult.Fail(FormatError((int)response.StatusCode, body), duration);
            }

            if (IsContentFilteredPayload(body))
            {
                _logger.LogWarning("Image API content filter: {Body}", Trim(body));
                return ImageGenerationResult.Filtered(Loc.T("ContentFilterMessage"), duration);
            }

            var bytes = ExtractImageBytes(body);
            if (bytes is null || bytes.Length == 0)
                return ImageGenerationResult.Fail(Loc.T("NoImageData"), duration);

            return ImageGenerationResult.Ok(bytes, duration);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image generation failed");
            return ImageGenerationResult.Fail(ex.Message, DateTime.UtcNow - started);
        }
    }

    private static IReadOnlyList<byte[]> PrepareInputImages(
        ImageModelProfile profile,
        IReadOnlyList<byte[]>? inputImages)
    {
        if (inputImages is null || inputImages.Count == 0)
            return [];

        // Hosted Kontext / Qwen-Edit take one image. Stitch person | clothing.
        if (inputImages.Count > 1 &&
            (ImageModelPresets.LooksLikeKontext(profile) || ImageModelPresets.LooksLikeQwenEdit(profile)))
            return [ImageCodec.StitchHorizontal(inputImages)];

        return inputImages;
    }

    private static List<string> EncodeInputImages(IReadOnlyList<byte[]>? inputImages)
    {
        if (inputImages is null || inputImages.Count == 0)
            return [];

        var list = new List<string>(Math.Min(inputImages.Count, 5));
        foreach (var bytes in inputImages.Take(5))
        {
            if (bytes is { Length: > 0 })
                list.Add(ImageCodec.ToJpegDataUri(bytes));
        }

        return list;
    }

    private static HttpRequestMessage BuildNvidiaGenAiRequest(
        ImageModelProfile profile,
        string prompt,
        int seed,
        IReadOnlyList<string> dataUris)
    {
        var url = profile.ApiBaseUrl.TrimEnd('/');
        if (!url.Contains("/genai/", StringComparison.OrdinalIgnoreCase) &&
            !url.EndsWith("/infer", StringComparison.OrdinalIgnoreCase) &&
            !url.Contains("/images/", StringComparison.OrdinalIgnoreCase))
        {
            url = $"{url}/genai/{profile.Model.Trim('/')}";
        }

        var (steps, cfg) = NormalizeGenAiParams(profile);
        var payload = new Dictionary<string, object?>
        {
            ["prompt"] = prompt.Trim(),
            ["seed"] = seed,
            ["steps"] = steps
        };

        // Klein image-edit infer example is only prompt + image[] + seed + steps.
        // Extra fields (mode, width/height, sometimes cfg_scale) return 422.
        var kleinEdit = ImageModelPresets.LooksLikeKlein(profile) && dataUris.Count > 0;
        if (!kleinEdit)
            payload["cfg_scale"] = cfg;

        if (dataUris.Count == 0)
        {
            payload["width"] = 1024;
            payload["height"] = 1024;
        }

        AttachInputImages(payload, profile, dataUris);
        return CreateJsonRequest(url, profile.ApiKey, payload);
    }

    private static void AttachInputImages(
        Dictionary<string, object?> payload,
        ImageModelProfile profile,
        IReadOnlyList<string> dataUris)
    {
        if (dataUris.Count == 0)
            return;

        // Klein (local NIM) takes `image` as a data-URI array. Hosted Klein rejects
        // custom photos (example_id only) — those requests should not reach here.
        if (ImageModelPresets.LooksLikeKlein(profile) && dataUris.Count > 1)
        {
            payload["image"] = dataUris.ToArray();
            return;
        }

        payload["image"] = dataUris[0];
        if (ImageModelPresets.LooksLikeKontext(profile) || ImageModelPresets.LooksLikeQwenEdit(profile))
        {
            payload["aspect_ratio"] = "match_input_image";
            payload["samples"] = 1;
        }
    }

    private static (int Steps, double Cfg) NormalizeGenAiParams(ImageModelProfile profile)
    {
        var model = $"{profile.Model} {profile.ApiBaseUrl}".ToLowerInvariant();
        var steps = profile.Steps;
        var cfg = profile.CfgScale;

        // FLUX.2-klein-4b hosted API: steps <= 4, cfg_scale <= 1
        if (model.Contains("flux.2-klein", StringComparison.Ordinal) ||
            model.Contains("flux_2-klein", StringComparison.Ordinal))
        {
            if (steps < 1 || steps > 4) steps = 4;
            if (cfg <= 0 || cfg > 1) cfg = 1;
            return (steps, cfg);
        }

        // FLUX.1-Kontext: 20-50 steps
        if (model.Contains("kontext", StringComparison.Ordinal))
        {
            if (steps < 20 || steps > 50) steps = 30;
            if (cfg <= 0) cfg = 3.5;
            return (steps, cfg);
        }

        // FLUX.1-dev style: steps >= 5
        if (steps < 5) steps = 20;
        if (cfg <= 0) cfg = 3.5;
        return (steps, cfg);
    }

    private static HttpRequestMessage BuildOpenAiRequest(
        ImageModelProfile profile,
        string prompt,
        int seed,
        IReadOnlyList<string> dataUris)
    {
        var baseUrl = profile.ApiBaseUrl.TrimEnd('/');
        if (baseUrl.EndsWith("/images/generations", StringComparison.OrdinalIgnoreCase))
            baseUrl = baseUrl[..^"/images/generations".Length].TrimEnd('/');

        var url = $"{baseUrl.TrimEnd('/')}/images/generations";

        if (profile.ApiBaseUrl.Contains("integrate.api.nvidia.com", StringComparison.OrdinalIgnoreCase) ||
            profile.ApiBaseUrl.TrimEnd('/').EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            url = $"{profile.ApiBaseUrl.TrimEnd('/')}/images/generations";
        }

        if (dataUris.Count > 0)
            url = url.Replace("/images/generations", "/images/edits", StringComparison.OrdinalIgnoreCase);

        var payload = new Dictionary<string, object?>
        {
            ["model"] = profile.Model,
            ["prompt"] = prompt.Trim(),
            ["n"] = 1,
            ["response_format"] = "b64_json",
            ["seed"] = seed
        };

        if (dataUris.Count == 1)
            payload["image"] = dataUris[0];
        else if (dataUris.Count > 1)
            payload["images"] = dataUris.ToArray();

        return CreateJsonRequest(url, profile.ApiKey, payload);
    }

    private static HttpRequestMessage CreateJsonRequest(string url, string apiKey, object payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static byte[]? ExtractImageBytes(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
        {
            var first = data[0];
            if (first.TryGetProperty("b64_json", out var b64) && b64.ValueKind == JsonValueKind.String)
                return Convert.FromBase64String(b64.GetString()!);
            if (first.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
                return null;
        }

        if (root.TryGetProperty("artifacts", out var artifacts) && artifacts.ValueKind == JsonValueKind.Array && artifacts.GetArrayLength() > 0)
        {
            var first = artifacts[0];
            foreach (var name in new[] { "base64", "b64_json", "image", "image_base64" })
            {
                if (first.TryGetProperty(name, out var val) && val.ValueKind == JsonValueKind.String)
                    return DecodeBase64(val.GetString());
            }
        }

        foreach (var name in new[] { "image", "b64_json", "base64", "output" })
        {
            if (root.TryGetProperty(name, out var val))
            {
                if (val.ValueKind == JsonValueKind.String)
                    return DecodeBase64(val.GetString());
                if (val.ValueKind == JsonValueKind.Array && val.GetArrayLength() > 0 && val[0].ValueKind == JsonValueKind.String)
                    return DecodeBase64(val[0].GetString());
            }
        }

        return null;
    }

    private static byte[]? DecodeBase64(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        value = value.Trim();
        if (value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var idx = value.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                value = value[(idx + "base64,".Length)..];
        }

        try
        {
            return Convert.FromBase64String(value);
        }
        catch
        {
            // Some NVIDIA responses include whitespace/newlines in base64.
            try
            {
                var compact = string.Concat(value.Where(c => !char.IsWhiteSpace(c)));
                return Convert.FromBase64String(compact);
            }
            catch
            {
                return null;
            }
        }
    }

    internal static string ContentFilterMessage => Loc.T("ContentFilterMessage");

    internal static string EditFailedMessage => Loc.T("EditFailedMessage");

    private static bool IsContentFilteredPayload(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(json);
            return HasFilteredFinishReason(doc.RootElement) || HasFilterErrorText(doc.RootElement);
        }
        catch (JsonException)
        {
            return LooksLikeFilterText(json);
        }
    }

    private static bool HasFilterErrorText(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (prop.NameEquals("image") || prop.NameEquals("base64") ||
                        prop.NameEquals("b64_json") || prop.NameEquals("image_base64") ||
                        prop.NameEquals("output"))
                        continue;

                    if (prop.Value.ValueKind == JsonValueKind.String &&
                        LooksLikeFilterText(prop.Value.GetString()))
                        return true;

                    if (HasFilterErrorText(prop.Value))
                        return true;
                }
                return false;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (HasFilterErrorText(item))
                        return true;
                }
                return false;
            default:
                return false;
        }
    }

    private static bool LooksLikeFilterText(string? text) =>
        !string.IsNullOrEmpty(text) &&
        Regex.IsMatch(
            text,
            @"CONTENT[_\s-]?FILTERED|content[_\s-]?policy|safety[_\s-]?filter|guardrail",
            RegexOptions.IgnoreCase);

    private static bool HasFilteredFinishReason(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if ((prop.NameEquals("finish_reason") || prop.NameEquals("finishReason") ||
                         prop.NameEquals("finishReasonType")) &&
                        prop.Value.ValueKind == JsonValueKind.String &&
                        prop.Value.GetString() is { } reason &&
                        reason.Contains("FILTER", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (HasFilteredFinishReason(prop.Value))
                        return true;
                }
                return false;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (HasFilteredFinishReason(item))
                        return true;
                }
                return false;
            default:
                return false;
        }
    }

    private static string FormatError(int statusCode, string body)
    {
        var detail = Trim(body);
        return statusCode switch
        {
            401 or 403 => Loc.Tf("Unauthorized", statusCode, detail),
            404 => Loc.Tf("EndpointNotFound", detail),
            422 => FormatUnprocessable(detail),
            429 => Loc.Tf("RateLimit", detail),
            529 => Loc.Tf("ServiceBusy", detail),
            >= 500 => Loc.Tf("ServerError", statusCode, detail),
            _ => Loc.Tf("ApiError", statusCode, detail)
        };
    }

    private static string FormatUnprocessable(string detail)
    {
        if (IsPreviewImageReject(detail))
            return HostedPreviewImageMessage + "\n\n" + detail;

        if (detail.Contains("extra_forbidden", StringComparison.OrdinalIgnoreCase) ||
            detail.Contains("Extra inputs are not permitted", StringComparison.OrdinalIgnoreCase))
            return Loc.Tf("Rejected422Field", detail);

        return Loc.Tf("Rejected422", detail);
    }

    internal static bool IsPreviewImageReject(string? text) =>
        !string.IsNullOrEmpty(text) &&
        text.Contains("example_id", StringComparison.OrdinalIgnoreCase);

    internal static bool IsMissingEndpoint(string? text) =>
        !string.IsNullOrEmpty(text) &&
        (text.Contains("Endpoint not found (404)", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("404 page not found", StringComparison.OrdinalIgnoreCase) ||
         text.Contains("(404)", StringComparison.Ordinal));

    internal static string PhotoEditUnavailableMessage => Loc.T("PhotoEditUnavailable");

    internal static string HostedPreviewImageMessage => Loc.T("HostedPreviewImage");

    private static void WriteLastResponse(int statusCode, string? url, string body)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HalimLabs3");
            Directory.CreateDirectory(folder);
            var safe = Regex.Replace(body ?? string.Empty, @"data:image\/[a-zA-Z0-9.+-]+;base64,[A-Za-z0-9+/=\s]+", "[image]");
            if (safe.Length > 4000)
                safe = safe[..4000] + "…";
            File.WriteAllText(
                Path.Combine(folder, "last-generate.log"),
                $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\nHTTP {statusCode}\n{url}\n{safe}\n");
        }
        catch
        {
            // diagnostics only
        }
    }

    private static string Trim(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return Loc.T("NoDetails");
        body = body.Trim();
        return body.Length > 400 ? body[..400] + "…" : body;
    }
}

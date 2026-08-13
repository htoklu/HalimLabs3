using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HalimLabs.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace HalimLabs.Services.Image;

public sealed class NvidiaImageCaptionService : IImageCaptionService
{
    private const string ChatUrl = "https://integrate.api.nvidia.com/v1/chat/completions";
    private const string VisionModel = "meta/llama-3.2-11b-vision-instruct";
    private const string VisionFallback = "nvidia/llama-3.1-nemotron-nano-vl-8b-v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NvidiaImageCaptionService> _logger;

    public NvidiaImageCaptionService(
        IHttpClientFactory httpClientFactory,
        ILogger<NvidiaImageCaptionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string?> BuildTryOnPromptAsync(
        IReadOnlyList<byte[]> images,
        string englishInstruction,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (images.Count == 0 || string.IsNullOrWhiteSpace(apiKey))
            return null;

        var client = _httpClientFactory.CreateClient("ImageApi");
        var uris = images.Take(3)
            .Select(b => "data:image/jpeg;base64," + Convert.ToBase64String(ImageCodec.NormalizeJpeg(b, 640)))
            .ToList();

        var personUri = uris[0];
        var garmentUri = uris.Count > 1 ? uris[1] : null;

        if (uris.Count >= 2)
        {
            var kind0 = await ClassifyAsync(client, apiKey, uris[0], cancellationToken).ConfigureAwait(false);
            var kind1 = await ClassifyAsync(client, apiKey, uris[1], cancellationToken).ConfigureAwait(false);
            if (kind0 == "GARMENT" && kind1 == "PERSON")
            {
                personUri = uris[1];
                garmentUri = uris[0];
            }
        }

        var person = await CaptionAsync(
            client,
            apiKey,
            personUri,
            "Describe this adult person's appearance for image generation. Include hair, face, skin, sunglasses or jewelry, pose, and background. Do NOT mention any clothing or shirt. English, one paragraph, no preamble.",
            cancellationToken).ConfigureAwait(false);

        string? garment = null;
        if (garmentUri is not null)
        {
            garment = await CaptionAsync(
                client,
                apiKey,
                garmentUri,
                "Describe only the garment in this photo. Type, color, fit, neckline, fabric. Ignore hanger, hands, and background. English, one sentence, no preamble.",
                cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(person))
            return null;

        person = Clean(person);
        garment = string.IsNullOrWhiteSpace(garment) ? null : Clean(garment);

        if (garment is null)
        {
            return "Photorealistic photograph. " + person +
                   ". Follow this request: " + englishInstruction.Trim().TrimEnd('.') +
                   ". Single centered photo, not a split or collage, no watermark";
        }

        return
            "Photorealistic chest-up portrait. " + person +
            ". The person is wearing " + garment +
            ". Single centered photo, not a split or collage, no watermark, no text";
    }

    private async Task<string> ClassifyAsync(
        HttpClient client,
        string apiKey,
        string dataUri,
        CancellationToken cancellationToken)
    {
        var text = await CaptionAsync(
            client,
            apiKey,
            dataUri,
            "Reply with exactly one word: PERSON or GARMENT. PERSON if a human is the main subject. GARMENT if clothing on a hanger, mannequin, or laid out.",
            cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
            return "PERSON";
        return text.Contains("GARMENT", StringComparison.OrdinalIgnoreCase) ? "GARMENT" : "PERSON";
    }

    private async Task<string?> CaptionAsync(
        HttpClient client,
        string apiKey,
        string dataUri,
        string ask,
        CancellationToken cancellationToken)
    {
        foreach (var model in new[] { VisionModel, VisionFallback })
        {
            var text = await CompleteAsync(client, apiKey, model, dataUri, ask, cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return null;
    }

    private async Task<string?> CompleteAsync(
        HttpClient client,
        string apiKey,
        string model,
        string dataUri,
        string ask,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, ChatUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var payload = new
            {
                model,
                temperature = 0.1,
                max_tokens = 220,
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = ask },
                            new { type = "image_url", image_url = new { url = dataUri } }
                        }
                    }
                }
            };
            request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Vision {Model} {Status}: {Body}", model, (int)response.StatusCode, Trim(body));
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var text = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim().Trim('"');
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vision {Model} failed", model);
            return null;
        }
    }

    private static string Clean(string text)
    {
        text = Regex.Replace(
            text,
            @"^(The image (features|depicts|shows)|The garment in this photo is|The person is wearing)\s+",
            string.Empty,
            RegexOptions.IgnoreCase);
        return text.Trim().TrimEnd('.');
    }

    private static string Trim(string body) =>
        body.Length <= 300 ? body : body[..300];
}

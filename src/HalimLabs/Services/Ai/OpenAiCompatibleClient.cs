using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HalimLabs.Models;
using HalimLabs.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace HalimLabs.Services.Ai;

public sealed class OpenAiCompatibleClient : IAiProviderClient
{
    private const int MaxTransientRetries = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenAiCompatibleClient> _logger;

    public OpenAiCompatibleClient(IHttpClientFactory httpClientFactory, ILogger<OpenAiCompatibleClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public ProviderType SupportedType => ProviderType.OpenAICompatible;

    public async IAsyncEnumerable<ChatCompletionChunk> StreamChatAsync(
        ProviderConfig provider,
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("AiProviders");
        using var response = await SendWithRetryAsync(
                client,
                () => BuildRequest(provider, request.Model, request.Messages, stream: true, maxTokens: null),
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;

            var payload = line["data:".Length..].Trim();
            if (payload is "[DONE]" or "[done]")
            {
                yield return new ChatCompletionChunk { IsFinished = true };
                yield break;
            }

            var content = ExtractDeltaContent(payload);
            if (!string.IsNullOrEmpty(content))
                yield return new ChatCompletionChunk { Content = content };
        }

        yield return new ChatCompletionChunk { IsFinished = true };
    }

    public async Task<ConnectionTestResult> TestConnectionAsync(
        ProviderConfig provider,
        CancellationToken cancellationToken)
    {
        var started = DateTime.UtcNow;
        try
        {
            if (string.IsNullOrWhiteSpace(provider.ApiBaseUrl))
                return ConnectionTestResult.Fail("API Base URL is required.", DateTime.UtcNow - started);

            if (string.IsNullOrWhiteSpace(provider.Model))
                return ConnectionTestResult.Fail("Model is required.", DateTime.UtcNow - started);

            if (string.IsNullOrWhiteSpace(provider.ApiKey))
                return ConnectionTestResult.Fail("API Key is required.", DateTime.UtcNow - started);

            var client = _httpClientFactory.CreateClient("AiProviders");
            var messages = new[]
            {
                new ChatMessage { Role = ChatRole.User, Content = "Reply with OK only." }
            };

            using var response = await SendWithRetryAsync(
                    client,
                    () => BuildRequest(provider, provider.Model, messages, stream: false, maxTokens: 32),
                    HttpCompletionOption.ResponseContentRead,
                    cancellationToken)
                .ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ConnectionTestResult.Ok("Connection successful.", DateTime.UtcNow - started);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ConnectionTestResult.Fail(ex.Message, DateTime.UtcNow - started);
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient client,
        Func<HttpRequestMessage> requestFactory,
        HttpCompletionOption completionOption,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage? lastResponse = null;

        for (var attempt = 0; attempt <= MaxTransientRetries; attempt++)
        {
            lastResponse?.Dispose();
            using var httpRequest = requestFactory();
            lastResponse = await client.SendAsync(httpRequest, completionOption, cancellationToken)
                .ConfigureAwait(false);

            if (lastResponse.IsSuccessStatusCode)
                return lastResponse;

            var status = (int)lastResponse.StatusCode;
            var errorBody = await lastResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!IsTransient(status) || attempt == MaxTransientRetries)
            {
                _logger.LogWarning("Chat request failed: {Status} {Body}", lastResponse.StatusCode, errorBody);
                lastResponse.Dispose();
                throw new HttpRequestException(FormatHttpError(status, errorBody));
            }

            var delayMs = (attempt + 1) * 1500;
            _logger.LogInformation(
                "Transient API status {Status}. Retry {Attempt}/{Max} in {Delay}ms. {Body}",
                status,
                attempt + 1,
                MaxTransientRetries,
                delayMs,
                TrimError(errorBody));

            lastResponse.Dispose();
            lastResponse = null;
            await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
        }

        throw new HttpRequestException("API request failed after retries.");
    }

    private static bool IsTransient(int statusCode) =>
        statusCode is 408 or 429 or 500 or 502 or 503 or 504 or 529;

    private static HttpRequestMessage BuildRequest(
        ProviderConfig provider,
        string model,
        IReadOnlyList<ChatMessage> messages,
        bool stream,
        int? maxTokens)
    {
        var baseUrl = NormalizeBaseUrl(provider.ApiBaseUrl);
        var url = $"{baseUrl}/chat/completions";
        var apiKey = provider.ApiKey.Trim();

        var body = new ChatCompletionsBody
        {
            Model = model.Trim(),
            Stream = stream,
            Messages = messages.Select(m => new ChatCompletionsMessage
            {
                Role = ToApiRole(m.Role),
                Content = m.Content
            }).ToList(),
            MaxTokens = maxTokens,
            Temperature = 0.7
        };

        var json = JsonSerializer.Serialize(body, JsonOptions);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (stream)
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        return httpRequest;
    }

    private static string NormalizeBaseUrl(string apiBaseUrl)
    {
        var baseUrl = apiBaseUrl.Trim().TrimEnd('/');
        if (baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            baseUrl = baseUrl[..^"/chat/completions".Length].TrimEnd('/');
        return baseUrl;
    }

    private static string? ExtractDeltaContent(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];

                if (choice.TryGetProperty("delta", out var delta))
                {
                    if (delta.TryGetProperty("content", out var deltaContent) &&
                        deltaContent.ValueKind == JsonValueKind.String)
                        return deltaContent.GetString();
                }

                if (choice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var messageContent) &&
                    messageContent.ValueKind == JsonValueKind.String)
                    return messageContent.GetString();

                if (choice.TryGetProperty("text", out var text) &&
                    text.ValueKind == JsonValueKind.String)
                    return text.GetString();
            }
        }
        catch (JsonException)
        {
            // ignore malformed chunks
        }

        return null;
    }

    private static string ToApiRole(ChatRole role) => role switch
    {
        ChatRole.System => "system",
        ChatRole.User => "user",
        ChatRole.Assistant => "assistant",
        _ => "user"
    };

    private static string FormatHttpError(int statusCode, string body)
    {
        var detail = TrimError(body);
        return statusCode switch
        {
            401 or 403 => $"Yetkisiz ({statusCode}). API Key hatalı veya süresi dolmuş olabilir. {detail}",
            404 => $"Endpoint bulunamadı (404). API Base URL kontrol et. {detail}",
            429 => $"Rate limit (429). Biraz bekleyip tekrar dene. {detail}",
            529 => $"NVIDIA sunucusu meşgul (529). Model geçici olarak dolu — birkaç saniye sonra tekrar dene. {detail}",
            >= 500 => $"Sunucu hatası ({statusCode}). {detail}",
            _ => $"API error {statusCode}: {detail}"
        };
    }

    private static string TrimError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "No error details.";

        body = body.Trim();
        return body.Length > 400 ? body[..400] + "…" : body;
    }

    private sealed class ChatCompletionsBody
    {
        public string Model { get; set; } = string.Empty;
        public bool Stream { get; set; }
        public List<ChatCompletionsMessage> Messages { get; set; } = [];
        public int? MaxTokens { get; set; }
        public double? Temperature { get; set; }
    }

    private sealed class ChatCompletionsMessage
    {
        public string Role { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }
}

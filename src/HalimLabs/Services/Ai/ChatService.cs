using HalimLabs.Models;
using HalimLabs.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace HalimLabs.Services.Ai;

public sealed class ChatService : IChatService
{
    private readonly IReadOnlyDictionary<ProviderType, IAiProviderClient> _clients;
    private readonly ILogger<ChatService> _logger;

    public ChatService(IEnumerable<IAiProviderClient> clients, ILogger<ChatService> logger)
    {
        _clients = clients.ToDictionary(c => c.SupportedType);
        _logger = logger;
    }

    public IAsyncEnumerable<ChatCompletionChunk> StreamAsync(
        ProviderConfig provider,
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        if (!_clients.TryGetValue(provider.Type, out var client))
        {
            _logger.LogError("No client registered for provider type {Type}", provider.Type);
            throw new NotSupportedException($"Provider type '{provider.Type}' is not supported.");
        }

        var request = new ChatCompletionRequest
        {
            Model = provider.Model,
            Messages = messages,
            Stream = true
        };

        return client.StreamChatAsync(provider, request, cancellationToken);
    }
}

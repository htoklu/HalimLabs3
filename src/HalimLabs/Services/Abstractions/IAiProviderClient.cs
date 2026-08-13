using HalimLabs.Models;

namespace HalimLabs.Services.Abstractions;

public interface IAiProviderClient
{
    ProviderType SupportedType { get; }

    IAsyncEnumerable<ChatCompletionChunk> StreamChatAsync(
        ProviderConfig provider,
        ChatCompletionRequest request,
        CancellationToken cancellationToken);
}

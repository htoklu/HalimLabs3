using HalimLabs.Models;

namespace HalimLabs.Services.Abstractions;

public interface IChatService
{
    IAsyncEnumerable<ChatCompletionChunk> StreamAsync(
        ProviderConfig provider,
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken);
}

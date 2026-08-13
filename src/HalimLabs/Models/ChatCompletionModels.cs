namespace HalimLabs.Models;

public sealed class ChatCompletionRequest
{
    public required string Model { get; init; }
    public required IReadOnlyList<ChatMessage> Messages { get; init; }
    public bool Stream { get; init; } = true;
}

public sealed class ChatCompletionChunk
{
    public string Content { get; init; } = string.Empty;
    public bool IsFinished { get; init; }
}

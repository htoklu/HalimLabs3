namespace HalimLabs.Models;

public sealed class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
    public bool IsStreaming { get; set; }
}

namespace HalimLabs.Models;

public sealed class ChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "New Chat";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public Guid? ProviderId { get; set; }
    public List<ChatMessage> Messages { get; set; } = [];
}

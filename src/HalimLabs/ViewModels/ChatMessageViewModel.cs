using CommunityToolkit.Mvvm.ComponentModel;
using HalimLabs.Models;

namespace HalimLabs.ViewModels;

public partial class ChatMessageViewModel : ObservableObject
{
    public ChatMessageViewModel(ChatMessage message)
    {
        Id = message.Id;
        Role = message.Role;
        Content = message.Content ?? string.Empty;
        Timestamp = message.Timestamp;
        IsStreaming = message.IsStreaming;
    }

    public Guid Id { get; }

    public ChatRole Role { get; }

    public DateTimeOffset Timestamp { get; }

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private bool _isStreaming;

    public bool IsUser => Role == ChatRole.User;
    public bool IsAssistant => Role == ChatRole.Assistant;

    public ChatMessage ToModel() => new()
    {
        Id = Id,
        Role = Role,
        Content = Content,
        Timestamp = Timestamp,
        IsStreaming = false
    };
}

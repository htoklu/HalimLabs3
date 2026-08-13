using CommunityToolkit.Mvvm.ComponentModel;
using HalimLabs.Models;

namespace HalimLabs.ViewModels;

public partial class ChatSessionItemViewModel : ObservableObject
{
    public ChatSessionItemViewModel(ChatSession session)
    {
        Id = session.Id;
        Title = session.Title;
        UpdatedAt = session.UpdatedAt;
        ProviderId = session.ProviderId;
    }

    public Guid Id { get; }

    [ObservableProperty]
    private string _title = "New Chat";

    [ObservableProperty]
    private DateTimeOffset _updatedAt;

    public Guid? ProviderId { get; set; }

    public string UpdatedDisplay => UpdatedAt.LocalDateTime.ToString("g");
}

using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HalimLabs.ViewModels;

public partial class ImageAttachmentItem : ObservableObject
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public required string FileName { get; init; }
    public required byte[] Bytes { get; init; }
    public required ImageSource Thumbnail { get; init; }
    public required ImageSource Preview { get; init; }

    [ObservableProperty] private bool _isPrimary;
}

public partial class StudioChatItem : ObservableObject
{
    public string Id { get; } = Guid.NewGuid().ToString("N");

    [ObservableProperty] private string _title = "Sohbet 1";

    public List<ImageAttachmentItem> Attachments { get; set; } = [];
    public byte[]? ResultBytes { get; set; }
    public ImageSource? ResultPreview { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public bool CompareMode { get; set; }
}

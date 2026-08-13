using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using HalimLabs.Helpers;

namespace HalimLabs.Controls;

public sealed class MarkdownViewer : RichTextBox
{
    public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
        nameof(Markdown),
        typeof(string),
        typeof(MarkdownViewer),
        new PropertyMetadata(string.Empty, OnMarkdownChanged));

    public static readonly DependencyProperty IsStreamingProperty = DependencyProperty.Register(
        nameof(IsStreaming),
        typeof(bool),
        typeof(MarkdownViewer),
        new PropertyMetadata(false, OnStreamingChanged));

    public MarkdownViewer()
    {
        IsReadOnly = true;
        IsDocumentEnabled = true;
        IsTabStop = false;
        BorderThickness = new Thickness(0);
        Background = System.Windows.Media.Brushes.Transparent;
        Padding = new Thickness(0);
        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Document = new FlowDocument();

        // Clicking Copy (or other controls) inside FlowDocument triggers BringIntoView,
        // which jumps the outer chat ScrollViewer. Suppress it.
        AddHandler(RequestBringIntoViewEvent, new RequestBringIntoViewEventHandler((_, e) =>
        {
            e.Handled = true;
        }), handledEventsToo: true);
    }

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public bool IsStreaming
    {
        get => (bool)GetValue(IsStreamingProperty);
        set => SetValue(IsStreamingProperty, value);
    }

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownViewer viewer && !viewer.IsStreaming)
            viewer.QueueRender(e.NewValue as string);
    }

    private static void OnStreamingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not MarkdownViewer viewer)
            return;

        if (e.NewValue is false)
            viewer.QueueRender(viewer.Markdown);
    }

    private void QueueRender(string? markdown)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (IsStreaming)
                return;

            try
            {
                Document = MarkdownRenderer.ToFlowDocument(markdown);
            }
            catch
            {
                Document = MarkdownRenderer.ToPlainDocument(markdown);
            }
        }, System.Windows.Threading.DispatcherPriority.Background);
    }
}

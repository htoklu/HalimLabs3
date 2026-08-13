using System.Windows;
using HalimLabs.ViewModels;

namespace HalimLabs;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync().ConfigureAwait(true);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
            return;

        var images = files.Where(f =>
        {
            var ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
            return ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".bmp" or ".gif";
        });

        _viewModel.AddImageFiles(images);
    }
}

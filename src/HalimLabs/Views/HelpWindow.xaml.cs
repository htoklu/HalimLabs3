using System.Windows;

namespace HalimLabs.Views;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (DataContext is ViewModels.HelpViewModel vm)
                HelpViewer.Markdown = vm.Content;
        };
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

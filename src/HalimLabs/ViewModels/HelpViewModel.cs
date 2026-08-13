using CommunityToolkit.Mvvm.ComponentModel;
using HalimLabs.Localization;

namespace HalimLabs.ViewModels;

public partial class HelpViewModel : ObservableObject
{
    public HelpViewModel()
    {
        Refresh();
        Loc.Current.LanguageChanged += (_, _) => Refresh();
    }

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _content = string.Empty;

    private void Refresh()
    {
        Title = Loc.T("HelpTitle");
        Content = Loc.T("HelpContent");
    }
}

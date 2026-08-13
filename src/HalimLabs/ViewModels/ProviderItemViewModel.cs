using CommunityToolkit.Mvvm.ComponentModel;
using HalimLabs.Models;

namespace HalimLabs.ViewModels;

public partial class ProviderItemViewModel : ObservableObject
{
    public ProviderItemViewModel(ProviderConfig config)
    {
        Id = config.Id;
        Name = config.Name;
        Type = config.Type;
        ApiBaseUrl = config.ApiBaseUrl;
        ApiKey = config.ApiKey;
        Model = config.Model;
        Description = config.Description;
        Enabled = config.Enabled;
    }

    public Guid Id { get; set; }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private ProviderType _type = ProviderType.OpenAICompatible;
    [ObservableProperty] private string _apiBaseUrl = string.Empty;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private bool _enabled = true;

    [ObservableProperty] private bool? _testSuccess;
    [ObservableProperty] private string _testMessage = string.Empty;
    [ObservableProperty] private bool _isTesting;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Model) ? Name : $"{Name} — {Model}";

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(DisplayName));
    partial void OnModelChanged(string value) => OnPropertyChanged(nameof(DisplayName));

    public ProviderConfig ToModel() => new()
    {
        Id = Id,
        Name = Name,
        Type = Type,
        ApiBaseUrl = ApiBaseUrl,
        ApiKey = ApiKey,
        Model = Model,
        Description = Description,
        Enabled = Enabled
    };
}

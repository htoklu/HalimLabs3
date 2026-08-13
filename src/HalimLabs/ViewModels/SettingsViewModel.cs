using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HalimLabs.Configuration;
using HalimLabs.Localization;
using HalimLabs.Models;
using HalimLabs.Services.Abstractions;

namespace HalimLabs.ViewModels;

public partial class ImageProfileItemViewModel : ObservableObject
{
    public string Id { get; private set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty] private string _name = Loc.T("NewModel");
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _apiBaseUrl = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private ImageApiMode _apiMode = ImageApiMode.NvidiaGenAi;
    [ObservableProperty] private int _steps = 20;
    [ObservableProperty] private double _cfgScale = 3.5;
    [ObservableProperty] private int _seed;
    [ObservableProperty] private bool _randomSeed = true;

    public static ImageProfileItemViewModel From(ImageModelProfile profile) => new()
    {
        Id = profile.Id,
        Name = profile.Name,
        ApiKey = profile.ApiKey,
        ApiBaseUrl = profile.ApiBaseUrl,
        Model = profile.Model,
        ApiMode = profile.ApiMode,
        Steps = profile.Steps,
        CfgScale = profile.CfgScale,
        Seed = profile.Seed,
        RandomSeed = profile.RandomSeed
    };

    public static ImageProfileItemViewModel FromPreset(ImageModelProfile profile) => From(profile);

    public ImageModelProfile ToModel() => new()
    {
        Id = Id,
        Name = Name.Trim(),
        ApiKey = ApiKey.Trim(),
        ApiBaseUrl = ApiBaseUrl.Trim(),
        Model = Model.Trim(),
        ApiMode = ApiMode,
        Steps = Steps,
        CfgScale = CfgScale,
        Seed = Seed,
        RandomSeed = RandomSeed
    };
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly IImageSettingsRepository _settingsRepository;
    private readonly IImageGenerationService _imageService;

    public SettingsViewModel(IImageSettingsRepository settingsRepository, IImageGenerationService imageService)
    {
        _settingsRepository = settingsRepository;
        _imageService = imageService;
    }

    public Array ApiModes { get; } = Enum.GetValues(typeof(ImageApiMode));
    public ObservableCollection<ImageProfileItemViewModel> Profiles { get; } = [];

    [ObservableProperty] private ImageProfileItemViewModel? _selectedProfile;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool? _testSuccess;
    [ObservableProperty] private string _testMessage = string.Empty;
    [ObservableProperty] private bool _isTesting;

    public async Task InitializeAsync()
    {
        var store = await _settingsRepository.GetAsync().ConfigureAwait(true);
        Profiles.Clear();
        foreach (var profile in store.Profiles)
            Profiles.Add(ImageProfileItemViewModel.From(profile));

        SelectedProfile = Profiles.FirstOrDefault(p => p.Id == store.ActiveProfileId)
                          ?? Profiles.FirstOrDefault();
        StatusText = Loc.Tf("ModelsLoaded", Profiles.Count);
    }

    [RelayCommand]
    private void AddModel()
    {
        var item = ImageProfileItemViewModel.FromPreset(ImageModelPresets.CreateBlank());
        Profiles.Add(item);
        SelectedProfile = item;
        StatusText = Loc.T("AddedCustom");
    }

    [RelayCommand]
    private void AddFlux()
    {
        var key = SelectedProfile?.ApiKey;
        var item = ImageProfileItemViewModel.FromPreset(ImageModelPresets.CreateFluxDev(key));
        Profiles.Add(item);
        SelectedProfile = item;
        StatusText = Loc.T("AddedFlux");
    }

    [RelayCommand]
    private void AddKontext()
    {
        var key = SelectedProfile?.ApiKey;
        var item = ImageProfileItemViewModel.FromPreset(ImageModelPresets.CreateFluxKontext(key));
        Profiles.Add(item);
        SelectedProfile = item;
        StatusText = Loc.T("AddedKontext");
    }

    [RelayCommand]
    private void AddKlein()
    {
        var key = SelectedProfile?.ApiKey;
        var item = ImageProfileItemViewModel.FromPreset(ImageModelPresets.CreateFluxKlein(key));
        Profiles.Add(item);
        SelectedProfile = item;
        StatusText = Loc.T("AddedKlein");
    }

    [RelayCommand]
    private void AddQwen()
    {
        var key = SelectedProfile?.ApiKey;
        var item = ImageProfileItemViewModel.FromPreset(ImageModelPresets.CreateQwenHosted(key));
        Profiles.Add(item);
        SelectedProfile = item;
        StatusText = Loc.T("AddedQwen");
    }

    [RelayCommand]
    private void AddQwenLocal()
    {
        var key = SelectedProfile?.ApiKey;
        var item = ImageProfileItemViewModel.FromPreset(ImageModelPresets.CreateQwenSelfHost(key));
        Profiles.Add(item);
        SelectedProfile = item;
        StatusText = Loc.T("AddedQwenLocal");
    }

    [RelayCommand]
    private void DuplicateModel()
    {
        if (SelectedProfile is null)
            return;

        var copy = ImageProfileItemViewModel.From(SelectedProfile.ToModel().Clone());
        copy.Name = Loc.Tf("CopySuffix", SelectedProfile.Name);
        Profiles.Add(copy);
        SelectedProfile = copy;
        StatusText = Loc.T("DuplicatedModel");
    }

    [RelayCommand]
    private void RemoveModel()
    {
        if (SelectedProfile is null || Profiles.Count <= 1)
        {
            StatusText = Loc.T("KeepOneModel");
            return;
        }

        var index = Profiles.IndexOf(SelectedProfile);
        Profiles.Remove(SelectedProfile);
        SelectedProfile = Profiles.ElementAtOrDefault(Math.Clamp(index, 0, Profiles.Count - 1));
        StatusText = Loc.T("RemovedModel");
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _settingsRepository.SaveAsync(ToStore(keepActive: true)).ConfigureAwait(true);
        StatusText = Loc.T("Saved");
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (SelectedProfile is null)
        {
            TestSuccess = false;
            TestMessage = Loc.T("SelectModelFirst");
            return;
        }

        IsTesting = true;
        TestSuccess = null;
        TestMessage = Loc.T("Testing");
        try
        {
            var result = await _imageService.GenerateAsync(
                SelectedProfile.ToModel(),
                "a simple red circle on white background").ConfigureAwait(true);

            TestSuccess = result.Success;
            TestMessage = result.Success
                ? Loc.Tf("TestOk", result.Duration.TotalSeconds)
                : (result.ErrorMessage ?? Loc.T("Failed"));
            StatusText = result.Success ? Loc.T("ConnectionOk") : Loc.T("ConnectionFailed");
        }
        catch (Exception ex)
        {
            TestSuccess = false;
            TestMessage = ex.Message;
            StatusText = Loc.T("ConnectionFailed");
        }
        finally
        {
            IsTesting = false;
        }
    }

    public ImageSettingsStore ToStore(bool keepActive)
    {
        var activeId = SelectedProfile?.Id
                       ?? Profiles.FirstOrDefault()?.Id
                       ?? string.Empty;

        return new ImageSettingsStore
        {
            ActiveProfileId = activeId,
            Profiles = Profiles.Select(p => p.ToModel()).ToList()
        };
    }
}

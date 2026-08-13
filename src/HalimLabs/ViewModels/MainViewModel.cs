using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HalimLabs.Configuration;
using HalimLabs.Localization;
using HalimLabs.Models;
using HalimLabs.Services.Abstractions;
using HalimLabs.Services.Image;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace HalimLabs.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IImageGenerationService _imageService;
    private readonly IPromptTranslationService _translationService;
    private readonly IImageCaptionService _captionService;
    private readonly IImageSettingsRepository _settingsRepository;
    private readonly ISupportInfoProvider _supportInfoProvider;
    private readonly ILogger<MainViewModel> _logger;
    private readonly Func<SettingsViewModel> _settingsFactory;
    private readonly Func<HelpViewModel> _helpFactory;

    private CancellationTokenSource? _cts;
    private byte[]? _currentImageBytes;
    private bool _suppressProfileSave;
    private bool _suppressChatSwitch;

    public MainViewModel(
        IImageGenerationService imageService,
        IPromptTranslationService translationService,
        IImageCaptionService captionService,
        IImageSettingsRepository settingsRepository,
        ISupportInfoProvider supportInfoProvider,
        ILogger<MainViewModel> logger,
        Func<SettingsViewModel> settingsFactory,
        Func<HelpViewModel> helpFactory)
    {
        _imageService = imageService;
        _translationService = translationService;
        _captionService = captionService;
        _settingsRepository = settingsRepository;
        _supportInfoProvider = supportInfoProvider;
        _logger = logger;
        _settingsFactory = settingsFactory;
        _helpFactory = helpFactory;

        var first = new StudioChatItem { Title = Loc.Tf("ChatTitle", 1) };
        Chats.Add(first);
        _selectedChat = first;
        SelectedLanguage = Languages.First(l => l.Code == Loc.Current.Language.ToCode());
        Loc.Current.LanguageChanged += OnLanguageChanged;
        RefreshAttachmentHint();
    }

    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("tr", "Türkçe"),
        new("en", "English")
    ];

    public ObservableCollection<ImageModelProfile> Profiles { get; } = [];
    public ObservableCollection<StudioChatItem> Chats { get; } = [];
    public ObservableCollection<ImageAttachmentItem> Attachments { get; } = [];

    [ObservableProperty] private ImageModelProfile? _selectedProfile;
    [ObservableProperty] private StudioChatItem? _selectedChat;
    [ObservableProperty] private string _prompt = string.Empty;
    [ObservableProperty] private LanguageOption? _selectedLanguage;
    [ObservableProperty] private string _statusText = Loc.T("Ready");
    [ObservableProperty] private string _translatedPrompt = string.Empty;
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private ImageSource? _previewImage;
    [ObservableProperty] private ImageSource? _compareSourceImage;
    [ObservableProperty] private bool _isCompareMode;
    [ObservableProperty] private string _attachmentHint = Loc.Tf("AttachmentHint", 0, ImageModelPresets.MaxImagesPerChat);
    [ObservableProperty] private string _modeHint = Loc.T("ModeTextToImage");

    public bool CanGenerate =>
        !IsGenerating && !string.IsNullOrWhiteSpace(Prompt) && SelectedProfile is not null;

    public bool CanAddImages => !IsGenerating && Attachments.Count < ImageModelPresets.MaxImagesPerChat;
    public bool CanAddResult => !IsGenerating && _currentImageBytes is { Length: > 0 } &&
                                Attachments.Count < ImageModelPresets.MaxImagesPerChat;

    partial void OnPromptChanged(string value) => GenerateCommand.NotifyCanExecuteChanged();
    partial void OnIsGeneratingChanged(bool value)
    {
        GenerateCommand.NotifyCanExecuteChanged();
        AddImagesCommand.NotifyCanExecuteChanged();
        AddResultAsInputCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedProfileChanged(ImageModelProfile? value)
    {
        GenerateCommand.NotifyCanExecuteChanged();
        RefreshModeHint();
        if (_suppressProfileSave || value is null)
            return;

        _ = PersistActiveProfileAsync(value.Id);
    }

    partial void OnSelectedChatChanged(StudioChatItem? oldValue, StudioChatItem? newValue)
    {
        if (_suppressChatSwitch || newValue is null)
            return;

        if (oldValue is not null)
            PersistChat(oldValue);

        ApplyChat(newValue);
    }

    public async Task InitializeAsync()
    {
        await ReloadProfilesAsync().ConfigureAwait(true);
    }

    private async Task ReloadProfilesAsync()
    {
        var store = await _settingsRepository.GetAsync().ConfigureAwait(true);
        _suppressProfileSave = true;
        try
        {
            Profiles.Clear();
            foreach (var profile in store.Profiles)
                Profiles.Add(profile);

            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == store.ActiveProfileId)
                              ?? Profiles.FirstOrDefault();
        }
        finally
        {
            _suppressProfileSave = false;
        }

        RefreshReadyStatus();
        RefreshModeHint();
    }

    partial void OnSelectedLanguageChanged(LanguageOption? value)
    {
        if (value is null)
            return;
        Loc.Current.SetLanguage(value.Code);
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            ApplyLanguageChange();
            return;
        }

        if (dispatcher.CheckAccess())
            ApplyLanguageChange();
        else
            dispatcher.Invoke(ApplyLanguageChange);
    }

    private void ApplyLanguageChange()
    {
        for (var i = 0; i < Chats.Count; i++)
            Chats[i].Title = Loc.Tf("ChatTitle", i + 1);

        RefreshAttachmentHint();
        RefreshModeHint();
        if (!IsGenerating)
            RefreshReadyStatus();
    }

    private void RefreshReadyStatus()
    {
        StatusText = SelectedProfile is null || string.IsNullOrWhiteSpace(SelectedProfile.ApiKey)
            ? Loc.T("AddApiKey")
            : Loc.T("Ready");
    }

    private async Task PersistActiveProfileAsync(string profileId)
    {
        try
        {
            var store = await _settingsRepository.GetAsync().ConfigureAwait(true);
            store.ActiveProfileId = profileId;
            await _settingsRepository.SaveAsync(store).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save active profile");
        }
    }

    [RelayCommand]
    private void NewChat()
    {
        PersistCurrentChat();
        var chat = new StudioChatItem { Title = Loc.Tf("ChatTitle", Chats.Count + 1) };
        Chats.Add(chat);
        _suppressChatSwitch = true;
        SelectedChat = chat;
        _suppressChatSwitch = false;
        Attachments.Clear();
        Prompt = string.Empty;
        TranslatedPrompt = string.Empty;
        PreviewImage = null;
        CompareSourceImage = null;
        IsCompareMode = false;
        _currentImageBytes = null;
        RefreshAttachmentHint();
        RefreshModeHint();
        StatusText = Loc.Tf("NewChatStatus", chat.Title);
        AddImagesCommand.NotifyCanExecuteChanged();
        AddResultAsInputCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanAddImages))]
    private void AddImages()
    {
        var dialog = new OpenFileDialog
        {
            Filter = Loc.T("ImagesFilter"),
            Multiselect = true,
            Title = Loc.Tf("AddImagesTitle", ImageModelPresets.MaxImagesPerChat)
        };

        if (dialog.ShowDialog() != true)
            return;

        AddImageFiles(dialog.FileNames);
    }

    public void AddImageFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (Attachments.Count >= ImageModelPresets.MaxImagesPerChat)
            {
                StatusText = Loc.T("MaxImagesInChat");
                break;
            }

            try
            {
                var bytes = File.ReadAllBytes(path);
                var preview = ImageCodec.LoadDisplay(bytes);
                var item = new ImageAttachmentItem
                {
                    FileName = Path.GetFileName(path),
                    Bytes = bytes,
                    Preview = preview,
                    Thumbnail = ImageCodec.CreateThumbnail(preview),
                    IsPrimary = Attachments.Count == 0
                };
                Attachments.Add(item);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load image {Path}", path);
                StatusText = Loc.Tf("ImageReadFailed", Path.GetFileName(path));
            }
        }

        if (Attachments.Count > 0 && Attachments.All(a => !a.IsPrimary))
            Attachments[0].IsPrimary = true;

        PersistCurrentChat();
        RefreshCompareSource();
        RefreshAttachmentHint();
        RefreshModeHint();
        AddImagesCommand.NotifyCanExecuteChanged();
        AddResultAsInputCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void RemoveAttachment(ImageAttachmentItem? item)
    {
        if (item is null)
            return;

        var wasPrimary = item.IsPrimary;
        Attachments.Remove(item);
        if (wasPrimary && Attachments.Count > 0)
            Attachments[0].IsPrimary = true;

        PersistCurrentChat();
        RefreshCompareSource();
        RefreshAttachmentHint();
        RefreshModeHint();
        AddImagesCommand.NotifyCanExecuteChanged();
        AddResultAsInputCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectAttachment(ImageAttachmentItem? item)
    {
        if (item is null)
            return;

        foreach (var a in Attachments)
            a.IsPrimary = a.Id == item.Id;

        RefreshCompareSource();
    }

    [RelayCommand(CanExecute = nameof(CanAddResult))]
    private void AddResultAsInput()
    {
        if (_currentImageBytes is null || _currentImageBytes.Length == 0)
            return;

        if (Attachments.Count >= ImageModelPresets.MaxImagesPerChat)
        {
            StatusText = Loc.T("ChatFull");
            return;
        }

        try
        {
            var preview = ImageCodec.LoadDisplay(_currentImageBytes);
            var item = new ImageAttachmentItem
            {
                FileName = Loc.Tf("ResultFileName", DateTime.Now.ToString("HHmmss")),
                Bytes = _currentImageBytes,
                Preview = preview,
                Thumbnail = ImageCodec.CreateThumbnail(preview),
                IsPrimary = Attachments.Count == 0
            };
            Attachments.Add(item);
            PersistCurrentChat();
            RefreshCompareSource();
            RefreshAttachmentHint();
            RefreshModeHint();
            StatusText = Loc.T("ResultAdded");
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }

        AddImagesCommand.NotifyCanExecuteChanged();
        AddResultAsInputCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ToggleCompare()
    {
        if (PreviewImage is null || Attachments.Count == 0)
        {
            StatusText = Loc.T("CompareNeedImages");
            return;
        }

        IsCompareMode = !IsCompareMode;
        RefreshCompareSource();
    }

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateAsync()
    {
        if (SelectedProfile is null)
            return;

        if (string.IsNullOrWhiteSpace(SelectedProfile.ApiKey))
        {
            StatusText = Loc.T("ApiKeyMissing");
            return;
        }

        IsGenerating = true;
        TranslatedPrompt = string.Empty;
        StatusText = Loc.T("Translating");
        _cts = new CancellationTokenSource();
        var dispatcher = Application.Current.Dispatcher;
        var inputBytes = Attachments.Select(a => a.Bytes).ToList();
        var workProfile = SelectedProfile;

        try
        {
            var translation = await _translationService
                .ToEnglishPromptAsync(Prompt.Trim(), SelectedProfile.ApiKey, _cts.Token)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(translation.Error))
            {
                await dispatcher.InvokeAsync(() =>
                {
                    StatusText = Loc.T("TranslationFailed");
                    MessageBox.Show(translation.Error, "Halim Labs 3",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                });
                return;
            }

            var englishPrompt = translation.Prompt;

            await dispatcher.InvokeAsync(() =>
            {
                TranslatedPrompt = translation.Translated ? englishPrompt : string.Empty;
                var modelNote = !ReferenceEquals(workProfile, SelectedProfile) &&
                                workProfile.Name != SelectedProfile.Name
                    ? $" · {workProfile.Name}"
                    : $" · {SelectedProfile.Name}";
                StatusText = (translation.Translated
                    ? $"EN ({translation.Engine}): {englishPrompt}"
                    : Loc.Tf("Generating", modelNote)) + (inputBytes.Count > 0 ? Loc.Tf("WithImages", inputBytes.Count) : "");
            });

            var result = await GenerateWithFilterRetryAsync(
                    workProfile, englishPrompt, inputBytes, dispatcher, _cts.Token)
                .ConfigureAwait(false);

            await dispatcher.InvokeAsync(() => ApplyGenerationResult(result));
        }
        catch (OperationCanceledException)
        {
            await dispatcher.InvokeAsync(() => StatusText = Loc.T("Cancelled"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Generate failed");
            await dispatcher.InvokeAsync(() =>
            {
                StatusText = Loc.T("Error");
                MessageBox.Show(ex.Message, "Halim Labs 3", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }
        finally
        {
            await dispatcher.InvokeAsync(() => IsGenerating = false);
            _cts?.Dispose();
            _cts = null;
        }
    }

    private async Task<ImageGenerationResult> GenerateWithFilterRetryAsync(
        ImageModelProfile profile,
        string englishPrompt,
        IReadOnlyList<byte[]> inputImages,
        System.Windows.Threading.Dispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        if (inputImages.Count == 0)
            return await GenerateOnceAsync(profile, englishPrompt, inputImages, cancellationToken)
                .ConfigureAwait(false);

        if (ImageModelPresets.IsHostedNvidia(profile))
        {
            var lookalike = await TryLookalikeAsync(englishPrompt, inputImages, profile.ApiKey, dispatcher, cancellationToken)
                .ConfigureAwait(false);
            if (lookalike is not null)
                return lookalike;
            return ImageGenerationResult.Fail(
                Loc.T("PhotoEditUnavailable"), TimeSpan.Zero);
        }

        return await GenerateOnceAsync(profile, englishPrompt, inputImages, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<ImageGenerationResult?> TryLookalikeAsync(
        string englishPrompt,
        IReadOnlyList<byte[]> inputImages,
        string apiKey,
        System.Windows.Threading.Dispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        await dispatcher.InvokeAsync(() =>
            StatusText = Loc.T("ReadingImages"));

        var caption = await _captionService
            .BuildTryOnPromptAsync(inputImages, englishPrompt, apiKey, cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(caption))
            return null;

        await dispatcher.InvokeAsync(() => TranslatedPrompt = caption);

        var flux = (Profiles.FirstOrDefault(ImageModelPresets.LooksLikeFluxDev)
                    ?? ImageModelPresets.CreateFluxDev(apiKey)).Clone();
        flux.ApiKey = apiKey;
        flux.Steps = 28;
        flux.CfgScale = 4;

        var result = await GenerateOnceAsync(flux, caption, [], cancellationToken).ConfigureAwait(false);
        var filtered = await dispatcher.InvokeAsync(() => LooksLikeContentFilter(result));
        if (result.Success && !filtered)
        {
            await dispatcher.InvokeAsync(() =>
            {
                var existing = Profiles.FirstOrDefault(ImageModelPresets.LooksLikeFluxDev);
                if (existing is not null)
                    SelectedProfile = existing;
                StatusText = Loc.T("DoneTryOn");
            });
            return result;
        }

        return null;
    }

    private Task<ImageGenerationResult> GenerateOnceAsync(
        ImageModelProfile profile,
        string englishPrompt,
        IReadOnlyList<byte[]> inputImages,
        CancellationToken cancellationToken) =>
        _imageService.GenerateAsync(profile, englishPrompt, inputImages, cancellationToken);

    private void ApplyGenerationResult(ImageGenerationResult result)
    {
        if (!result.Success || result.ImageBytes is null)
        {
            if (result.ContentFiltered)
                PreviewImage = null;
            StatusText = result.ContentFiltered ? Loc.T("Filtered") : Loc.T("Error");
            MessageBox.Show(result.ErrorMessage ?? Loc.T("GenerationFailed"), "Halim Labs 3",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var bmp = ImageCodec.LoadDisplay(result.ImageBytes);
            if (IsNearlyBlack(bmp))
            {
                PreviewImage = null;
                _currentImageBytes = null;
                StatusText = Attachments.Count > 0 ? Loc.T("EditFailedStatus") : Loc.T("Filtered");
                MessageBox.Show(
                    Attachments.Count > 0
                        ? Loc.T("EditFailedMessage")
                        : Loc.T("ContentFilterMessage"),
                    "Halim Labs 3",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            _currentImageBytes = result.ImageBytes;
            PreviewImage = bmp;
            IsCompareMode = Attachments.Count > 0;
            RefreshCompareSource();
            PersistCurrentChat();
            var enNote = string.IsNullOrWhiteSpace(TranslatedPrompt) ? string.Empty : " · TR→EN";
            var editNote = Attachments.Count > 0 ? Loc.Tf("WithImages", Attachments.Count) : string.Empty;
            StatusText = Loc.Tf("DoneStatus", result.Duration.TotalSeconds, bmp.PixelWidth, bmp.PixelHeight, enNote, editNote);
            AddResultAsInputCommand.NotifyCanExecuteChanged();
        }
        catch (Exception decodeEx)
        {
            PreviewImage = null;
            StatusText = Loc.T("Error");
            MessageBox.Show(
                Loc.Tf("ImageDisplayFailed", decodeEx.Message),
                "Halim Labs 3",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static bool LooksLikeContentFilter(ImageGenerationResult result)
    {
        if (result.ContentFiltered)
            return true;
        if (!result.Success || result.ImageBytes is null)
            return false;

        try
        {
            return IsNearlyBlack(ImageCodec.LoadDisplay(result.ImageBytes));
        }
        catch
        {
            return false;
        }
    }

    [RelayCommand]
    private void Stop() => _cts?.Cancel();

    [RelayCommand]
    private void SaveImage()
    {
        if (_currentImageBytes is null || _currentImageBytes.Length == 0)
        {
            StatusText = Loc.T("NoImageToSave");
            return;
        }

        var isJpeg = _currentImageBytes.Length > 3
                     && _currentImageBytes[0] == 0xFF
                     && _currentImageBytes[1] == 0xD8;
        var dialog = new SaveFileDialog
        {
            Filter = Loc.T("SaveFilter"),
            FilterIndex = isJpeg ? 1 : 2,
            FileName = $"halimlabs3-{DateTime.Now:yyyyMMdd-HHmmss}.{(isJpeg ? "jpg" : "png")}"
        };

        if (dialog.ShowDialog() != true)
            return;

        File.WriteAllBytes(dialog.FileName, _currentImageBytes);
        StatusText = Loc.T("ImageSaved");
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var vm = _settingsFactory();
        var window = new Views.SettingsWindow
        {
            Owner = Application.Current.MainWindow,
            DataContext = vm
        };
        await vm.InitializeAsync().ConfigureAwait(true);
        window.ShowDialog();
        await vm.SaveCommand.ExecuteAsync(null).ConfigureAwait(true);
        await ReloadProfilesAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void OpenHelp()
    {
        var window = new Views.HelpWindow
        {
            Owner = Application.Current.MainWindow,
            DataContext = _helpFactory()
        };
        window.ShowDialog();
    }

    [RelayCommand]
    private void CopyUsdt()
    {
        var s = _supportInfoProvider.Current;
        Clipboard.SetText(s.UsdtAddress);
        StatusText = Loc.T("UsdtCopied");
        MessageBox.Show(Loc.Tf("UsdtDialog", s.UsdtAddress), "USDT", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void OpenKofi()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _supportInfoProvider.Current.KofiUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    [RelayCommand]
    private void ShowIban()
    {
        var s = _supportInfoProvider.Current;
        var text = Loc.Tf("IbanDialog", s.BankName, s.IbanHolder, s.Iban);
        if (MessageBox.Show(text, "IBAN", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
        {
            Clipboard.SetText(s.Iban);
            StatusText = Loc.T("IbanCopied");
        }
    }

    private void PersistCurrentChat() => PersistChat(SelectedChat);

    private void PersistChat(StudioChatItem? chat)
    {
        if (chat is null)
            return;

        chat.Prompt = Prompt;
        chat.Attachments = Attachments.ToList();
        chat.ResultBytes = _currentImageBytes;
        chat.ResultPreview = PreviewImage;
        chat.CompareMode = IsCompareMode;
    }

    private void ApplyChat(StudioChatItem chat)
    {
        Attachments.Clear();
        foreach (var item in chat.Attachments)
            Attachments.Add(item);

        Prompt = chat.Prompt;
        _currentImageBytes = chat.ResultBytes;
        PreviewImage = chat.ResultPreview;
        IsCompareMode = chat.CompareMode && chat.Attachments.Count > 0 && chat.ResultPreview is not null;
        RefreshCompareSource();
        RefreshAttachmentHint();
        RefreshModeHint();
        AddImagesCommand.NotifyCanExecuteChanged();
        AddResultAsInputCommand.NotifyCanExecuteChanged();
    }

    private void RefreshCompareSource()
    {
        var primary = Attachments.FirstOrDefault(a => a.IsPrimary) ?? Attachments.FirstOrDefault();
        CompareSourceImage = primary?.Preview;
        if (Attachments.Count == 0)
            IsCompareMode = false;
    }

    private void RefreshAttachmentHint() =>
        AttachmentHint = Loc.Tf("AttachmentHint", Attachments.Count, ImageModelPresets.MaxImagesPerChat);

    private void RefreshModeHint()
    {
        if (Attachments.Count == 0)
        {
            ModeHint = Loc.T("ModeTextToImage");
            return;
        }

        ModeHint = Loc.Tf("ModeWithImages", Attachments.Count, SelectedProfile?.Name ?? "model");
    }

    internal static bool IsNearlyBlack(BitmapSource source)
    {
        BitmapSource bgra = source;
        if (source.Format != PixelFormats.Bgra32 && source.Format != PixelFormats.Pbgra32)
            bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        var width = bgra.PixelWidth;
        var height = bgra.PixelHeight;
        if (width <= 0 || height <= 0)
            return true;

        var stride = width * 4;
        var pixels = new byte[height * stride];
        bgra.CopyPixels(pixels, stride, 0);

        var max = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var lum = pixels[i] + pixels[i + 1] + pixels[i + 2];
            if (lum > max)
                max = lum;
            if (max >= 24)
                return false;
        }

        return true;
    }
}

using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows.Data;

namespace HalimLabs.Localization;

public sealed class Loc : INotifyPropertyChanged
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static Loc Current { get; } = new();

    private readonly Dictionary<string, string> _tr;
    private readonly Dictionary<string, string> _en;
    private readonly string _filePath;
    private AppLanguage _language;

    private Loc()
    {
        _tr = BuildTurkish();
        _en = BuildEnglish();

        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HalimLabs3");
        Directory.CreateDirectory(folder);
        _filePath = Path.Combine(folder, "ui-settings.json");

        _language = LoadSavedLanguage() ?? DetectSystemLanguage();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LanguageChanged;

    public AppLanguage Language => _language;

    public string this[string key] => Get(key);

    public static string T(string key) => Current.Get(key);

    public static string Tf(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Current.Get(key), args);

    public void SetLanguage(AppLanguage language)
    {
        if (_language == language)
            return;

        _language = language;
        Save();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Binding.IndexerName));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetLanguage(string? code) => SetLanguage(AppLanguageExtensions.FromCode(code));

    private string Get(string key)
    {
        var table = _language == AppLanguage.English ? _en : _tr;
        if (table.TryGetValue(key, out var value))
            return value;
        if (_en.TryGetValue(key, out value))
            return value;
        return key;
    }

    private AppLanguage? LoadSavedLanguage()
    {
        try
        {
            if (!File.Exists(_filePath))
                return null;

            using var stream = File.OpenRead(_filePath);
            var settings = JsonSerializer.Deserialize<UiSettings>(stream, JsonOptions);
            return settings?.Language is null
                ? null
                : AppLanguageExtensions.FromCode(settings.Language);
        }
        catch
        {
            return null;
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(new UiSettings { Language = _language.ToCode() }, JsonOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // local preference only
        }
    }

    private static AppLanguage DetectSystemLanguage() =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("tr", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Turkish
            : AppLanguage.English;

    private sealed class UiSettings
    {
        public string Language { get; set; } = "tr";
    }

    private static Dictionary<string, string> BuildTurkish() => new()
    {
        ["Language"] = "Dil",
        ["Model"] = "Model",
        ["ModelTooltip"] = "Model değiştir (Cursor ajan seçici gibi)",
        ["NewChat"] = "Yeni Sohbet",
        ["Help"] = "Yardım",
        ["Settings"] = "Ayarlar",
        ["Chats"] = "Sohbetler",
        ["ChatLimitHint"] = "Her sohbette en fazla 5 görsel. Yeni sohbette 5 tane daha.",
        ["Source"] = "Kaynak",
        ["Result"] = "Sonuç",
        ["EmptyPreview"] = "Prompt yaz veya görsel sürükle · Üret",
        ["AddImage"] = "+ Görsel",
        ["AddResult"] = "Sonucu ekle",
        ["AddResultTooltip"] = "Üretilen görseli sonraki düzenlemeye kaynak olarak ekle",
        ["Compare"] = "Karşılaştır",
        ["Stop"] = "Durdur",
        ["SaveImage"] = "Görseli Kaydet",
        ["Generate"] = "Üret",
        ["EnglishPromptPrefix"] = "İngilizce prompt: ",
        ["CopyUsdt"] = "USDT Adresini Kopyala",
        ["OpenKofi"] = "Ko-fi Aç",
        ["IbanInformation"] = "IBAN Bilgisi",
        ["FooterText"] = "Geliştiren: Halim Toklu",
        ["SupportText"] = "💖 Bu projeyi destekle",

        ["SettingsTitle"] = "Ayarlar — Görsel Modelleri",
        ["ImageModels"] = "Görsel Modelleri",
        ["AddCustom"] = "Özel Ekle",
        ["AddCustomTooltip"] = "Kendi URL ve anahtarınla herhangi bir model ekle",
        ["Duplicate"] = "Çoğalt",
        ["Remove"] = "Sil",
        ["Test"] = "Test",
        ["Save"] = "Kaydet",
        ["SettingsHint"] = "Sınırsız model ekleyebilirsin (20+). Her modelin kendi API key'i olur. Ana ekrandaki Model menüsünden anında değiştir.",
        ["DisplayName"] = "Görünen Ad",
        ["ApiKey"] = "API Key",
        ["ApiMode"] = "API Modu",
        ["ApiBaseUrl"] = "API Base URL",
        ["ModelId"] = "Model ID",
        ["Steps"] = "Adım",
        ["CfgScale"] = "CFG Scale",
        ["Seed"] = "Seed",
        ["RandomSeed"] = "Her üretimde rastgele seed",
        ["Close"] = "Kapat",

        ["HelpTitle"] = "Halim Labs 3 — Yardım",
        ["HelpWindowTitle"] = "Yardım — Halim Labs 3",
        ["HelpContent"] =
"""
# Halim Labs 3

Halim Labs 2'nin görsel stüdyosu + **görsel yükleme / düzenleme / karşılaştırma**.

## Dil

Üst menüden **Türkçe** veya **English** seçebilirsin. Seçim bu bilgisayarda hatırlanır.

## Sohbet ve 5 görsel

- Her sohbette en fazla **5 görsel** eklenir.
- 5 dolduysa **Yeni Sohbet** aç — orada 5 tane daha ekleyebilirsin.
- Görselleri sürükle-bırak veya **+ Görsel**.
- Küçük resme tıkla: o kaynak “ana görsel” olur (karşılaştırmada solda).

## Ne yapar

- Görsel yok: metinden görsel (FLUX.1-dev).
- 2 görsel (kişi + kıyafet): her resim ayrı okunur, **FLUX.1-dev** ile yeni kare üretilir. NVIDIA cloud senin fotoğrafının üzerine çizmez; yüz birebir aynı olmayabilir.
- **Karşılaştır**: kaynak | sonuç yan yana.
- **Sonucu ekle**: üretilen kareyi sonraki düzenlemenin kaynağı yap.

Örnek promptlar:
- `1. resimdeki kişiye 2. resimdeki kıyafeti giydir`
- `bu kıyafeti kırmızı yap, yüzü değiştirme`
- `stüdyo ışığı ekle, arka planı sadeleştir`

## Modeller

- **FLUX.1-dev** — metinden görsel
- **FLUX.1-Kontext** — fotoğraf düzenle (cloud'da asıl çalışan)
- **FLUX.2-klein-4b** — NVIDIA cloud'da sadece örnek resim; kendi fotoğrafın için lokal NIM gerekir
- **Qwen-Image-Edit** — yedek düzenleme modeli
- **Qwen-Image** — hosted / lokal NIM

API key'ler Halim Labs 2'den otomatik kopyalanır.

## Siyah görsel / example_id

NVIDIA cloud Klein `Expected: example_id, got: base64` derse bu senin hatan değil: o API kendi fotoğrafını almıyor. Program otomatik Kontext'e geçer.
""",

        ["Ready"] = "Hazır",
        ["AddApiKey"] = "Ayarlar'dan API key ekle",
        ["ChatTitle"] = "Sohbet {0}",
        ["NewChatStatus"] = "{0} — 5 görsele kadar ekleyebilirsin",
        ["ImagesFilter"] = "Görseller|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.gif",
        ["AddImagesTitle"] = "Görsel ekle (en fazla {0})",
        ["MaxImagesInChat"] = "Bu sohbette en fazla 5 görsel. Yeni Sohbet aç, 5 tane daha ekle.",
        ["ImageReadFailed"] = "Görsel okunamadı: {0}",
        ["ChatFull"] = "5 görsel doldu. Yeni Sohbet aç veya birini sil.",
        ["ResultFileName"] = "sonuc-{0}.jpg",
        ["ResultAdded"] = "Sonuç bir sonraki düzenlemeye eklendi",
        ["CompareNeedImages"] = "Karşılaştırmak için görsel ekle ve üret",
        ["ApiKeyMissing"] = "API Key yok. Ayarlar'ı aç.",
        ["Translating"] = "Türkçe → İngilizce çevriliyor…",
        ["TranslationFailed"] = "Çeviri başarısız",
        ["Generating"] = "Üretiliyor{0}…",
        ["WithImages"] = " · {0} görsel",
        ["Cancelled"] = "İptal edildi",
        ["Error"] = "Hata",
        ["ReadingImages"] = "Görseller okunuyor, kıyafet tarif edilip yeni kare üretiliyor…",
        ["DoneTryOn"] = "Bitti · kıyafet giydirme (yeni kare)",
        ["Filtered"] = "Filtrelendi",
        ["GenerationFailed"] = "Üretim başarısız.",
        ["EditFailedStatus"] = "Düzenleme başarısız",
        ["DoneStatus"] = "Bitti ({0:0.0}s) · {1}x{2}{3}{4}",
        ["ImageDisplayFailed"] = "Görsel alındı ama gösterilemedi.\n{0}",
        ["NoImageToSave"] = "Kaydedilecek görsel yok",
        ["SaveFilter"] = "JPEG Görsel|*.jpg|PNG Görsel|*.png",
        ["ImageSaved"] = "Görsel kaydedildi",
        ["UsdtCopied"] = "USDT kopyalandı",
        ["UsdtDialog"] = "USDT (TRC20)\n\n{0}",
        ["IbanDialog"] = "Banka: {0}\nHesap sahibi: {1}\nIBAN: {2}\n\nIBAN kopyalansın mı?",
        ["IbanCopied"] = "IBAN kopyalandı",
        ["AttachmentHint"] = "{0}/{1} görsel",
        ["ModeTextToImage"] = "Metinden görsel · NVIDIA",
        ["ModeWithImages"] = "{0} görsel · {1}",
        ["UnexpectedError"] = "Beklenmeyen hata:\n{0}",

        ["ModelsLoaded"] = "{0} model yüklendi",
        ["AddedCustom"] = "Özel model eklendi",
        ["AddedFlux"] = "FLUX.1-dev eklendi",
        ["AddedKontext"] = "FLUX.1-Kontext eklendi",
        ["AddedKlein"] = "FLUX.2-klein-4b eklendi",
        ["AddedQwen"] = "Qwen-Image (hosted) eklendi",
        ["AddedQwenLocal"] = "Qwen-Image (lokal NIM) eklendi",
        ["CopySuffix"] = "{0} (kopya)",
        ["DuplicatedModel"] = "Model çoğaltıldı",
        ["KeepOneModel"] = "En az bir model kalsın",
        ["RemovedModel"] = "Model silindi",
        ["Saved"] = "Kaydedildi",
        ["SelectModelFirst"] = "Önce bir model seç",
        ["Testing"] = "Test ediliyor…",
        ["TestOk"] = "Tamam ({0:0.0}s)",
        ["Failed"] = "Başarısız",
        ["ConnectionOk"] = "Bağlantı tamam",
        ["ConnectionFailed"] = "Bağlantı başarısız",
        ["NewModel"] = "Yeni Model",

        ["ApiKeyRequired"] = "API Key gerekli.",
        ["PromptRequired"] = "Prompt gerekli.",
        ["ApiBaseUrlRequired"] = "API Base URL gerekli.",
        ["NoImageData"] = "Yanıtta görsel verisi yoktu.",
        ["Unauthorized"] = "Yetkisiz ({0}). API key'i kontrol et. {1}",
        ["EndpointNotFound"] = "Uç nokta bulunamadı (404). Bu model NVIDIA hesabında cloud olarak açık değil. {0}",
        ["RateLimit"] = "İstek limiti (429). Biraz bekleyip tekrar dene. {0}",
        ["ServiceBusy"] = "Servis meşgul (529). Kısa süre sonra tekrar dene. {0}",
        ["ServerError"] = "Sunucu hatası ({0}). {1}",
        ["ApiError"] = "API hatası {0}: {1}",
        ["Rejected422Field"] = "İstek reddedildi (422). NVIDIA bu alanı kabul etmiyor. {0}",
        ["Rejected422"] = "İstek reddedildi (422). {0}",
        ["NoDetails"] = "Ayrıntı yok.",

        ["ContentFilterMessage"] =
            "NVIDIA güvenlik filtresi bu görseli kesti — bu yüzden siyah kare geliyor.\n\n" +
            "Kıyafet giydirme masum bir istek olsa da cloud FLUX bazen kişi fotoğrafını keser. Üret'e tekrar bas (farklı seed).",
        ["EditFailedMessage"] =
            "Görsel düzenleme başarısız oldu (siyah kare veya geçersiz yanıt).\n\n" +
            "Üstte FLUX.2-klein-4b seçili olsun. İki görsel: 1) kişi, 2) kıyafet. Promptu kısa tut, örneğin: put the yellow shirt on the person, keep the same face and background.",
        ["PhotoEditUnavailable"] =
            "Bu NVIDIA key ile kendi fotoğrafını düzenleyemiyoruz.\n\n" +
            "FLUX.1-Kontext ve Qwen-Image-Edit hesabında cloud olarak yok (404).\n" +
            "FLUX.2-Klein var ama senin fotoğrafını almıyor; sadece NVIDIA'nın örnek resimleri.\n\n" +
            "Metinden görsel için model menüsünden FLUX.1-dev seç, görselleri kaldır.\n" +
            "Kıyafet giydirme için bu modellerin NVIDIA hesabında açık olması veya lokal NIM gerekir.",
        ["HostedPreviewImage"] =
            "NVIDIA cloud bu modele kendi fotoğrafını yükletmiyor — sadece sitedeki örnek resimleri kabul ediyor (example_id).\n\n" +
            "Kıyafet giydirme için program Kontext / Qwen-Image-Edit ile tekrar dener. " +
            "Onlar da aynı hatayı verirse bu NVIDIA key ile fotoğraf düzenleme kapalıdır; " +
            "Ayarlar'dan lokal NIM (localhost) gerekir."
    };

    private static Dictionary<string, string> BuildEnglish() => new()
    {
        ["Language"] = "Language",
        ["Model"] = "Model",
        ["ModelTooltip"] = "Switch model (like Cursor agent picker)",
        ["NewChat"] = "New Chat",
        ["Help"] = "Help",
        ["Settings"] = "Settings",
        ["Chats"] = "Chats",
        ["ChatLimitHint"] = "Up to 5 images per chat. Open a new chat for 5 more.",
        ["Source"] = "Source",
        ["Result"] = "Result",
        ["EmptyPreview"] = "Write a prompt or drop an image · Generate",
        ["AddImage"] = "+ Image",
        ["AddResult"] = "Add result",
        ["AddResultTooltip"] = "Use the generated image as the next edit source",
        ["Compare"] = "Compare",
        ["Stop"] = "Stop",
        ["SaveImage"] = "Save Image",
        ["Generate"] = "Generate",
        ["EnglishPromptPrefix"] = "English prompt: ",
        ["CopyUsdt"] = "Copy USDT Address",
        ["OpenKofi"] = "Open Ko-fi",
        ["IbanInformation"] = "IBAN Information",
        ["FooterText"] = "Developed by Halim Toklu",
        ["SupportText"] = "💖 Support this project",

        ["SettingsTitle"] = "Settings — Image Models",
        ["ImageModels"] = "Image Models",
        ["AddCustom"] = "Add Custom",
        ["AddCustomTooltip"] = "Add any model with your own URL and key",
        ["Duplicate"] = "Duplicate",
        ["Remove"] = "Remove",
        ["Test"] = "Test",
        ["Save"] = "Save",
        ["SettingsHint"] = "Add as many models as you want (20+). Each model has its own API key. Switch instantly from the Model menu.",
        ["DisplayName"] = "Display Name",
        ["ApiKey"] = "API Key",
        ["ApiMode"] = "API Mode",
        ["ApiBaseUrl"] = "API Base URL",
        ["ModelId"] = "Model ID",
        ["Steps"] = "Steps",
        ["CfgScale"] = "CFG Scale",
        ["Seed"] = "Seed",
        ["RandomSeed"] = "Random seed each generation",
        ["Close"] = "Close",

        ["HelpTitle"] = "Halim Labs 3 — Help",
        ["HelpWindowTitle"] = "Help — Halim Labs 3",
        ["HelpContent"] =
"""
# Halim Labs 3

The image studio from Halim Labs 2 plus **upload / edit / compare**.

## Language

Use the top menu to switch between **Türkçe** and **English**. Your choice is remembered on this computer.

## Chats and 5 images

- Each chat can hold up to **5 images**.
- If 5 are full, open **New Chat** — you can add 5 more there.
- Drag and drop images or use **+ Image**.
- Click a thumbnail: that source becomes the primary image (left side in compare).

## What it does

- No image: text-to-image (FLUX.1-dev).
- 2 images (person + garment): each photo is read separately, then a new frame is generated with **FLUX.1-dev**. NVIDIA cloud does not paint over your photo; the face may not match 1:1.
- **Compare**: source | result side by side.
- **Add result**: use the generated frame as the next edit source.

Example prompts:
- `put the clothes from image 2 on the person in image 1`
- `make this garment red, do not change the face`
- `add studio lighting, simplify the background`

## Models

- **FLUX.1-dev** — text to image
- **FLUX.1-Kontext** — photo edit (the one that actually works in the cloud)
- **FLUX.2-klein-4b** — NVIDIA cloud only accepts sample images; local NIM is required for your own photos
- **Qwen-Image-Edit** — backup edit model
- **Qwen-Image** — hosted / local NIM

API keys are copied automatically from Halim Labs 2.

## Black image / example_id

If NVIDIA cloud Klein says `Expected: example_id, got: base64`, that is not your fault: that API will not take your photo. The app automatically falls back to Kontext.
""",

        ["Ready"] = "Ready",
        ["AddApiKey"] = "Add API key in Settings",
        ["ChatTitle"] = "Chat {0}",
        ["NewChatStatus"] = "{0} — you can add up to 5 images",
        ["ImagesFilter"] = "Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp;*.gif",
        ["AddImagesTitle"] = "Add images (max {0})",
        ["MaxImagesInChat"] = "This chat already has 5 images. Open New Chat to add 5 more.",
        ["ImageReadFailed"] = "Could not read image: {0}",
        ["ChatFull"] = "5 images already. Open New Chat or remove one.",
        ["ResultFileName"] = "result-{0}.jpg",
        ["ResultAdded"] = "Result added as the next edit source",
        ["CompareNeedImages"] = "Add an image and generate to compare",
        ["ApiKeyMissing"] = "API Key missing. Open Settings.",
        ["Translating"] = "Translating Turkish → English…",
        ["TranslationFailed"] = "Translation failed",
        ["Generating"] = "Generating{0}…",
        ["WithImages"] = " · {0} images",
        ["Cancelled"] = "Cancelled",
        ["Error"] = "Error",
        ["ReadingImages"] = "Reading images, describing the garment, generating a new frame…",
        ["DoneTryOn"] = "Done · virtual try-on (new frame)",
        ["Filtered"] = "Filtered",
        ["GenerationFailed"] = "Generation failed.",
        ["EditFailedStatus"] = "Edit failed",
        ["DoneStatus"] = "Done ({0:0.0}s) · {1}x{2}{3}{4}",
        ["ImageDisplayFailed"] = "Image received but could not be displayed.\n{0}",
        ["NoImageToSave"] = "No image to save",
        ["SaveFilter"] = "JPEG Image|*.jpg|PNG Image|*.png",
        ["ImageSaved"] = "Image saved",
        ["UsdtCopied"] = "USDT copied",
        ["UsdtDialog"] = "USDT (TRC20)\n\n{0}",
        ["IbanDialog"] = "Bank: {0}\nHolder: {1}\nIBAN: {2}\n\nCopy IBAN?",
        ["IbanCopied"] = "IBAN copied",
        ["AttachmentHint"] = "{0}/{1} images",
        ["ModeTextToImage"] = "Text to image · NVIDIA",
        ["ModeWithImages"] = "{0} images · {1}",
        ["UnexpectedError"] = "Unexpected error:\n{0}",

        ["ModelsLoaded"] = "{0} model(s) loaded",
        ["AddedCustom"] = "Added custom model",
        ["AddedFlux"] = "Added FLUX.1-dev",
        ["AddedKontext"] = "Added FLUX.1-Kontext",
        ["AddedKlein"] = "Added FLUX.2-klein-4b",
        ["AddedQwen"] = "Added Qwen-Image (hosted)",
        ["AddedQwenLocal"] = "Added Qwen-Image (local NIM)",
        ["CopySuffix"] = "{0} (copy)",
        ["DuplicatedModel"] = "Duplicated model",
        ["KeepOneModel"] = "Keep at least one model",
        ["RemovedModel"] = "Removed model",
        ["Saved"] = "Saved",
        ["SelectModelFirst"] = "Select a model first",
        ["Testing"] = "Testing…",
        ["TestOk"] = "OK ({0:0.0}s)",
        ["Failed"] = "Failed",
        ["ConnectionOk"] = "Connection OK",
        ["ConnectionFailed"] = "Connection failed",
        ["NewModel"] = "New Model",

        ["ApiKeyRequired"] = "API Key is required.",
        ["PromptRequired"] = "Prompt is required.",
        ["ApiBaseUrlRequired"] = "API Base URL is required.",
        ["NoImageData"] = "Response did not contain image data.",
        ["Unauthorized"] = "Unauthorized ({0}). Check API key. {1}",
        ["EndpointNotFound"] = "Endpoint not found (404). This model is not enabled as cloud on the NVIDIA account. {0}",
        ["RateLimit"] = "Rate limit (429). Wait and try again. {0}",
        ["ServiceBusy"] = "Service busy (529). Try again shortly. {0}",
        ["ServerError"] = "Server error ({0}). {1}",
        ["ApiError"] = "API error {0}: {1}",
        ["Rejected422Field"] = "Request rejected (422). NVIDIA does not accept this field. {0}",
        ["Rejected422"] = "Request rejected (422). {0}",
        ["NoDetails"] = "No details.",

        ["ContentFilterMessage"] =
            "NVIDIA's safety filter blocked this image — that is why you get a black frame.\n\n" +
            "Virtual try-on is a harmless request, but cloud FLUX sometimes blocks a person photo. Press Generate again (different seed).",
        ["EditFailedMessage"] =
            "Image edit failed (black frame or invalid response).\n\n" +
            "Keep FLUX.2-klein-4b selected at the top. Two images: 1) person, 2) garment. Keep the prompt short, for example: put the yellow shirt on the person, keep the same face and background.",
        ["PhotoEditUnavailable"] =
            "This NVIDIA key cannot edit your own photo.\n\n" +
            "FLUX.1-Kontext and Qwen-Image-Edit are not enabled as cloud on the account (404).\n" +
            "FLUX.2-Klein is available but will not take your photo; only NVIDIA sample images.\n\n" +
            "For text-to-image, pick FLUX.1-dev from the model menu and remove the images.\n" +
            "For virtual try-on those models must be enabled on the NVIDIA account, or you need a local NIM.",
        ["HostedPreviewImage"] =
            "NVIDIA cloud will not let this model use your photo — it only accepts the sample images on the site (example_id).\n\n" +
            "For virtual try-on the app retries with Kontext / Qwen-Image-Edit. " +
            "If those fail too, photo editing is closed for this NVIDIA key; " +
            "a local NIM (localhost) is required in Settings."
    };
}

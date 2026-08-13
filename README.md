# Halim Labs 3

Windows için NVIDIA Build destekli görsel stüdyosu. Metinden görsel üret, fotoğraf ekle, kıyafet giydirmeyi dene. Kurulum yok — ZIP’i açıp çalıştırın.

**API anahtarları bu depoda yoktur.** Ücretsiz `nvapi-...` anahtarını [NVIDIA Build](https://build.nvidia.com) sitesinden kendiniz alın ve Ayarlar’a yapıştırın.

## ZIP olarak indir

Tarayıcı ham `.exe` indirmelerinde “tehlikeli / Sil” uyarısı çıkar. O yüzden program **ZIP** olarak yayınlanır.

1. [Releases](https://github.com/htoklu/HalimLabs3/releases/latest) sayfasından `HalimLabs3.zip` indirin.
2. ZIP’i bir klasöre çıkarın.
3. `HalimLabs3.exe` dosyasına çift tıklayın.

Windows SmartScreen ilk açılışta uyarı verebilir: **Ek bilgi → Yine de çalıştır**.

## Git clone ile indir ve çalıştır

Kaynak kod ve `dist\HalimLabs3.zip` birlikte gelir. Ham EXE GitHub’da durmaz; clone sonrası ZIP açılır.

```powershell
git clone https://github.com/htoklu/HalimLabs3.git
cd HalimLabs3
.\run.bat
```

`run.bat` ZIP’i `dist\` içine açar ve programı başlatır. İsterseniz ZIP’i kendiniz de açabilirsiniz: `dist\HalimLabs3.zip`.

Sonra Ayarlar’a kendi ücretsiz NVIDIA anahtarınızı yapıştırın (aşağıdaki bölüm).

## Ücretsiz NVIDIA API anahtarı

1. [build.nvidia.com](https://build.nvidia.com) adresine gidin ve NVIDIA / e-posta ile giriş yapın.
2. Bir model sayfası açın (örnek: **FLUX.1-dev** veya **Llama**).
3. **Get API Key** ile ücretsiz `nvapi-...` anahtarını oluşturun.
4. Halim Labs 3’ü açın → **Ayarlar**.
5. Görsel modeli (FLUX.1-dev önerilir) seçin, anahtarı **API Key** kutusuna yapıştırın, kaydedin.

Aynı ücretsiz anahtar sohbet sağlayıcıları (DeepSeek, Llama vb.) için de kullanılabilir. Anahtar bilgisayarınızda `%LocalAppData%\HalimLabs3\` altında saklanır; GitHub’a veya ZIP içine yazılmaz.

## Ne yapar

- Görsel yok: metinden görsel (**FLUX.1-dev**)
- Fotoğraf ekle: sohbet başına en fazla 5 görsel; **Yeni Sohbet** ile devam
- 2 görsel (kişi + kıyafet): kısa prompt, örn. `kıyafeti modele giydir`
- Türkçe prompt otomatik İngilizceye çevrilir
- Kaynak | sonuç yan yana karşılaştırma
- Koyu tema, Markdown sohbet

NVIDIA Build **ücretsiz / preview** uçları sizin JPEG’inizi piksel piksel düzenlemez (Klein/Kontext cloud’da `example_id` ister). Program kişi ve kıyafeti okuyup **benzer yeni bir kare** üretir; yüz birebir ChatGPT/Gemini kadar aynı olmayabilir.

## Kaynaktan derleme

Gereksinim: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (Windows). `git clone` sonrası:

```powershell
dotnet run --project src\HalimLabs\HalimLabs.csproj
```

Tek EXE + ZIP üretmek için `build.bat` veya:

```powershell
dotnet publish src\HalimLabs\HalimLabs.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist
```

Çıktı: `dist\HalimLabs3.exe` (yerel) ve `build.bat` ile `dist\HalimLabs3.zip`

Visual Studio 2022 ile `HalimLabs.sln` dosyasını da açabilirsiniz.

## Gizlilik

- Repodaki kod ve varsayılan ayarlarda API Key alanları **boştur**.
- Kendi anahtarınızı asla commit etmeyin, Issue/PR’a yapıştırmayın.
- Çalışma ayarları: `%LocalAppData%\HalimLabs3\image-models.json`

## Destek

Geliştirici: [Halim Toklu](https://github.com/htoklu) · [Ko-fi](https://ko-fi.com/htoklu)

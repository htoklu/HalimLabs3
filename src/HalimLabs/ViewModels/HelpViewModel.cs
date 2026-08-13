using CommunityToolkit.Mvvm.ComponentModel;

namespace HalimLabs.ViewModels;

public partial class HelpViewModel : ObservableObject
{
    public string Title => "Halim Labs 3 — Help";

    public string Content =>
"""
# Halim Labs 3

Halim Labs 2'nin görsel stüdyosu + **görsel yükleme / düzenleme / karşılaştırma**.

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
""";
}

# Galaxy 101 · teslim paketi v4 · sürüm 0.5.0

Bu paket oyunu Unity (istemci) ve .NET (sunucu) tarafına tahmin gerektirmeden taşımak için hazırlandı. Kaynak, çalışan prototiptir: `Galaxy 101 - Gerçek Ana Oyun.dc.html`. Paketteki her değer oyunda çalışan koddan çıkarıldı ya da onunla birlikte test edildi.

## İçindekiler

| Klasör / dosya | İçerik |
|---|---|
| `RULES.md` | Kural kataloğu R-01…R-98 (her kural için örnek ve test kimlikleri) |
| `DECISIONS.md` · `OPEN_QUESTIONS.md` | Verilen kararlar ve gerekçeleri · açık soru yok |
| `REMOVED.md` · `DEMO_LIST.md` | Kaldırılanlar · yalnız prototipe ait kısayollar (`// DEMO:`) |
| `logic/` | `rules.js` kural motoru (saf, deterministik) · `bots.js` bot kararları · `mockServer.js` sunucu provası · `scenarios/` sabit dağıtımlı senaryolar |
| `protocol/` | `protocol.json` / `.md` C2S–S2C mesajları, hata kodları ve arayüz karşılıkları · `timings.md` süreler ve animasyon çakışma kuralları |
| `tests/` | `rules/*.json` altın test vektörleri · `runner.js` · `server.tests.js` sunucu akış testleri · `rules.test.html` tarayıcıda koşucu |
| `states/` | `states.json` 151 durum kataloğu; `?state=ID` ile oyunda açılır |
| `export/layout-all.html` | Tüm durumların öğe yerleşim dökümü (`data-g101-id`, kutu, hesaplanmış stil) |
| `anim/` | `css.json` arayüz animasyonları · `tween3d.json` 3D taş/kamera/efekt formülleri · `easings.json` eğriler |
| `strings/` | `strings.json` + `strings.csv` TR/EN metinler ve hata metinleri |
| `assets/` | `themes.json` gezegen temaları · `fonts.json` + `fonts/` (TTF, lisanslarıyla) · `materials.json` 3D malzemeler/kaplamalar · `lighting.json` ışıklar ve görüntü ayarları · `icons/` (69 SVG) · `rank/` lig rozetleri (75 SVG) + `ranks.json` · `cursors/` (11 SVG) + `cursors.json` |
| `sound/` | `sfx/` 28 efekt (WAV) · `music/` 2 kesintisiz döngü (WAV) · `events.json` tetikleyiciler, kanal ve ses düzeyi kuralları |
| `screenshots/` | 144 durumun her biri için bir görüntü + `INDEX.md` |

## Doğrulama
- Kural vektörleri: `tests/rules.test.html` (ya da Node: `runner.js` + `rules/*.json`). Son koşu: 106 / 106 geçti.
- Sunucu akışları: `tests/server.tests.js` (sanal saat). Son koşu: 26 / 26 geçti (SRV-SC1 dahil).
- Görsel: `?state=ID&seed=42&theme=saturn` aynı yerleşimi verir; `export/layout-all.html` "İki kez aç, karşılaştır".

## Taşıma sırası (öneri)
1. `logic/rules.js` → C# kural motoru; `tests/rules/*.json` aynen xUnit/NUnit vektörü olur.
2. `protocol/` + `logic/mockServer.js` → .NET sunucu; `tests/server.tests.js` entegrasyon testine çevrilir.
3. Unity istemci: `states/` ekran ekran, `export/` yerleşim, `assets/`, `anim/`, `strings/`, `sound/`.
4. 3D modeller: `Araç - 3D Varlık Dışa Aktarma.html` masa, ıstaka, taş setleri, zar, koltuk, oda, ikram/hediyeler, oyun işaretleri, tam sahne ve 5 uzay gemisini (Kervan, Ok, Halka, 2 avcı) GLB olarak tek zip halinde indirir (5 gezegen teması seçilebilir; malzeme adları `assets/materials.json` ile aynı).

## Fazlar
| Faz | Durum |
|---|---|
| 1 · Kural motoru, testler, kararlar | tamam |
| 2 · Sunucu sınırı, protokol, süreler | tamam |
| 3 · Deterministik durumlar, yerleşim dökümü, animasyon kataloğu, senaryolar | tamam |
| 4 · Metinler, görsel varlıklar, ses olayları, ekran görüntüleri | tamam |

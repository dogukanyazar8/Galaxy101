# T09 — Üç yerel bot

2026-10-20. T09 tamamlandı. İnsan koltuğu 0; diğer üç koltuk otomatik oynar. Görsellik hâlâ A0/A1 ilkel varlıklarıdır; Blender varlıkları, hareket ve ses T11 kapsamındadır.

## Uygulama

- `Okey101.Bots` yalnız Core ve Application'a bağımlı. `BotPlanner` yalnız `PlayerView` alır; rakip elleri veya deste sırası API'ye girmez.
- Bitiren plan önceliklidir. Ardından eldeki taşların olası ceza değeri, kalan taş sayısı, atış cezası ve kanonik eylem/TileId sırası karşılaştırılır. Açılmamış elin sabit 202 puanı yerine stratejide açılmış seri elinin olası ceza değeri kullanılır; gerçek sonuç puanlaması Core'da değişmedi.
- `PlanTurn` en fazla 4096 strateji düğümü kullanır. `ChooseDraw`, 4096 düğümü yandan alma ve mevcut bilinen el karşılaştırmasına bölüştürür. Bilinmeyen kapalı taş tahmin edilmez. Yandan alma yalnız bu temkinli karşılaştırmada kesin iyileşme veya bitiş sağlıyorsa seçilir. Bu bir optimal oyun garantisi değildir.
- T05'in kesin yandan alma uygunluğu araması strateji sınırından bağımsız ve iptal edilebilirdir. Doğrulanmış tanık plan korunur; çekişten sonra duraklatıp devam edilirse tanık yeniden bulunur. Kapalı çekişte geçerli atış geri dönüşü vardır. Son plan Core tarafından tekrar doğrulanır.
- Seri/çift açma, yeni grup, uçtan işleme ve normal taşla okey değiştirme desteklenir. Çift açanın toplam iki işleme sınırı ile yeni çiftin bu sınıra girmemesi Core'dan uygulanır.
- `PlayerViewProjector.ForEvaluation`, T08'deki değerlendirme projeksiyonunu ortaklaştırır. Bilinmeyen kimliklerin kanonik dağılımı sadece fiziksel bütünlük ve Core değerlendirmesi içindir; karar hesabında gizli taş yüzleri okunmaz, bu durum otoriteye kurulmaz.
- `BotRunner` tek seri yürütücü ve arka plan hesaplaması kullanır. Görünüm ile oturum epoch token'ı birlikte alınır; bekleyen işin RoundId/Version bilgisi sonradan yeniden etiketlenmez. Her gönderimden önce tazelik doğrulanır. Duraklatma, yeni el ve dispose eski işi iptal eder; kaynaklar bekleyen işler bitince temizlenir.
- Gerçek sahne başlangıcı botları açar. Önceki testlerin açık `Initialize` çağrıları manuel davranışını korur; yeni overload botlu başlangıç sağlar. Geliştirme paketinde bot/manuel kontrol geçişi vardır. Release yalnız insan koltuğunu sunar. Beklenmeyen bot hatası otomatik oyunu durdurur ve görünür hata bırakır.

## Test kanıtı

| Kontrol | Sonuç | Kanıt |
|---|---|---|
| İlk BotPlanner RED | 8 test, 7 assertion hatası | [NUnit](../../artifacts/test-results/20261020T184648329Z-3e508a5b/results.xml) |
| İlk BotLifecycle RED | 4 test, 3 assertion hatası | [NUnit](../../artifacts/test-results/20261020T184658068Z-39fa89cc/results.xml) |
| İlk planner GREEN | 8/8 | [NUnit](../../artifacts/test-results/20261020T185151379Z-af9eb066/results.xml) |
| İlk lifecycle GREEN | 4/4 | [NUnit](../../artifacts/test-results/20261020T185159760Z-c76d53fc/results.xml) |
| Gerçek masa entegrasyonu RED | 2/2 assertion hatası | [NUnit](../../artifacts/test-results/20261020T185501910Z-868204f8/results.xml) |
| Tam EditMode | **290/290** | [NUnit](../../artifacts/test-results/20261020T185931621Z-7880ce1f/results.xml) |
| Tam PlayMode | **30/30** | [NUnit](../../artifacts/test-results/20261020T185941123Z-bdc830b9/results.xml) |

Komutlar: `& .\scripts\Test-Unity.ps1 -Platform EditMode` ve `& .\scripts\Test-Unity.ps1 -Platform PlayMode`. Skip/inconclusive yok. T09, 10 planner ve 9 lifecycle/entegrasyon vakası ekledi. Önceki 280 EditMode ve 21 PlayMode testi de geçti.

Fixture'lar seri/çift açılış, zorunlu atışla kazanma, fiziksel yandan alınan taşı kullanma, iki taş bütçesi, yeni çift, okey değişimi, gizli dağılımdan bağımsızlık, deterministik karar, faydasız yandan almama ve iptal kapsamını doğruladı. Yaşam döngüsü testleri yeni el, yinelenen istekler, duraklat/devam, kesinleşmiş yandan çekiş sonrası devam, dispose ve başlangıç koltuğu bot olduğunda çift çekmeme durumlarını kapsadı.

Seed 71 entegrasyonu insanın gerçek draft/çekiş/onay API'si ile üç otomatik botu tamamlanan ele götürdü. İnsan hamlesi testte bir planlayıcıyla üretildi; aşağıdaki native kabul ayrıca gerçek fare girişleriyle yapıldı. Bir ilk test, kanonik eşitlik kuralında zorunlu olmayan okeyle atışı bekliyordu; geçerli herhangi bir bitişi kabul edecek şekilde düzeltildi. Üretim puanlaması değiştirilmedi.

## Windows paketleri ve native kabul

Unity 6000.6.6f1, Windows x64, Mono, .NET Standard 2.1, Direct3D11:

| Paket | Sonuç | Boyut |
|---|---|---|
| [Development](../../artifacts/builds/20261020T190254032Z-f690bd7a/Okey101.exe) | 0 hata, 0 uyarı | 171.086.468 bayt |
| [Release](../../artifacts/builds/20261020T190319203Z-ddb688fa/Okey101.exe) | 0 hata, 0 uyarı | 103.745.058 bayt |

Komutlar: `& .\scripts\Build-Windows.ps1 -Configuration Development` ve `-Configuration Release`. Betik artık allowlist ile iki yapı türünü seçer; raporun istenen yapı türüne eşit olduğunu da doğrular. Eski çıktılar üzerine yazılmaz.

Release'te Unity development player-connection listener ayarı yok; `boot.config` içinde `player-connection-mode` bulunmuyor. Public A2 kabul günlükleri korunuyor. Native testte Windows güvenlik sorusu açılmadı. Ağ izni verilmedi, OS güvenlik ayarları değiştirilmedi. Development paketi ayrıca üretildi; T09 native kabul Release üzerinde yapıldı.

1280×720 pencerede 22 insan taşı, gösterge, deste ve üç bot etiketi gözlendi. İnsan dört tur fareyle oynadı; ilk tur atış, sonraki üç tur kapalı çekiş ve atış yapıldı. Bot 1 seri, Bot 2 çift, Bot 3 seri açtı. Her bot çevriminde kontrol insan koltuğuna döndü. Deste tükenmeden Bot 1 normal atışla kazandı: **Version 27, stock 9, DiscardWin, winner=1, scores=202,-101,38,8**. Sonuç panelinde N/J/P/B/M kırılımları görüldü. [720p günlük](../../artifacts/acceptance/a2-720-player.log).

1920×1080'de aynı Release yeni elle açıldı; 22 taşın tamamı gözlendi. Sırala, taş seç/boş yuvaya taşı, duraklat/devam çalıştı; Version 0, 22 taş ve deste 20 korundu. Oyun bu test elinde açık bırakıldı. [1080p günlük](../../artifacts/acceptance/a2-1080-player.log), [makine kaydı](../../artifacts/acceptance/a2-machine.md).

Native ekranlar Computer Use ile doğrudan incelendi; kalıcı ekran görüntüsü dosyası üretilmedi. Önceki T07'nin iki çözünürlükte 22 taş gözlemi bu güncel sahnede kapandı. T08'in hazır seri/çift/değişim/özel bitiş ellerindeki bütün insan kontrolleri için ayrı native kabul hâlâ açık; otomatik testlerle eş tutulmadı.

## Kalan kapsam

T10 takip durumu (2026-10-20): 1.000 dağıtım, sürümlü kayıt/tekrar oynatma ve altı senaryolu panel tamamlandı; aşağıdaki T10 durumu T09'un teslim anına ait tarihsel kayıttır. Güncel kanıt [T10 raporunda](t10-replay-and-scenarios.md).

Salt okunur bağımsız kaynak incelemesinde doğrulanmış correctness, gizli bilgi veya lifecycle kusuru bulunmadı. İlk inceleme girişimi model kapasite hatası verdi; aynı incelemeciyle tekrar tamamlandı. İncelemeci test/build çalıştırmadı; yukarıdaki komutlar ana çalışma tarafından yürütüldü.

T10'un 1.000 dağıtımı, sürümlü kayıt/tekrar oynatma ve senaryo paneli yapılmadı. T11 görsel varlıkları/animasyon/ses, T12 menü/yeni oyun akışı, T13 FPS/uzun süre kararlılık kabulü yapılmadı. Online oda ve mobil bu aşamanın dışında. Tam ilk oynanabilir paket planının bittiği iddia edilmiyor.

## Additional observations

- **minor:** `TablePresenter.Initialize` sabit atık konumları, `TurnToolbar.Initialize` oyuncu panelleriyle bazı noktalarda örtüşüyor. 720p normal elde rakibin atık taşı ve insan atığı üzerindeki yardım yazısı kısmen kapandı. İşlevsel bot kabulünü engellemedi; T11 masa/arayüz yerleşiminde çözülmeli.
- **minor:** Kullanılmayan URP Gaussian/Bokeh/Panini post-processing shader strip günlük uyarıları Release'te de var. Kamera post-processing kullanmıyor; bu testte görünür masa kaybı gözlenmedi. Renderer kapsam dışı değiştirilmedi.

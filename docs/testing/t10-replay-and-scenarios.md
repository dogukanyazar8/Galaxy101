# T10 — Senaryo, tekrar kaydı ve simülasyon

2026-10-20. T10 tamamlandı: sürümlü kayıt/tekrar oynatma, güvenli senaryo yükleme, altı hazır el, geliştirme paneli ve 1.000 dağıtım kabulü geçti. Tam ilk oynanabilir planın T11–T13 işleri henüz tamamlanmadı.

## Kayıt ve güvenli yükleme

`ReplayRecord`: SchemaVersion=1, RuleVersion=`101-v1`, ShuffleVersion=`xorshift32-fy-v1`, seed, tam başlangıç otoritesi, kabul edilmiş komutlar ve beklenen SHA-256 sonuç hash'i. Başlangıç; göstergeyi, sıralı desteyi, dört eli, açılışları, masa gruplarını ve okey karşılıklarını, atıkları, çekiş bilgisini, sıra/version/sequence alanlarını ve varsa puan kırılımlarını içerir. Seed'den tekrar dağıtmak yerine kayıttaki tam durum kullanılır.

Application katmanı Unity bağımsızdır. Yeni JSON paketi eklenmedi: .NET `DataContractJsonSerializer`, zorunlu alanlı kapalı DTO'lar ve sabit Draw/Commit/Open/Add/Extend/Swap ayrımları kullanılır. CLR tip adı yüklenmez. Üstbilgi, koleksiyonlar, enum/taş değerleri, otorite invariant'ları, bütün komutların motor tarafından kabulü ve sonuç hash'i doğrulanır. Görsel `Presentation` metadatası kural durumuna girmez.

Dosya sınırı 20 MiB, JSON derinliği 32 ve kayıt sınırı 2.000 komuttur. Fiziksel koleksiyonların ayrıca küçük üst sınırları vardır. Mono okuyucusunun ikinci kök JSON nesnesini görmezden geldiği regresyonla doğrulandı; standart okuyucudan önce tek nesne çerçevesi ve derinlik kontrolü eklendi. JSON sözdizimini yine standart okuyucu doğrular.

Runtime kökü `Application.persistentDataPath/Diagnostics`. Codec yalnız basit `.json` dosya adlarını kabul eder; mutlak yol, dizin ayıracı, traversal, NTFS stream/device adları ve mevcut symbolic-link/junction ataları reddedilir. Kök yolu runtime'da sabittir; test/Editor kodu kendi güvenilir kökünü verir. Yeni dosya adı uygulama tarafından üretilir. Yazım create-new geçici dosyada tamamlanır, flush ve üzerine yazmayan rename ile yayımlanır; hata mevcut dosyayı veya eli değiştirmez.

`LocalSession` başlangıcı ve yalnız kabul edilmiş komutları tutar. 2.000 kayıt sınırı aşılırsa export devre dışı kalır; oyun kuralları veya tur bitişi değiştirilmez. Kayıt alınması dosya yazmaz, otomatik gönderim yoktur. Ağ SDK'sı veya servis eklenmedi.

`RestoreReplayAsync` doğrulamayı worker'da yapar. Commit sırasında oturum epoch'u ve aynı durum referansı kontrol edilir; arada oynanan hamle, duraklatma, yeni el veya dispose eski yüklemeyi geçersiz kılar. Son durumla birlikte orijinal başlangıç ve kabul edilmiş geçmiş atomik olarak korunur. Böylece tekrar yükledikten sonra yeniden dışa aktarma geçmişi kaybetmez ve aynı dağıtımı başlatma başlangıca döner. Unity nesneleri worker'da kullanılmaz.

## Geliştirme paneli

Masadaki **Test paneli** düğmesi altı hazır eli sunar: seri açılışı, çift açılışı, okey değişimi, ×8 bitiş, deste sonu ve dört çift iptali. Panel açılınca otomatik botlar durdurulur. **Aktif koltuk: bot adımı** yalnız bir tur oynar; testte insan koltuğu da adımlanabilir. **Aynı dağıtımı yeniden başlat**, **Kaydı dışa aktar** ve dosya adıyla **Kaydı tekrar oynat** bulunur. Panel kapatılınca normal masa kontrolleri kullanılabilir; otomatik botları açmak geliştirme alt çubuğundaki ayrı düğmeyle yapılır.

Fixture üretimi: `Okey101.Editor.ScenarioExporter.Export`; TestSupport'a bağımlı değildir, altı durumu aynı Core invariant'larıyla doğrular. Sabit RoundId'ler ve tam JSON durumları `Assets/Okey101/Tests/Fixtures/Scenarios` altında saklanır. İlk açılışta hazır eller insan adına kendiliğinden oynanmaz.

Release sahne işlemcisi paneli ve TextAsset referanslarını kaldırır. `OKEY_DIAGNOSTICS` yalnız Development derlemesine eklenir; hazır el üreticisi ve panel gövdesi Release C# kodundan da dışlanır. Unity 6.6'nın deprecated `DEVELOPMENT_BUILD` derleyici sembolü kullanılmaz. Ek sembol için [Unity 6.6 BuildPlayerOptions.extraScriptingDefines](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/BuildPlayerOptions-extraScriptingDefines.html) API'si kullanılır.

## Doğrulama kanıtı

| Kontrol | Gözlenen sonuç | Kanıt |
|---|---|---|
| Kayıt/yükleme ilk RED | 9/9 başarısız | [NUnit](../../artifacts/test-results/20261020T191228799Z-4fd6ddf9/results.xml) |
| İlk GREEN | 9/9 | [NUnit](../../artifacts/test-results/20261020T191423279Z-76557d56/results.xml) |
| İkinci JSON kökü RED | 1/1 başarısız | [NUnit](../../artifacts/test-results/20261020T192143071Z-44ac4834/results.xml) |
| Panel ilk RED | 1/1 başarısız | [NUnit](../../artifacts/test-results/20261020T192107450Z-d950b0dd/results.xml) |
| Replay geçmişi regresyon RED | 1/1 başarısız | [NUnit](../../artifacts/test-results/20261020T192704397Z-e245e26b/results.xml) |
| Son tam EditMode, Simulation hariç | **308/308** | [NUnit](../../artifacts/test-results/20261020T194654011Z-7a2a25dd/results.xml) |
| Son tam PlayMode | **32/32** | [NUnit](../../artifacts/test-results/20261020T194714592Z-caa7c3cc/results.xml) |
| Ayrı Simulation | **1.000/1.000** | [NUnit](../../artifacts/test-results/20261020T193043753Z-953d30ec/results.xml) |

Skip/inconclusive yok. T10, sekiz Replay, dört ScenarioLoader, altı fixture ve iki gerçek panel entegrasyonu vakası ekledi. Önceki T01–T09 testleri de geçti. Komutlar `& .\scripts\Test-Unity.ps1 -Platform EditMode` ve `-Platform PlayMode`.

Kayıt testleri tam durum/puan/kimlik/okey karşılığı, Draw/Open/Add/Extend/Swap, kabul edilmeyen komutun kayda girmemesi, yeni elde kayıt sıfırlama, kültürden bağımsız hash ve deste sırasına duyarlılığı kapsar. Bozuk/sürümü bilinmeyen/eksik/derin/20 MiB üstü/107 taşlı/çift kimlikli veriler, bilinmeyen komut ve action, çift zorunlu alan, hash oynama, traversal ve var olan dosya üzerine yazma reddedildi. İptal edilmiş ve eski replay yüklemeleri mevcut ele dokunmadı.

PlayMode, altı gerçek panel düğmesinin TextAsset JSON'unu import edip doğru sabit RoundId'yi yüklediğini, tek komutla tur oynadığını ve kaydedilen sonucu tekrar yüklediğini doğruladı. ×8 kazananın toplamı −808, deste sonu StockExhausted, dört çift AllPairs/Total=null; okey değişimi ayrı fixture testinde SwapJoker eylemi üretti. Bunlar Windows'taki doğrudan fare gözleminden ayrıdır.

Salt okunur bağımsız incelemede bir medium bulgu düzeltildi: son snapshot'ı yeni başlangıç sayan replay yükleme yolu. Regresyon RED/GREEN, yeniden export ve başlangıca dönüş ile doğrulandı. Başka doğrulanmış bulgu yok; incelemeci test/build/UI çalıştırmadı.

## 1.000 dağıtım kabulü

Komut: `& .\scripts\Test-Unity.ps1 -Platform EditMode -Filter 'Okey101.Application.Tests.SimulationTests' -Category Simulation`.

Tohumlar 0–999, başlangıç koltuğu `seed % 4` ile her koltuktan 250 dağıtım. Dört koltuk da aynı PlayerView tabanlı gerçek planlayıcıyla oynar. Her kabul edilen komutta otorite/106 kimlik ve version artışı doğrulanır. Her sonuçta N/J/P/B/M/Total, gerçek elde kalan taşlardan ve R12–R16'nın bağımsız aritmetiğinden kontrol edilir. Dağıtım başına 2.000 komut sınırı test hatasıdır; zorla bitiş, rastgele yeniden deneme veya iptal sonrası yeni vakaymış gibi yeniden dağıtım yoktur.

İlk uzun deneme ara ilerleme vermediği için yaklaşık altı dakika sonra yalnız sahipli batch süreci durduruldu; NUnit sonucu yok, başarı sayılmadı. Tam yeniden çalıştırma her seed için başlangıç/bitiş/süre/komut/terminal türünü logladı ve **1.000/1.000** geçti; 0 başarısız/atlanan/belirsiz.

| Ölçüm | Sonuç |
|---|---:|
| Normal bitiş | 320 |
| Bitirensiz deste sonu | 679 |
| Dört çift iptali | 1 |
| Seri açılışı | 1.534 |
| Çift açılışı | 448 |
| Kabul edilmiş komut | 39.312 |
| Toplam test süresi | 815,704 saniye |
| En uzun dağıtım | 9.674 ms |

[Tohum bazlı özet](../../artifacts/acceptance/t10-simulation-summary.json). Süre; Editor/Mono/NUnit ve bu makine koşulları içindir. Native panel testleri çalışmanın son bölümünde aynı makinede yapıldı; FPS veya genel bot gecikme garantisi çıkarılmaz. Son derleme sembolü/paket dışlama değişikliği Core/Bot/Editor davranışını değiştirmedi; uzun test tekrarlanmadı, son tam Editor/PlayMode testleri ayrıca geçirildi. Simülasyon hedefli kural ve yetenek fixture'larının yerine geçmez.

## Native ve paket durumu

İlk T10 paketlerindeki üç UAC0009 uyarısı, özel sembol değişikliğiyle kaldırıldı. Yeni paket kapısı eski Release'te `PracticeDeals.Create` kaldığını da yakaladı (beklenen RED); hazır el üreticisi ve CLI yolu ayrıca geliştirme bayrağına alındı. `scripts/Verify-DiagnosticsBuild.ps1` DLL'leri çalıştırmadan PE metadata'sındaki panel/üretici metotlarını ve paketlenmiş `.assets` içindeki altı fixture'ı denetler. Build betiği bu kapı geçmeden başarı bildirmez.

| Son paket | BuildReport | Paket kapısı |
|---|---|---|
| [Development](../../artifacts/builds/20261020T194447760Z-2f23425e/Okey101.exe) | [0 hata, 0 uyarı; 171.136.104 bayt](../../artifacts/builds/20261020T194447760Z-2f23425e/build-report.json) | Panel metotları/üretici mevcut, 6 fixture |
| [Release](../../artifacts/builds/20261020T194548885Z-5dc22c07/Okey101.exe) | [0 hata, 0 uyarı; 103.761.170 bayt](../../artifacts/builds/20261020T194548885Z-5dc22c07/build-report.json) | Panel metotları/üretici yok, 0 fixture |

Windows x64 / Unity 6000.6.6f1 / Mono / .NET Standard 2.1 / DX11. Release sahne işlemcisi Table'dan bir panel kaldırdı. Son Release ayrıca native 1280×720'de açıldı: 22 taş, üç bot ve normal masa görüldü, test paneli yok; güvenlik sorusu açılmadı. [Release günlüğü](../../artifacts/acceptance/t10-release-player.log).

Native panel kabulü ilk işlevsel T10 Development paketinde (`20261020T192838516Z-d6ea8201`) 1920×1080'de yapıldı. Kullanıcı Windows Güvenliği sorusunu İptal et ile kapattığını bildirdi; güvenlik sorusuna otomatik müdahale edilmedi. Altı fixture fareyle yüklendi, her biri tek tur adımlandı, her biri dışa aktarılıp tekrar yüklendi. Seri −202, çift −404, ×8 bitiş −808, bitirensiz son 202/202/202/202 ve AllPairs için puan yerine İptal görüldü. Okey değişimi turunda kendi elindeki taş sayısı 3'ten 1'e indi; kayıt aynı hash ile geri geldi. Replay sonrasında aynı dağıtım düğmesi seriyi 0 yerine orijinal 12 taşla yeniden başlattı.

[Native günlük](../../artifacts/acceptance/t10-development-player.log), [makine/kabul kaydı](../../artifacts/acceptance/t10-machine.md), [altı native replay](../../artifacts/acceptance/t10-native-replays). Final Development'ın işlevsel gövdeleri aynıdır; değişen derleme sembolü ve paket dışlama kapıları son DLL/asset kontrolünden geçti. Final Development için altı fare akışı yeniden çalıştırılmadı. Kalıcı ekran görüntüsü saklanmadı; Computer Use görüntüleri doğrudan incelendi. OS izinleri/güvenlik ayarları değiştirilmedi.

T11 varlık/Blender/animasyon/ses, T12 menü/yeni oyun ve T13 uzun süre/FPS profili başlanmadı. Önceki T08 tüm hazır ellerle insan kontrollerinin native kabulü açık; T10 otomatik bot adımı bunun yerine geçmez. Online özel oda veya mobil kabul iddiası yok.

## Additional observations

- **minor:** `TablePresenter.Initialize` sabit 3D atık konumları ile `TurnToolbar.Initialize` panellerinin örtüşmesi sürüyor; bazı atıklar kısmen kapanıyor. T11 yerleşim kapsamı.
- **minor:** Kullanılmayan URP Gaussian/Bokeh/Panini post-process shader strip günlük uyarıları sürüyor; bu görev renderer ayarlarını değiştirmedi.
- Stratejinin 4.096 düğüm sınırı, kesin yandan alma aramasına süre sınırı koymaz. Kaynak incelemesi bu sınırı doğruladı; T10 performans hedefi geçti iddiası taşımıyor. Süreler yalnız bu makine ve koşullar için raporlanır.

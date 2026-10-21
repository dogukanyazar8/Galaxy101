# T08 — Açılış, işleme ve atomik tur önizlemesi (A1)

Tarih: 2026-10-20. Uygulama, otomatik testler ve Windows derlemesi hazır. Native görsel/fare kabulü açık; T08'in son kabul adımı tamamlandı sayılmadı.

## Uygulanan davranış

`TurnDraft.PreviewAsync` yalnız `PlayerView` tüketir. Karşı oyuncuların gizli ellerine veya kapalı deste sırasına erişmez. Core'un fiziksel bütünlük kontrolünü kullanabilmek için bilinmeyen kimlikleri yalnız geçici değerlendirme durumunda kanonik yerleştirir. Bu sentetik durum oturuma yazılmaz, rakip puanlamasında kullanılmaz, UI'ya döndürülmez. Önizleme değiştirilemez el/masa/karşılık kopyaları içerir.

Grup geçerliliği, açılış eşiği, çift adedi, işleme hakkı, okey değişimi, yandan alınan taşı kullanma ve atış cezası Core doğrulayıcılarından gelir. Sunumda ikinci bir kural hesabı yoktur. Grup başına sınırlı Core doğrulaması ile açılış biriktirme kararı verilir; önizleme ve kesin yandan alma araması görüntü döngüsü dışında yürür.

- Açılış tamamlanana kadar gruplar ilk `OpenGroups` içinde birikir. Eşik eksikse taşlar ıstakada işaretlenir, yeniden seçilemez; tur onayı kapalıdır.
- Açılış geçerli olduktan sonraki gruplar `AddGroup` olur. Açılış → okey değişimi → geri alınan okeyle yeni grup sırası korunur. Seri açan, başka çift açan varsa sonradan çift ekleyebilir.
- Okey karşılığı açıkça seçilir. Taslaktaki iki okey ayrı ayrı hedeflenebilir. Aynı fiziksel okey yeniden geri alınıp başka grupta kullanılırsa önceki yerleştirmenin karşılığı değiştirilmez. Kesinleşmiş masadaki okeyin karşılığı düzenlenemez.
- Çift açanın üçüncü seri işleme taşı reddedilir. Son işlem veya bütün taslak geri alınabilir. Kesinleşmiş çekiş, eldeki yeni taş ve kapalı deste sayısı geri alınmaz.
- Atılacak taş seçilmeden tur onaylanamaz. Bütün masa işlemleri ve atış tek komut olarak Core'a gönderilir. Geçersiz tur otorite sürümünü ilerletmez.
- Ceza önizlemesi tutarı gösterir. İptalde aynı taslak/seçim kalır; ceza ve otorite değişmez. Onayda bütün plan yeniden doğrulanır.
- Yandan alma sırasında ikinci çekiş kapalıdır; İptal aramayı iptal eder. Kabul sınırından önce iptal edilen çekişten sonra kapalı deste seçilebilir. `Cancelled`, `NoLegalUse` olarak sunulmaz. Çekiş zaten kabul edilmişse kaynak değiştirilemez.
- Duraklatma, sahne kapanması ve yeni başlangıç eski işleri iptal eder. Geç kalan önizleme yeni düzenlemeyi veya yeni otoriteyi ezemez.

Masada bir seri/sayı grubu ve bir çift grubu okunabilir 3D temsil olarak gösterilir. **Önceki/Sonraki** bütün açık grupları hedeflemeyi sağlar. Bu, A1'in grup sayfalamasıdır; son kamera/yakın görünüm ve görsel cilalama T11 kapsamındadır. El sonunda dört oyuncunun N/J/P/B/M ve toplamı bir özet panelinde gösterilir; tam sonuç/yeniden başlatma akışı T12 kapsamındadır.

## Kullanım

1. **Çoklu seç** açılır; grup taşları tıklanır. **Sıralı per**, **Sayı grubu** veya **Çift** taslağa ekler. Okey varsa önce **Okey karşılığı** seçilir.
2. Açılış toplamı ve çift adedi izlenir. Eksik/geçersiz taslakta kısa neden görünür. **Son işlemi al** veya **Taslağı sil** düzeltme sağlar.
3. İşleme için ıstakadan taş, masadan hedef grup seçilir; **Başa işle/Sona işle** kullanılır. Okey değişimi için ıstakadan normal taş ve masadan okey seçilip **Okey değiştir** kullanılır.
4. Atılacak tek taş seçilir; **At ve turu onayla** bütün taslağı kesinleştirir. Gerekirse ceza ayrıca onaylanır.
5. **Kapalıdan çek/Yandan al** kaynaklarıdır. Geliştirme paketinde **Aktif oyuncuya geç** koltukları elle denetler; bot değildir.

Yeni Windows paketi: `artifacts/builds/20261020T183730108Z-0e1b13e0/Okey101.exe`. Aynı klasördeki Data, UnityPlayer ve Mono dosyalarıyla birlikte tutulmalıdır.

Hazır kabul elleri yalnız geliştirme/Editor başlangıcında seçilebilir. Çalışan elin üzerine sessizce senaryo yükleme arayüzü eklenmedi. Örnek:

```powershell
& '.\artifacts\builds\20261020T183730108Z-0e1b13e0\Okey101.exe' -okeyPractice Series -screen-width 1280 -screen-height 720 -screen-fullscreen 0
```

`Series`, `Pairs`, `Swap`, `Finish`, `Penalty` seçenekleri vardır. Argüman yoksa A0'nın 71 tohumlu 22 taş dağıtımı başlar. `LocalSession.Start(RoundState)` bütün otorite invariant'larını doğrular; bozuk durumda mevcut oturum değişmez. Dosya senaryoları/test paneli T10 kapsamındadır.

## Gözlenen doğrulama

```powershell
& .\scripts\Test-Unity.ps1 -Platform PlayMode -Filter Okey101.PlayMode.Tests.TurnInteractionTests
& .\scripts\Test-Unity.ps1 -Platform PlayMode
& .\scripts\Test-Unity.ps1 -Platform EditMode
& .\scripts\Build-Windows.ps1 -Configuration Development
```

- İlk RED: **7/7 başarısız**, beklenen davranış assertion'ları. İlk test yazımındaki NUnit constraint derleme hatası düzeltildikten sonra bu RED alındı. [RED](../../artifacts/test-results/20261020T181721015Z-a9cd22d5/results.xml), [ilk GREEN 7/7](../../artifacts/test-results/20261020T182012408Z-bfcb842e/results.xml).
- Bağımsız kod incelemesi iki sıralama/hedef seçme kusuru buldu. Üç regresyon testi **3/3 RED**, sonra geçti. [Regresyon RED](../../artifacts/test-results/20261020T183143586Z-7f796c83/results.xml), [GREEN 15/15](../../artifacts/test-results/20261020T183230448Z-20341dc5/results.xml).
- Tek okeyin iki kez geri alınıp farklı gruplarda kullanılması ek testi **1/1 RED** verdi. Önceki karşılığı ezmeyen yerleştirme düzenlemesi sonrası geçti. [RED](../../artifacts/test-results/20261020T183509188Z-2143de1d/results.xml).
- Son tam PlayMode **21/21**, 0 başarısız/atlanan/belirsiz. T08 16 test, T07 5 test. [Sonuç](../../artifacts/test-results/20261020T183719566Z-6ae8f6cf/results.xml).
- Son tam EditMode **280/280**, 0 başarısız/atlanan/belirsiz. Önceki Core ve Application testleri korundu. [Sonuç](../../artifacts/test-results/20261020T183546668Z-25ec3f23/results.xml).
- Hazır seri, çift ve gerçek okeyle bitiş elleri gerçek `GameBootstrap`/oturum üzerinden oynatıldı. Onaydan önce otorite aynı kaldı; onayda sürüm yalnız bir arttı. Seri elden bitiş M=2, çift elden bitiş M=4, seri elden gerçek okeyle bitiş M=4; kazanan −202/−404, açmamış rakip 404/808 çıktı. Bütün M=1/2/4/8 ve B/P ayrımı ayrıca T03 Core testlerindedir.
- Ceza iptal/onay, gerçek yandan taş değişimi, iptal sonrası kapalı çekiş, kesinleşmiş çekişi koruyan geri alma, ikinci okey hedefleme ve bozuk başlangıcın reddi test edildi.
- Bağımsız inceleme düzeltmeleri tekrar kontrol etti; doğrulanmış yeni regresyon bulmadı. İncelemeci test/Unity çalıştırmadı.
- Son Windows64 Development / Mono / .NET Standard 2.1 / Direct3D11 derlemesi başarılı: **0 hata, 0 uyarı**, 171.034.843 bayt. [BuildReport](../../artifacts/builds/20261020T183730108Z-0e1b13e0/build-report.json).

İlk A1 native denemesi 1280×720/PC kalite/Direct3D11 ve 22 el/22 görünür taş bildirdi. Yeni exe için Windows güvenlik duvarı penceresi açıldı; bu pencereye müdahale edilmedi. Kullanıcıdan İptal et ile kapatması istendi. Bu ilk paket sonraki hata düzeltmeleriyle değiştirildi; son paket native kabul edilmiş sayılmaz. 720p/1080p native fare/görsel kontrolü ve hazır ellerle elle kabul hâlâ açık. Otomatik PlayMode projeksiyonu bunun yerine geçmez. T07 native 22 taş kabulü de açık tutuldu.

## R01–R17 kapsamı

| Kural | Kanıt ve kalan kabul |
|---|---|
| R01 Kapsam | Dört koltuk A0/sunum testleri. Bir insan+üç bot A2/T09'da açık. |
| R02 Taş/dağıtım | T01/T06 fiziksel kimlik invariant'ları; TableSmoke 22 taş ve projeksiyon. Native iki çözünürlük açık. |
| R03 Tur | DrawDiscardAndReorder, CancelledSearchCanChooseStockAndUndoPreservesDraw; T06 komut testleri. |
| R04 Gruplar | T02 per/çift doğrulaması; açılış/işleme UI fixture'ları aynı Core'u kullanıyor. |
| R05 Açılış | Opening101AndFivePairsShowExactTotals, PracticeOpeningsCommitAtomicallyAndFinishUsesExactMultiplier. |
| R06 İşleme izinleri | CanProcessAfterDraftOpening, PairAfterSeriesOpeningRemainsAfterOpening; T04 izin matrisi. |
| R07 İşleme sınırı | RejectThirdProcessingAndUndoDraft; T04 kümülatif iki taş/yeni çift testleri. |
| R08 Okey karşılığı | ChangingJokerChoiceRecomputesPreview, EitherStagedJokerCanBeTargetedForEditing, committed binding kilidi. |
| R09 Okey alma | RealSwapReturnsJokerWithoutChangingAuthorityUntilDiscard, sıra/tekrar kullanım regresyonları. |
| R10 Yandan alma | DiscardWitnessSwapLeavesJokerInRack, iptal sonrası kaynak seçimi; T05 kesin tanık araması. |
| R11 Bitiş | CommitRequiresDiscard, hazır el bitişlerinin gerçek oturum onayı; T05 son taş zorunluluğu. |
| R12 Temel puan | T03/T06 puan kırılımları; gerçek oturum bitiş toplamları ve A1 özet paneli. |
| R13 Özel bitiş | Hazır elden/çift/okey bitişi M=2/4; T03 bütün 1/2/4/8 birleşimleri. |
| R14 Cezalar | PenaltyDialogCancelAndAcceptUseSameUncommittedDraft, T05 ceza ayrıntıları. |
| R15 Deste sonu | T06 RoundEndTests; GUI senaryo paneli T10'da. |
| R16 Dört çift | T06 AllPairs/terminal önceliği; GUI senaryo paneli T10'da. |
| R17 Yardım/görünürlük | Önizleme, okey seçici, geri alma ve özel görünüm testleri. Bot/adımlatma A2/T09–T10'da açık. |

Bu tablo bot veya native görsel kabulünün geçtiği iddiası taşımaz. İlk oynanabilir sürüm bütünüyle tamamlanmadı. Performans/FPS profili, mobil kabul ve online özel oda bu görevde çalışılmadı.

## Additional observations

T10 takip durumu (2026-10-20): geliştirme paneli, altı fixture, tek bot turu ve sürümlü kayıt/tekrar oynatma hem PlayMode hem native Windows'ta doğrulandı; 1.000 dağıtım geçti. R17'nin bot adımlatma/senaryo kısmı kapandı. T08'in bütün hazır ellerle insan açılış/işleme/karşılık düzenleme/ceza kontrollerinin ayrı native kabulü hâlâ açık. [T10 raporu](t10-replay-and-scenarios.md).

T09 takip durumu (2026-10-20): bir insan+üç bot gerçek Windows penceresinde normal bitişe ulaştı; R01 bot kısmı ve T07 iki çözünürlükte22taş gözlemi kapandı. R17 bot tek tur debug adımlatma T10'da; T08 hazır seri/çift/değişim/özel bitiş ellerinin tüm insan kontrolleriyle ayrı native kabulü açık kalıyor. Ayrıntı [T09 raporu](t09-bots.md). Yukarıdaki eski paket/firewall durumu tarihsel kayıttır.

- **minor:** Development paketinin `boot.config` dosyası `player-connection-mode=Listen` içeriyor; yerel oyuna ağ SDK'sı eklenmemesine rağmen Unity'nin geliştirme bağlantısı Windows güvenlik duvarı sorusu oluşturuyor. Ağ izni verilmedi; Windows güvenlik ayarları değiştirilmedi. Unity bu modu [PlayerConnectionInitiateMode](https://docs.unity3d.com/jp/current/ScriptReference/PlayerConnectionInitiateMode.html) içinde tanımlıyor.
- **minor:** T07'de gözlenen, kullanılmayan URP post-processing shader'larının strip edilmesi günlük uyarısı ilk A1 native denemesinde de var. A1 kameralarında post-processing etkin değil. Bağımlılık/renderer ayarları kapsam dışı değiştirilmedi; native görsel etkisi henüz kabul edilmedi.

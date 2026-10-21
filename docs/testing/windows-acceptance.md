# Windows A4 kabul prosedürü

Ürün sürümü **0.1.0**, kural sürümü **101-v1**. Unity **6000.6.6f1**, Windows x64, Mono, .NET Standard2.1, Direct3D11, PC kalite. Online/Android/iOS kabulü bu pakete dahil değildir.

## Paketleri kullanma

2026-10-21 güncel normal Release: `artifacts/builds/windows-release-20261021-1132/Okey101.exe`. Derleme0hata/0uyarı; Release tanılama dışlama kapısı geçti (fixture0). Oyun bu teslimde başlatılmadı; kullanıcı elle deneyecek. Ayrı test paneli paketi: `artifacts/builds/windows-test-20261021-0858/Okey101.exe`.

Nihai yollar: `artifacts/builds/windows-release/Okey101.exe` ve `artifacts/builds/windows-development/Okey101.exe`. **Exe tek başına taşınmaz:** aynı klasördeki `Okey101_Data`, `MonoBleedingEdge`, UnityPlayer.dll ve diğer yan dosyalar birlikte korunur. BuildReport'un Succeeded olması ve paket kapısının geçmesi, oynanış kabulü değildir.

- Release: Yerel Oyna, Ayarlar, Çıkış. Bir insan/üç bot. Geliştirme paneli, hazır tam deste fixture'ları ve kabul probu kodu pakette bulunmaz.
- Development: Aynı oyun + Test paneli. Seri/çift/değişim/×8/deste sonu/dört çift fixture'ları; aktif koltuğa tek bot adımı, yerel tekrar dışa aktarma/doğrulama. Tam başlangıç içeren tekrarlar yalnız yerel test içindir; ağ güvenliğinin yerine geçmez.
- Kayıtlar: `%USERPROFILE%/AppData/LocalLow/Okey101/Okey 101/Player.log`; özel `-logFile` verilirse belirtilen dosya. Ayarlar aynı kökte `settings.json`, tanılama tekrarları `Diagnostics` klasöründe. Oyun eli kaydedilip sonradan sürdürülmez.
- Yerel masa için ağ izni gerekmez. Windows Güvenliği penceresi çıkarsa kullanıcı İptal et ile kapatır; ajan işletim sistemi güvenlik sorusuna işlem yapmaz.

## Otomatik doğrulama komutları

**Derleme öncesi:** `BuildWindows.Build`, `ProjectSetup.CreateOrUpdate` çağrısıyla sahne/prefab/materyalleri yeniden üretir. Elle düzenlediğiniz varlıkları korumak için mevcut derleme betiğini ana proje yerine Assets, Packages, ProjectSettings ve scripts klasörlerini içeren güncel bir proje kopyasında çalıştırın. O kopyayı açık tutan Unity Editor olmamalı. Kopyanın kökünde PowerShell7 ile `& .\scripts\Build-Windows.ps1 -Configuration Release` kullanın. Çıktıdaki `Windows Release build:` satırı exe yolunu verir. Her çağrı yeni klasör üretir. Ana projenin üzerine izole kopyanın sahnelerini geri kopyalamayın.

PowerShell7, proje kökü:

```powershell
& .\scripts\Test-Unity.ps1 -Platform EditMode
& .\scripts\Test-Unity.ps1 -Platform PlayMode
& .\scripts\Test-Unity.ps1 -Platform EditMode -Category Simulation
& .\scripts\Build-Windows.ps1 -Configuration Development -FinalPackage
& .\scripts\Build-Windows.ps1 -Configuration Release -FinalPackage
```

Editor proje kilidi varsa betikler durur; kullanıcının Editor'ı öldürülmez. Normal testler Simulation'ı hariç tutar. Simulation açık çağrıda1000 seed beklenir. Her çağrı benzersiz XML/log üretir; sıfır test veya atlama başarısızlık sayılır. `-FinalPackage` sabit teslim klasörünü oluşturur ve var olan çıktıyı üzerine yazmayı reddeder. Normal build çağrısı benzersiz tarih/GUID klasöründe kalır. Test için bağımlılık/SDK yükseltmesi yapılmaz.

BuildReport tarih, Unity, ürün/kural sürümü, Mono, DX11, yapı türü, hata/uyarı ve paket boyutunu içerir. Verify-DiagnosticsBuild.ps1, PE metadatasında tanılama metodlarını ve `.assets` içinde altı fixture adını kontrol eder: Development6, Release0.

## Paket üzerinde elle oynanış

Her satırda gerçek gözlemi, paket yolunu ve tarihi kaydedin. Otomatik testleri bu tabloya elle kontrol olarak yazmayın.

1. Ana menüden Yerel Oyna. İnsan ve üç bot, tek masa/tek kamera, Türkçe metinler ve ıstaka görünmeli. İlk başlangıç dört koltuktan seçilir; botla başlanırsa insan sırası beklenir.
2. Istakada bir taşı seçip boş yuvaya taşıyın; sürükle/bırak ve Sırala kullanın. Kapalıdan çekin, bir taş atın. Botların sırayla oynayıp kontrolü geri verdiğini görün. Normal bir eli başlangıçtan sonuca kadar tamamlayın.
3. Development hazır seri elinde üç grubu Çoklu seç + Sıralı per ile taslağa ekleyin; toplam101 görünmeli. Son taşı seçip At ve turu onayla kullanın. Çift elinde beş çifti ayrı gruplar halinde ekleyin; beş çift ve puansız eşik görünmeli. Test panelinin otomatik bot adımı bu insan giriş kontrolünün yerine geçmez.
4. Okey değişim elinde masadaki okeyi/grubu seçin; ıstakadan temsil edilen normal taşı seçip Okey değiştir kullanın. Taslak önizlemede okey elde görünür; atış onayı gelmeden otorite değişmez. Atıştan sonra eski okey elde kalabilir.
5. Cezalı atış elinde işlek taşı atın: önce Vazgeç ile aynı seçimi/eli koruyun, sonra aynı atışı onaylayın; tek101 ceza uygulanmalı. Seri/çift/×8/deste sonu/dört çift fixture'larının sonuç ve tekrar hash'lerini ayrıca doğrulayın.
6. Sonuç ekranında N/J/P/B/M ve toplamı görün. AllPairs iptalinde Puan yazılmadı görünmeli. Sonraki el normalde başlangıcı bir ilerletir; iptalde aynı başlangıç koltuğu kalır.
7. Duraklat/Devam et; odak kaybedip geri gelme; animasyon sırasında duraklama. Odak geri geldiğinde açık Devam et gerekir. Modal açıkken klavye arkadaki kontrolleri çalıştıramamalı.
8. Ayarlar: sesi değiştirip Vazgeç; eski değer korunmalı. Ses/tam ekran/azaltılmış hareketi Kaydet; yeniden açınca değerler yüklenmeli. Yazma hatası eski dosyayı korumalı ve tekrar denemeye izin vermeli.
9. Menüye dön: Vazgeç aynı eli/ıstakayı korur; onay masayı kapatır. Yerel Oyna/masa/menü döngüsünü10 kez tekrarlayın. Development Profiler veya Editor Hierarchy'de tek oturum/bot işi, tek Motion/AudioListener; menüde yetim hareket/ses üreticisi kalmamalı.
10. Çıkış uygulamayı kapatmalı. Öznel ses seviyesi ve tekrar rahatsızlığını insan dinleyerek değerlendirir.

## Uzun ölçüm

Development'ta `-okeyA4Output <mevcut-boş-klasör>` açık opt-in ile otomatik kabul iş yükünü başlatır. Çıktı dizini boş olmalı; log dosyası **bu dizinin dışında** olmalı. 1920×1080, PC kalite, aynı A0 donanımı; diğer Editor/build/simülasyon süreçleri çalışmamalı. Kullanıcının açık oyunları kapatılmadıysa bunun ölçüme etkisi raporlanır.

30 saniye ısınma, sonraki600 saniye tüm kareler, toplam1800 saniye kararlılık hedeflenir. Gerçek üç bot ve insan koltuğu için BotPlanner, yeni el ve duraklat/devam döngüsü otomatik sürülür. Bu iş yükü insan tarafından elle oynanmış el sayılmaz. Odak kaybı/manuel duraklama veya bekleyen iş ölçümü tamamlanmış gibi gösterilmez.

`frames.csv`: monoton zaman, gerçek kare aralığı, CPU/GPU, ana/render iş parçacığı, Present beklemesi, sürüm, bot işi ve GC sayacı. `memory.csv`: 15 saniyede bir managed/Unity allocated/reserved/working set ve oturum/Motion/AudioSource sayıları. `soak.json`: gerçek süreler, sayaçlar, p95, en yavaş20kare ve sonuç. p95 hedef **16.7ms**; ortalama FPS veya Present beklemesi hedef aşımını gizlemek için kullanılmaz. Başarısız veya kesilen koşu da saklanır; yalnız iyi koşu seçilmez.

Son gözlenen sonuçlar [A4 raporunda](../../artifacts/acceptance/a4-report.md). Her kontrol **geçti / başarısız / çalıştırılmadı / ortamdan engellendi** olarak ayrılır. Tüm native tam el ve uzun ölçüm kapıları kapanmadan ilk oynanabilir kabulü tamamlandı denmez.

## Kullanıcı isteğiyle arka plan koşusu — 2026-10-21

Kullanıcı masaüstünde çalışırken kalan kontrollerin arka planda yapılmasını istedi; ses ayrı ele alınacak. Fiziksel giriş ve ön plan sunumu için mouse/klavye otomasyonu başlatılmaz. Açık Unity Editor kilidi korunur; test/derleme, kayıtlı kaynakların `C:/GD/Okey101-A4-20261021-0043` kopyasında yapılır. Ana projenin sahneleri ve kullanıcının açık Editor'ı bu kopyanın build üreticisi tarafından değiştirilmez.

`Acceptance` yalnız yerel kabul için Release derleme seçenekleri + `OKEY_DIAGNOSTICS` tanımlarını kullanır; oyuncuya teslim edilen normal Release'te bu kod/fixture'lar yoktur. Başlatıcı, PlayerConnection anahtarı içeren paketi reddeder. İşletim sistemi güvenlik/izin ayarı değiştirilmez.

```powershell
& .\scripts\Build-Windows.ps1 -Configuration Acceptance
& .\scripts\Run-BackgroundAcceptance.ps1 -BuildDirectory <paket> -OutputDirectory <yeni-klasör> -Mode Smoke
& .\scripts\Run-BackgroundAcceptance.ps1 -BuildDirectory <paket> -OutputDirectory <yeni-klasör> -Mode Flow
& .\scripts\Run-BackgroundAcceptance.ps1 -BuildDirectory <paket> -OutputDirectory <yeni-klasör> -Mode Soak
```

Başlatma: Unity `-batchmode -force-d3d11`, Hidden pencere stili, BelowNormal süreç önceliği, ayrı log/ayar/tekrar klasörü. [Unity6000.6 Player belgeleri](https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/player) batch modunda görünür ekran ve kullanıcı girdisi olmadığını belirtir. İlk gerçek deneme otomatik kamera çiziminin çalışmadığını gösterdi: siyah PNG'ler ve0GPUörneği. Bu koşu grafik/performance kabulü sayılmadı. Sonraki sürüm `RenderPipeline.StandardRequest` ile 1920×1080 RenderTexture'a masa ve uGUI'yi açıkça çizer. Kamera/UI hedefi yalnız tanılama modunda değişir; siyah kare kontrolü ve render istek sayısı rapora girer.

Flow: gerçek prefab/sahne, aktif ve etkileşebilir düğme callback'leri + taş giriş controller'ı kullanılır. Normal tam el,101/beşçift/okey bitişi, değişim, ceza iptal/onay, sonuç/yeni başlangıç, altı fixture/replay ve10menü/masa döngüsü doğrulanır. Her fixture başlangıcı golden hash ile denetlenir; tekrar öncesi farklı otorite yüklenir, ardından beklenen hash/RoundId/komut sayısı aranır. Görüntü ancak kabul edilmiş komut ve arayüz yenilemesi bitince alınır. Bu, fiziksel Windows giriş testi değildir.

Soak:30sısınma/600sprofil/1800skararlılık, üç gerçek bot ve insan koltuğunda planner, periyodik2sduraklat/devam, sonraki eller. Kayıtlar `frames.csv`, `memory.csv`, `soak.json`; ana/render/present CPU sayaçlarına ek ekran dışı çizim isteğinin CPU süresi vardır. Batch modundaki GPU süre örnekleri0ise **GPU süresi ölçülemedi** yazılır;0msGPU başarısı çıkarılmaz. `performanceTargetMet` arka planda daima false; yalnız ayrı `backgroundFrameTargetMet` ham döngü hedefini değerlendirir. Ön plan DXGI/swapchain p95 kabulü bununla kapatılmaz.

Başlatıcı yalnız kendi yarattığı PID'yi izler; Flow/Smoke180s, Soak1875s üst sınırı vardır. Süre aşımında yalnız bu gizli test süreci kapatılır, kanıtlar korunur. `system-load.csv`, toplam CPU yükünü ve boş belleği15s arayla kaydeder. Kullanıcının diğer uygulamaları/Editor'ı açık olduğundan sonuçlar bu yük altında yorumlanır. Ses yalnız test sürecinde susturulur; kullanıcı ayarları/diğer uygulamaların sesi değiştirilmez.

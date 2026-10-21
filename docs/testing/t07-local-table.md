# T07 — Yerel oturum ve temel 3D masa (A0)

Durum: Uygulama ve otomatik kontroller tamam; yerel pencere kabulü sürüyor. Tarih: 2026-10-20.

## Davranış ve sınırlar

`LocalSession` tek seri komut kuyruğuyla değiştirilemez Core durumunu yönetir. Kural hesabı işçi iş parçacığında, görünüm ve Unity nesneleri ana iş parçacığındadır. Snapshot, sıra ve kabul sınırı aynı kilitle korunur. Duraklatma, yeni el ve Dispose eski bekleyen işleri iptal eder; geç kalan sonuç yeni otoriteye yazılamaz. Dispose kuyruk boşalınca semaforu kapatır.

Oyuncu yalnız kendi elini, rakiplerin taş adetlerini ve herkese açık masayı görür. `GetLastAcceptedReceipt(viewer)` yalnız son kabul edilmiş geçişi saklar; kapalı çekişin kimliğini diğer oyunculardan gizler. İşçi iş parçacığından Unity callback'i çalıştırılmaz.

`TurnDraft` otoriteyi değiştirmez. `TablePresenter` 24 yuvalı ıstakada fiziksel taş kimliklerini korur. Bir taşı seçip başka taşa tıklamak yer değiştirir; boş yuvaya tıklamak taşır. Sıralama renk/sayı/kimlik sırasındadır; gerçek okeyler sona gelir. Başlangıç dağıtımı sabit test tohumu 71, başlangıç koltuğu 0'dır.

`GameBootstrap` çekme/atma komutlarını görünümün RoundId/Version/Sequence bilgisiyle gönderir. Bir işlem sürerken tekrar giriş engellenir. Başarısız komut otoriteyi veya görünür taş kümesini değiştirmez. Ceza isteyen atışta 101 puan açıkça onaylanır. Duraklatma devam eden işi geçersiz kılar.

A0'da kapalı desteden çekilir. Yandan çekişin kural/oturum API'si mevcuttur; açılış/işleme önizlemesi ve bu çekişi tamamlayan arayüz T08 kapsamındadır. Development paketindeki **Aktif oyuncuya geç** dört oyuncuyu elle denemek içindir. Bot değildir. Üç bot T09, Blender varlıkları T11, tam menü/sonuç/yeni el akışı T12 kapsamındadır. A0 tüm ilk oynanabilir sürümün tamamlandığı anlamına gelmez.

## Çalıştırma ve geliştirme

Proje Unity 6000.6.6f1 ile açılır. `Assets/Okey101/Scenes/Bootstrap.unity` sahnesi çalıştırılır. Bootstrap, Table sahnesini yükler. Geçici masa, ıstaka ve taş modelleri prefab'lardan gelir; runtime UI uGUI ve Input System kullanır. Yeni ağ SDK'sı veya dış varlık eklenmedi.

```powershell
& .\scripts\Test-Unity.ps1 -Platform EditMode
& .\scripts\Test-Unity.ps1 -Platform PlayMode -Filter Okey101.PlayMode.Tests.TableSmokeTests
& .\scripts\Build-Windows.ps1 -Configuration Development
```

Build betiği açık proje kilidini denetler; Editor kapatmaz ve kilit dosyasını silmez. Her derleme `artifacts/builds` altında yeni bir klasöre gider. Mevcut exe üzerine yazılmaz. Windows64, Mono, .NET Standard 2.1 ve Direct3D11 açıkça seçilir. Başarısız BuildReport sıfır olmayan süreç sonucuna dönüşür.

`Okey 101/Create or update A0 table` yalnız Okey101 sahne/prefab/materyallerini üretir. Kirli açık sahne varsa kayıt ister; sessizce sahne atmaz. GUID'ler korunarak güncellenir. Build listesi Bootstrap ve Table olarak ayarlanır. SampleScene veya diğer varlıklar değiştirilmez.

## Otomatik kanıt

- Application RED: 8 test, 6 assertion hatası. [Sonuç](../../artifacts/test-results/20261020T175527505Z-c387d508/results.xml).
- Application GREEN: 8/8. [Sonuç](../../artifacts/test-results/20261020T175710559Z-1560b24b/results.xml).
- Sunum RED: 2/2 başarısız; beklenen 22 görünür taş yerine 0. [Sonuç](../../artifacts/test-results/20261020T175541757Z-3fdbf883/results.xml).
- İlk sunum GREEN: 2/2. [Sonuç](../../artifacts/test-results/20261020T180708773Z-ad9a6382/results.xml).
- Tam EditMode: **280/280**, 0 başarısız/atlanan/belirsiz. Önceki 272 Core testi de çalıştı. [Sonuç](../../artifacts/test-results/20261020T180823219Z-127a43fa/results.xml).
- Son PlayMode: **5/5**, 0 başarısız/atlanan/belirsiz. Kapalı/açık durak, taş takası, iki çözünürlük için projeksiyon sınırları ve en az 24 piksel sayı yüksekliği; gerçek Bootstrap/Table/prefab giriş akışı. [Sonuç](../../artifacts/test-results/20261020T181256633Z-e8e36a5d/results.xml). Son derleyici günlüğünde C# hata/uyarısı yok.
- Bağımsız, salt okunur T07 kod incelemesinde düzeltme gerektiren doğrulanmış bulgu çıkmadı. Bu inceleme ayrıca test çalıştırmadı.

Projeksiyon testi native Windows gözleminin yerine geçmez. Performans ölçümü ve Android/iOS kabulü bu aşamada yapılmadı.

## Derleme ve pencere kabulü

İlk Windows Development derlemesi başarılı: 0 hata, 0 uyarı; 170.979.227 bayt. [BuildReport](../../artifacts/builds/20261020T180842043Z-1fb164fb/build-report.json).

1280×720 paket başlatıldı. Oyuncu günlüğü Direct3D11, PC kalite, 22 el/22 görünür taş ve 20 deste bildirdi. Windows güvenlik duvarı izin penceresi native gözlemi kapattı. Ağ izni verilmedi; kullanıcıdan pencereyi elle kapatması istendi. Native görsel/girdi kabulü henüz tamamlanmış sayılmaz.

Oyunu elle deneme sırası:

1. Güvenlik duvarı sorusu çıkarsa **İptal et** seçilir; A0 internet gerektirmez.
2. Başlangıçta 22 taş görünür. İlk oyuncu çekmeden atar. Bir taş seçilip boş yuvaya taşınabilir veya başka taşla yer değiştirilebilir; **Sırala** renk/sayı sırasına dizer.
3. Bir taş seçilip **Seçili taşı at** kullanılır. Gerekirse ceza açıkça onaylanır. El 21 taşa iner ve sıra sonraki oyuncuya geçer.
4. **Aktif oyuncuya geç**, ardından **Taş çek** kullanılır. Deste 20'den 19'a, el 21'den 22'ye değişir. Bir taş daha atılarak tur tamamlanır.
5. **Duraklat** masayı durdurur, **Devam et** kaldığı yerden sürdürür. Yeniden açılış aynı A0 test dağıtımını başlatır.

## Additional observations

T09 takip kabulü (2026-10-20): güncel Release paketinde 1280×720 ve 1920×1080'de 22 taş doğrudan gözlendi; sıralama, seç/taşı, duraklat/devam ve insan çekiş/atışları çalıştı. Böylece ortak sahnenin T07 native kapısı kapandı. Eski A0 paketinin engellenen gözlemi geçmiş kaydıdır; o pakete geriye dönük kabul verilmedi. Güncel kanıt ve paket: [T09 raporu](t09-bots.md), [makine kaydı](../../artifacts/acceptance/a2-machine.md). T08 hazır el kontrolleri ayrı ve açık.

- **minor:** Paketlenmiş oyunda URP 17.6.0, kullanılmayan Gaussian/Bokeh depth-of-field ve PaniniProjection shader'larının strip edildiğini bildiriyor. BuildReport başarılı; A0 kamerasında post-processing etkin değil. Görsel etkisi ayrıca pencere kabulünde gözlenecek. Core kurallarıyla ilgili değil; paket/renderer ayarları bu görevde kapsam dışı değiştirilmedi.

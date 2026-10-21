# T11 — Masa varlıkları, hareket ve okunabilirlik

Tarih: 2026-10-20. Kapsam: Windows, 3D masa/2D arayüz, yerel oyun. Menü/kalıcı ayarlar T12; uzun kararlılık ve nihai kabul T13. T08'in bütün hazır ellerle insan kontrolü kabulü ayrıca açık.

## Uygulama

- Blender **5.2.2 LTS** ile gerçek Tile/Rack/Table FBX ve düzenlenebilir `.blend` üretildi. Özgün dokular, elle yazılmış rakam çizgilerinden ortak atlas ve üç özgün kısa PCM ses var. Kaynak/kullanım kaydı: [asset-sources.md](../../Assets/Okey101/Art/asset-sources.md); tekrar üretici: [generate-table-art.py](../../scripts/generate-table-art.py).
- FBX gövdesi Unity'de `.72 × .16 × .92` ölçülüyor. Basit BoxCollider, Rigidbody yok. 22 taş ortak mesh ve ortak yüz materyalini kullanıyor; yüz seçimi `MaterialPropertyBlock` ile yapılıyor.
- Kabul edilmiş komutlar **180 ms** hareketle gösteriliyor. Otorite önce güncelleniyor; hareket tamamlanması giriş kilidini açıyor. Azaltılmış harekette aynı fiziksel TileId'ler aynı son konuma ulaşıyor. Hareket, duraklatma boyunca konumunu koruyor. Sahne imhası işi iptal ediyor; devamlar asenkron, kapatılan sahnede yeni nesne/hata oluşmuyor.
- Bütün açık grup kimlikleri kaydırılabilir tarayıcıdan seçiliyor; yakın görünüm seçilen grubu büyütüyor. Kamera yönü sabit. Grup gezinmesi hareket/giriş kilidine bağlı; duraklatma tarayıcıyı kapatıyor.
- Çekme, yerleştirme ve atış sesleri yalnız kabul edilmiş görünür olaylardan geliyor. Aynı karede aynı ses türü bir kez çalıyor; duraklatmada ses de duruyor. Ana ses ayarı ve azaltılmış hareketin kalıcı UI ayarı T12'de.

## Test kanıtı

| Kontrol | Sonuç / dosya |
|---|---|
| İlk davranış RED | 5 test, 4 hata: `artifacts/test-results/20261020T195626648Z-8792dfca/results.xml` |
| İnceleme bulguları RED | Grup tarayıcısı pause ve gerçek bot hareketinde scene unload 2/2 hata: `20261020T200843343Z-287b610a` |
| Renk işareti örtülmesi RED | `20261020T200639377Z-48513561` |
| Renk işareti boyutu RED | `20261020T201353714Z-f3cdcdbb` |
| 13 taşlık per/başlık örtüşmesi RED/GREEN | `20261020T202524490Z-f007dc6b` / `20261020T202547868Z-63732528` |
| Son bütün PlayMode | **43/43**, `artifacts/test-results/20261020T202617528Z-99cf3dac/results.xml` |
| Son bütün EditMode, Simulation hariç | **308/308**, `artifacts/test-results/20261020T201137491Z-4d9dcbf2/results.xml` |

11 T11 testi: planın beş testi ve gerçek komut sırasında pause/resume, token iptalinde son konum/kilit, gerçek bot hareketinde unload, pause/hareket sırasında grup tarayıcısı, gerçek FBX/ortak atlas/dört çözünürlük/işaret okunabilirliği, 13 taşlık perde başlık örtüşmesinin engellenmesi. `ManyGroupsRemainSelectable` 63 fiziksel taş, 21 farklı grup kimliği, son satır UI düğmesi ve kaydırılabilir içerikle çalışıyor. Uzun perin normal/yakın görünüm kontrolünde yeni hareket eden Collider sınırları önce `Physics.SyncTransforms` ile güncelleniyor.

Piksel testleri gerçek boyutlu RenderTexture kullanıyor: batch GameView `pixelRect` değerini 640×480'e sıkıştırabildiği için yalnız istenen dikdörtgeni atamak çözünürlük kanıtı sayılmıyor. Paket görüntüleri ayrıca kontrol ediliyor.

Kural motoru/bot algoritması değişmedi; T10'un ayrı 1000/1000 Simulation kanıtı korunuyor, bu görsel değişiklik için tekrar çalıştırılmadı. İnceleme salt okunur ayrı ajanla yapıldı; iki doğrulanmış bulgu regresyon testleriyle düzeltildi.

## İlk profil

İlk ölçümler gerçek paketlenmiş **Development**, Direct3D11, PC kalitesi, A0'daki Ryzen 7 9800X3D / RTX 5090 bilgisayarında. Editor/test işi profil sırasında çalışmadı; önceden açık eski test oyunları korunarak ölçüldü. 1080p'de 120 kare ısınma sonrası 600 kare ve gerçek komut/hareketler kaydediliyor.

Önceki adayın `artifacts/acceptance/a3/final-20261020T2013/profile.json` ölçümü: CPU toplam ortalama16.671ms/p9516.740ms; ana iş parçacığı ortalama0.730ms/p951.453ms; Present beklemesi ortalama15.936ms; render iş parçacığı0.252ms; GPU ortalama0.360ms/p951.006ms. Gerçek kare aralığı ortalama16.671ms/p9516.669ms, yaklaşık60FPS. CPU toplamı beklemeyi içerir; bunu aktif CPU işi diye okumayın. GPU/CPU değerleri bağımsızdır, toplanmaz.

Aynı adayda 99 karede oyun miksinde ses saptandı, tepe0.1005, pencere RMS0.00645; dijital kırpılma yok. Bu ölçüm hoparlör seviyesi/işitsel kalite/tekrar rahatsızlığının insan onayı değildir.

## Son paket ve A3 görüntüleri

- Development: `artifacts/builds/20261020T202645979Z-1ffc2874/Okey101.exe`, 194014989 bayt, 0 hata/0 derleme uyarısı.
- Release: `artifacts/builds/20261020T202704423Z-4909565c/Okey101.exe`, 126628346 bayt, 0 hata/0 derleme uyarısı. Yan veri dosyaları aynı dizinde korunmalı.
- İki PE/asset paketi kapısı doğrulandı: Development6fixture; Release0fixture ve geliştirme paneli/PracticeDeals/ekran-profili probu metotları yok.
- Son görüntüler gerçek Development framebuffer'ından `artifacts/acceptance/a3/final-20261020T2028/` içine üretildi; dört dosya **gözle incelendi**. Ekran ölçüleri PNG başlığıyla ve native oyuncu günlüğüyle eşleşiyor. Bunlar masaüstü/güvenlik penceresi ekran görüntüleri değil, yalnız oyunun kendi çıktısı.

| Çözünürlük | İnsan taşı | En küçük sayı yüksekliği |
|---|---|---|
| [1280×720](../../artifacts/acceptance/a3/final-20261020T2028/1280x720.png) | 22 | 36.43px |
| [1920×1080](../../artifacts/acceptance/a3/final-20261020T2028/1920x1080.png) | 22 | 54.65px |
| [2560×1440](../../artifacts/acceptance/a3/final-20261020T2028/2560x1440.png) | 22 | 72.86px |
| [2560×1080](../../artifacts/acceptance/a3/final-20261020T2028/2560x1080.png) | 22 | 54.65px |

Türkçe metinler, kırmızı daire/sarı baklava/mavi üçgen/siyah kare, iki sıralı ıstaka ve 22 taş görünür. Renk işaretleri gövde üzerinde kalıyor; testte 720p genişliği en az4px. Uzun açık gruplar yakın görünüm/tarayıcı ile seçiliyor, kamera dönmüyor.

**Son kısa profil:** [profile.json](../../artifacts/acceptance/a3/final-20261020T2028/profile.json), son Development paketi, aynı A0 donanımı, 1920×1080, 600 CPU/GPU örneği, 14 bot turu çağrısı. CPU toplam ortalama16.675ms/p9516.776ms; ana iş parçacığı ortalama0.815ms/p951.458ms; render iş parçacığı ortalama0.239ms; Present beklemesi15.855ms. GPU ortalama0.277ms/p950.702ms. Kare aralığı ortalama16.675ms (**59.97FPS**), p95 **16.670ms**. Önceki adayda karep9516.715ms ile16.7ms sınırı0.015ms aşılmıştı; iki kısa koşunun sonucu T13'ün10dakika/30dakika performans ve kararlılık kapısını kapatmaz. Editor çalışmadı; eski test oyunları korundu, Windows güvenlik diyaloğu açıktı ve son örnek sırasında Release de başlatıldı. Bu koşullar ayrı bir kontrollü nihai performans deneyi değildir.

Son oyun miksinde97 karede ses, tepe0.0937, pencere RMS0.00588; kırpılma yok. İşitsel seviye/tekrar rahatsızlığı insan kontrolü bekliyor. Son Release1920×1080/PC/DX11/22taş ile açıldı; native ekran gözleminde Windows Güvenliği penceresi son oyuncu UI testini engelliyor. Kullanıcıya tüm açık T11 güvenlik sorularını İptal et ile kapatması soruldu; yanıt bekleniyor. Native giriş/odak/ses kabulü otomatik test veya framebuffer görüntüleriyle geçmiş sayılmıyor.

## Additional observations

- **minor — URP PostProcessUtils:** Kullanılmayan GaussianDepthOfField/BokehDepthOfField/PaniniProjection shader'larının strip edildiğine ilişkin çalışma zamanı uyarıları önceki aşamalardan sürüyor. Bu masada ilgili efektler kullanılmıyor. BuildReport uyarı sayısı ile çalışma zamanı uyarıları ayrı; renderer kapsam dışı değiştirilmiyor.


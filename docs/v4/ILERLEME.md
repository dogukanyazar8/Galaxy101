# v4 · ilerleme günlüğü

Görev: `Design/PROMPT_v4_OTONOM_GELISTIRME.md`. Bağlam sıfırlanırsa buradan devam et. Kararlar `KARARLAR.md`, eksikler `EKSIKLER.md`.

## Durum özeti

| Faz | Durum |
|---|---|
| 0.1 taban commit | tamam · `39bed5b` |
| 0.2 paket okuma + Node doğrulama | tamam · 106/106 vektör, 26/26 akış (`node tools/v4/run_package_tests.js`) |
| 0.3 referans yakalama | sürüyor · `tools/ref_capture.py` → `artifacts/v4/ref/` |
| 0.4 fark matrisi | dizin + durumlar + animasyon yazıldı; hat bölümleri hatlardan gelecek |
| 0.5 mimari kararlar | yazıldı · `KARARLAR.md` |
| 1 paralel hatlar | sürüyor · worktree'ler `C:/GD/Okey101-v4/{rules,server,assets3d,data}`, dallar `v4/{rules,server,assets3d,data}` (alt ajanlar) |
| 2 Unity istemci | altyapı başladı · `G101StateRouter`, `GameClock`, `G101Dump` (`fdbb251`) |
| 3 uçtan uca | başlamadı |
| 4 rapor | başlamadı |

## Günlük

### 2026-10-25 · Faz 0

- Mevcut testler (taban, değişiklikten önce):
  - Sunucu xUnit (Docker PostgreSQL, `-p:OutDir=<geçici>`): **99/99**.
  - Unity EditMode (`scripts/Test-Unity.ps1`): 738 test, **699 geçti, 32 kırmızı, 7 atlandı** (`artifacts/test-results/20261025T185452975Z-3c88c7fc/results.xml`).
    - 31 kırmızı: silinen `Design/galaxy101_unity_handoff/spec/data/content.json` ve `.../screenshots` yollarını okuyan testler (MenuServicesTests, NativePanelTests, FramePacingTests, GalaxyCaptureTests). Eski klasör artık `Design/old/galaxy101_unity_handoff`.
    - 1 kırmızı: `LocalizationAssetsTests.NativeTablesContainEveryCatalogEntryAndContextualTranslation` — beklenen "Bronz III", gelen "Elmas I".
    - Düzeltme Faz 2'de (Unity içi veri yolları v4 paketine taşınınca).
  - Paket testleri Node'da: 106/106 vektör, 26/26 akış (`artifacts/v4/package/node_results.json`).
- Taban commit `39bed5b` (yerel; push yok).
- Referans yakalama: `tools/ref_capture.py` v4 yönlendiricisine göre yeniden yazıldı. Bulgular: donmadan sonra sayfanın `setTimeout`'u tutulduğu için hazır olma Python'dan yoklanıyor; SwiftShader masa durumlarında yavaş, yüklü Chrome GPU ile çekiliyor; yıldız/gezegen tuvali iki açılışta farklı (EK-001).

- Faz 1 başlatıldı (753287e'den dört worktree). Hat B'ye ek istek: maç akışı (dağıtım takvimi, tur, botlar, el sonu) taşımadan bağımsız saf C# çalışma zamanı olarak yeni `Packages/com.galaxy101.table/` paketinde; sunucu bunu sarar, Unity çevrimdışı/yönlendirici için aynısını koşar (prototipteki "masa kendi mockServer'ına bağlanır" modeli).
- Faz 2 altyapısı (ana kopya, hatlarla çakışmayan dosyalar): `GameClock` (D-60/61), `G101StateRouter` (menü 01–97, S01–S08), `G101Dump`, `scripts/Capture-G101States.ps1`, `tools/v4/{compare_states,unity_compile,make_meta,states_table,anim_matrix}.py`. Unity'siz derleme: `python tools/v4/unity_compile.py` → OK.

- Unity yönlendirici ilk koşu: 70 durum (menü 54, S01–S15) Development oyuncusunda çekildi (`artifacts/v4/unity/run2`). Menü ve giriş ekranları referansa çok yakın; farklar: lig rozeti, "YAKINDA" (kaldırıldı, K-10), sürüm (0.4.0'a çekildi), `app.version` etiketi (eklendi), açılış ilerlemesi (prototip algoritmasına geçti), lobi demo arka ucu (düzeltildi).
- Hat ilerlemesi (22:35): A 3 commit (V4 motor + botlar, 106/106 vektör, 72/72 JS eşdeğerlik, sürüm kaydı); B 1 commit (protokol merkezi + `com.galaxy101.table` çalışma zamanı); C 2 commit (103 GLB yerleşti); D 1 commit (metinler, temalar, fontlar, ikonlar, rozetler, imleçler, sesler).

- 22:36 referans yakalama bitti: 992/992 kare (ana 144, tekrar 144, EN 144, 4 tema × 140), yol hatası 0. Tekrar farkı yalnız arka plan tuvali (UI kutularında %0). Ana + EN için metin dökümü ikinci geçişte (`ref_capture_text.log`).
- 22:40 Hat A bitti → ana kopyaya birleşti (`9c42c73`). `dotnet test tools/v4/nunit/CoreV4Tests.csproj` 791/791 (vektör 106/106, eşdeğerlik 72/72, mevcut 583). Unity EditMode 946: 907 geçti, 32 kırmızı (eski 32), 7 atlandı → eski handoff yolu düzeltildi (`f5ec3c4`); "Bronz III" testi Hat D birleşince Unity'de yerelleştirme içe aktarımıyla kapanacak (EK-410).
- 22:40 Hat C ve Hat D bitti (birleşme sırası gereği B'yi bekliyor). Hat B: 26/26 paket akışı xUnit'te yeşil, sürüyor.
- Faz 2 kod hatları (K-11, worktree): `v4/menu` (01–97, S06–S08), `v4/ranked` (Dereceli ekranları, S16), `v4/lobby` (S09–S15 + bekleme/davet/izle/katıl), `v4/audio` (28 ses olayı, müzik, 11 imleç). Masa (`v4/table`) B bitince.
- İlk ölçüm (run3, menü+giriş+lobi 69 durum): metin eşleşmesi menüde ~%85, UI piksel %6–30. Eşikler ölçüldü (DURUMLAR "Eşikler").

- 22:50 run4 (Unity, 69 durum): lobi demo arka ucu artık doğru (S13 bekleme odası referansa yakın). Ölçümler `durumlar.json`'a işlendi; menü hattına iş listesi olarak iletildi.
- 22:52 `v4/table` (ana kopya + v4/server + v4/data + v4/assets3d) açıldı; masa hattı başladı (istemci modeli → yerel oturum → TableGame v4 → ağ → yönlendirici → HUD).

- 23:05 Faz 1 birleşmeleri tamam: A (`9c42c73`) → B (`4b7e976`, son `d949edc`) → D (`321d118`) → C (`e372d47`). Ana kopyada:
  - `node tools/v4/run_package_tests.js`: 106/106 vektör, 26/26 akış.
  - `dotnet test tools/v4/nunit/CoreV4Tests.csproj`: 791/791.
  - Sunucu xUnit (Docker): 130/130 (99 mevcut + 27 v4 akış + 4 çalışma zamanı).
  - Unity içe aktarma (`MenuAssetBuilder.PrepareAssets`, batch): yerelleştirme tabloları, Nunito-700, font atlasları (`3daf284`).
  - Unity EditMode: 946 test, 939 geçti, 0 kırmızı, 7 atlandı (GPU testleri; `-Graphics` koşusu ayrıca).

- 23:09 Unity EditMode `-Graphics`: **946/946**. Docker: `.dockerignore`/`railway.json` paket kaynaklarını içermiyordu (imaj derlenmezdi) → düzeltildi; `docker build -f Server/Dockerfile` tamam, PostgreSQL 17 ile `/health` 200, misafir 10.000 çip / 20 elmas; `/v1/info` artık 0.4.0 / 101-v4 (`451b53d`).

- 26 Ekim 06:55 (kullanım sınırından sonra): menü (`d7eb810`), Dereceli (v4 protokolü), lobi (v4 istemci) ana kopyaya birleşti; çift `GalaxyCursor` tekleştirildi (`6565e6a`); menü birleşmesindeki iki "satır ortası yorum" hatası düzeltildi (`f7e440f`).
- Döküm düzeltmeleri: döndürülmüş metin kutusu, çok satırlı metin ölçümü; geri sayımlar biçimle karşılaştırılır (EK-003); EK-006 (Masa sekmesi EN), EK-007 (S14/S15 paket yolu).
- run8 (Unity, 70 durum: menü + giriş + lobi + S16): **50 eşleşti, 7 bilinçli fark**, 13 bekliyor (küçük kaymalar) → menü ve lobi hatlarına geri verildi.
- Masa hattı (`v4/table`) kullanım sınırında kesilmişti; devam ettirildi.

## Sıradaki

- Masa son birleşmesinin Unity EditMode / PlayMode doğrulaması (sunucu açık).
- Masa görsel eşitliği: G32/T15, sonuç kartı, T16–T18; ardından kalan G/T durumları.
- Menü 60/61/95/81; EN ve tema koşusu; Faz 3 istemci uçtan uca boşlukları; son koşu ve rapor.

### 2026-10-26 · Devam oturumu

- Başlangıç ana kopya `ed7f035`, temiz; `v4/table` `b6c4733` ve üç değişmiş dosya. Değişiklikler diff ve paket kaynağıyla incelendi: üst çubuk ölçüleri (G13 referans `deckCount` 34,63 px, `chipsLabel` 92,34 px), tek `table.roomLabel`, atma etiketi CSS animasyonu, gösterge ankrajı, açılmış el bildirimi ve eylem yerleşimi. Korundu; yerel commit `81727ff`, `main` ile birleştirme `5cbc5e1`, ana kopyaya alındı.
- Worktree'nin dört test projesi Unity'siz derlendi: `Galaxy101.PlayMode.Tests`, `Galaxy101.Service.Tests`, `Galaxy101.Editor.Tests`, `Okey101.PlayMode.Tests`: hepsi **0 hata**. Gerçek Unity doğrulaması ayrı, henüz tamamlanmadı.
- Docker Desktop ve yerel sunucu yeniden açıldı; `/health` **200**. `dotnet test tools/v4/nunit-table/TableClientTests.csproj` (worktree, canlı testler dahil): **38/38**, atlanan yok. `dotnet test tools/v4/nunit/CoreV4Tests.csproj`: **791/791**, atlanan yok; mevcut üç CS0649 uyarısı. Günlükler `artifacts/v4/resume-20261026/`.
- Kullanıcı EK-211'i sahiplik ilk insana devredilecek diye cevapladı (K-12); K-10 "Botlara karşı" açık kalacak diye doğrulandı. Sahiplik uygulaması ve regresyon testi sırada.
- Ana kopya birleşmesi sonrası Unity EditMode `-Graphics`: **1006/1006** (`20261026T054009787Z-e22f8e1e`). PlayMode ilk koşu: **145/147**, iki canlı masa testi kırmızı; HighDpiAuditCaptures ortamı verilerek çalıştırıldı, atlanan yok.
- Canlı testlerin kök nedenleri: `GalaxyRealtime.ReceiveAsync` beklenmeyen socket Abort'un `OperationCanceledException` olayını da yutuyordu; `TableGame` anonim `ConnectionChanged` aboneliğini yok edilirken kaldırmıyordu. `7b72fc5` düzeltmesi ve kalkıştan önce oturumu yok eden test sırası düzeltmesi sonrası canlı masa testleri **2/2** geçti (`20261026T054934540Z-98ee6847`; aynı koşunun tek kırmızısı yeni sonuç kartı regresyonuydu).
- Sonuç kartı v4 dört sütununa taşındı; ceza el puanına dahil, XP yalnız o ele ait, son el eylemi "Maçı bitir", D-62 adları eklendi. Hediye fiyatı artık bağlantı türüne değil `VsBots` kipine bağlı. Yeni fiyat regresyonu önce kırmızı (yerel referans masası ücretsiz sayılıyordu), ardından sonuç/fiyat/mevcut masa PlayMode testleri **8/8** (`20261026T060013104Z-d2eb127c`). Görsel kabul henüz yapılmadı.
- Metin kaynağı hatası: CSV ve JSON aynı `strings_v4` Resources anahtarını paylaşıyordu; masa CSV okuyucusu JSON okuyup v4 anahtarlarını kaybediyordu. CSV+meta `strings_v4_flat` olarak adlandırıldı; import betiği güncellendi, JSON anahtarı korundu. Yeni kaynak testi önce `Has(app.STR.ldMsg.0)=false` ile kırmızı; düzeltme sonrası TableSpaceTests **5/5** (`20261026T060002306Z-4c227fd1`).
- Ayar/ses/sunucu commit'leri ana kopyaya alındı: `b63ee81`, `fc434f9`, `8d18cad`, `8f072ea`. Ana kopyada odaklı Unity EditMode **25/25** (`20261026T060149736Z-b856d40a`), PlayMode **4/4** (`20261026T060205229Z-edc54835`); gerçek AudioSource ve üç kamera seçimi dahil. Sunucu 158/158 sonucu hat worktree'sinde; ana kopyanın son sunucu/Core/TableClient koşuları donma yüzünden gözlenemedi, son koşu diye sayılmayacak.

### 2026-10-26 · 09:04 civarı sistem donması / 09:09 yeniden başlatma

- Development build başarılı: `artifacts/builds/20261026T060302714Z-cb7d129e/`, ana kopya `8f072ea`. Sonraki `-States all` yakalaması sistem donması ve kullanıcının yeniden başlatmasıyla kesildi. Yeni Unity/oyuncu başlatılmadı; ajanlar ve eski araç hücreleri yeniden başlatmada sona ermiş.
- `artifacts/v4/unity/run13` **tam koşu değildir**: yeni 01–06 kareleri 09:03:48–09:03:57'de yazıldı; 60/61 aynı klasörde önceki koşudan kalmış (08:28). Manifest yok. Bu klasörü 144 durum kabul kanıtı olarak kullanma veya eski kareleri yeni koşuya sayma.
- `player.log:383,410` son kayıtları: `GALAXY101_DISPLAY mode=fullscreen size=3840x2160 hz=160`, `dxgi: swapchain SetFullscreenState failed (887a0022)`. Pencereli 1280×720 başlatılmasına rağmen her menü yeniden yüklemesi önce kayıtlı/default tam ekranı uyguluyor; yönlendirici yalnız yükleme sonrasında pencere modunu atıyor (`G101StateRouter.OpenMenu`, `MainMenuController.ApplySettings`, `GameDisplay.Refresh`). Başlangıç ve sonraki ekranlarda aynı çakışma tekrar ediyor. Bu doğrulanmış bir yakalama hatası ve donmanın güçlü şüphelisi; tek başına sistem/GPU kök nedeni kanıtı değil.
- Windows son boot 09:09:34; Kernel-Power 41 `BugcheckCode=0`. Son iki saatte Resource-Exhaustion, Display/nvlddmkm/WHEA/volmgr olayı bulunmadı. RAM 63,65 GB; Unity son varlık bellek satırı 194,7 MB (bu toplam sistem/GPU belleği değildir). CPU/RAM/GPU telemetrisi donma anında toplanmadı. WER'nin restart sonrası yeniden sıraya aldığı 141/116/9f kayıtları eski 2026 aylarına ait dosyalara atıf yapıyor; güncel donma kanıtı olarak kullanılmayacak. Korumalı dump dosyaları okunamadı; izin değiştirilmedi.
- Devamdan önce: yakalama kipini ilk menü başlangıcından itibaren sabit pencere/istenen boyutta tut; ardışık ekran yüklemesi regresyonu ekle. Tam ekran geçişini tekrar tetiklemeden doğrula; ilk denemeyi küçük bir durum kümesi ve bellek/GPU izlemeyle yap. Ağır test/derleme/yakalamaları sıralı yürüt. Henüz bu koruma uygulanmadı.
- Korunan worktree WIP: `v4/table` 3D/atlas/SkinService regresyonları, `v4/audio` yükleme/hediye/sohbet yerleşimleri, `v4/server` yeni gerçek sahne uçtan uca testleri; bunlar henüz ana kopyada veya doğrulanmış/commit'li değil. Körü körüne birleştirme.

### 2026-10-26 · Güvenli devam / ilk engel kapandı

- K-15 uygulandı: tanılama ekran isteği ilk sahneden itibaren pencereli ve yakalama boyutunda. Kaydedilmiş ekran tercihi mantıksal olarak korunur; her sahnenin ayarı tekrar uygulaması fiziksel pencereyi değiştiremez. Yönlendirici sahibi yok edilince koruma temizlenir.
- Yeni ekran regresyonu önce **0/2**, sonra GPU kapalı PlayMode'da **2/2** (`artifacts/v4/resume-safe-20261026/display-{red,green}-native/results.xml`). İlk test hazırlığında nullable alan ve EditMode/PlayMode yaşam döngüsü hataları düzeltildi; bunlar davranış RED kanıtına sayılmadı. Mevcut GameDisplay EditMode testleri **16/16** (`20261026T063316991Z-a531231d`).
- Yakalama CLI regresyonları önce **0/3**, sonra **3/3**; eski derleme koruması eklendiğinde yeni dördüncü vaka önce kırmızı, son koşu **4/4** (`python tools/v4/test_capture_guard.py`). Bu testler Unity/oyuncu başlatmaz. `all`/4'ten büyük grup, önceki karelerle karışma ve korumasız eski derleme başlangıçtan önce reddedilir.
- Yeni Development build: `artifacts/builds/20261026T063629348Z-cd3f84eb/`, **0 hata / 19 uyarı**, `captureWindowGuardVersion=1`. Uyarılar eski API/Unity derleme varyantı/serileştirme bildirimleri; uyarısız denmedi.
- İlk gerçek yakalama **01,02**: `artifacts/v4/unity/safe14/display-smoke/`, **2/2**, yol hatası yok, iki kare de **1280×720**, tam ekran/DXGI hata kaydı yok. Tepe çalışma kümesi **458473472 bayt** (437 MiB), özel bayt **931442688**; GPU ölçümü yalnız öncesi/sonrası, koşu içi tepe değildir. Kare gözle incelendi. Sonraki doğrulamalar en fazla dört durumluk yeni klasörlerde sıralı.

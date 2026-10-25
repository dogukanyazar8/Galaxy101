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

## Sıradaki

- Referans yakalamanın bitmesi → `capture_report.json` özetinin buraya ve DURUMLAR'a girmesi.
- Faz 1 hatlarının başlatılması (worktree + dal).
- FARK_MATRISI.

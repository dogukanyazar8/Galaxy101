# v4 · ilerleme günlüğü

Görev: `Design/PROMPT_v4_OTONOM_GELISTIRME.md`. Bağlam sıfırlanırsa buradan devam et. Kararlar `KARARLAR.md`, eksikler `EKSIKLER.md`.

## Durum özeti

| Faz | Durum |
|---|---|
| 0.1 taban commit | tamam · `39bed5b` |
| 0.2 paket okuma + Node doğrulama | tamam · 106/106 vektör, 26/26 akış (`node tools/v4/run_package_tests.js`) |
| 0.3 referans yakalama | sürüyor · `tools/ref_capture.py` → `artifacts/v4/ref/` |
| 0.4 fark matrisi | sürüyor |
| 0.5 mimari kararlar | yazıldı · `KARARLAR.md` |
| 1 paralel hatlar | başlamadı |
| 2 Unity istemci | başlamadı |
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

## Sıradaki

- Referans yakalamanın bitmesi → `capture_report.json` özetinin buraya ve DURUMLAR'a girmesi.
- Faz 1 hatlarının başlatılması (worktree + dal).
- FARK_MATRISI.

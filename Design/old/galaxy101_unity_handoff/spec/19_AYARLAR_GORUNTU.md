# 19 — Ayarlar → Görüntü (güncel)

Görsel: `screenshots/v3/S07_ayarlar_goruntu.jpg`. Kaynak: `G101-Menu.dc.html` (`resRow`, `fpsRow`, `pickMode`).

Satırlar sırasıyla:
1. **Arka plan hareketi** (aç/kapa) — dönen gezegen ve kayan yıldızlar.
2. **Görüntü modu** — segment: *Tam ekran / Çerçevesiz / Pencere*. Unity: `FullScreenMode.ExclusiveFullScreen`, `FullScreenWindow`, `Windowed`.
3. **Çözünürlük** — ‹ değer › adım seçici. Liste monitörün yerel çözünürlüğüne kadar: standart listeden `[1280×720, 1366×768, 1600×900, 1920×1080, 2560×1440, 3200×1800, 3840×2160, 5120×2880, 7680×4320]` monitörü aşmayanlar + en sonda yerel çözünürlük. Varsayılan yerel. Açıklama: "Yerel · Monitör en fazla W×H". Unity: `Screen.currentResolution` / `Screen.resolutions` ile aynı filtre.
4. **Kare hızı** — ‹ › seçici: Otomatik (monitör tazeleme hızı) / 30 / 60 / 90 / 120 / 144 (monitörün desteklediğine kadar). Açıklama tek satır, satır yüksekliği sabit (kayma yok). Unity: `Application.targetFrameRate`, `QualitySettings.vSyncCount`.
5. Diğer mevcut satırlar (`spec/03`).

**Gezegen teması satırı kaldırıldı** (tema ana menüdeki gezegen seçicisinden değişir).
Seçici okları uçta %30 opaklık ve `default` imleç; klavyede ←/→ çalışır.

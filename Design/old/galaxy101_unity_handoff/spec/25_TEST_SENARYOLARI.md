# 25 — Test senaryoları (Unity'de geliştirici menüsü olarak)

Prototipte `G101-Masa` Tweaks → **Test senaryosu** ve masa üstündeki akış düğmeleri. Unity'de `#if DEVELOPMENT_BUILD` hata ayıklama menüsü olarak kurulmalı.

| Senaryo | İçerik | Beklenen |
|---|---|---|
| Bekleme | 1/4 → 4/4 oyuncu gelir, 3‑2‑1 geri sayım | dağıtım töreni başlar |
| Dağıtım | törenin tamamı (dağıtan rastgele) | `spec/21` |
| Oyun | ortada el, sıra bende (çek) | — |
| El açma | hazır açılabilir el | per ortaya, deste sola |
| **İşleme testi** | Herkes açmış (118/141/132/127), 15 per, ellerde 4/3/5 gizli taş. Istakam: Y4, B12 (okey al), R13, K5, Y8, B4 (işle), K13 (at). Gösterge B6 → okey B7 | tüm işleme/okey alma/bitiş yolları |
| **Hatalı açma cezası** | Perlerim toplamı 66 (R3‑5, K7/B7/Y7, B10‑12) | Aç turuncu; basınca taşlar 1.5 sn masada, +101 ceza |
| **İşlek taş cezası** | İşleme masası; botlar %60 işlek taş atar | işlek atan +101 |
| Elden bitiş | açılmamış, 14 taş perde + 1 tek | Elden bit → nükleer animasyon, ×2 ceza |
| Hediye / Sohbet / El sonu / Çıkış / Bağlantı / Ayarlar | `spec/13`, `spec/17` | — |

Deneme verisi: şifreli masa **1234**, özel masa kodu **GX-7Q2M**.

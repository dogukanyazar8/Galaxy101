# Galaxy 101 — v3 tam oyun planı (23 Ekim 2026)

Kaynak: `Design/galaxy101_unity_handoff` v3 (`spec/00`, `spec/18`–`25`, `reference/game`, `screenshots/v3`). Çelişkide en yeni belge geçerli (`spec/00`).

## Kararlar (23 Ekim 2026)
- Sunucu bu hatta devralındı; geliştirme `main`'de, `Server/`. Yerel Docker PostgreSQL ile geliştirilir/test edilir; Railway dağıtımı sonra.
- v3 3D modelleri dışa aktarma sayfasından GLB olarak indirilir, Blender (MCP) ile cilalanır.
- "Botlara karşı" v3'teki gibi **YAKINDA**; botlar çevrimiçi masada boş koltuğu doldurur. Çevrimdışı bot masası yalnız geliştirici test menüsünde (spec/25).
- Ürün kararı bekleyenler (en sonda kullanıcı belirleyecek), şimdilik önerilen varsayılanlar: Eşli puanı takım toplamı; ödül havuzu bahis × 4, kazanan hepsini alır (spec/20); cezalar çarpılmaz, elden bitişte rakip cezası × 2 (spec/22); oda sahibi çıkarsa sahiplik masadaki ilk insana geçer.

## Aşamalar (her biri derlenir, testlenir, v3 görüntüleriyle karşılaştırılır, yerelde commit edilir)
| # | Kapsam | Belgeler | Durum |
|---|---|---|---|
| V1 ✓ | Görüntü: görüntü modu (Tam ekran / Çerçevesiz / Pencere), monitöre göre çözünürlük, kare hızı; değişimde menü, lobi, 3D masa, yükleme ekranı bozulmaz (RT yeniden boyut, UI ölçek, kamera oranı) | 19, 18 | |
| V2 | Kural motoru v3: okey iki uca işleme, okey alma, hatalı açma +101, işlek taş +101, geri topla, soldan alınan taşı geri bırak, elden bitiş (×2), süre sıfırlama, el puanı; NUnit | 22 | |
| V3 | Sunucu: e-posta/şifre kayıt-giriş (kilit), başlangıç bakiyeleri (misafir 10.000, kayıtlı 25.000), Klasik lobi (canlı masa listesi), masa aç (tür, ad, görünürlük, PIN, el, bahis, katlamalı, tur süresi, kurallar), kodla katıl, bekleme odası (bot ekle, çıkar, başlat, geri sayım), davet; v3 kurallarıyla maç | 18, 20, 22 | |
| V4 | İstemci: açılış (S01), giriş seçimi/giriş/kayıt/misafir (S02–S05), ana menü v3 (S06, Klasik + YAKINDA), ayarlar (S07–S08); `Http*Service` + WebSocket, mock'lar testte | 18, 19 | |
| V5 | Lobi (S09), masa aç (S10), PIN (S11), kodla katıl (S12), bekleme odası (S13), davet (S14–S15), bağlanıyor/doldu/kapandı | 20 | |
| V6 | Varlıklar: GLB indir + Blender (zar, boş okey, yonca sahte okey, ıstakalar, masalar), yatan taş yüzleri Unlit; dağıtım töreni (G01–G12) | 21, 23, 24 | |
| V7 | Masa v3: orta tahta 8×20, deste sola, işleme/okey alma göstergeleri, taş izi, geri topla, hatalı açma, işlek taş, elden bit + nükleer, el/maç sonu → lobi; ağ oturumu (sunucu otoriteli) | 22, 23, 17 | |
| V8 | Geliştirici test menüsü (spec/25), kabul listeleri (07, 15) ve tüm v3 görüntüleriyle karşılaştırma | 25, 07, 15 | |

## V1 notları (23 Ekim 2026)
- `GameDisplay`: Tam ekran (exclusive, o boyutun en hızlı modu), Çerçevesiz, Pencere (çalışma alanına sığar). Çözünürlük listesi standart 16:9 boyutlar ≤ yerel + yerel (varsayılan); Tam ekranda yalnız monitörün gerçek modları. Kare hızı v3 listesi (Otomatik, 30/60/90/120/144/165/240 < Hz, monitör Hz). Ayar anahtarları `wmode`, `res`, `fps`.
- Panel ölçeği "Expand": 1280×720 tuval her ekran oranında tam ve ortalı (16:10, 21:9, 4:3, 4K test edildi; kenarlarda tema arka plan rengi).
- Gerçek ekran soak'u (`-galaxyDisplaySoak <dir> -galaxyMenuState <file>`, Acceptance derlemesi): menü, ayarlar paneli ve 3D masa el ortasında 13 mod/çözünürlük/FPS geçişi; 3840×2160 @160 Hz monitörde hepsi istenen boyut/modda, FPS sınırları tuttu (30/60/120/144/160), tuval tam.
- Derlemede bulunup düzeltilenler: URP son işlem gölgelendiricileri ayıklandığı için oyuncu derlemesinde ACES/bloom hiç çalışmıyordu (ayıklama kapatıldı); editörde çalışıp derlemede yüklenemeyen SSAO kaldırıldı; masa açılırken NaN ışın hatası.
- Bilinen: oyuncu başlangıcında "different serialization layout (44/112)" uyarısı (21 Ekim'den beri, işlevsel etkisi yok); kaynağı henüz bulunamadı.

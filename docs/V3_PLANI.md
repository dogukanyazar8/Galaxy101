# Galaxy 101 — v3 tam oyun planı (23 Ekim 2026)

Kaynak: `Design/galaxy101_unity_handoff` v3 (`spec/00`, `spec/18`–`25`, `reference/game`, `screenshots/v3`). Çelişkide en yeni belge geçerli (`spec/00`).

## Kararlar (23 Ekim 2026)
- Sunucu bu hatta devralındı; geliştirme `main`'de, `Server/`. Yerel Docker PostgreSQL ile geliştirilir/test edilir; Railway dağıtımı sonra.
- v3 3D modelleri dışa aktarma sayfasından GLB olarak indirilir, Blender (MCP) ile cilalanır.
- "Botlara karşı" v3'teki gibi **YAKINDA**; botlar çevrimiçi masada boş koltuğu doldurur. Çevrimdışı bot masası yalnız geliştirici test menüsünde (spec/25).
- Ürün kararları (24 Ekim 2026 onaylandı): Eşli puanı takım toplamı, havuz iki ortağa bölünür; ödül havuzu bahis × 4, kazanan hepsini alır (spec/20); cezalar çarpılmaz, elden bitişte rakip cezası × 2 (spec/22); oda sahibi çıkarsa sahiplik masadaki ilk insana geçer. Diğerleri `Server/README.md` "Ürün kararları".

## Aşamalar (her biri derlenir, testlenir, v3 görüntüleriyle karşılaştırılır, yerelde commit edilir)
| # | Kapsam | Belgeler | Durum |
|---|---|---|---|
| V1 ✓ | Görüntü: görüntü modu (Tam ekran / Çerçevesiz / Pencere), monitöre göre çözünürlük, kare hızı; değişimde menü, lobi, 3D masa, yükleme ekranı bozulmaz (RT yeniden boyut, UI ölçek, kamera oranı) | 19, 18 | |
| V2 ✓ | Kural motoru v3: okey iki uca işleme, okey alma, hatalı açma +101, işlek taş +101, geri topla, soldan alınan taşı geri bırak, elden bitiş (×2), süre sıfırlama, el puanı; NUnit | 22 | |
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

## V4b–V6 notları (23 Ekim 2026)
- V4b (d4c8245): ana menü v3 mod şeridi — Klasik oynanır; Dereceli ve Botlara karşı YAKINDA (HEMEN OYNA yalnız tost gösterir).
- V5 (751310a): Klasik lobi — liste/filtre/arama, Masa aç, PIN/kod/bağlanıyor/dolu/kapandı/çevrimdışı/paylaş diyalogları, bekleme odası (bot ekle, çıkar, başlat, geri sayım), arkadaş daveti (30 sn) ve gelen davet diyaloğu. Sunucu Tables işleyicisiyle `LobbyClient` üzerinden konuşur; canlı sunucu testi geçti. Maç başlayınca ağ masası gelene kadar 3D masa yerel motorla açılır.
  - UI Toolkit: kaydırma alanında onlarca filtre (drop-shadow) olunca filtre dokuları kayboluyor ("texture not allocated"); lobi yüzeyleri gölgeyi alt kenarlıkla çiziyor.
- V6 (4c50a41): dağıtım töreni (15×7 balya, iki zar, gösterge, fazla taş, 2+2+2+2+1 dağıtım, kuleler, ıstakalar), HUD dağıtım kartı + Zar at/Tekrar at; v3 atlas (yonca sahte okey), gerçek okey boş yüz, masada yatan taş yüzleri ışıksız.
- Kalan: GLB indirme + Blender cilası (masa/ıstaka/koltuk modelleri; şu an prosedürel ve mevcut prefabrikler), V7 (orta tahta 8×20, işleme/okey alma göstergeleri, taş izi, geri topla, hatalı açma/işlek taş cezası, elden bit + nükleer, ağ oturumu, izleme/oyuna katılma), V8.

## V7–V8 ve 3D varlık notları (23 Ekim 2026)
- V7a/b (afa2bb3): orta tahta 8×20 (açılış sırasına göre 2 satır, seriler sayısına hizalı, gruplar 14+. sütun, çakışma yok), deste ve gösterge sola kayar; işleme uç yuvaları (altın "+") ve okey alma (yeşil dönen oklar), bırakınca/tıklayınca oynanır; taş izi.
- V7c/d (beb7834): Geri topla; 101 altı Aç turuncu, basınca perler 1,5 sn masada → +101 ve süre baştan; Elden bit + nükleer iniş, "ELDEN BİTTİN!", ×2.
- V7e (0f66b2c): 3D masa sunucu maçını oynar (NetworkTableSession, MatchWire, koltuk döndürme). Sözleşmeye DealDto ve kazananın açılış/elden bilgisi eklendi; sunucu ilk turdan önce `Game:DealSeconds` (22 sn) bekler. Lobide masa başlayınca masa sahnesi açılır, dönüşte giriş tekrarlanmaz, lobiye dönülür.
- V8 (3f75c5e): F9 geliştirici test menüsü (spec/25 senaryoları) ve her senaryo için PlayMode testi + görüntü.
- 3D: zar Blender'da üretildi (`tools/blender/make_die.py`, 30 mm, 5 mm köşe, gerçek çukur noktalar, kırmızı 1); masa/ıstaka/koltuk/ikram kaynakları (racks3d.js, gifts3d.js) v3'te değişmedi, mevcut cilalı GLB'ler geçerli; taş yüzleri v3'ten yeniden pişirildi.
- İzleme ve oynanan masaya katılma 24 Ekim 2026'da eklendi (Server/README v5). Açık: Google girişi ve şifre sıfırlama yapılandırması, ürün kararları (eşli puanlama, havuz, sahip ayrılınca).

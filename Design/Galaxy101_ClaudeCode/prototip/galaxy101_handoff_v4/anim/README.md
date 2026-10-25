# anim/ · animasyon kataloğu · v0.4.0

| Dosya | İçerik |
|---|---|
| `css.json` | Arayüzdeki 67 CSS `@keyframes` tanımı (anahtar kare + özellikler) ve 157 kullanım yeri (öğe kimliği, süre, gecikme, eğri, tekrar, dolgu). 56 tanesi kullanılıyor; `unused` listesindekiler taşınmaz. `dynamic`: durumla değişen animasyon değerleri (açılış çıkışı, menü girişi). |
| `tween3d.json` | 3D masa: taş geçiş modları (travel, slide, lay, deal, block, drop, flip, nuke) formülleriyle, zar, kamera görünümleri ve geçişleri, hediye uçuşu, parçacık efektleri, okeyle bitiş patlaması, kutlama. |
| `easings.json` | Adlandırılmış eğriler: formül + Unity `AnimationCurve` için bezier yaklaşımı. |

Süre ve çakışma kuralları (sıra / yeniden hedefle / atla / paralel): `protocol/timings.md` §2. Bu klasör "nasıl hareket eder"i, timings.md "ne zaman ve neyle çakışınca"yı tanımlar.

Kurallar:
- Süre ms (css.json) ya da s (tween3d.json); konumlar 1280×720 tasarım pikseli ya da sahne birimi.
- Rastgelelik: 3D'de `hash(id + anahtar)` deterministiktir (aynı taş aynı yayı çizer). Parçacık dağılımı `Math.random`; görsel, oyun durumunu etkilemez.
- Yeniden üretim: `?state=ID&ft=T` ile her durumda animasyon T ms'de dondurulur (states/README.md).

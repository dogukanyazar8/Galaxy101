# 04 — Animasyonlar ve Efektler

Kaynak kod: `reference/planet-renderer.js`, `reference/starfield.js`, HTML içindeki `@keyframes`. Süreler saniye, eğriler CSS eşdeğeri. Unity'de UI Toolkit transition'ları (USS `transition`) veya bir tween kütüphanesi (DOTween / PrimeTween) kullanın.

## 1. Sürekli animasyonlar (ana ekran)

| Ad | Öğe | Süre | Eğri | Davranış |
|---|---|---|---|---|
| `planetFloat` | Gezegen grubu | 14 | ease-in-out, sonsuz | dönüş -16° → -14° → -16° |
| Gezegen dönüşü | Gezegen yüzeyi | **16 / tur** (Hızlı) — Normal 40, Yavaş 80 | doğrusal | doku boylamı kayar (küresel izdüşüm) |
| Halka parçacıkları | Satürn halkası | — | — | halka gezegenle birlikte statik; ışık/gölge sabit |
| `floatA` | 1-0-1 taşları | 3.2 | ease-in-out, sonsuz | translateY 0 → -14 → 0, kendi dönüş açısını korur; gecikme 0 / .4 / .8 |
| `pulseG` | HEMEN OYNA | 2.2 | ease, sonsuz | gölge: `0 7px 0 aS` sabit + halka `0 0 0 0 gl` → `0 0 0 16px transparent` (dışa yayılan ışık halkası) + `0 14px 26px rgba(0,0,0,.4)` |
| `shine` | HEMEN OYNA, Mağaza fırsatı | 2.8 / 3.6 | ease, sonsuz | 60–70 px beyaz bant (`rgba(255,255,255,.45)` / `.35`), -20° eğik, translateX -160% → 320%; döngünün %60'ında biter, kalan %40 bekler |
| `iconShine` | Coin ve elmas (sağ üst) | 3.2 | ease-in-out, sonsuz; elmas 1.6 gecikmeli | 8–9 px beyaz şerit (`transparent → rgba(255,255,255,.95) → transparent`), -20° eğik; %0 x=-14 opak 0 → %8 opak 1 → %30 x=+34 → %31–100 görünmez (bekleme). İkon dışına taşmaz (maske/clip). |
| `bob` | Günlük ödül butonu, bugünün ödül kartı | 1.6 | ease, sonsuz | translateY 0 → -4 → 0 |
| Yıldız alanı | Zemin | sürekli | — | bkz. §4 |

## 2. Etkileşim geçişleri

| Öğe | Durum | Değişim | Süre / eğri |
|---|---|---|---|
| Mod kartı | hover / seçili | bkz. 02 §7 | .2 `cubic-bezier(.3,1.5,.6,1)` |
| HEMEN OYNA | hover / basılı | -3 & scale 1.02 / +5 | .12 |
| Alt menü öğesi | hover | -5 & parlaklık 1.12 | .15 |
| Ayarlar butonu | hover / basılı | -2 & parlaklık 1.25 / +3 | .15 |
| Avatar | hover / basılı | scale 1.06 & parlaklık 1.1 / .97 | .15 |
| Gezegen küresi | hover / seçim | scale 1.15 / 26→34 px | .15 / .2 |
| Toggle topuzu | değişim | sol 3 ↔ 27 | .22 `cubic-bezier(.3,1.5,.5,1)` |
| Slider | tık / sürükle | dolgu+topuz / anlık; topuz scale 1.06 + 4 px hale | .15 ease-out / 0 (+ .12 scale) |
| Sekme | değişim | zemin & metin rengi | .15 |
| Liste satırı / kart | hover | zemin `pnH`, kartlarda translateY -4…-5 | .15–.18 |
| Pencere katmanı | açılış | opaklık 0 → 1 | .2 ease-out |
| Pencere | açılış | opaklık 0 / y +14 / scale .96 → 1 | .28 `cubic-bezier(.2,1.2,.4,1)` |
| Alt pencere | açılış | aynı | .26 aynı eğri |
| Kopyalandı / İstek gönderildi | geri bildirim | buton rengi & metin | .15; 1.5–1.8 sn sonra eski haline |
| Bildirim (toast) | görünme | modalIn .2 ease-out | 1.8 sn görünür |
| Tema değişimi | renkler | tüm tema renkleri | .6 (öneri) |

Basılı (active) durumların hepsinde sert alt gölge 1–2 px'e iner veya kaybolur.

## 3. Gezegen render algoritması

Prototip, CPU'da canvas 2D ile çalışır. Unity'de **aynı matematiği HLSL fragment shader'a** taşıyın (tam ekran quad değil, gezegen boyutunda bir UI/sprite quad'ı üzerinde).

### 3.1 Doku (bir kez üretilir)
- Eşdikdörtgen (equirectangular) doku **W×H = 768×384** (gezegen ekranda 420 px'ten büyükse **1024×512**).
- Her piksel için enlem/boylam → küre üzerinde (x, y, z); gezegene özel `gen()` fonksiyonu renk üretir: 3D değer gürültüsü (value noise) + fBm (oktav başına ×2.03 frekans, ×0.5 genlik), renk rampaları (`JUP`, `SAT`, `MARS`, `NEP`, `VEN` tabloları), bant bozulması, fırtına lekeleri.
  - **Jüpiter**: enleme bağlı 16 duraklı bant rampası + gürültüyle bükülme + **Büyük Kırmızı Leke** (boylam 1.2 rad, enlem -0.38, elips 0.2×0.1).
  - **Satürn**: 22 duraklı soluk altın bant rampası, ince şeritler, güney ılıman kuşakta beyaz fırtına ovalleri, **kuzey kutup altıgeni** (60° segmentli altıgen sınır, mavi-gri iç).
  - **Mars**: 5 oktav gürültüden kayalık koyu/açık bölgeler, kuzey ve güney **kutup başlıkları** (farklı boyut).
  - **Neptün**: koyu mavi bantlar, beyaz bulut çizgileri, **koyu fırtına lekesi** (boylam 2.6, enlem -0.35) ve yanında parlak bulut.
  - **Venüs**: düşük kontrastlı, girdaplı sarı bulutlar.
- Unity önerisi: dokuları bir **Editor aracıyla** üretip `Assets/Art/Planets/*.png` olarak kaydedin (2048×1024 önerilir, mipmap açık, wrap U=Repeat). Böylece çalışma zamanında üretim beklemesi olmaz. Tohum (seed) değerleri: Jüpiter 11, Satürn 5, Mars 23, Neptün 37, Venüs 51 (ikinci gürültü tohum+77).

### 3.2 Her kare (shader)
Gezegen parametreleri:

| Gezegen | Eksen eğikliği | Düzleşme | Kenar karartma k | Ortam | Atmosfer rengi | Atmosfer gücü | Halka |
|---|---|---|---|---|---|---|---|
| Jüpiter | 6° | 1.07 | .40 | .02 | (255,226,190) | .22 | yok |
| Satürn | 22° | 1.10 | .45 | .02 | (255,222,175) | .20 | **var** |
| Mars | 14° | 1.00 | .12 | .02 | (255,165,125) | .42 | yok |
| Neptün | 18° | 1.02 | .32 | .02 | (120,185,255) | .75 | yok |
| Venüs | 4° | 1.00 | .28 | .03 | (255,240,212) | .85 | yok |

Adımlar (ekran pikseli → normal):
1. `dx = (px − Cx)/R`, `dy = −(py − Cy)/R × düzleşme`; `r² = dx²+dy²`; kenar yumuşatma alfa = `clamp((1 − √r²)·R/düzleşme + .5)`.
2. `nz = √(1 − r²)`. Işık yönü `L = normalize(−0.5, 0.45, 0.74)` (sol üstten).
3. `diff = dx·Lx + dy·Ly + nz·Lz`; `lam = clamp((diff + .06)/1.06)`.
4. Gölgelendirme `s = amb + (1−amb)·lam^1.15·((1−k) + k·nz^0.55)`.
5. Eksen eğikliği ile döndür: `qx = dx`, `qy = dy·cos a + nz·sin a`, `qz = −dy·sin a + nz·cos a`; `lat = asin(qy)`, `lon = atan2(qx, qz)`.
6. Doku örnekle: `u = (lon + π)/(2π) − zaman/periyot` (tekrarlı), `v = 0.5 − lat/π`; bilinear.
7. Atmosfer kenarı: `rim = (1−nz)^2.4 · clamp(diff + .35) · güç`; renk = `doku·s + atmRengi·rim`.
8. **Satürn halkasının gezegene gölgesi**: ışık yönünde halka düzlemine (qy=0) ışın; kesişim yarıçapındaki halka opaklığı `A` → `s *= 1 − 0.85·A`.
9. Kare hızı: 30 FPS yeterli (prototip 33 ms aralık).

### 3.3 Satürn halkası
- Canvas genişliği gezegen çapının **2.4 katı**; halka gezegenle aynı eğiklikte düzlem.
- Yarıçap profili (gezegen yarıçapı birimiyle) ve opaklık:
  - 1.24–1.53: C halkası, opaklık .10 → .22, renk (150,140,124)
  - 1.53–1.95: **B halkası**, opaklık .78 + .18·sin(πt), renk (214+22·sin, 194+18·sin, 152+10t)
  - 1.95–2.03: **Cassini boşluğu**, .05
  - 2.03–2.27: **A halkası**, .62 (2.205–2.218 arası Encke boşluğu .05), renk (196,182,156)
  - 2.31–2.33: F halkası, .35
  - İnce halkacıklar için 1D gürültü: parlaklık ×(.82 + .26·n(r·140) + .08·n(r·380)), opaklık ×(.75 + .35·n(r·210+7)).
- Halka aydınlatma `.62 + .38·Lpy`; **gezegenin halkaya gölgesi**: ışık yönünde gezegene çarpan noktalarda ×0.1.
- Gezegenin önündeki halka parçası gezegenin üstüne, arkasındaki altına çizilir (z testi: halka noktasının z'si > gezegen yüzeyinin nz'si ise önde).

### 3.4 Gezegen altı katmanlar
- Hale: 660×660 radyal `pg`.
- Atmosfer glow elipsi: `-3px -3px 14px -6px atm` + `0 0 80px pg`.

### 3.5 Performans
- Ekran görünmezken, uygulama arka plandayken veya Ayarlar → **Arka plan hareketi kapalıyken** gezegen ve yıldız güncellemesi durur (son kare kalır).
- Efekt kalitesi: Düşük = 512 doku, 20 FPS, yıldız sayısı %50; Orta = 1024, 30 FPS; Yüksek = 2048, 60 FPS (öneri).
- Çözünürlük ölçeği (1×, 1.5×, 2×) render boyutunu artırır, en fazla 2.2× cihaz piksel oranı.

## 4. Yıldız alanı

1280×720 alan, temanın `st` rengiyle tonlanır, toplamalı (additive) karışım.

| Katman | Adet | Opaklık | Boyut (çekirdek px) | Hız (px/sn, sola) |
|---|---|---|---|---|
| Uzak | 150 | .35 × (.6–1.0) | .9 × (.6–1.3) | 3 |
| Orta | 70 | .70 × (.6–1.0) | 1.5 × (.6–1.3) | 8 |
| Yakın | 22 | 1.10 × (.6–1.0) | 2.4 × (.6–1.3) | 16 |

- Yıldız görüntüsü: radyal sprite (merkez 1.0 → %18'de .85 → %45'te .18 → kenar 0); çizim boyutu çekirdek×2.6×2.
- Yıldızların %35'i tema rengine %55 boyanır, kalanı beyaz.
- **Parıldama**: opaklık × (.55 + .45·sin(t·tw + faz)), `tw` 0.4–2.6, faz rastgele.
- Sol kenardan çıkan yıldız sağdan rastgele y'de yeniden girer.
- **Kayan yıldız**: her 2.2–6.4 sn'de bir (rastgele). Ekranın üst %45'inden başlar; yön rastgele sola-aşağı veya sağa-aşağı (açı ±20° civarı); hız ~820 px/sn yatay + ≥120 px/sn dikey; ömür 0.75–1.25 sn; kuyruk 120–210 px, kalınlık 1.8, uçları yuvarlak; degrade baş `rgba(255,255,255,f)` → %25 `rgba(255,240,210,f·.5)` → kuyruk şeffaf; `f = sin(π·ilerleme)` (yumuşak giriş/çıkış); başta 14 px parlak nokta.
- Unity: 3 ParticleSystem (Start Lifetime ∞ / wrap) + bir Trail/LineRenderer'lı kayan yıldız yayıcı; veya tek bir GPU shader.

## 5. Logo ve taşlar
Statik degrade + sert gölge + glow; tema değişince renkler değişir (bkz. 02 §5). Animasyon yalnızca `floatA`.

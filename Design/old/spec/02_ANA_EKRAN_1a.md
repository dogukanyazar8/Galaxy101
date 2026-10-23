# 02 — Ana Menü (1a)

Ekran 1280×720. Koordinatlar sol üstten. Tüm öğeler mutlak konumlu katmanlardır. Görsel referans: `screenshots/01_ana_ekran_saturn.png`.

## Katman sırası (alttan üste)

1. Zemin degradesi
2. Yıldız alanı (canvas, 1280×720)
3. Gezegen grubu (hale + gezegen + halka)
4. Üst çubuk
5. Logo + 1-0-1 taşları
6. HEMEN OYNA butonu
7. Mod şeridi
8. Alt karartma degradesi
9. Alt menü
10. Gezegen seçici (sol alt), CDX Games + sürüm (sağ alt)
11. Pencereler (z 20), alt pencereler (z 30)

## 1. Zemin

`radial-gradient(ellipse 80% 70% at 50% 35%, bg1 0%, bg2 55%, bg3 100%)` — merkez (640, 252), yarıçap x 1024 (80%), y 504 (70%).

## 2. Gezegen grubu

- Merkez **(640, 300)**. Grup **-16° döndürülmüş**; 14 sn'lik `ease-in-out` döngüyle -16° ↔ -14° arasında hafifçe sallanır.
- **Hale**: 660×660 daire, `radial-gradient(closest-side, pg, transparent 70%)`.
- **Atmosfer/glow halkası**: 296×(296/düzleşme) elips, `box-shadow: -3px -3px 14px -6px atm, 0 0 80px pg`.
- **Gezegen**: 300×300 alan (halkalı Satürn'de canvas genişliği 300×2.4 = 720, gezegen ortada). Detay: `04_ANIMASYON_VE_EFEKTLER.md`.
- Düzleşme (y eksenini sıkıştırma): Satürn 1.10, Jüpiter 1.07, Neptün 1.02, Mars/Venüs 1.
- Gezegenin büyük kısmı logo ve taşların arkasında kalır; bu bilinçlidir.

## 3. Alt karartma

Alt 110 px: `linear-gradient(transparent, rgba(0,0,0,.55))`, tıklamayı engellemez.

## 4. Üst çubuk

Konum: sol 0, sağ 0, üst 0; padding 16/24; yatay flex, gap 14, dikey ortalı.

### 4.1 Avatar (tıklanır → **Profilim**)
- 62×62. Hover: scale 1.06 + parlaklık 1.1; basılı: scale .97.
- **Klasik çerçeve**: halka `conic-gradient(a1 0–245°, dp 245–360°)` (seviye ilerlemesi %68).
- **Altın çerçeve**: `conic-gradient(from 210deg, #8A5A12, #FFE9A8 12%, #E8A317 25%, #FFF6D0 38%, #B8790F 52%, #FFE08A 66%, #C88A1A 80%, #8A5A12)`, `box-shadow 0 0 0 2px #5A3A08, 0 0 16px rgba(255,201,74,.55)`; üstte 9×9 45° dönük altın elmas (`#FFF6D0→#E8A317`, 1.5px `#5A3A08` kenar).
- İç daire: inset 5 px, avatar rengi (varsayılan `#FF8A5B`), 3 px kenar (`pn`; altında `#3A2604`), baş harfler "DY" Montserrat 700 20 beyaz.
- Seviye rozeti: alt -7, ortalı, zemin `a1`, metin `aT` Baloo 900 10, padding 1/7, köşe 7, metin "SV 24" (EN "LV 24").

### 4.2 İsim + derece
- Dikey, gap 5.
- İsim: "Doğukan Y." Montserrat 700 18, tek satır (nowrap).
- Derece: 18×18 kare 45° dönük, köşe 4, `linear-gradient(135deg,#FFE08A,#F5A623)`, `0 0 10px rgba(255,201,74,.5)`; içinde "II" Montserrat 700 9 `#3A1F00`. Yanında "Altın II" (EN "Gold II") Montserrat 700 14 `#FFC94A`.

### 4.3 Boşluk (flex 1)

### 4.4 Çip pill
- Zemin `dp`, köşe 999, padding 5/5/5/6, gap 8, iç gölge `inset 0 2px 0 rgba(0,0,0,.3)`.
- Coin 28×28 (bkz. sabit renkler) + `0 0 10px rgba(255,201,74,.45)`; üzerinden **parlama** geçer (bkz. 04).
- Değer "125.400" Montserrat 700 17, min genişlik 84.
- "+" butonu 28×28 daire `#3CCB6E`, `0 3px 0 #22914A`, "+" 700 20 beyaz. Hover scale 1.08 + parlaklık; basılı translateY 2. **Tık → Mağaza / Çip sekmesi.** Tooltip "Çip satın al".

### 4.5 Elmas pill
- Aynı yapı, padding 5/5/5/8. Elmas 22×22, 45° dönük, köşe 4, `#7FE3FF→#3D7BFF`, glow `rgba(127,227,255,.5)`; parlama 1.6 sn gecikmeli.
- Değer "340", min genişlik 44.
- "+" → **Mağaza / Elmas sekmesi.** Tooltip "Elmas satın al".

### 4.6 Ayarlar butonu
46×46, köşe 14, zemin `pnH`, `0 4px 0 pnS`; dişli ikon 24, stroke `ic` 2.2. Hover translateY -2 + parlaklık 1.25; basılı translateY 3. Dönme animasyonu **yok**. **Tık → Ayarlar.**

## 5. Logo bloğu

Üst 84, yatay ortalı, dikey flex.
- **"GALAXY"**: Montserrat 700 64, satır 1.15, harf aralığı 7. Dolgu: dikey degrade `lg1 0% → a1 45% → a2 100%` (metne kırpılmış). Gölgeler: `0 5px 0 lS` (sert), `0 10px 18px rgba(0,0,0,.5)`, `0 0 26px gl` (glow). Unity: TextCore gradient + outline/underlay veya metni sprite/mesh olarak hazırlayıp shader ile degrade.
- **1-0-1 taşları**: satır, gap 12, alt hizalı, yükseklik 132, üstten 10.
  - Yan taşlar 78×108 köşe 12; orta taş 88×122 köşe 13.
  - Yüzey `linear-gradient(tf1, tf2)`; `inset 0 -7px 0 te` (orta -8), alt `0 5px 0 te2` (orta 6), `0 16px 26px rgba(0,0,0,.45)` (orta `0 18px 30px`), glow `0 0 22px gl` (orta 26).
  - Rakam Montserrat 700 64 (orta 74), satır 1, üstten 8. Renkler `t1`, `t2`, `t3`. Altında 14×14 (orta 16) daire, aynı renk, 3 px boşluk.
  - Dönüş: -10°, +2°, +10°. Süzülme animasyonu (bkz. 04).

## 6. HEMEN OYNA

- Üst 352, yatay ortalı, 360×86, köşe 24.
- Zemin dikey `a1 → a2`; nabız gölgesi animasyonu `pulseG` (bkz. 04).
- Parlama bandı: 60 px genişlik, `rgba(255,255,255,.45)`, -20° eğik, 2.8 sn döngü.
- Metin "HEMEN OYNA" (EN "PLAY NOW") Montserrat 700 34, harf aralığı 1, renk `aT`, tek satır. Alt satır seçili moda göre (`content.json → modes[i].playSub_tr/en`) Baloo 800 13, opaklık .8, üstten 4.
- Hover translateY -3 + scale 1.02; basılı translateY 5. Geçiş .12 sn.

## 7. Mod şeridi

Üst 474, yatay ortalı, gap 12, 6 kart. Varsayılan seçili: **Tekli**.

Kart: 156×74, köşe 18, overflow gizli, padding 0/14, dikey ortalı.
- Zemin mod rengi; üst yarıda `linear-gradient(rgba(255,255,255,.18), transparent)` parlaklık.
- Sağ altta büyük sembol (1, 2, ×2, II, •, #) Montserrat 700 58, `rgba(255,255,255,.16)`, sağ -4 alt -14.
- Ad Montserrat 700 18 beyaz.
- Çevrimiçi satırı: 6×6 `#8EF0B0` nokta (`0 0 6px` glow) + sayı Baloo 800 11 `rgba(255,255,255,.9)`, üstten 4. Botlara karşı: "Çevrimdışı".

Durumlar:
| Durum | Görünüm |
|---|---|
| Seçili değil | `saturate(.7) brightness(.78)`; gölge `0 5px 0 <mod gölgesi>` |
| **Seçili** | filtre yok, translateY -6; gölge: `0 5px 0 <mod gölgesi>`, `0 2.5px 0 5.5px bg3` (boşluk), `0 2.5px 0 8.5px #fff` (beyaz çerçeve, alt kenarı da sarar), `0 14px 32px <mod rengi %60>`; sağ üstte 22×22 beyaz daire + onay tiki (stroke `#1B2440` 3.6) |
| Hover | translateY -7, scale 1.04, filtre yok; `0 5px 0 rgba(0,0,0,.35)`, `0 2.5px 0 5px rgba(255,255,255,.9)`, `0 14px 28px rgba(0,0,0,.45)` |

Geçiş: transform .2 sn `cubic-bezier(.3,1.5,.6,1)` (hafif yaylanma), filtre/gölge .2 sn. Tık → seçili mod değişir, HEMEN OYNA alt yazısı güncellenir.

## 8. Alt menü

Alt 14, yatay ortalı, gap 22, 5 öğe: **Mağaza, Arkadaşlar, Liderlik, Günlük ödül, Kurallar**.
- Öğe: dikey flex gap 6. İkon karesi 56×56 köşe 18; renk/degrade/gölge/glow (bkz. 01 §2), `inset 0 1px 0 rgba(255,255,255,.4)`; ikon 28 (Mağaza 30), stroke beyaz (Mağaza `aT`, dükkan tentesi `rgba(255,255,255,.6)` dolgu).
- Etiket Baloo 800 12, renk tabloda.
- Hover: translateY -5 + parlaklık 1.12; geçiş .15 sn.
- **Günlük ödül** sürekli yukarı-aşağı süzülür (`bob` 1.6 sn).
- Rozetler (ikon karesinin köşesine bağlı, karesiyle birlikte hareket eder):
  - Mağaza: "%50" (EN "%50") `#FF5E6C`, Baloo 900 10, padding 4/6, köşe 7, `0 2px 0 #C23E4A`, üst -7 sağ -14.
  - Arkadaşlar: "3", 20×20 min, köşe 10, `#FF5E6C`, Baloo 900 11, üst -6 sağ -4.
  - Günlük ödül: "1", aynı stil, üst -7 sağ -8.
- Tık → ilgili pencere.

## 9. Gezegen seçici (sol alt)

Sol 24, alt 22, dikey gap 8.
- Etiket "GEZEGEN TEMASI" (EN "PLANET THEME") Baloo 900 10, harf aralığı 1.5, `mt`, sol 4.
- Pill: `rgba(0,0,0,.35)`, 1 px `rgba(255,255,255,.12)` kenar, köşe 999, padding 6/16/6/6, arka plan bulanıklığı 8, gap 10.
- 5 küre (Satürn, Mars, Neptün, Jüpiter, Venüs), gap 6: 26×26, seçili **34×34**; zemin `radial-gradient(circle at 35% 30%, orb0, orb1 55%, orb2)`. Seçili halka `0 0 0 2px #0008, 0 0 0 4px <o temanın a1>, 0 0 14px <o temanın gl>`; diğerleri `0 0 0 1px rgba(255,255,255,.15)`. Hover scale 1.15. Boyut geçişi .2 sn. Tooltip gezegen adı.
- Metin (min genişlik 64): ad Montserrat 700 15 `a1`, açıklama Baloo 700 10 `mt` ("Altın halkalar" vb.).

## 10. Sağ alt bilgi

Sağ 20, alt 14, gap 8, Baloo 700 11 `mt`: [9×9 45° elmas `a1→a2` + "CDX GAMES" (opaklık .85)] · 1×10 ayırıcı (`mt` %40) · "v0.2.0" (opaklık .6).

## 11. Tema değişimi

Gezegen seçilince (sol alttan veya Ayarlar → Görüntü): tema değişkenleri anında güncellenir, gezegen yeniden üretilir (ilk seferde kısa üretim süresi kabul edilir, tercihen önceden hazırlanmış doku), yıldız tonu değişir. Geçiş için renkler 0.6 sn'de yumuşak değişebilir.

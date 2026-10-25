# 09 · Oyun Masası — 3D Sahne

Birim **metre**, **Y yukarı**, sağ el koordinat sistemi (three.js). Unity'ye aktarırken: X aynen, Y aynen, **Z işaret değiştirir** (glTFast bunu otomatik yapar). Tüm sayılar `reference/table/table3d.js` ve `spec/data/table_scene.json` ile aynıdır.

## 1. Temel sabitler

| Sabit | Değer | Anlam |
|---|---|---|
| `TY` | 0.76 | Çuha yüzeyi yüksekliği |
| `TW × TH × TD` | 0.050 × 0.070 × **0.010** | Taş genişlik × yükseklik × kalınlık |
| `SL` | 0.053 | Istakada yuva aralığı (15 yuva / sıra) |
| `RZ` | 0.80 | Istakanın koltuk eksenindeki mesafesi |
| `TILT` | −0.36 rad | Istakanın geriye yatışı |
| `HALF` | 0.86 | Masa dış yarı genişliği (1.72 × 1.72 kare, köşe r 0.20) |
| `FELT` | 0.76 | Çuha yarı genişliği (1.52 × 1.52, köşe r 0.13) |

## 2. Koltuk çerçeveleri

Her oyuncu için Y ekseni etrafında döndürülmüş bir çerçeve: `YAW = [0, +90°, 180°, −90°]` (0 = ben, +Z tarafı). Çerçeve içinde +Z masadan dışarı (oyuncuya doğru), +X oyuncunun sağıdır. Aşağıdaki "yerel" koordinatlar bu çerçevededir.

## 3. Kamera

Dikey FOV **40°**, 16:9.

| Görünüm | Konum | Bakış hedefi |
|---|---|---|
| Oyuncu gözü (varsayılan) | (0, 1.55, 1.62) | (0, 0.62, −0.25) |
| Kuşbakışı | (0, 2.60, 1.42) | (0, 0.58, 0.16) |
| Bekleme (genel) | (0, 3.20, 2.95) | (0, 0.45, −0.30) |

- Geçişler easeInOutCubic; görünüm değişimi 1.3 sn, oturma sonrası "Bekleme → Oyuncu gözü" 3.0 sn (geri sayım boyunca), dağıtım başında 0.8 sn.
- Nefes hareketi: x += sin(t·0.21)·0.006, y += sin(t·0.33)·0.004.
- Roket inişinde 0.4 sn sarsıntı (genlik kalan_süre·0.03).
- Bitiş anında kamera bitiren oyuncuya %16 yaklaşır, hedef %50 kayar (1.3 sn), şerit kapanınca 1.1 sn'de geri döner.

## 4. Işıklar ve ortam

- Ortam: PMREM'li stüdyo ortamı (koyu kutu + üstte 5×1.4 beyaz panel ×3.2, solda 3×3 ×1.6, arkada tema rengi 6×2.5 ×1.4, sağda 2×4 ×1.0), yoğunluk 0.75. Unity: aynı düzende bir Reflection Probe ya da HDRI.
- Hemisphere: gök `--st` (tema), yer `#0a0a14`, 0.45
- Directional (anahtar, gölge): `#fff4e2`, 1.7, konum (1.1, 4.2, 2.0) → (0, TY, 0); gölge haritası 2048, ortografik ±1.5, bias −0.0004, normalBias 0.015
- Spot (masa): beyaz, 9, menzil 7, açı 0.62, penumbra 0.7, decay 1.6; (0, 3.2, 0.3) → masa merkezi
- Point (arka, tema rengi `--a2`): 5, menzil 7; (0, 1.5, −2.3)
- Point (dolgu): `#9fb6ff`, 1.6; (0, 1.6, 2.2)
- Tonemapping ACES, pozlama 1.05

## 5. Oda

| Parça (isim) | Geometri | Konum |
|---|---|---|
| `zemin` | düzlem 7 × 5.4, ızgara dokusu (64 px aralık) | merkez (0, 0, 0.1) |
| `pencere_esik` + `esik_led` | kutu 7×0.14×0.12 + LED şerit 7×0.008×0.01 | z −2.62 / −2.57 |
| `dikme0..3` + `dikme_led0..3` | 0.07×3.2×0.09 + 0.008×3.0×0.01 | x = ±1.2, ±2.35, z −2.64 |
| `duvar` ×2 | düzlem 5.2×3, panel dokusu + LED emissive | x = ±2.6 |
| `kose` ×2 | 0.14×3.2×0.14 | x = ±2.56, z −2.6 |
| `masa_hale` | 3.6×3.6 toplamalı (additive) parıltı | zemin üstü |

Pencerenin ötesi boştur (şeffaf); gezegen ve yıldız alanı arkadan görünür. Unity: kamera Clear = Solid Color alfa 0 değil, **arka planı UI katmanı yerine ayrı bir arka plan kamerası** (gezegen + yıldız) ile çizin, 3D kamera üstüne bindirsin.

## 6. Masa (temalara göre `11` → bkz. §8)

| Parça | Geometri | Y |
|---|---|---|
| `masa_govde` | yuvarlak kare 1.72 (r 0.20) ekstrüzyon 0.05, bevel 0.01 | üstü TY−0.002 |
| `cuha` | yuvarlak kare 1.52 (r 0.13) düz yüzey, 1024² doku | TY |
| `masa_kenar` | halka (dış 1.72 r0.20, iç 1.52 r0.13) ekstrüzyon 0.026, bevel 0.007 | TY−0.006 tabanlı |
| `masa_led` | ince halka (dış +0.03, iç +0.004) 0.008 | TY−0.05 |
| `cuha_led` | çuha kenarında 1 cm iç çizgi | TY+0.0008 |
| `ayak` | silindir r 0.16→0.22, h TY−0.08 | — |
| `taban` + `taban_led` | silindir r 0.55→0.60 h 0.04 + torus r 0.585 | — |
| `holo_halka` | 0.56×0.56 düzlem, kesikli halka dokusu, toplamalı, yavaş döner (0.12 rad/sn; dağıtımda hızlanır) | TY+0.0015 |

**Çuha dokusu (İstasyon)**: radyal degrade (merkez `f1`, kenar `f2`), ince toz gürültüsü, merkezde r 252 ve 262 px beyaz %7 halkalar, r 300 kesikli `--a1` %18 halka, alt merkezde "GALAXY 101" (Montserrat 700 30px, letter-spacing 14px, beyaz %9).

## 7. Istaka

Yerel çerçeve: konum (0, TY+0.033, RZ), X'te TILT dönük. Parçalar:

| Parça | Kutu | Konum |
|---|---|---|
| `istaka_taban` | 0.88×0.012×0.09 | (0, −0.004, −0.012) |
| `istaka_alt` (alt raf) | 0.84×0.011×0.032 | (0, 0.005, 0.006) |
| `istaka_orta` (üst raf) | 0.84×0.009×0.026 | (0, 0.089, −0.012) |
| `istaka_arka` | 0.84×0.18×0.012 | (0, 0.088, −0.03) |
| `istaka_led` | 0.84×0.005×0.006 | (0, 0.18, −0.025) |
| `istaka_uc` ×2 | 0.026×0.19×0.075 | x ±0.433 |

**Yuva konumu** (yerel): `x = (i − 7)·SL`, alt sıra (r=1) `y = 0.045, z = −0.003 + TD/2`, üst sıra (r=0) `y = 0.128, z = −0.022 + TD/2`. i = 0…14.

Rakip ıstakaları aynı modeldir; taşlar sırtları bize dönük, yüzleri görünmez. Rakip taşlarının sabit yuvaları vardır: yeni taş merkezden dışa sırayla doldurulur (`[1,7],[0,7],[1,8],[0,8],[1,6],[0,6],…`), çekme/atma diğer taşları kaydırmaz.

## 8. Temalar (skin) — malzemeler

Renkler gezegen temasından gelir (`table_content.json → planets_table`: `f1`, `f2`, `rim`, `pn`, `a1`, `a2`, `st`, `tf1`).

**Masa**
| Tema | Gövde | Kenar | LED şerit | Çuha |
|---|---|---|---|---|
| İstasyon | koyu metal `#131829` m0.75 r0.42 | metal `rim` m0.9 r0.28 | emissive `--a1` ×2.4 (yavaş nabız ±0.25) | §6 dokusu |
| Ceviz salon | ceviz doku `#3A2014` clearcoat 0.5 | cilalı ceviz `#5A3220` r0.32 clearcoat 1 | pirinç metal `#D9A94A` (ışıksız) | aynı + koyu vinyet + pirinç ince çerçeve |
| Hologram | koyu cam `#070a16` clearcoat | yarı saydam cam `#1a2448` α0.42 | emissive `--a1` ×3.4 | koyu zemin + 48 px `--a1` ızgara (emissive 0.35) |
| Andromeda | `#080a1c` m0.5 r0.3 | iridescent metal `#2a2470` (IOR 1.35, kalınlık 300–520 nm) + emissive `#3a2a90` ×0.25 | emissive `#8FB4FF` ×2.8 | sarmal galaksi dokusu (altın çekirdek, mavi kollar, pembe yıldız oluşum bölgeleri, toz şeritleri), emissive 0.5, "ANDROMEDA · 101" yazısı |

**Istaka**: 6 tema, geometri ve malzemeler aşağıdaki "Istaka modelleri (racks3d.js)" bölümünde. Eski 4 kutu tabanlı ıstaka (Krom/Ceviz/Neon/Andromeda) kaldırıldı.

Unity notu: URP Lit iridescence desteklemez → Shader Graph ile Fresnel + thin-film renk rampası (mor→mavi→pembe) yapın. Clearcoat için URP Complex Lit.

## 9. Taşlar, deste, gösterge, atma, açılan perler

- **Taş gövdesi**: yuvarlak dikdörtgen (r 0.006) ekstrüzyon, bevel min(0.0028, TD·0.2); fiziksel malzeme renk `tf1`, r0.36, clearcoat 0.7.
- **Yüz**: 128×180 doku, **Galaxy 101 Numerals** rakam çizimleri (font değil, aşağıdaki "Taş rakam yazısı" bölümü) + altta r11 nokta; renk ve işleme taş görünümüne göre ("Taş görünümleri" tablosu). Yüz düzlemi taşın %92'si, gövdenin 0.6 mm önünde.
- **Gövde**: 2 malzeme — `tas_<anahtar>` (ön/arka) ve `tas_kenar_<anahtar>` (yan + pah).
- **Geçerli per işareti**: taşın altında 0.78·TW genişliğinde yeşil `#3CCB6E` çubuk. "Yeni gelen taş" için aynı yerde sarı `#FFD964` yanıp sönen çubuk (1.6 sn).
- **Deste**: 5'li yığınlar, yüzü aşağı; sıra sayısı: >60 taş → 4, >25 → 2, aksi 1; aralık TW+0.006 / TH+0.006; her taş ±1.7° rastgele dönük. Desteyi tıklama kutusu tüm yığını kapsar.
- **Gösterge**: (0, TY + TH/2·cos0.42 + 0.002, 0.07), X'te −0.42 eğik, oyuncuya dönük.
- **Atma yığınları**: her oyuncunun sağ köşesi, yerel (0.60, TY + TD/2 + min(k,4)·TD, 0.60), ±0.25 rad rastgele dönüş; son 4 taş tutulur. Ben → dünya (0.6, ·, 0.6); Burak'ın yığını sol önümde (−0.6, ·, 0.6) ve onu ben alırım.
- **Atma halkası** (benim): halka r 0.058–0.066, iç dolgu; atma sırasında nabız (opaklık 0.55–1.0, ölçek +%8); bırakma yarıçapı 0.10.
- **Alınabilir taş halkası**: Burak'ın yığınında aynı halka, çekme sırasında nabız.
- **Açılan perler**: yerel x = −0.245 + a·0.049, z = 0.37 − sıra·0.08, yüzü yukarı. Bir sırada en çok 11 hücre (per + 1 boşluk), dolunca yeni sıra.

## 10. İkram yuvaları ve çapalar

- **İkram yuvaları** (alıcının köşesi, yerel x,z): rakipler `[−0.43, 0.57] [−0.30, 0.61] [−0.46, 0.44]`; ben `[−0.58, 0.42] [−0.60, 0.27] [−0.47, 0.50]`. En çok 3; dolunca en eski küçülerek kaybolur, yeni gelen onun yerine.
- **Avatar çapaları**: Oyuncu gözü — sağ/sol (0, 0.86, 0.98), üst (0, 1.16, 1.14). Kuşbakışı — sağ/sol (0, 0.80, 1.22), üst (0.56, 0.86, 0.98) (üst oyuncunun avatarı açılan perlerin üstüne binmesin). Ekran koordinatı x ∈ [84, 1196], y ∈ [150, 580] aralığına sıkıştırılır.
- **Koltuklar** (rakipler): yerel (0, 0, 1.32); oturak 0.5×0.46 r0.12, sırt 0.5×0.46 (+0.14 rad geriye), dikey LED çizgi (oturan varsa emissive 2.4, boşsa 0.25). Üst kenarda yatay ışık **yoktur** (üst bilgi şeridiyle karışmaması için).

## 11. Blender / Unity varlık hattı

Adım adım iş akışı: `spec/16_MASA_VARLIK_TESLIMI.md`. Özet:

1. `reference/table/Galaxy101_3D_Varlik_Disa_Aktarma.html` → tema **Satürn** → **Tümünü GLB olarak indir** → 28 dosya:
   - `galaxy101_masa_{istasyon|ceviz|hologram|andromeda}_saturn.glb` (4)
   - `galaxy101_istaka_{ceviz|sedef|aluminyum|karbon|neon|andromeda}_saturn.glb` (6)
   - `galaxy101_tas_seti_{klasik|galaksi|altin|neon|mermer}_saturn.glb` (5; her biri 52 taş + arka + sahte okey), `galaxy101_tas_tek_saturn.glb`
   - `galaxy101_koltuk.glb`, `galaxy101_oda_istasyon.glb`
   - `galaxy101_ikram_{tea|coffee|cola|simit|baklava|seeds}.glb`, `galaxy101_hediye_{flower|cake|trophy|rocket}.glb`
2. Gezegen renkleri modele gömülü değildir; Unity'de `SkinService` malzeme adlarına göre (`*_led`, `istaka_eloksal`, `istaka_karbon_dikis`, `istaka_neon_tup1/2`, `masa_kenar`, `deri`, `cuha`, `tas`) çalışma anında boyar. Bu yüzden tek tema (Satürn) indirmek yeterlidir.
3. Blender'da parça/malzeme isimleri, ölçü (metre) ve orijin korunur; kenar yumuşatma, UV, 2K PBR doku pişirme, LOD1.
4. Unity'ye **glTFast** ile GLB.
5. Taş yüzleri Unity'de tek **atlas** (5 görünüm × [13×4 + sahte okey]) olarak üretilmeli; GLB'deki taş başına doku yalnızca referanstır.

## Istaka modelleri (racks3d.js)

Her ıstaka, yan kesit profilinin (u = −z, v = y) x ekseni boyunca 0.84 m çekilmesiyle üretilir; uçlara ayrı uç parçaları eklenir. Tüm temalar aynı yuva sözleşmesine uyar:

| Ölçü | Değer |
|---|---|
| Alt sıra taş merkezi | y 0.045 · taş sırtı z −0.003 · raf üstü y 0.0105 |
| Üst sıra taş merkezi | y 0.128 · taş sırtı z −0.022 · raf üstü y 0.0935 |
| Orta dudak | u 0.003 → (0.022 − TD − 0.0015) · TD 10 mm'de 7.5 mm |
| Gövde boyu | 0.84 m, uç parçalar ±0.42'den dışa |

| Anahtar | Ad | Yapı |
|---|---|---|
| walnut | Klasik ceviz | Yuvarlatılmış masif profil, arka yüzde iki akçaağaç kakma çizgisi + pirinç isim plakası, pirinç ön çubuk, kavisli uç parçaları, pirinç topuz |
| sedef | Sedef kakma | Gül ağacı profil, arka yüzde sedef yıldız/baklava desen paneli, yaldız tepe ve ön çubuk, uçlarda sedef madalyon + yaldız halka |
| chrome | Alüminyum | İnce eloksal profil, arka füme cam şerit, sıralar arası ve tepe LED çizgisi, eloksal uç kapakları (gezegen rengine göre ton) |
| carbon | Karbon | Pahlı karbon fiber profil, eğimli tepe, dudaklarda ve arkada ışıklı dikiş, bıçak formlu uçlar |
| neon | Neon cam | Füme cam kabuk + siyah omurga, ön/orta/tepe neon tüpler, alt ışık, halkalı kule uçlar |
| andromeda | Andromeda | Yanardöner gövde, takımyıldız paneli, LED halka uçlar |

Taş kalınlığı değişirse ıstaka yeniden üretilir (orta dudak taşın önünde kalır).


## Taş görünümleri (tiles3d.js)

Mağaza → Görünüm sekmesindeki taş setleri. Gövde tek malzeme (`tas_<anahtar>`), yüz ayrı şeffaf düzlem (`tas_yuz_<anahtar>_<renk><sayı>`). Her set 52 taş + arka + sahte okey içerir.

| Anahtar | Ad | Gövde | Yüz |
|---|---|---|---|
| classic | Klasik | Fildişi, gezegen temasının taş rengi | Standart renkler, hafif gölge |
| galaxy | Galaksi | Mor/lacivert nebula + sarmal + yıldız dokusu, hafif yanardöner cila, altın çerçeve çizgisi | Açık tonlar, parlayan rakam (emissive) |
| gold | Altın | Parlak siyah oniks gövde, altın yan kenarlar + ince altın iç çerçeve ve köşe elmasları (metalnessMap + emissiveMap) | Canlı renkler (kırmızı, mavi, fildişi, altın sarısı) ince altın konturla |
| neon | Neon | Koyu füme gövde, turkuaz ışıklı kenar (#3DF5E0) | Neon rakamlar, parlama |
| marble | Mermer | Beyaz mermer, gri ve altın damarlar, ayna cila | Lacivert siyah, ince altın kontur |

**Sahte okey**: halka içinde beş köşeli yıldız ve altında "OKEY" yazısı, her sette kendi vurgu rengiyle. Mevcut sadeleştirilmiş kurallarda oyuna dahil değil; varlık olarak hazır (`tas_sahte_okey`).


### Taş rakam yazısı (Galaxy 101 Numerals)

Taş rakamları artık bir font değil, `tiles3d.js → GLYPHS` içindeki özel çizimlerdir: 0–9, tek kalınlıkta çizgi, yuvarlak uç ve birleşim, 60 × 100 em, çizgi kalınlığı 14. Arayüzdeki yuvarlak, kalın ve geometrik dile uyar. "1" dar (42 birim), iki haneli sayılar −2 aralıkla yan yana dizilir. Altta sayının renginde nokta (r 11). Aynı SVG yolları mağaza ve ayarlar önizlemelerinde kullanılır; Unity'de SVG → mesh/SDF olarak alınabilir ya da 128×180 doku olarak pişirilebilir.

Gövde iki malzeme: `tas_<anahtar>` (ön/arka yüz) + `tas_kenar_<anahtar>` (yan ve pah). Kenar renkleri mağaza kartlarındaki alt kenar rengiyle aynı: Klasik #E3D7BC, Galaksi #120E30, Altın #D9B25A (metal), Neon #06090E (turkuaz ışıma), Mermer #C9CCD6.

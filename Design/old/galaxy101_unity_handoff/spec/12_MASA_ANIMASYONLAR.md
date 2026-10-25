# 12 · Oyun Masası — Animasyonlar ve Efektler

Kolaylaştırmalar: `ease` = easeOutCubic, `easeIO` = easeInOutCubic. Süreler saniye.

## 1. Taş hareketleri (`startTween`)
| Tür | Ne zaman | Süre | Yol | Efekt |
|---|---|---|---|---|
| **slide** | ıstaka içi, aynı oyuncu | clamp(0.18 + d·0.6, 0.18, 0.34) | doğrusal, ease | yok |
| **travel** | ıstaka ↔ yer/masa/deste | clamp(0.42 + d·0.38, 0.42, 0.85) | easeIO + yay yüksekliği clamp(d·0.42, 0.05, 0.24)·sin(πu); dönüş easeIO(u·1.15) | uçuşta kıvılcım (`#FFE08A`, %60/kare) |
| **deal** | dağıtım | 0.78 | kübik Bezier (bkz. 10 §2) + 360° dönüş | seyrek kıvılcım |

İniş efektleri:
- Atma yığınına iniş: masada `#FFD964` halka dalgası (ölçek 0.03→0.19, 0.7 sn) + 12 parçacık + pop.
- Açılan pere iniş: `#3CCB6E` halka dalgası + 12 parçacık + pop.
- Benim ıstakama dışarıdan iniş: 10 parçacık + pop + 1.6 sn yanıp sönen sarı çubuk (opaklık 0.55 + 0.45·sin(9t)).
- Pop: 0.28 sn, ölçek 1 + 0.07·sin(πu).
- Gösterge açılışı: 40 parçacık + `--a1` halka + beyaz parlama.
- Bitiş taşı: 60 altın parçacık + iki merkez halkası.

Parçacık patlaması: n parçacık, yatay açı rastgele, dikey 0.2–1.4 rad, hız 0.5–1.5 × taban, yerçekimi 1.4 m/s², 0.9 sn'de söner, toplamalı boyama, 2.2 cm nokta.

## 2. Işıklar ve sahne nabızları
- Masa LED: emissive 2.2 + 0.25·sin(1.2t).
- Atma halkası (atma aşamasında): opaklık 0.55 + 0.45·p, ölçek 1 + 0.08·p, p = 0.5 + 0.5·sin(5t); diğer zamanlarda 0.16.
- Deste parıltısı (çekme aşamasında): 0.25 + 0.35·p. Burak'ın yığın halkası: 0.5 + 0.5·p.
- Holo halka: 0.12 rad/sn döner; opaklık 0.22 ± 0.06.

## 3. İkramlar ve hediyeler (`sendGift`)
1. **Uçuş** 1.35 sn: gönderenin çapasından (ben: sol alt, yerel (−0.6, 1.1, 1.25)) alıcının boş köşe yuvasına. Konum easeIO, yay +0.38·sin(πe), Y'de (1−e)·3π dönüş, Z'de 0.25·sin(πu) yalpa, ölçek 0.35 → 1 (ilk %45'te), %35 olasılıkla kıvılcım.
2. **İniş**: 28 parçacık + `#FFD964` halka; 0.5 sn esneme (y −%30, xz +%18, sönümlü).
3. **Bekleme animasyonları** (el bitene kadar):
   - Çay ve kahve: 4 buhar sprite'ı, döngü 1/0.32 sn, 7.5 cm yükselir, opaklık 0.2·sin(π·faz), hafif yalpa.
   - Çiçek: demet x 0.04·sin(1.1t), z 0.06·sin(1.4t) salınır.
   - Pasta: mum alevi y ölçeği 1 ± 0.12 ± 0.08 titrer.
   - Kupa: 0.7 rad/sn döner.
   - Soğuk içecek: 10 kabarcık yükselir.
   - Roket: 3 mm nefes.
4. **Kaldırma** (yeni el ya da yuva dolu): 0.35 sn küçülerek, 14 beyaz parçacık.

Ölçekler (`GIFT_SCALE`): çay 1.05, kahve 1.0, kola 0.98, simit 0.85, baklava 0.82, çekirdek 0.9, çiçek 0.95, pasta 0.82, kupa 1.1, roket 1.15.

## 4. Roket turu
- Süre **5.6 sn**. Yol: centripetal Catmull-Rom; noktalar: başlangıç → başlangıç +0.32 y → ekran NDC (−0.78, 0.62, 1.7 m) → (−0.05, 0.86, 1.15) → (0.82, 0.5, 1.0) → (0.62, −0.5, 0.78) → (−0.15, −0.62, 0.7) → (−0.72, −0.1, 0.95) → (−0.25, 0.42, 1.6) → hedef +0.42 → hedef +0.07 → hedef. (NDC noktaları kameradan o uzaklıkta, `unproject` ile dünyaya çevrilir — kamera hareket ederse yol uçuş başında sabitlenir.)
- Zaman: k = u·0.65 + smoothstep(u)·0.35.
- Yönelim: yola teğet, kendi ekseninde 16 rad tur; son %10'da dik duruşa döner.
- Alev konisi açık, titreşim; her kare 1–2 kıvılcım (`#FFB347`/`#FF6A3D`/`#FFF1C0`) + %55 olasılıkla duman (1.3 sn, 2→13 cm, yükselir).
- İniş: normal iniş + 40 cyan + 30 pembe parçacık + 140 ms sonra ikinci cyan halka + 0.4 sn kamera sarsıntısı.
- Bildirim ve teşekkür iniş sonrası (5.8 / 6.3 sn).

## 5. Emojiler (2D)
Arayüz katmanında: 1.7 sn, kavisli yol (orta nokta iki avatarın üstünden −120 px), ölçek 0.4 → 1.3 (−10°) → 1.7 (+6°) → 1.35 → yukarı kayıp söner. Emoji 44 px, gölge. "Emoji efektleri" kapalıysa gösterilmez.

## 6. Dağıtım ve bitiş
Bkz. `10_MASA_OYUN_AKISI.md` §2 ve §7.

## 7. Arayüz animasyonları (CSS karşılıkları)
| Ad | Kullanım | Tanım |
|---|---|---|
| `popIn` 0.5 | avatar oturma, bitiş şeridi | 0 → 1.1 → 1 ölçek |
| `modalIn` 0.28 | pencereler, kartlar | y +18, ölçek 0.95 → 1, `cubic-bezier(.2,1.2,.4,1)` |
| `drawerIn` / `drawerInL` 0.3 | ikram (sağ), sohbet (sol) | x ±44 → 0 |
| `toastIn` 0.3 | toast'lar | y −14 → 0 |
| `bubble` 2.8 | sohbet balonu | belir 12%, kal, son %15'te yukarı söner |
| `ring` tur süresi | sıra halkası | conic yüzde 100 → 0, doğrusal |
| `pulseG` 1.6–2 | aktif ana buton | gölge halkası 0 → 12 px |
| `countPop` 1.0 | geri sayım | 2.4 → 1 (25%) → 0.7 söner |
| `barFill` 15 / 3 | Sonraki el, bağlantı | genişlik 0 → 100% |
| `floatA` 1.4–2 | "BURAYA AT", bitiş avatarı | y 0 ↔ −6 |
| `wifi` 1.6 | bağlantı ikonu | opaklık 0.2 ↔ 1, yaylar 0.3 sn kaydırmalı |

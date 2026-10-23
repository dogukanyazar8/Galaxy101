# 23 — Masa düzeni ve görsel kalite (v3)

Görseller: `G13`–`G34`. Kaynak: `table3d.js`, `tiles3d.js`, `G101-Masa.dc.html` (`layoutBoard`, `isleTargets`).

## Ortak orta tahta (yere açılan perler)
- İlk per masaya düşünce **deste ve gösterge sola kayar**: x = −0.62 (üstel yumuşatma, hız 3.2/s). Gösterge sadece gösterge balyası kalınca yanından alınır: deste solda iken (x, z) = (−0.62+0.036, 0.115), ortada iken (0.121, 0.02), hafif açılı, küçük altın patlama.
- Tahta: **8 satır × 20 sütun**, koltuk 0 çerçevesinde hücre (col, row) → x = −0.465 + col × 0.049, z = −0.30 + row × 0.0857; taşlar yüzü yukarı, hepsi yerel oyuncuya okunur.
- Her oyuncuya açılış sırasına göre **2 satır** (1. açan 0–1, 2. açan 2–3 …).
- **Seriler sayıya hizalı**: n sayısı sütun n−1'e oturur (12‑13‑1 sarmasında 1 → sütun 13). Ayrılan aralık [start−2, end] (0–13 içinde) — uçlarda işleme yeri saklı.
- Grup/çift: 4/2 sütunluk boş aralık, önce sütun 14+ denenir.
- Her satır dolu aralıkları tutar; **perler üst üste binmez**. Eski perler yerinde kalır; büyüyen per komşuya çarparsa yalnız o per oyuncunun diğer satırına / boş satıra / son çare başka satıra taşınır.
- Satır başında (x −0.505) oyuncu renginde 11 mm nokta + halka; sıra o oyuncudaysa halka nabız atar.
- Yeni el / akış değişiminde tahta kayıtları sıfırlanır.

## İşleme göstergeleri
Bir ıstaka taşı seçilince ya da sürüklenince geçerli tüm hedefler (en fazla 48) görünür:
- **Uç yuvası** (sol/sağ): taş boyutunda (TW+0.014) yumuşak altın parıltı + kesik kenarlı yuvarlatılmış çerçeve (çizgi 22/13, gölge bulanıklığı 14) + ortada süzülen altın düğme (çap 30 mm, beyaz kenar, üstte parlama, koyu "+" 13 px kalın, yuvarlak uçlu).
- **Okey al**: okey taşının üstünde yeşil (#5CF29A → #22B565) düğme, iki kavisli ok, y ekseninde 1.4 rad/s döner.
- Opaklık girişi smoothstep, nefes 3.2 rad/s; sürüklenen taş 80 px yakına gelince hedefe yapışır (yükseklik +22 mm / okeyde +35 mm, düz), hedef 1.1× büyür ve parlaklaşır. Seçili taştan sonra işarete tıklamak da işler.

## Taş izi
Uçan taş (çekme, atma, ıstakaya gelme, işleme) arkasında kare başına 3 ara noktalı yumuşak ışık izi bırakır: additive glow sprite 50 mm, ömür 0.38 s, ölçek 1→0.35, opaklık `0.14 × sin(π·u)` × (1−yaş)². Renk altın #FFD98A, işlemede yeşil #8EF0B0. Kıvılcım yok.

## Taş yüzleri
- **Gerçek okey: yüz tamamen boş** (rakam, renk, desen yok).
- Sahte okey: yonca simgesi (4 daire + sap), renk tema `joker`.
- Masada yatan taşlar (gösterge, perler, atılanlar, balyalar): yüz malzemesi **ışıksız** (Unity: Unlit, tonemapping dışı) — renkler tam doygun ve net. Istakadaki dik taşlar normal PBR.

## Orta daire çizimleri
- Çuha dokusu (1024²): r 200–330 px yumuşak beyaz hale (%3.5), r 256 ince parlak halka (1.6 px, gölge 6), r 302 tema renginde parlayan halka (2.4 px, %50, gölge 10), r 318'de 72 yuvarlak nokta (her 6.'sı büyük 3.2 px, diğerleri 1.7 px).
- Dönen holo halka (1024² doku, düzlem 0.5 m): r 470'te 40 yuvarlak uçlu parlak segment (her 5.'si boş, kalınlık 9, gölge 16), r 438 ince iç halka, r 500'de 8 parlak nokta; dönüş 0.12 rad/s. Perler masadayken holo opaklığı %55 azalır.

## Elden bitiş animasyonu
Taş ıstakadan 0.58 m yükselir (0–%46), titreyerek büyür (×1.15→1.4, kıvılcım), sonra kübik hızlanma ile desteye çakılır (duman + kıvılcım). Çarpmada: beyaz/altın/turuncu ışık küreleri, 3 şok halkası, 280 partikül, 34 mantar bulutu sprite'ı (gövde + şapka, turuncudan griye), nokta ışık 90→0 (e^−3t), 1.2 s kamera sarsıntısı, masadaki tüm taşlar mesafeye göre zıplar, ekranda radyal beyaz-turuncu flaş (1.3 s). Sonra "ELDEN BİTTİN!" kartı.

## Atma / alma bölgeleri
- Sağa atma halkası: tablo düzleminde 0.14 m ya da ekranda 100 px; ıstaka yuvası üzerindeyken tetiklenmez. Yakında taş halkaya yapışır, halka 1.45× büyür.
- Soldan alma: görünmez silindir (r 0.13) + ekranda 92 px; üstüne gelince halka 1.4×.

## Üst sağ sayaçlar
Taş ve çip kutuları 44 px yüksek (çıkış/ayar düğmeleriyle aynı), iç boşluk 0 16 0 8, rakamlar tabular, sağa hizalı, min genişlik 3ch / 8ch.

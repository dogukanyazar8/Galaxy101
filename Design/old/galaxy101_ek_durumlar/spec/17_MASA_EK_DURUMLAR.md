# 17 · Oyun Masası — Ek Durumlar (T28–T51)

T01–T27 prototipinin basitleştirdiği, gerçek 101 kural motorunun (`com.okey101.core`) ürettiği durumlar.

- Prototip: `reference/table/Galaxy101_Masa_EkDurumlar.dc.html` (her durum Satürn + Mars yan yana; Tweaks → `lang` tr/en, `themes`).
  Tek kareyi tam ekran görmek için konsolda: `__g101.solo('T34','mars')` · EN: `__g101.solo('T34','sat','en')` · çıkış: `__g101.solo(null)`.
- Görüntüler: `screenshots/table/T28…T51_*_saturn.jpg` / `_mars.jpg` + `_EN_` örnekleri (T28, T34, T38, T42, T45).
- Numaralar sabit tutuldu; kaldırılan durumlar: T29 (soluk çip), T31–T32 (okey seçici), eski T34–T35 (ceza onayı), T37 (alınamaz taş).
- Metinler: `spec/data/table_strings_ek_tr_en.csv`.
- Tuval 1280×720, T05 düzeni korunur (üst bar, oyuncu paneli, aksiyonlar, çapalı avatarlar). Kareler masa sahnesini 2D taklit eder; gerçek uygulamada 3D sahne aynen kalır, yalnız HUD/kartlar değişir.

## Ortak bileşen kuralları
| Öğe | Değer |
|---|---|
| Kart | `--pn`, 1px `rgba(255,255,255,.1)` kenar, 22 px köşe, gölge `0 7px 0 --pnS, 0 26px 60px rgba(0,0,0,.55)`, açılış `modalIn .3s` |
| Birincil düğme | `linear-gradient(--a1,--a2)`, yazı `--aT`, gölge `0 5px 0 --aS`, 15 px köşe |
| İkincil düğme | `--pnH`, gölge `0 5px 0 --pnS` |
| Tehlike düğmesi | `linear-gradient(#FF7A86,#E8455A)`, beyaz yazı, gölge `0 5px 0 #9E1F33` |
| Ceza rozeti | tehlike degradesi hap, Montserrat 800 12 px, gölge `0 3px 0 #9E1F33`; metin `+101`, iki ceza `+202` (toplam yazılır) |
| Karartma | `rgba(3,5,14,.62)`; ceza onayında `.5`, maç sonunda `.7` |
| Taş (ıstaka) | 46×66, köşe 7, yüz `--tf1→--tf2`, alt kenar `inset 0 -5px 0 --te`; seçili: 3 px `--a1` çerçeve + 16 px yukarı |
| Okey işareti | taşın kendi yüzü (gösterge Siyah 12 → okey Siyah 13) + altın nokta `#E8B730` + 2 px altın iç çerçeve |
| Okey rozeti | taşın sağ üst köşesinde beyaz hap, 2 px altın kenar; temsil edilen rakam, temsil edilen renkte (masada 17 px, kartta 22 px) |

Renk sabitleri (tema bağımsız): Kırmızı `#D7263D`, Mavi `#1F5FBF`, Sarı `#E09A12`, Siyah `#24242C`, Yeşil (olumlu puan/XP) `#6BE8B0`, Pembe (olumsuz puan) `#FF8A95`.

## 1 · Bot masası başlığı (T28, T30)
- Oda hapı: `Botlara karşı · 101` + `ANTRENMAN` etiketi (`--a1` zemin, 9 px 900, 1.2 px aralık) + `·` + `Çipsiz · XP`.
- El sayacı: oda hapının 38 px altında küçük hap — 5 nokta (oynanan `--a1`, kalan `rgba(255,255,255,.22)`) + `2 / 5. el`.
- Rakip plakasında ad yanında `BOT` etiketi.
- Çip hapı yerine XP hapı (karar): 30 px seviye dairesi (a1→a2, rakam) + 100×7 çubuk + `11.200 / 12.000 XP`.
- T30 Masadan kalk (T25 bot varyantı): başlık aynı, gövde `El bitmeden kalkarsan bu elin XP'sini alamazsın.`, düğmeler `Masada kal` (birincil) / `Kalk` (tehlike).

## 2 · Okey: otomatik değer + masada gösterim (T33)
- Seçici yok. Okey Aç / Çift aç / Yere aç / İşle anında, konduğu yerde alabileceği **en yüksek değeri** otomatik alır (Mavi 5-6-okey → Mavi 7, Mavi 5-okey-Mavi 7 → Mavi 6). Puanlar eşitse (9-9-okey) motor ilk uygun rengi seçer; oyuncuya sorulmaz.
- Masada: okeyin sağ üst köşesinde temsil ettiği rakam, o taşın renginde (Okey rozeti, üstteki tablo). Okeye hover/uzun basma: `Okey · Mavi 7 yerine` ipucu.
- Açış göstergesi (gauge) okeyi en yüksek değerle sayar.

## 3 · Cezalar (T34–T36)
Onay kartı yok; oyuncu ceza alacağı hamleyi bilerek yapar. Ceza olunca anında yazılır.

| Durum | Ceza | Kime |
|---|---|---|
| İşlek taş atmak (masadaki bir pere işlenebilen taş) | +101 | atan |
| Okey atmak | +101 | atan |
| Eksik açılış (seri < 101 ya da çift < 5) — açılış geri alınır, taşlar ıstakaya döner | +101 | açan |
| Attığın taşla sonraki oyuncu elini açarsa | taş değeri × 10 (9 → +90, 13 → +130) | atan |
| Masaya açtığın okeyi başka oyuncu yerden alıp kullanırsa (T47) | +101 | okeyi açan |

- Aç düğmesi 101 altında da basılabilir (pasif görünmez); gösterge 101 altındayken kırmızı `#FF5E6C`.
- Rozet: oyuncu panelinde avatarın üst ortasında, rakipte avatarın sağ üstünde. O elin toplam cezası yazılır (+90, +101, +202…), `popIn`; el sonunda sıfırlanır.
- Bana yazılan ceza: **Ceza toast**'ı (§5) — metin + kırmızı tutar hapı. T34 `Attığın 9 ile Zeynep elini açtı` +90 · T35 `Eksik açılış · 88 / 101` / `Taşlar ıstakana döndü` +101 · T36 `Ayşe masadaki okeyini aldı` +101.
- Rakibe yazılan ceza: avatarının üstünde kırmızı balon (`İşlek taş attı · +101`, `Okey attı · +101`, `Eksik açtı · +101`) + rozet. Sonraki oyuncunun açtığı taşın sahibi rakipse: `{ad} attığı 9 ile açtırdı · +90`.
- T34: açan oyuncunun perinde ceza doğuran taş 1.2 sn `--a1` parlamayla işaretlenir.

## 4 · Yerden taş alma (T38–T39)
- Önceki oyuncunun attığı taş her zaman alınabilir (T05 nabız halkası; "alınamaz" durumu yok).
- Alınan taş: ıstakada 3 px `--a1` çerçeve + üstünde `YERDEN` etiketi. Boşalan atma halkası kesik `--a1` çerçeve + `GERİ BIRAK` etiketi. Bilgi toast `İşine yaramazsa taşı yerine geri bırakabilirsin`.
- Kullanılırsa (pere girerse) etiketler kalkar, tur normal devam eder.
- Geri bırakma (T39): taş halkaya sürüklenir ya da halkaya dokunulur → taş yığına döner (300 ms), oyuncu desteden çeker. Sıra bandı `Geri bırakmak için halkaya sürükle`.
- Alınan taş sonraki oyuncuya atılamaz: sağ halkaya bırakılırsa ıstakaya döner, hata `Yerden aldığın taşı atamazsın · kullan ya da geri bırak`.

## 5 · Mesajlar (T40–T41)
| Tür | Görünüm | Süre |
|---|---|---|
| Ceza | koyu kırmızı `#3A0A14→#22060B`, 2 px `#FF5E6C` kenar, kırmızı "!" ikon, sağda tutar hapı (`+101`, tehlike degradesi, Montserrat 800 14 px) | 3.5 sn + rozet popIn |
| Bilgi | koyu cam hap, `--a1` ikon "i" | 2.5 sn |
| Başarı | `#6BE8B0→#2FBF7E`, koyu yeşil yazı, ✓ | 2 sn |
| Uyarı | koyu hap, 2 px `#FFB13D` kenar, amber yazı, "!" | 3.5 sn; `Süren bitiyor!` tur bitene dek |
| Hata | tehlike degradesi, "×" | 3 sn + 6 px yatay titreşim (2 kez) |
| Rakip balonu | beyaz balon; rakip cezasında tehlike degradesi | 2.5 sn |

- Konum: üst bar ortası (sıra bandının yeri, x 500–950); toast varken sıra bandı gizlenir. Alt satır destekli (11 px).
- Kuyruk: tek toast tam görünür; sıradakiler 30 px aşağıda, %88 ölçek, %60 opaklıkla arkada; üsttekinde `+n` sayacı. Ceza ve hata öne geçer. Aynı metin tekrar gelirse süre baştan başlar. Rakip olayları ve cezaları kuyruğa girmez.
- Kaldırılan engeller: `Açmak için en az 101 gerekli` ve `Çift açmak için 5 çift gerekli` artık ceza mesajı (`Eksik açılış …`, `Eksik çift açılışı …`). `Yerden aldığın taşı bu turda kullanmalısın` ve `Bu taşı bu turda kullanamazsın` kaldırıldı.
- Tam metin listesi: `table_strings_ek_tr_en.csv` (`msg.*`, `pen.*`, `ev.*`).

## 6 · El sonu tablosu v2 (T42–T44) — T24'ün yerine
- Kart 900 px. Sütunlar: sıra 36 · avatar 40 · oyuncu (ad + not; altında rozetler) · CEZA 80 · EL PUANI 180 (sağa yaslı, altında açıklama) · TOPLAM 80.
- Not: `bitirdi` / `açtı` / `açmadı` / `çift açtı` / `çiftle bitti`.
- Bitiren satırı 2 px `--a1` kenar; benim satırım `--pnH`.
- CEZA sütunu: o elin toplam ceza hapı + altında nedeni (10 px, `#FF8A95`): `işlek taş`, `okey attı`, `eksik açılış`, `9 ile açtırdı`, `okeyi alındı`. Ceza toplama eklenir.
- Çarpan rozetleri (birincil degrade hap): `Okeyle bitti ×2`, `Çiftle ×2`, `Elden bitti ×2`; başlık yanında toplam çarpan (`×2`…`×8`). El puanı açıklaması: `−101 × 2 = −202`, `202 × 8 = 1.616`.
- Çift açan oyuncu: gri rozet `Çift açtı · el ×2`, açıklamada ek `× 2`.
- Puansız el (T44): el puanı sütunu `—`, kesik `--a1` kutu `Bu el puanlanmadı` / `Çiftle bitti · toplamlar değişmedi`.
- Sağ üst: bot maçında yalnızca XP (`+340 XP` kazanınca, `+60 XP` kaybedince, yeşil). Çevrimiçi: `ÇİP` + jeton + tutar; XP alt şeritte.
- Alt şerit: `ALDIĞIN İKRAMLAR` (T24 gibi). Düğmeler: `Masadan kalk` / `Sonraki el · 15` (15 sn dolum). Son elde `Sonraki el` yerine maç sonu ekranına geçilir.

## 7 · Maç uzunluğu ve maç sonu (T45–T46)
Seçenekler: A sabit 5 el · **B seçilebilir 3 / 5 / 7 el (önerilen, varsayılan 5)** · C puan eşiği (1.000). C süreyi belirsizleştirir ve el sayacını imkânsız kılar; önerilmez.

XP önerisi: el kazanma +340, el kaybetme +60; maç sırası bonusu 1. +500 · 2. +200 · 3. +80 · 4. +0. Maç bitmeden kalkan o elin XP'sini ve sıra bonusunu almaz. En düşük toplam kazanır.

Ekran (1060 px kart, karartma .7):
- Sol: 84 px rozet (kazanınca kupa, a1→a2; değilse gümüş sıra rakamı), `MAÇ BİTTİ · 5 EL`, başlık `Kazandın!` / `2. oldun`, alt metin; `FİNAL SIRALAMASI` 4 satır (madalya daireleri altın/gümüş/bronz/nötr, 1. satır `--a1` kenar).
- Sağ üst: `KAZANDIĞIN XP` (+1.360 XP), döküm satırları, seviye çubuğu (önceki XP a2→a1, bu maçta kazanılan yeşil `#5BE3A6`). Seviye atlama: çubuk dolar → 300 ms parıltı → yeni seviye çubuğu sıfırdan dolar, `SEVİYE ATLADIN · SV 25` hapı `popIn`.
- Sağ orta: `EL EL PUANLAR` 4 × 5 mini tablo + toplam (negatifler yeşil).
- Konfeti yalnız kazanınca (T23 parçacık sistemi). Düğmeler: `Lobiye dön` / `Tekrar oyna`.

## 8 · Okeyi masadan alma (T47–T49)
Koşul: oyuncu elini açmış olmalı; okeyin temsil ettiği gerçek taş ıstakada. Okeyi masaya açan oyuncuya +101 ceza yazılır (§3).
- Sürükleme başlayınca masadaki okeylerden temsil ettiği taş = sürüklenen taş olanlar parlar (`0 0 0 3px --a1, 0 0 22px --gl`, 1.1 sn nabız). Diğerleri değişmez.
- Bırakma: gerçek taş okeyin yerine oturur (pop), okey ıstakadaki boş yuvaya uçar (450 ms yay, T05 çekme eğrisi). Okeyin rozeti kalkar.
- Uzun basma (500 ms) okeyde: `Bu okeyin yerine Mavi 7 koyarak alabilirsin` ipucu (okeyin altında).
- Geçersiz (T49): hedef okey kırmızı çerçeve, taş ıstakaya döner, hata toast `Bu okey yalnızca Mavi 7 ile alınabilir`.

## 9 · Menü → masa geçişi (T50–T51)
- Yükleme: tema degradesi (`bg1→bg3`), yıldızlar, sağda gezegen (Satürn'de halka), solda `BOTLARA KARŞI · 101 · 5 EL`, logo 76 px, ilerleme çubuğu (12 px, a2→a1, yüzde), ipucu kartı. İpucu her yüklemede listeden rastgele.
- Hata (T51): arka plan sönük (%50), 460 px kart, kırmızı ikon, `Masa açılamadı`, gövde, `Lobiye dön` / `Yeniden dene`. 15 sn içinde yükleme bitmezse gösterilir.

## Kabul
- [ ] Bot maçında bahis/çip metni hiçbir yerde görünmüyor; XP hapı ve el sayacı doğru (T28, T30)
- [ ] Okey seçici yok; okey en yüksek değeri otomatik alıyor; masada temsil rozeti (T33)
- [ ] 5 ceza kuralı doğru tutarla yazılıyor; Aç 101 altında da basılabiliyor; ceza toast + rozet + rakip balonu (T34–T36)
- [ ] Yerden alınan taş YERDEN etiketli; geri bırakılabiliyor; sonraki oyuncuya atılamıyor (T38–T39)
- [ ] Ceza dahil 6 mesaj türü, süreler, kuyruk (T40–T41)
- [ ] El sonu v2: çarpanlar, açıklama, nedenli ceza sütunu, puansız el, XP/çip varyantı (T42–T44)
- [ ] Maç sonu: sıralama, XP dökümü, seviye atlama, mini tablo (T45–T46)
- [ ] Okeyi alma: parlayan hedefler, uçuş, ipucu, geçersiz, sahibine +101 (T47–T49)
- [ ] Yükleme ve hata ekranı (T50–T51)

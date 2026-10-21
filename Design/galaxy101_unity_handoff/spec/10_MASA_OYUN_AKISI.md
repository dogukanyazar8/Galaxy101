# 10 · Oyun Masası — Oyun Akışı, Kurallar ve Zamanlamalar

Durum makinesi (`phase`): `seat → deal → discard|draw (ben) ⇄ opp (rakipler) → fin → end → deal …`
Ek katmanlar (oyunu durdurmaz): ikram paneli, sohbet, oyuncu kartı, ayarlar, masadan kalk onayı, bağlantı koptu.

## 1. Bekleme (`seat`) — görüntü T01, T02
- Kamera "Bekleme" görünümünde. Benim koltuğum dolu, diğerleri boş: boş koltukta kesikli çerçeveli daire + yanıp sönen nokta + **Davet et** butonu (tıklayınca o koltuk hemen dolar).
- Ortada kart (top 470): "MASA HAZIRLANIYOR" (11px, `--a1`), "Oyuncular bekleniyor · n/4" (22px), altında **tek parça ilerleme çubuğu** 200×8: dolan kısım genişliği n/4·100%, degrade `--a2 → --a1`, geçiş 0.6 sn yaylı. Doluysa metin "Masa doldu · oyun başlıyor".
- Demo zamanlaması: Ayşe 1.3 sn ("Selam!"), Zeynep 2.7 sn, Burak 4.1 sn ("Kolay gelsin"). Oyuncu oturunca avatarı pop animasyonuyla (0.5 sn) gelir, koltuk ışığı yanar.
- Dördü dolunca 0.9 sn sonra **geri sayım**: 3-2-1 (her biri 1 sn, 140px logo degradeli rakam, ölçek 2.4→1 sonra 0.7'ye sönerek). Kamera bu 3 sn içinde oyuncu görünümüne iner. Ardından dağıtım.

## 2. Dağıtım (`deal`) — T03, T04
1. **Karma anı (0–750 ms)**: masa merkezinden 3 ışık halkası (0, 170, 340 ms; `#FFD964`, `--a1`, beyaz), 50+30 parçacık patlaması, deste 0.9 sn'de bir tam tur döner ve 5 cm zıplar, holo halka 2.6 sn hızlanıp parlar.
2. **Dağıtım**: 750 ms'den itibaren taşlar sırayla (round-robin: ben, 1, 2, 3; her taş arası **24 ms**). Ben 22 taş, rakipler 21. Deste sayacı 106 → 20 doğrusal azalır.
3. **Taş yolu**: kübik Bezier, süre 0.78 sn, easeInOutCubic. Kontrol 1: masa merkezi çevresinde r 0.16–0.24 rastgele açı, yükseklik TY+0.30…0.42 (girdap). Kontrol 2: hedefin %30 merkeze çekilmiş hâli +0.22 yukarı. Yol boyunca bir tam tur (±360°) Y dönüşü, seyrek kıvılcım. İnince 0.28 sn %7 ölçek "pop".
4. **Gösterge**: son taştan 500 ms sonra deste üstünden göstergeye uçar; inişte 40 parçacık + halka dalgası + beyaz parlama.
5. Son taştan 1.5 sn sonra sıra bende, `discard` (22 taşla başlayan önce atar). Demo: 1.8 sn sonra Ayşe herkese çay ısmarlar ("Çaylar benden!").

## 3. Benim turum
- Süre **30 sn**; avatarımın çevresinde conic sıra halkası 100 → 0. Kalan süre ≤ uyarı süresi (varsayılan 5 sn) olunca halka `#FF5E6C` olur, toast "Süren bitiyor!".
- Süre biterse otomatik: çekme aşamasındaysa desteden çek + 0.7 sn sonra çekilen taşı at; atma aşamasındaysa son çekilen (yoksa en sağdaki) taşı at.
- **Çekme (`draw`)**: desteye tıkla ya da desteyi/yerdeki taşı ıstakaya sürükle. Burak'ın son atığı alınabilir (halka nabzı). Bırakılan yuva doluysa §11'deki yerleştirme kuralı; tıklamayla çekilirse "Otomatik yerleştir" açıkken taş uyduğu taşın yanına (aynı renk ±1 ya da aynı sayı farklı renk), değilse ilk boş yuvaya (önce alt sıra).
- **Atma (`discard`)**: taşı atma halkasına sürükle, ya da taşa çift tıkla (ayar açıksa, 380 ms), ya da taşı seç (tek tık, taş 2.2 cm yükselir) + halkaya tıkla.

## 4. Rakip turu (`opp`)
Her rakip için (ms, tur başına göreli): aktif +120 · çek +720 · (açtıysa) işle +1150 · at +1450 (işlediyse +1800) · sonraki +1600 (işlediyse +1950).
- Çekme: önceki oyuncunun son atığını %25 olasılıkla alır, yoksa desteden.
- İşleme: açmış rakip her turda perlerine en çok 2 taş işler (seriyi uzatır ya da gruba eksik rengi ekler).
- Atma: rastgele taş; %16 olasılıkla sohbet balonu.
- Açmış rakibin elinde 1 taş kaldıysa o taşı **desteye kapatır → bitirir** (§7).
- Deste 0 olursa → "TAŞ BİTTİ" (§8).

## 5. El açma ve işleme — T14
Açılış ölçeri: ıstakadaki geçerli perlerin toplamı / 101 (açıldıktan sonra "AÇIK").
Geçerli per: aynı renkte ardışık ≥3 (seri, 13'ten 1'e dönüş yok) ya da aynı sayı farklı renk 3–4 (grup). Per, ıstakada **boşluksuz komşu** taşlardan oluşur.

| Buton | Görünür | Aktif | Etki |
|---|---|---|---|
| **Aç** | açmadan önce | atma aşaması + toplam ≥ 101 | geçerli perler masaya uçar (yeşil halka dalgası); durum "Açtı · {toplam}" |
| **Çift aç · n/5** | açmadan önce, ıstakada ≥3 çift varken | ≥5 çift | çiftler masaya; mod "çift" |
| **Yere aç** | açtıktan sonra (Aç'ın yerini alır) | yeni geçerli per (çift modunda yeni çift) varsa | perler mevcut sıraya eklenir, toplam artar |
| **İşle** | açtıktan sonra | ıstakadaki bir taş masadaki herhangi bir pere uyuyorsa | uyan taşları perlere ekler (seri başı/sonu, grubun eksik rengi) |

- Açıldı bilgisi butonların üstünde: yeşil nokta + "AÇILDI · SERİ 131" / "AÇILDI · 5 ÇİFT".
- Bir taş sürüklenip masadaki bir perin taşına (7.5 cm içinde) bırakılırsa tek taş işlenir; uymuyorsa kırmızı uyarı "Bu taş o pere işlenemez".
- Son taş hiçbir zaman işlenmez (atmak için 1 taş kalmalı).
- Sıralama butonları: **Seri diz** (renk, sonra sayı; seriler arası boşluk), **Çift diz** (sayı, sonra renk).

## 6. Puanlama (el sonu)
Bitiren −101. Açmayan 202. Açan: ıstakada kalan taşların toplamı (demo rakipler 34). Kazanan en düşük puan. Ben kazanırsam +15.000 çip ve +340 XP, aksi −5.000 ve +60 XP. Önceki toplamlar demo: `[48, 12, −101, 202]`.

## 7. Bitiş anı — T23
1. Son taş kapalı olarak destenin üstüne uçar; iner inmez 60 parçacık + iki halka dalgası (merkezden).
2. Bitirenin köşesine altın spot ışığı (3.4 sn, sinüs zarf, tepe 40).
3. Avatar üstünde 8 renkli konfeti patlaması (240 ms arayla; `#FFD964 #FF6A9A #7FE3FF #8EF0B0 #B79CFF`).
4. Kamera bitirene yaklaşır (rakipse).
5. Üstte şerit (top 96): dönen halkalı avatar + "AYŞE Y. BİTİRDİ!" (Montserrat 800 40px degrade) + "Son taşını desteye kapattı · −101 puan". Ben bitirirsem "BİTİRDİN!".
6. 3.8 sn sonra şerit kapanır, kamera döner, el sonu tablosu açılır.

## 8. Taş bitti
Deste 0 iken tur dönerse: karma efekti + şerit "TAŞ BİTTİ · En düşük puanlı oyuncu eli alır", 2.4 sn sonra tablo.

## 9. El sonu tablosu — T24
780 px kart. Başlık "{n}. EL BİTTİ · AYŞE BİTİRDİ" (ya da "TAŞ BİTTİ"), büyük "Kazandın!" / "{Ad} kazandı", sağda çip değişimi. 4 satır (puana göre): sıra, avatar, ad, not (bitirdi / açtı / açmadı), el puanı (≤0 yeşil, 202 kırmızı), toplam. Satırlar 90 ms arayla soldan kayarak gelir. Alt şerit: "ALDIĞIN İKRAMLAR" + bu elde gelen ikramların küçük görselleri, XP. Butonlar: **Masadan kalk** ve **Sonraki el · 15** (buton gövdesi 15 sn'de soldan sağa dolar, sayı her saniye azalır; bitince otomatik yeni el). Yeni elde masadaki ikramlar küçülerek kaybolur.

## 10. Masadan kalk — T25
Üst soldaki buton ya da tablodaki buton. Kart 460: "Masadan kalkmak istiyor musun?" / "El bitmeden kalkarsan 5.000 bahsin kaybedilir ve yerine bot oynar." **Masada kal** (tema) · **Kalk** (kırmızı). Kalk → yeşil bildirim "Masadan kalktın · lobiye dönülüyor", 1.7 sn sonra lobi (prototipte bekleme ekranına döner).

## 11. Bağlantı koptu — T26
Tam ekran karartma + bulanık gri. Kart: Wi-Fi ikonu (3 yay sırayla yanıp söner), "Bağlantı koptu", açıklama, "Deneme n/5" + dönen gösterge + 3 sn'de dolan çubuk. Demo: 3 deneme (3'er sn) sonra kapanır, yeşil bildirim "Bağlantı yeniden kuruldu". **Şimdi dene** → 1.2 sn'de bağlanır. **Lobiye dön** → masadan kalk onayı. Bu sırada oyun arka planda bot tarafından sürdürülür.

## 12. Klavye
Esc: açık panel/kart/onay penceresini kapatır.

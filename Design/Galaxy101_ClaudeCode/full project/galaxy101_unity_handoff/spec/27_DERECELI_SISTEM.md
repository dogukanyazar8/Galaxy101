# 27 · Dereceli sistem

Kaynak: `Galaxy 101 - Dereceli Sistem.dc.html` (katalog + LP simülatörü), `g101-rank.js` (veri, amblem üretici, kurallar), `rank/` (SVG + `rank.json`).

## Ligler
| # | Lig | Kademe | Hedef dağılım |
|---|---|---|---|
| 0–2 | Bronz | III · II · I | ~%25 (herkes Bronz III'ten başlar) |
| 3–5 | Gümüş | III · II · I | ~%30 |
| 6–8 | Altın | III · II · I | ~%25 |
| 9–11 | Elmas | III · II · I | ~%13 |
| 12–14 | Usta | III · II · I | ~%6 |
| 15 | Galaksi | kademe yok, sıra #1–#100 | Usta I'in üstündeki ilk 100 |

Renkler (açık → koyu kenar), yazı rengi ve çubuk gradyanı `rank/rank.json` içinde.

## Amblem (madalya)
- Yuvarlak madalya, gerçekçi metal; merkezde fildişi okey taşı. Taştaki sayı lig taşının renginde, altında yuvalı lig taşı:
  Bronz 1 · Kehribar → Gümüş 5 · Safir → Altın 9 · Zümrüt → Elmas 12 · Platin/Elmas → Usta 13 · Gül altın/Yakut → Galaksi ★ (okey) · Titanyum/Opal.
- Katmanlar: fırçalanmış metal kenar + tırtık, bevel halkası, guilloche iç plaka, gölgeli okey taşı (kalınlık, iç çizgi, üst parlama), üst kenar parlaması, taş renginde hale. Elmas+: plakada 7 boncuk. Galaksi: yıldızlı plaka, yanardöner kenar.
- Boyutlar 24 / 52 / 160 px. 24 px'te sayı yerine renkli nokta.
- Kademe: alt kenarda 1–3 yuvalı lig taşı (III=1, II=2, I=3). Galaksi: #sıra plakası.

## Sezon çerçeveleri
- Çentikli metal halka: 72 dişli ince kenar, 4 ana yönde V çentik ve içinde eşkenar dörtgen lig taşı, çaprazlarda yıldız parıltı, üst sol parlama. Yuvarlak süs yok.

## Dereceli modu (G101-Dereceli.dc.html)
- Ana menü › Dereceli kartı (artık açık) › HEMEN OYNA.
- Sol: lig madalyası 150 px, kademe, LP çubuğu, sonraki kademe, düşme koruması rozeti, Maç / 1.lik / 1.lik oranı, "Dereceli ilerlemem".
- Mod: Solo (tek başına, 3 rakip), Duo (sen + 1 arkadaş eşli, karşıda bir çift), Squad (4 arkadaş aynı dereceli masada; rakip aranmaz, herkes kendi sırasına göre LP alır).
- Masa: 4 koltuk. Arkadaş yeri › davet penceresi (çevrimiçi, ±1 lig; aralık dışı ve çevrimdışı devre dışı). Davet 1–2 sn sonra kabul edilir; takımdan çıkarılabilir.
- Eşleşme aralığı ±1 lig, 30 sn sonra ±2. Arama: süre, bulunan rakip sayısı, koltuklar tek tek dolar, İptal.
- Maç bulundu: 4 oyuncu, lig, takım (Duo), 10 sn kabul sayacı; herkes kabul edince "Masa hazırlanıyor" › yükleme › masa. Reddetme veya süre bitimi: aramaya dönülür.
- Kurallar: 101 · 5 el · 30 sn · katlamasız, bahis yok.
- Maç sonu: sıra, ±LP, LP çubuğu animasyonu; kademe/lig atlamada tam ekran kutlama; düşmede sade not; koruma devredeyse yeşil not. "Tekrar ara". Maçtan ayrılmak = 4. sıra.
- İlerleme: lig yolu (6 lig, III/II/I, buradasın), sezon ödülleri (çip + çerçeve, ulaşıldı/kazanacağın/kilitli), son maçlar.
- Veri: hesap başına kaydedilir {idx, lp, prot, peak, games, top, hist}. Başlangıç Altın II 72 LP (tasarım verisi). Profil kartı ve menü rozetleri bu veriden okunur.

## LP
- Her kademe 100 LP. Ekranda kademe içi "72 / 100 LP"; sunucu toplam LP tutar.
- Maç sonu (4 kişi): Bronz–Altın +30 / +10 / −10 / −20 · Elmas–Usta (ve Galaksi) +25 / +5 / −12 / −22.
- 100'e ulaşınca üst kademeye geçilir, artan LP taşınır. 0'ın altına inince alt kademeye düşülür, LP = 100 + kalan.
- Usta I'de 100'e ulaşan, ilk 100 içindeyse Galaksi'ye çıkar.

## Koruma ve eşleştirme
- Bir lige yeni çıkan oyuncu 3 maç boyunca alt lige düşmez (LP 0'da durur). Lig içi kademe düşüşü serbesttir.
- Bronz III'ün altı yok; LP 0'ın altına inmez.
- Eşleştirme en fazla ±1 lig; 30 sn bekleme sonrası ±2.

## Sezon
- 8 hafta. Sonunda 4 kademe aşağı inilir; taban Gümüş III (Bronz'daki oyuncu yerinde kalır), LP 0.
- Ödül sezonun en yüksek ligine göre: çip (Bronz 5.000 · Gümüş 15.000 · Altın 40.000 · Elmas 100.000 · Usta 250.000 · Galaksi 1.000.000 — önerilen değerler), o lige özel avatar çerçevesi, profilde sezon rozeti. Profil kartındaki "Sezon en iyi" bu değere bağlanır.

## Ekranlar
1. **Lig atlama:** karartılmış arka plan, dönen ışın, 160 px amblem pop animasyonu, "LİG ATLADIN", lig adı, koruma notu, Devam.
2. **Kademe atlama:** aynı yapı, ışın %45 opaklık, "Altın II → Altın I".
3. **Düşme bildirimi:** üstte sade toast, 24 px amblem, "Gümüş ligine düştün · Altın III → Gümüş I". 2,6 sn.
4. **Galaksi'ye çıkış:** "ZİRVEYE ULAŞTIN", "Galaksi · #87".
5. **Sezon sonu:** en iyi lig amblemi, üç ödül kartı (çip, çerçeve, rozet), yeni başlangıç kademesi, "Ödülleri al".

## Oyuna bağlanan yerler
- Profil kartı: elmas kutu yerine 52 px amblem (60 px kutuda), "72 / 100 LP", "Altın I'e 28 LP".
- Liderlik tablosu satırları: 24 px amblem + lig adı.

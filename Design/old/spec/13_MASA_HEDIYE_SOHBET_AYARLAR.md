# 13 · Oyun Masası — İkram/Hediye, Sohbet, Oyuncu Kartı, Ayarlar

Metinler: `spec/data/table_strings_tr_en.csv`. Katalog ve varsayılanlar: `spec/data/table_content.json`.

## 1. İkram ve hediye kataloğu (fiyat çip)
| Anahtar | TR | EN | Sekme | Fiyat | Model |
|---|---|---|---|---|---|
| tea | Çay | Tea | İkram | 100 | ince belli modern bardak, altın yaldız ve bel, kalın dip; kızıl-kehribar çay; tabak, altın kaşık, 2 kesme şeker; buhar |
| coffee | Türk kahvesi | Turkish coffee | İkram | 150 | İznik desenli fincan, köpük dokusu, tabak, su bardağı, 2 lokum; buhar |
| cola | Soğuk içecek | Cold drink | İkram | 120 | buzlu bardak, maun tonlu kola, köpük, 3 buz, kabarcık, çizgili pipet, limon dilimi, nane, mantar altlık |
| simit | Simit | Simit | İkram | 200 | iki burgulu hamur, 240 susam, ahşap altlık |
| baklava | Baklava | Baklava | İkram | 500 | bakır tepsi, 9 baklava dilimi (katman dokusu, şerbet, fıstık) |
| seeds | Çekirdek | Seeds | İkram | 80 | gazete kâğıdı külah, dökülen çekirdek ve kabuklar |
| flower | Çiçek | Flowers | Hediye | 1.000 | seramik vazo, 5 lale (kırmızı, sarı, pembe, mor, turuncu) |
| cake | Pasta | Cake | Hediye | 2.500 | ayaklı tabak, pembe pasta, krema damlaları, 6 çilek, mum + alev |
| trophy | Kupa | Trophy | Hediye | 5.000 | altın kupa, mermer kaide, "101" plaka, yıldız |
| rocket | Roket | Rocket | Hediye | 10.000 | beyaz gövde, kırmızı burun, 4 kanat, cam pencere, motor alevi |
Emoji (ücretsiz): 😂 😍 😎 😡 👏 🔥 🤔 🙏 😱

Panel görselleri, aynı 3D modellerden 220×220 şeffaf PNG olarak (kamera 30° FOV, yukarıdan çapraz) üretilir. Unity'de: model başına bir kez RenderTexture → PNG (editör aracı) ya da hazır sprite.

## 2. İkram paneli — T08–T10
Sağda 396 px çekmece (top 74, bottom 16). Başlık + kapat. **KİME**: 4 kutu (Zeynep, Ayşe, Burak, "Herkes ×3" — fiyat 3 katı). Sekmeler: İkram · Hediye · Emoji (seçili sekme tema degradesi). 3 sütunlu kartlar: görsel, ad, fiyat (coin). Seçili kart `inset 0 0 0 2px --a1` + parıltı. Alt bar: BAKİYE + buton: seçim yoksa "Bir şey seç", yetersizse "Yetersiz çip", uygunsa yeşil "Gönder · 3.000". Gönderince panel kapanır, 120 ms sonra uçuş başlar, bakiye düşer, sohbete sistem satırı ("Sen, Zeynep için roket gönderdin"), 1.9 sn (roket 6.3 sn) sonra alıcı teşekkür balonu.

Açma yolları: sol alttaki hediye butonu (son seçilen alıcı, varsayılan Ayşe), oyuncu kartında "Tüm hediyeler" (o oyuncu seçili).

## 3. İkram bildirimi — T06
Bana gelen ikramda, inişten sonra (1.4 sn; roket 5.8 sn) sağ üstte kart: model görseli, "SANA İKRAM VAR", "Ayşe sana çay ısmarladı", **Teşekkür et** (tıklanınca benden "Eyvallah, eline sağlık!" balonu + gönderene 🙏 emoji uçar). 5.2 sn sonra kapanır. Gelen ikramlar el sonu tablosunda listelenir. "İkram bildirimleri" kapalıysa kart gösterilmez, liste yine tutulur.

Fiil kuralı: İkram sekmesi "ısmarladı / ısmarladın", Hediye sekmesi "gönderdi / gönderdin". Başkaları arası: "{A}, {B} için {ikram} ısmarladı"; herkese: "{A} herkese {ikram} ısmarladı".

## 4. Oyuncu kartı — T07
Rakip avatarına tıklayınca avatarın yanında 290 px kart (sağdaki oyuncu için solda, soldaki için sağda, üstteki için altta): avatar, ad, "Elmas III · SV 31", 3 istatistik (SEVİYE, KAZANMA %, EL), HIZLI İKRAM (çay, kahve, simit, çiçek — tek tıkla gönderir; yetersizse soluk), **Tüm hediyeler**, **Arkadaş ekle** (→ "İstek gönderildi").

## 5. Sohbet — T13
Solda 360 px panel (top 74, bottom 118). Mesajlar: rakip solda (renkli avatar, ad, koyu balon), ben sağda (tema degradesi balon), sistem satırları (ikram görseli + `--a1` metin). Hızlı cümleler: Kolay gelsin · Hadi bakalım · Güzel hamle! · Şanslısın · Çaylar benden! · Bir el daha?. Metin kutusu (en çok 80 karakter, Enter gönderir). Gönderilen mesaj oyuncunun yanında balon olarak da çıkar (2.8 sn). Panel kapalıyken gelen mesajlar sohbet butonunda kırmızı sayaçla gösterilir. Demo: mesajıma %60 olasılıkla 1.4 sn sonra bir bot cevap verir.

## 6. Masa ayarları — T15–T18
Sağ üstteki dişli. 920×600 pencere, solda 4 sekme (ana menü ayarlarıyla aynı görünüm), altta "Varsayılana dön" + **Tamam**. Tüm değişiklikler anında uygulanır.

| Sekme | Ayar | Tür | Varsayılan | Etki |
|---|---|---|---|---|
| Oyun | Otomatik yerleştir | açık/kapalı | açık | tıklamayla çekilen taş uyduğu taşın yanına |
| | Perleri vurgula | açık/kapalı | açık | geçerli per yeşil çizgisi |
| | Çift tıkla at | açık/kapalı | açık | çift tık ile atma |
| | Tur süresi uyarısı | 3 sn / 5 sn / 10 sn / Kapalı | 5 sn | halka kırmızı + "Süren bitiyor!" |
| | Dil | Türkçe / English | Türkçe | tüm masa metinleri |
| Ses | Ana ses, Masa müziği, Efektler | kaydırıcı 0–100 | 80, 55, 90 | ses karışımı |
| | Taş sesleri, Mesaj sesi | açık/kapalı | açık | — |
| Görünüm | Görüş açısı | 2 kart | Oyuncu gözü | kamera 1.3 sn geçiş |
| | Masa teması | 4 kart | İstasyon | anında malzeme değişimi |
| | Istaka teması | 6 kart (3 sütun) | Klasik ceviz | anında model değişimi |
| | Taş görünümü | 5 kart (5 sütun) | Klasik | anında gövde + yüz değişimi |
| Bildirimler | Sohbet balonları | açık/kapalı | açık | oyuncu yanındaki balonlar |
| | İkram bildirimleri | açık/kapalı | açık | sağ üst kart |
| | Emoji efektleri | açık/kapalı | açık | uçan emojiler |
| | Sıra ipuçları | açık/kapalı | açık | üst toast + "BURAYA AT" |

Kart önizlemeleri: görüş açısı için küçük çizimler; masa için mini masa (kenar degradesi + çuha + çizgi); ıstaka için mini ıstaka (arka + raf rengi + 5 örnek taş). Değerler `table_content.json → skinPreviewCss`. Seçili kartta sağ üstte "SEÇİLİ" etiketi. Ayarlar kalıcıdır (PlayerPrefs).

Prototipte ses yoktur; ses ayarları yalnızca durumu tutar.


## Ayarlar → Görünüm → TAŞ GÖRÜNÜMÜ

Istaka temasının altında 5 kart: Klasik, Galaksi, Altın, Neon, Mermer. Seçim anında tüm taşlara uygulanır (gövde malzemesi + yüz dokuları yenilenir). Varsayılan Klasik; "Varsayılana dön" sıfırlar. Mağazada satın alınmamış setler üretimde kilitli gösterilmeli.

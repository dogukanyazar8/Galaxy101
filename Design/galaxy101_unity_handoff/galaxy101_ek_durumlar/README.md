# Galaxy 101 · Oyun Masası Ek Durumları (T28–T51)

Mevcut Unity masasına (T01–T27, `galaxy101_unity_handoff`) eklenecek durumlar: bot masası başlığı, okey gösterimi, cezalar, yerden taş alma, kural/sistem mesajları, el sonu tablosu v2, maç sonu, okeyi masadan alma, yükleme ekranı.

## İçerik
```
README.md                          ← bu dosya
PROMPT_CLAUDE_CODE.md              ← Claude Code'a verilecek görev metni
spec/17_MASA_EK_DURUMLAR.md        ← tam spesifikasyon: ölçü, renk, süre, kural, kabul listesi
spec/data/table_strings_ek_tr_en.csv ← yeni TR/EN metinler (anahtar,tr,en)
spec/data/table_strings_tr_en.csv  ← mevcut masa metinleri (referans)
spec/data/themes.json              ← tema renk değişkenleri (Satürn, Mars, …)
screenshots/                       ← T28–T51, her biri _saturn + _mars; _EN_ örnekleri
screenshots/_baz_T05…              ← tüm karelerin üstüne kurulduğu temel masa düzeni
screenshots/_eski_T24, _eski_T25   ← yerini alan eski ekranlar (karşılaştırma için)
prototip/Galaxy101_Masa_EkDurumlar.dc.html + support.js ← canlı tasarım kaynağı
```

## Prototipi açmak
`prototip/` klasörünü bir yerel sunucuyla açın (ör. `npx serve prototip` ya da `python -m http.server`), `Galaxy101_Masa_EkDurumlar.dc.html` sayfasına gidin. İnternet gerekir (Google Fonts: Montserrat, Baloo 2).
- Sayfada tüm durumlar Satürn ve Mars yan yana.
- Tek kare tam ekran: tarayıcı konsolunda `__g101.solo('T34','mars')`, İngilizce `__g101.solo('T34','sat','en')`, çıkış `__g101.solo(null)`.
- Her ölçü, renk ve metin HTML içinde satır içi stil olarak durur; spec'te eksik bir değer olursa kaynak burasıdır.

## Durum listesi
| No | Dosya | İçerik |
|---|---|---|
| T28 | bot_baslik_xp | Bot masası: ANTRENMAN, "Çipsiz · XP", el sayacı, XP/seviye hapı, BOT etiketleri |
| T30 | bot_masadan_kalk | Masadan kalk, bot varyantı (T25 yerine) |
| T33 | okey_rozet_masada | Okey otomatik en yüksek değeri alır; masada temsil rozeti + ipucu |
| T34 | ceza_attigin_tasla_acti | Attığın 9 ile sonraki oyuncu açtı: +90 |
| T35 | ceza_eksik_acilis | Eksik açılış 88/101: +101, açılış geri alınır |
| T36 | ceza_rozet_bildirim | Okeyin alındı +101 (toast), rakip işlek taş balonu, +202 rozeti |
| T38 | yerden_alindi | Alınan taşta YERDEN, halkada GERİ BIRAK |
| T39 | geri_birakma | Alınan taş yığına geri bırakılıyor |
| T40 | mesaj_katalogu | 6 mesaj türü, süreler, tüm TR/EN metinler, kuyruk kuralları |
| T41 | mesaj_kuyrugu | Ceza önde, süre uyarısı arkada (+1), rakip balonu |
| T42 | el_sonu_v2_bot | El sonu v2 (T24 yerine), bot: ×2, nedenli ceza, XP |
| T43 | el_sonu_v2_cevrimici | El sonu v2, çevrimiçi: ×8, çip |
| T44 | el_sonu_puansiz | Çiftle bitti: el puanlanmadı |
| T45 | mac_sonu_kazandin | Maç sonu: kupa, konfeti, XP dökümü, seviye atlama |
| T46 | mac_sonu_ikinci | Maç sonu: 2. sıra |
| T47 | okey_al_surukle | Okeyi alma: gerçek taş sürükleniyor, hedef okeyler parlıyor |
| T48 | okey_al_ipucu | Okeye uzun basma ipucu |
| T49 | okey_al_gecersiz | Yanlış taşla deneme |
| T50 | yukleme | Menü → masa yükleme ekranı |
| T51 | masa_acilamadi | Bağlantı hatası |

Boş numaralar (T29, T31, T32, T37) bilerek kaldırılan durumlardır: soluk çip başlığı, okey seçici (2 ekran), alınamaz yer taşı.

## Kesinleşen kurallar (özet — ayrıntı spec/17)
- Bot maçı çevrimdışı, yalnızca XP; çip/bahis metni görünmez.
- Okey seçimi yok: okey konduğu yerdeki en yüksek değeri otomatik alır; eşitlikte motor ilk uygun rengi seçer.
- Ceza onay kartı yok. Cezalar: işlek taş atmak +101 · okey atmak +101 · eksik açılış (seri <101, çift <5) +101 · attığın taşla sonraki oyuncu açarsa taş değeri ×10 · masaya açtığın okeyi başka oyuncu alırsa +101.
- Yerden taş her zaman alınabilir; işe yaramazsa aynı tur geri bırakılır, desteden çekilir; sonraki oyuncuya atılamaz.
- Maç: lobide 3 / 5 / 7 el (varsayılan 5); en düşük toplam kazanır.

## Açık sorular (ürün kararı bekliyor)
1. Atılan okey sonraki oyuncuyu açtırırsa: yalnız +101 mi, yoksa +101 ve ayrıca ×10 mu?
2. "Çiftle ×2" çarpanı ile "çiftle bitti · el puanlanmadı" (T44) birlikte mi geçerli? Kural motoruna göre biri seçilmeli.
3. XP değerleri (+340 / +60, sıra bonusu 500/200/80/0) öneridir; ekonomi tablosuyla teyit edilmeli.

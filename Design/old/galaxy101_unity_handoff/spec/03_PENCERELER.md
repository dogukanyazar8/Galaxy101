# 03 — Pencereler ve Alt Pencereler

Tüm pencereler 1a'nın **üstünde**, aynı ekran içinde açılır. Metinlerin tamamı `data/strings_tr_en.csv` ve HTML'de; içerik verileri `data/content.json`'da.

## 0. Ortak pencere kabuğu

**Katman**: tam ekran, z 20. Arka plan `rgba(0,0,0,.55)` + bulanıklık 6 px. Arka plana tıklamak pencereyi kapatır.

**Pencere** (Mağaza, Arkadaşlar, Liderlik, Günlük ödül, Kurallar, Profilim): 980×610, ortalı, köşe 28, zemin `pn`, 1 px `rgba(255,255,255,.1)` kenar, gölge `0 10px 0 pnS, 0 40px 90px rgba(0,0,0,.6)`, overflow gizli, dikey flex.

**Açılış animasyonu**: katman opaklık 0→1 .2 sn ease-out; pencere `opacity 0, translateY 14, scale .96 → 1` .28 sn `cubic-bezier(.2,1.2,.4,1)` (hafif yaylanma).

**Başlık** (padding 20/24/16, gap 14, dikey ortalı):
- İkon karesi 50×50 köşe 16, pencereye özel degrade + 4 px alt gölge + `inset 0 1px 0 rgba(255,255,255,.4)`, ikon 28.
- Başlık Montserrat 700 26 / 1.15; alt başlık Baloo 700 13 `mt`, üstten 2.
- Boşluk.
- Sekmeler (varsa): grup zemin `dp`, padding 4, köşe 14, gap 4. Sekme padding 9/16, köşe 10, Montserrat 700 15, tek satır. Aktif: `linear-gradient(a1,a2)`, metin `aT`, `0 3px 0 aS`. Pasif: şeffaf, `mt`; hover metin `#F4F1E8`.
- Kapat 42×42 köşe 13 `pnH`, `0 4px 0 pnS`, çarpı 18 stroke `ic` 2.8; hover parlaklık 1.25, basılı translateY 3.

**Gövde**: padding 0/24/24, kalan yüksekliği doldurur.

**Kapanma**: Kapat butonu, arka plana tıklama, **Esc**. Alt pencere açıksa Esc önce alt pencereyi kapatır.

**Alt pencere kabuğu** (z 30): arka plan `rgba(0,0,0,.5)` + bulanıklık 4; kart köşe 26, aynı zemin/kenar/gölge; animasyon .26 sn aynı eğri. Genişlikler: Profili düzenle 720, Onay 480, Arkadaş ekle 560, Paylaş 640, İşlem tamam 420.

**Ortak butonlar**
| Tür | Zemin | Alt gölge | Metin |
|---|---|---|---|
| Birincil | `linear-gradient(a1,a2)` | `aS` | `aT` |
| İkincil | `pnH` | `pnS` | `#F4F1E8` |
| Başarı | `#3CCB6E` | `#22914A` | beyaz |
| Tehlike | `#FF5E6C` | `#C23E4A` | beyaz |
| Pasif (devre dışı) | `dp` | yok | `mt`, imleç not-allowed |

Basılı: translateY 2–3, gölge küçülür. Hover: parlaklık 1.08–1.2.

---

## 1. Ayarlar (dişli butonu)

Görüntüler 10–14. Boyut **900×560**, ızgara `230px | 1fr`.

**Sol menü** (zemin `sd`, padding 22/14, sağ kenar 1 px `rgba(255,255,255,.06)`):
- Başlık: dişli 26 (`a1`) + "Ayarlar" Montserrat 700 26.
- 5 sekme: Oyun, Ses, Görüntü, Dil, Hesap. Satır padding 12/14, köşe 14, gap 12; 8×8 45° dönük kare (aktif `a1`, pasif `bd`); ad Montserrat 700 17. Aktif: zemin `pnH`, sol iç kenarda 4 px `a1` çubuk, metin `#F4F1E8`. Pasif `mt`, hover `pnH`.
- Altta: "Galaxy 101 · v0.2.0" / "© CDX Games" Baloo 700 11 `mt` %70.

**Sağ içerik**:
- Başlık bloğu padding 22/28/14: sekme adı Montserrat 700 24 + açıklaması Baloo 700 13 `mt`; sağda Kapat (42).
- Liste: kaydırılabilir, scrollbar yeri sabit, padding 0/20/0/28.
- Satır: padding 10/0, alt kenar 1 px `rgba(255,255,255,.07)`, gap 16; sol: ad Montserrat 700 17 + açıklama Baloo 700 13 `mt`; sağ: kontrol.
- Alt çubuk (padding 16/28, üst kenar çizgisi, zemin `rgba(0,0,0,.12)`): "Varsayılana dön" (Baloo 700 14 `mt`, hover beyaz) · boşluk · "Vazgeç" (ikincil) · "Kaydet" (birincil, padding 12/28).

**Kontroller**
- **Toggle**: 56×32 köşe 16; açık `#3CCB6E`, kapalı `dp`; iç gölge; topuz 26×26 beyaz, `0 2px 0 rgba(0,0,0,.25)`, sol 3 ↔ 27, geçiş .22 sn `cubic-bezier(.3,1.5,.5,1)`.
- **Slider**: tıklanabilir alan 270×34 (yatay padding 12); ray 12 yükseklik köşe 6 `dp` + iç gölge; dolgu `linear-gradient(90deg,a2,a1)` + `0 0 12px gl`; topuz 26×26 beyaz radyal (`#fff → #E8EAF2`), ortasında 8×8 `a1→a2` nokta. **Tıklayarak ve sürükleyerek** ayarlanır; değer, rayın **iç genişliğine** göre hesaplanır (topuz imlecin tam altında kalır). Sürüklerken: topuz scale **1.06**, hale `0 0 0 4px gl`; dolgu ve topuz **aynı karede, geçişsiz** hareket eder. Tıklamada .15 sn ease-out geçiş. Klavye: ← ↓ azalt, → ↑ artır. Sağda değer kutusu min 58, `dp`, köşe 10, Montserrat 700 15 tabular; değer minimumdaysa renk `mt`.
- **Segment**: sekme grubuyla aynı stil, seçenek padding 8/14, Montserrat 700 14.

**Sekme içerikleri** (varsayılanlar `content.json → settingsDefaults`)
| Sekme | Satırlar |
|---|---|
| Oyun | Otomatik sırala (açık) · Perleri vurgula (açık) · Çift tıkla taş at (kapalı) · Masa sohbeti (açık) · Tur süresi uyarısı slider 1–10 sn (5) · Taş boyutu segment Küçük/Orta/Büyük (Orta) |
| Ses | Ana ses 0–100 % (80) · Müzik (60) · Efektler (90) · Taş sesleri (açık) · Bildirim sesi (açık) |
| Görüntü | Arka plan hareketi (açık; kapatınca gezegen ve yıldızlar durur) · Çözünürlük 720p/1080p/2K · Efekt kalitesi Düşük/Orta/Yüksek (Yüksek) · Gezegen teması: 5 karo 62 genişlik (30×30 küre + ad Baloo 700 12), seçili `pnH` |
| Dil | 2 kart yan yana (gap 14): padding 18, köşe 20, `sd`; 44×44 kod kutusu ("TR"/"EN" `a1`) + ad (Türkçe / English) Montserrat 700 18 + alt yazı (Türkiye / Uluslararası); sağda 24×24 radyo. Seçili: `inset 0 0 0 2px a1`. **Seçim anında tüm arayüzü değiştirir.** |
| Hesap | Profil kartı (58 avatar + çerçeve, ad, "Oyuncu ID · 4821 9930", "Profili düzenle" ikincil) · Google kartı ("G" beyaz kare, e-posta maskeli `dogukan•••@gmail.com`, "Bağlı" yeşil nokta) · Arkadaşlık istekleri toggle · Masa davetleri toggle · "Çıkış yap" (tehlike) + "Hesabı sil" (metin link, hover `#FF8A94`) |

"Kaydet" ayarları kalıcı yazar ve kapatır; "Vazgeç" kapatır; "Varsayılana dön" tüm değerleri varsayılana alır.

---

## 2. Mağaza

Görüntüler 30–32. İkon karesi `a1→a2`. Sekmeler: **Çip · Elmas · Görünüm**. Çip/elmas "+" butonu ilgili sekmeyle açar.

- **Fırsat bandı** (yükseklik 96, köşe 22, `linear-gradient(110deg,a2,a1)`, `0 5px 0 aS`, metin `aT`, parlama bandı 70 px 3.6 sn): coin yığını ikonu (2 coin + elmas) · "HAFTA SONU FIRSATI · %50" Baloo 900 11 harf aralığı 1.5 · "1.000.000 çip + 50 elmas" Montserrat 700 26 · boşluk · saat ikonu + **canlı geri sayım** gece yarısına (HH:MM:SS, `rgba(0,0,0,.16)` kutu) · üstü çizili "₺199,99" + "₺99,99" Montserrat 700 26 · "Satın al" (zemin `aT`, metin `a1`).
- **4 sütun ürün ızgarası** (gap 14). Kart: köşe 22, `sd`, padding 22/14/14, hover translateY -5 + `pnH`. "EN POPÜLER" kartı: `inset 0 0 0 2px a1, 0 0 24px gl` + üst ortada sekme etiketi. Bonus rozeti sağ üst `#3CCB6E` ("+%10"). Görsel alanı 104 yükseklik; miktar Montserrat 700 22; alt yazı; en altta tam genişlik birincil fiyat butonu.

| Sekme | Ürünler (miktar · bonus · fiyat) |
|---|---|
| Çip | 50.000 · — · ₺19,99 / 250.000 · +%10 · ₺79,99 / **1.000.000 · +%25 · ₺249,99 (EN POPÜLER)** / 5.000.000 · +%50 · ₺899,99 |
| Elmas | 100 · — · ₺29,99 / 550 · +%10 · ₺139,99 / **1.200 · +%20 · ₺279,99** / 2.500 · +%25 · ₺549,99 |
| Görünüm (taş görünümü, elmasla) | **Galaksi taşı 800** / Altın taş 1.200 / Neon taş 600 / Mermer taş 400 — her biri kendi yüzey/kenar/rakam rengiyle "7" taşı önizlemesi |

Görsel ölçeği küçükten büyüğe .8 / .9 / 1 / 1.12.

---

## 3. Arkadaşlar

Görüntüler 40–47. İkon `#7AA6FF→#3D6BF0`. Sekmeler: **Çevrimiçi · 3 · Tümü · 6 · İstekler · 2** (sayılar dinamik).

- **Araç satırı** (gap 12): arama kutusu (esnek; 48 yükseklik, `dp`, köşe 14; büyüteç + gerçek metin girişi Baloo 700 15; odakta `inset 0 0 0 2px a1, 0 0 16px gl`; doluysa sağda temizle ×) · "Kodun DY-4821" kutusu (1 px kesikli `bd`, kod Montserrat 700 17 `a1`, kopyala ikonu) · "Arkadaş ekle" birincil.
- **Arama**: yazdıkça açık sekmeyi filtreler; **ad** (tr-TR küçük harf, içerir) veya **kod** (`#`, boşluk ve `-` yok sayılır; "ay2187", "#AY-2187" eşleşir). Sonuç yoksa: `"<sorgu>" ile eşleşen arkadaş yok`.
- **Liste** (kaydırılabilir, scrollbar yeri sabit, gap 8). Satır: padding 12/16, köşe 18, `sd`, hover `pnH`; 48 avatar (çevrimdışı opaklık .55) + 16×16 durum noktası (çevrimiçi `#3CCB6E`, değil `#5C6680`, 3 px `sd` kenar); ad Montserrat 700 17 + seviye etiketi ("SV 28", `dp` zemin `a1` metin); durum Baloo 700 13 (renkli); sağda aksiyon butonları — genişlikleri sabit, **scrollbar çıksa bile hizaları kaymaz**.
- Aksiyonlar: Davet et (birincil → tıklanınca "Gönderildi" ikincil) · İzle (ikincil) · Mesaj (ikincil) · İstekler: Kabul et (başarı) + Reddet (ikincil) → satır listeden kalkar.
- Boş durum: ortalı Baloo 700 15 `mt`.

### 3.1 Arkadaş ekle (alt pencere, 560)
Başlık ikonu 44 mavi + "Arkadaş ekle" / "Arkadaş kodu veya oyuncu adıyla ara". Giriş (50 yükseklik, "#" öneki, büyük harf, placeholder "Örn. MK-3321") + "Ara" birincil; **Enter** de arar. Sonuç alanı 96 yükseklik:
- Boş: "Kod ya da ad girip Ara'ya bas."
- Bulundu: 52 avatar + ad + seviye + `#KOD` + "İstek gönder" (birincil) → "İstek gönderildi" (ikincil, yeşil metin).
- Bulunamadı (kırmızı kenarlı giriş): "Oyuncu bulunamadı. Kodu kontrol edip tekrar dene."
- Zaten arkadaş: "Bu oyuncuyla zaten arkadaşsınız."
Altta kesikli kutuda kendi kodun + "Kopyala" (→ "Kopyalandı" 1.5 sn). Arama dizini `content.json → searchDirectory`; "MK3321" yazımı "MK-3321" olarak normalize edilir.

---

## 4. Liderlik

Görüntüler 50–52. İkon `#C2A4FF→#7C4DEB`. Sekmeler **Haftalık · Sezon · Arkadaşlar**. Izgara `370px | 1fr`, gap 18.

- **Podyum** (sol; köşe 22, `radial-gradient(ellipse at 50% 35%, pnH, sd 72%)`): sıra 2-1-3. Her sütun 104 genişlik: taç (sadece 1.), avatar (1.: 66, diğer 54) 3 px madalya renkli kenar + glow, ad Montserrat 700 15, lig elması + puan, basamak (yükseklik 150/112/86, köşe 14 üst, madalya degradesi, büyük sıra rakamı `rgba(0,0,0,.32)` Montserrat 700 42). Madalya: altın `#FFE58A→#E8A317`, gümüş `#EEF2F8→#A9B4C6`, bronz `#F4C08C→#B8703A`.
- **Tablo** (sağ): başlık SIRA / OYUNCU / LİG / PUAN (Baloo 900 11 `mt`), sütunlar `48 | 1fr | 120 | 90`. Satırlar 4–10: padding 8/16, köşe 14, `sd`; 32 avatar; lig elması (elmas lig `#7FE3FF`, altın `#FFC94A`).
- **Kendi satırın** (altta sabit): `linear-gradient(90deg,pnH,sd)`, `inset 0 0 0 2px a1, 0 0 20px gl`; sıra `a1`.
- Puanlar: Haftalık = sezon × 0.32; kendi sıran Haftalık 128, Sezon 1.204, Arkadaşlar 3 (arkadaş sekmesinde kendi satırın listede vurgulu).

---

## 5. Günlük ödül

Görüntüler 60–61. İkon `#4FDB80→#25A857`.

- **7 günlük seri** (ızgara `6×1fr + 1.5fr`, gap 10, kart 120 yükseklik köşe 18): üstte "1. Gün" … Baloo 800 11. Gün 1–4: toplandı (koyu örtü + 36 yeşil tik dairesi). **Gün 5 = bugün**: `linear-gradient(160deg,#4FDB80,#1FA35A)`, `0 5px 0 #146B3C, 0 0 26px rgba(60,203,110,.55)`, süzülür (`bob`), altta beyaz "Topla" butonu (metin `#146B3C`) → tıklanınca toplandı olur. Gün 6: opaklık .6. **Gün 7**: büyük kart, sandık çizimi, `inset 0 0 0 2px a1, 0 0 20px gl`, miktar `a1`. Miktarlar 1.000 / 2.000 / 3.000 / 4.000 / 5.000 / 7.500 / 15.000.
- **Günlük görevler** başlığı Montserrat 700 20 + "2/5 tamamlandı" rozeti + sağda saat ikonu "Yenilenmeye 11 sa 22 dk" (gece yarısına canlı).
- **Görev satırı** (padding 9/14, köşe 16, `sd`, gap 14): 36 numara dairesi (tamamlanınca yeşil tik) · ad Montserrat 700 15 + ilerleme "1/3" (devam `a1`, bitti `#8EF0B0`) + 8 yükseklik çubuk (devam `a2→a1`, bitti `#3CCB6E`) · ödül (coin + miktar, varsa elmas) 150 genişlik · durum 118 genişlik:
  - Devam ediyor: `dp` kutu, `mt` metin.
  - Tamamlandı, alınmadı: **"Al"** başarı butonu + `0 0 16px rgba(60,203,110,.45)` → tıklanınca "Alındı".
  - Alındı: yeşil tik + "Alındı", satır opaklığı .6.
- Görevler `content.json → dailyQuests`.

---

## 6. Kurallar

Görüntüler 70–75. İkon `#5BE6D6→#14A596`. Izgara `230px | 1fr`, gap 18.

- **Sol gezinme**: 6 bölüm (Taşlar ve okey, Dağıtım, Elini açmak, Taş işlemek, Bitirmek, Puanlama). Satır padding 11/14, köşe 14; 28×28 numara kutusu (aktif `a1→a2`, pasif `dp`); ad Montserrat 700 16; aktif `pnH` + 4 px sol `a1` çubuk.
- **İçerik** (köşe 22, `sd`, padding 24/28, kaydırılabilir): başlık Montserrat 700 28; maddeler (9×9 45° `a1` elmas + Baloo 700 16 / 1.55).
- **ÖRNEK kutusu** (varsa; `dp`, köşe 18): 46×62 taşlar (temanın taş renkleri `tf1/tf2/te/te2`), rakam Montserrat 700 28 + nokta; vurgulu taş `0 0 0 3px a1, 0 0 18px gl` + altında etiket (Gösterge / Okey / İşle / Son taş) Baloo 800 11 `a1`; perler arası 22 boşluk; altında açıklama Baloo 700 14 `mt`.
- Metinler `content.json → rules` (TR+EN).

---

## 7. Profilim (avatar)

Görüntüler 80–82. İkon `#FFA27A→#F0683A`. Sekmeler **Genel · Rozetler · Maç geçmişi**. Izgara `290px | 1fr`, gap 18.

**Sol sütun** (her sekmede; köşe 22, `radial-gradient(ellipse at 50% 20%, pnH, sd 70%)`, padding 22/18/18): 124 avatar + seçili çerçeve + "SEVİYE 24" rozeti · ad Montserrat 700 24 · durum mesajı (yoksa "Henüz durum mesajı yok") · "Seviye 25'e" + "6.240 / 9.000 XP" + 10 yükseklik XP çubuğu %69 · kesikli kutuda kod + "Kopyala" · altta "Profili düzenle" (birincil, kalem ikonu) + paylaş butonu 46 (ikincil) → **Profili paylaş**.

**Genel**:
- 4 istatistik kartı (köşe 18, `sd`): OYNANAN EL 1.284 (527 kazanıldı) · KAZANMA %41 (`#3CCB6E`, Bu ay +%3) · EN İYİ AÇILIŞ 186 (`a1`, puan) · OKEY BİTİŞ 73 (katlanan el). Değer Montserrat 700 28 tabular.
- Derece kartı: 52 altın elmas "II" · "SEZON 3 · DERECELİ" / "Altın II" · sağda "SEZON EN İYİ Altın I" · LP çubuğu %72 (`#F5A623→#FFE08A`) · "720 / 1000 LP" — "Altın I'e 280 LP".
- Modlara göre kazanma: Tekli %44, Eşli %38, Katlamalı %36, Dereceli %42 (mod renginde çubuk).
- ÇERÇEVELER: Klasik / Altın / Galaksi kartları (40 önizleme + ad + durum "Takılı" yeşil / "Sahipsin" / "Seviye 30'da açılır"). Tık → çerçeve anında takılır, ana ekran avatarı da değişir. Galaksi kilitli (opaklık .55, not-allowed); önizleme `conic-gradient(#3B2F7A,#8C6EFF,#3DF5E0,#3B2F7A)`.

**Rozetler**: 3 sütun, 9 rozet (`content` HTML'de `BD` dizisi). Kazanılanlar renkli 46 kare + glow; kazanılmayanlar `dp`, opaklık .75, ilerleme çubuğu + "4/7".

**Maç geçmişi**: sütunlar `44 | 1fr | 110 | 110 | 90` (— / MOD / SIRA / ÇİP / ZAMAN); 7 satır: 30 mod karesi, mod adı + detay ("101 · 5K masa" veya "+24 LP"), sıra rozeti (1. altın `rgba(255,201,74,.18)` zemin `#FFC94A`), çip farkı (+ `#8EF0B0`, − `#FF8A94`, 0 "—"), "12 dk önce" / "1 sa önce" / "Dün".

### 7.1 Profili paylaş (alt pencere, 640)
Görüntüler 83–84.
- **Profil kartı önizlemesi** (180 yükseklik, köşe 22, tema zemini `radial-gradient(ellipse 70% 120% at 85% 50%, bg1, bg2 55%, bg3)`, sağ üstte yarı saydam gezegen dairesi, birkaç yıldız): 104 avatar + çerçeve + seviye · ad Montserrat 700 26 · derece + "· Sezon 3" · 3 mini istatistik (EL 1.284 / KAZANMA %41 / EN İYİ 186) · sağ altta GALAXY 101 logosu.
- **Profil bağlantısı**: `galaxy101.app/p/DY-4821` (zincir ikonlu `dp` kutu, taşan metin …) + "Bağlantıyı kopyala" 180 genişlik → yeşil "Kopyalandı" + tik 1.8 sn.
- **Şununla paylaş**: 5 karo (WhatsApp `#25D366`, Telegram `#2AABEE`, X `#2A2F3A`, Instagram degrade `#F9A13A→#E1306C→#8A3AB9`, Diğer `pnH`); karo hover translateY -4. Tık → platform paylaşım akışı (prototipte "… açılıyor" bildirimi). Unity: Android/iOS native share; Windows'ta bağlantı kopyalanır + tarayıcı.
- **Davet ödülü** satırı: "Arkadaşını davet et, çip kazan" + açıklama + "+2.000" coin rozeti.

---

## 8. Profili düzenle (alt pencere, 720)

Görüntüler 15–17. Ayarlar → Hesap → "Profili düzenle" veya Profilim → "Profili düzenle".
- Sol (220, `sd`): 110 avatar **canlı önizleme** (seçili çerçeve + renk + baş harfler) · "AVATAR RENGİ" · 8 renk 28×28 (4 sütun); seçili `0 0 0 3px sd, 0 0 0 5px #fff`.
- Sağ: **Oyuncu adı** (sayaç "10/16"; 48 giriş; geçersizse kırmızı kenar + kırmızı ipucu) · **Durum mesajı** (en fazla 40) · **Avatar çerçevesi** segment: Klasik / Altın / "Galaksi · SV 30" (kilit ikonlu, seçilemez).
- Alt: "Adını 30 günde bir değiştirebilirsin." · Vazgeç · Kaydet (ad geçersizse pasif).
- **Ad kuralı**: kırpılmış 3–16 karakter; harf (Türkçe dahil), rakam, boşluk, nokta, alt çizgi. Hata metni: "3–16 karakter: harf, rakam, boşluk, nokta veya alt çizgi."
- Kaydet → ana ekran avatarı/adı, Ayarlar hesap kartı, Profilim güncellenir. Baş harfler: kelimelerin ilk harfleri, en fazla 2, tr-TR büyük harf.

## 9. Çıkış yap / Hesabı sil (alt pencere, 480)

Görüntüler 19–22.
- Ortalı, padding 30/30/24. 72×72 ikon karesi (çıkış: `pnH→pn`; silme: `#FF7A86→#E23B4C` + kırmızı glow) · başlık Montserrat 700 24 · açıklama Baloo 700 14 / 1.55 `mt`.
- **Çıkış**: "Çıkış yapmak istiyor musun?" — "Çiplerin, ilerlemen ve arkadaşların Google hesabında saklanır…" · Vazgeç + **Çıkış yap** (tehlike).
- **Hesap sil**: "Hesabını silmek istiyor musun?" — "Hesabın 30 gün sonra kalıcı olarak silinir…" · kayıp listesi (kırmızı kutu `rgba(255,94,108,.1)` + kenar: 125.400 çip ve 340 elmas · Seviye 24, Altın II derecesi ve rozetler · Arkadaş listesi ve maç geçmişi) · "Onaylamak için SİL yaz" girişi (EN "DELETE") · **Hesabı sil** butonu yalnızca doğru yazılınca aktif.
- Onaydan sonra **İşlem tamam** (420): 64 yeşil tik dairesi · "Çıkış yapıldı" / "Silme talebin alındı" · alt metin · "Tamam".

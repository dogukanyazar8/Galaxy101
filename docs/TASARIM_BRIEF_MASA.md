# Claude Design brief'i — 3D masa, eksik ekran ve durumlar

Bu brief, `Design/galaxy101_unity_handoff` teslimindeki masa tasarımlarının (T01–T27) **kapsamadığı** durumları listeler. Bu durumlar, prototipin basitleştirdiği ama gerçek 101 kural motorunun (`com.okey101.core`) ürettiği durumlardır.

## Ortak kurallar (her ekran için)

- Tuval: 1280×720, mevcut masa sahnesinin üstünde (T05 düzeni). Üst bar, oyuncu paneli, aksiyonlar ve çapalı avatarlar yerinde kalır.
- Tema: 5 gezegen teması, mevcut token'lar (`--a1 --a2 --aS --aT --pn --pnH --pnS --dp --sd --mt --ic --gl --tf1 --tf2 --te`). Her ekran en az Satürn ve Mars ile gösterilsin.
- Yazı tipleri: Montserrat (başlık 700/800, logo 800), Baloo 2 (gövde 700/800).
- Bileşenler: mevcut kart (`--pn`, 22 px köşe, `0 7px 0 --pnS` gölge), birincil düğme (a1→a2 degrade), tehlike düğmesi (`#FF7A86→#E8455A`), toast (999 px hap), `modalIn` / `toastIn` animasyonları.
- Diller: her metin TR + EN.
- Teslim: HTML prototip + ekran görüntüleri + kısa spec (mevcut `screenshots/table/T*` ve `spec/*` biçiminde). Yeni görüntüler T28'den başlasın.

## Ürün kararları (tasarıma girdi)

- **Bot maçı çevrimdışıdır ve yalnızca XP verir.** Çip kazanılmaz, kaybedilmez, bahis yoktur.
- Kazanma ve kaybetme XP miktarları ile maç uzunluğu açık. Prototipte el sonunda +340 / +60 XP gösteriliyor; maç uzunluğu için öneri ve seçenek beklenir (bkz. 7).

---

## 1. Bot masası başlığı (antrenman modu)

**Neden:** Üst bardaki "Tekli · 101 • Bahis 5.000" bot maçında yanlış bilgi veriyor.

**İçerik:**
- Oda hapı: "Botlara karşı · 101" + küçük "ANTRENMAN" etiketi (EN: "vs Bots · 101", "PRACTICE").
- Bahis yerine "Çipsiz · XP" ya da benzer bir ifade.
- Çip bakiyesi gösterilsin mi, yoksa XP/seviye çubuğu mu? İkisinin de önerisi.
- Masadan kalk kartı (T25) bot maçı varyantı: "El bitmeden kalkarsan bu elin XP'sini alamazsın." (bahis cümlesi yok).

## 2. Okey seçici (okey bağlama)

**Neden:** Okey (joker) birden çok taşın yerine geçebiliyorsa motor hangi taşı temsil ettiğini bilmek zorunda. Örnek: Mavi 5 · okey · Mavi 7 (tek yol), Mavi 5-6 · okey (4 ya da 7), 9-9-okey (eksik renklerden hangisi).

**İçerik:**
- Aç / Yere aç / işle anında, belirsiz okey için açılan küçük seçici. Perin önizlemesi, okeyin aday taşları (2–3 seçenek) taş görselleriyle, her adayın per toplamına etkisi ("+7").
- Varsayılan seçili aday: en yüksek puanlı. "Onayla" ve "Vazgeç".
- Konum: perin üstünde çapalı kart ya da ekran ortası küçük modal (öneri bekleniyor).
- Masadaki okeyli perde, okeyin temsil ettiği taşın küçük göstergesi (rozet ya da köşe yazısı).

## 3. Ceza onayı ve ceza gösterimi

**Neden:** Masadaki bir pere işlenebilen taşı ya da okeyi atmak +101 ceza yazar. Şu an kırmızı toast + ikinci basış var.

**İçerik:**
- Onay kartı: atılacak taşın görseli, "Bu taş işlenebilir · atarsan +101 ceza" (okey için: "Okey attın · +101 ceza"), işlenebileceği perin küçük önizlemesi, düğmeler: "Vazgeç" (birincil) ve "Yine de at" (tehlike).
- Ceza rozeti: oyuncu panelinde ve rakip plakasında "+101" kırmızı rozet; birden çok ceza varsa "+202".
- Rakip ceza aldığında kısa bildirim (toast ya da avatar üstü balon).

## 4. Yerden taş alma durumları

**Neden:** Önceki oyuncunun attığı taş yalnızca o tur kullanılabiliyorsa alınabilir. Açmamışsan aldığın taş açılışta kullanılmalı. Aldıysan atmadan önce kullanmak zorundasın.

**İçerik:**
- Alınabilir taş: mevcut nabız halkası (T05).
- Alınamaz taş: sönük halka + üzerine gelince ipucu "Bu taşı bu turda kullanamazsın".
- Alındı, henüz kullanılmadı: ıstakadaki taşta "KULLAN" vurgusu (renkli çerçeve ya da etiket) + toast "Yerden aldığın taşı bu turda kullanmalısın".
- Kullanmadan atma denemesi: hata durumu.

## 5. Kural ve sistem mesajları kataloğu

**Neden:** Şu an hepsi tek tip kırmızı ya da yeşil toast.

**İçerik:** Toast türleri (bilgi / başarı / uyarı / hata) ve süreleri. Metinler (TR/EN):
- "Açmak için en az 101 gerekli" (eksik puanı da göster: "88 / 101 · 13 eksik")
- "Çift açmak için 5 çift gerekli"
- "Çiftle açtın · yalnızca çift açabilirsin"
- "Son taş işlenemez"
- "Bitirmek için önce elini açmalısın"
- "Bu taş o pere işlenemez"
- "Süren doldu · senin yerine oynandı"
- "Süren bitiyor!" (mevcut)
- "Per taşınıyor · ıstakada istediğin yere bırak" (mevcut)
- Rakip olayları: "Ayşe elini açtı · 118", "Zeynep çift açtı · 5 çift", "Burak okey attı · +101"

Ayrıca: toast'ların üst üste geldiğinde kuyruk davranışı.

## 6. El sonu tablosu v2 (T24'ün yerine)

**Neden:** Gerçek puanlama çarpanlar ve ceza içeriyor; bot maçında çip yok.

**İçerik:**
- Satır: sıra, avatar, ad, not (bitirdi / açtı / açmadı / çift açtı), **ceza** (+101 rozet), el puanı, toplam.
- Bitiren satırında çarpan rozetleri: "Okeyle bitti ×2", "Çiftle ×2", "Elden bitti ×2" (birlikte ×8'e kadar). El puanı rozetlerle açıklanır: "−101 × 4 = −404".
- Çiftle açanın el puanı ×2 notu.
- "Çiftle bitti" (puansız el) durumu: "Bu el puanlanmadı" açıklaması.
- Sağ üst: bot maçında yalnızca **XP** (+340 XP / +60 XP), çip alanı yok. Çevrimiçi maç için çip varyantı da.
- Alt şerit: ikramlar (T24 gibi) + "Sonraki el · 15" + "Masadan kalk".

## 7. Maç sonu ekranı

**Neden:** Prototip sonsuz el döngüsü.

**İçerik:**
- Maç uzunluğu önerisi: sabit el sayısı (ör. 5 el) ya da bir eşik. Seçenekleri karşılaştır.
- Ekran: final sıralaması (1.–4., toplam puan), kazanan vurgusu (konfeti / kupa), toplam XP, seviye çubuğu ilerlemesi (seviye atlama anı dahil), el el puan özeti (mini tablo).
- Düğmeler: "Tekrar oyna", "Lobiye dön".
- El sayacı: üst barda "2 / 5. el" gibi gösterim.

## 8. Okeyi masadan alma

**Neden:** Motor, masadaki perde okeyin temsil ettiği gerçek taşı koyarak okeyi almayı destekliyor.

**İçerik:**
- Etkileşim: gerçek taş masadaki okeyin üstüne sürüklenir, okey ıstakaya uçar.
- Hedef vurgusu: sürüklerken uygun okeylerin parlaması.
- Geçersiz deneme mesajı.
- İsteğe bağlı: okeye uzun basınca "Bu okeyin yerine Mavi 7 koyarak alabilirsin" ipucu.

## 9. Menü → masa geçişi

**İçerik:**
- Yükleme ekranı: tema degradesi + gezegen + logo + ilerleme çubuğu + ipucu metni ("İpucu: Bir pere basılı tutarak toplu taşıyabilirsin").
- "Masa açılamadı" hata durumu: yeniden dene / lobiye dön.

## 10. (İsteğe bağlı) Dokunmatik / mobil

Mobil hedefleniyorsa: uzun basma ile per taşıma, sürükleme izi, büyütülmüş dokunma alanları, HUD'un dar ekranda (19.5:9) yerleşimi.

---

Öncelik: 1, 2, 3, 4, 5, 6 ve 7 tek paket (oyunu tamamlıyor). 8 ve 9 ikinci paket. 10 mobil kararına bağlı.

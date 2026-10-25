# 20 — Lobi (Klasik), masa açma, katılma, bekleme odası, davet

Görseller: `S09`–`S15`. Kaynak: `reference/game/G101-Lobi.dc.html`.

## Modlar
Ana menüde tek mod: **Klasik** (Tekli / Eşli masalar). "Dereceli" ve "Botlara karşı" kartları "YAKINDA" rozetli, tıklanınca bilgi tostu.

## Masa listesi (S09)
- Üst çubuk (86px): geri, "Klasik" başlığı, alt metin, çip bakiyesi + "+" (mağaza), **Kodla katıl**, **Masa aç** (altın).
- Filtreler: Tür segmenti *Tümü / Tekli / Eşli* (sayılı), çipler *Boş yer var*, *Katlamalı*, bahis segmenti *Tümü/1K/5K/10K/50K/100K*, arama kutusu (odakta altın halka).
- Masa kartı: ad, tür rozeti, bahis, el sayısı, tur süresi, 4 koltuk avatarı (boş koltuk kesik çizgili), durum: *Bekliyor* / *Oynanıyor · N. el* / *Dolu*, kilit simgesi (şifreli). Düğmeler: **Otur** (altın), **Oyuna katıl** (yeşil, oynanan masada boş yer), **İzle** (göz, izleyiciye açık ise), **Masana dön** (kendi masan).
- Bahis bakiyeden büyükse düğme sönük + "Yetersiz bakiye · X çip eksik" tostu.
- Kart girişi `lbCard` .5s `cubic-bezier(.22,1,.36,1)`, kart başına kademe.
- **Hover**: translate 0 −6px, scale 1.012, brightness 1.08 + drop-shadow(0 14px 18px rgba(0,0,0,.28)); geçiş .55s `cubic-bezier(.22,1,.36,1)` (translate, scale), filter .45s. (Giriş animasyonu `backwards` doldurma kullanır ki hover'ı ezmesin.)
- Masalar canlı güncellenir (oyuncu girer/çıkar, masa dolar/kapanır). Filtre değişince liste yeniden dizilir.
- Boş sonuç: başlık + "Filtreleri temizle" + "Masa aç".

## Masa aç formu (S10)
Alanlar ve varsayılanlar: Tür (Tekli/Eşli), Masa adı ("Ad'ın masası"), Görünürlük (*Açık* / *Özel* — özel masaya yalnızca kodla girilir), Şifre (aç/kapa, 4 hane), El sayısı (3/5/7, varsayılan 5), Bahis (1K/5K/10K/50K/100K; bakiyeye göre kilit), Katlamalı (aç/kapa), Tur süresi (15/20/30/45 sn, varsayılan 30), Kurallar katlanır bölümü: izleyici, sohbet (açık/hızlı/kapalı), hediyeler, işleme, ceza kuralları. Sağda özet kartı: ödül havuzu = bahis × 4, "Kazanan X çipin tamamını alır". **Masayı oluştur · bahis** düğmesi. Doğrulama: ad boş olamaz; şifre açık ise 4 rakam.

## Diyaloglar
- **Şifreli masa** (S11): 4 haneli PIN kutuları; yanlışta sallanma + hata. Deneme şifresi **1234**.
- **Kodla katıl** (S12): `GX-XXXX` biçimi; deneme kodu **GX-7Q2M**.
- **Bağlanıyor** (850 ms), **Masa doldu**, **Masa kapandı** hata diyalogları.
- **Paylaş**: özel masa kodu kopyala / paylaş.

## Bekleme odası (S13)
Oval masa çizimi, 4 koltuk; boş koltukta **Davet** (altın) ve sahipse **Bot ekle**; dolu koltukta sahibe **Çıkar**. Sağ panel: masa özeti, kod (özel ise) + Kopyala/Paylaş, **Başlat** (sahip; 4 dolu olunca), geri sayım. Sahip değilse masa dolunca otomatik geri sayım.

## Arkadaş davet (S14, S15)
900×580 modal: sol liste (sekmeler *Arkadaşlar (çevrimiçi/toplam)* / *Son oynadıkların*, arama), sağ: koltuk mini görünümü, "N/4 dolu · M davet bekliyor", kodla davet, not. Satır: avatar + durum noktası (yeşil çevrimiçi, sarı oyunda, gri çevrimdışı), SV, alt metin, düğme durumları: *Davet et* → *Bekleniyor 30* (spinner + 30 sn azalan çubuk, tıkla=iptal) → *Masada* (yeşil) / *Reddetti* / *Tekrar davet*; oyunda/çevrimdışı/masa dolu ise sönük. Kabul eden boş koltuğa (seçilen koltuğa öncelikli) oturur. Prototipte kabul olasılığı arkadaşa göre (`r`), %25 red, kalan zaman aşımı.

## Masadan kalk
Masa → çıkış onayı (S/G33) → **Kalk** → "Masadan kalktın" tostu → 1.3 sn sonra **lobi**. Bahis kaybedilir, yerine bot oturur.

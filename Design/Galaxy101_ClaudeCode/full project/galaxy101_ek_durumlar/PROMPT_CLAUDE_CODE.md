# Claude Code / Codex için görev metni

Aşağıdaki metni olduğu gibi Claude Code'a verin. Bu klasörü Unity projesinin kökünde `Design/galaxy101_ek_durumlar/` altına koyun.

---

Galaxy 101 Unity projesinde 3D oyun masası (T01–T27) zaten uygulandı. Şimdi `Design/galaxy101_ek_durumlar/` klasöründeki ek durumları (T28–T51) uygula.

**Önce oku:**
1. `README.md` — içerik, durum listesi, kesinleşen kurallar, açık sorular
2. `spec/17_MASA_EK_DURUMLAR.md` — tamamı; ölçüler, renkler, süreler, kurallar, kabul listesi
3. `spec/data/table_strings_ek_tr_en.csv` — tüm yeni metinler; mevcut yerelleştirme tablosuna ekle, metinleri koda gömme
4. `spec/data/themes.json` — renkler tema değişkenlerinden okunmalı (`--a1`, `--pn` … → tema ScriptableObject alanları)
5. `screenshots/` — her durum için Satürn ve Mars görüntüsü; `_baz_T05` temel düzen
6. Bir değer spec'te yoksa: `prototip/Galaxy101_Masa_EkDurumlar.dc.html` içindeki satır içi stillerden al

**Uygulama sırası:**
1. Kural motoru (`com.okey101.core`): 5 ceza kuralı (spec/17 §3), okeyin otomatik en yüksek değeri (§2), yerden alınan taşı geri bırakma ve sonraki oyuncuya atılamaması (§4), okeyi masadan alma ve sahibine +101 (§8). Aç düğmesi 101 altında da basılabilir; eksik açılış geri alınır, ceza yazılır. Her kural için birim testi yaz.
2. Bildirim sistemi: 6 tür (Ceza, Bilgi, Başarı, Uyarı, Hata, Rakip balonu), süreler ve kuyruk kuralları (§5). Ceza toast'ı tutar hapıyla; rakip olayları kuyruğa girmez, avatar balonunda.
3. Ceza rozetleri: oyuncu paneli ve rakip plakaları, o elin toplamı, popIn, el sonunda sıfırlanır.
4. Okey temsil rozeti masadaki tüm okeylerde (T33); hover/uzun basma ipucu.
5. Yerden alma görünümü: YERDEN etiketi, GERİ BIRAK halkası (T38–T39).
6. Okeyi alma etkileşimi: hedef parlaması, uçuş animasyonu, ipucu, geçersiz deneme (T47–T49).
7. Bot masası başlığı: ANTRENMAN, el sayacı, XP hapı, BOT etiketleri; bot varyantlı Masadan kalk (T28, T30).
8. El sonu tablosu v2 — mevcut T24'ün yerine (T42–T44): ceza sütunu nedenleriyle, çarpan rozetleri, açıklamalı el puanı, puansız el, XP/çip varyantı.
9. Maç yapısı: lobide 3/5/7 el seçimi (varsayılan 5), maç sonu ekranı (T45–T46), XP dökümü, seviye atlama animasyonu.
10. Menü → masa yükleme ekranı ve "Masa açılamadı" hatası (T50–T51); 15 sn zaman aşımı.

**Kurallar:**
- 3D sahne değişmez; görüntülerdeki masa 2D taklittir. Yalnız HUD, kartlar, rozetler ve etkileşim eklenir.
- Her ekran Satürn ve Mars temasında görüntülerle karşılaştırılarak doğrulanmalı; TR ve EN dilde metin taşması olmamalı.
- README'deki "Açık sorular" için varsayılanı uygula (1: yalnız +101 · 2: T44 kuralı · 3: önerilen XP değerleri), değerleri tek bir yapılandırma dosyasında tut ki sonradan değiştirilebilsin.
- Bitirince `spec/17` sonundaki kabul listesini madde madde işaretle ve sapmaları raporla.

# 00 — Güncel durum (v3) ve belge önceliği

Bu paket üç aşamada büyüdü. **Çelişki olursa en yeni belge geçerlidir.** Öncelik sırası:

1. `spec/18`–`spec/25` (v3, bu sürüm) — giriş/yükleme, ayarlar-görüntü, lobi, dağıtım töreni, kural motoru, masa düzeni v3, Blender hattı, test senaryoları
2. `spec/17` (masa ek durumları)
3. `spec/08`–`spec/16` (ilk 3D masa)
4. `spec/01`–`spec/07` (ana menü 1a)

Birincil çalışan prototip artık **`reference/game/Galaxy 101 - Oyun.dc.html`** dosyasıdır: açılış → giriş → ana menü → lobi → masa → maç sonu → lobi akışının tamamı tek dosyadadır ve `G101-Menu`, `G101-Lobi`, `G101-Masa` dosyalarını içe alır. `reference/` ve `reference/table/` altındaki eski dosyalar yalnızca tarihçe içindir.

## v3'te değişen / eski belgeleri geçersiz kılan kararlar

| Konu | Eski | Yeni (geçerli) | Belge |
|---|---|---|---|
| Oyun modları | 6 mod | Tek mod **Klasik** (Tekli/Eşli masalar). Dereceli ve Botlara karşı "YAKINDA" | 20 |
| Dağıtım | Desteden taş taş girdap | Gerçek 101 töreni: dağıtan seçilir, 15×7 balya + 1 fazla, iki zar, gösterge, balya dağıtımı | 21 |
| Taş seti | Elle hazırlanmış eller | 106 taşlık gerçek set karıştırılır; oyun sırasında yeni taşlar **görülmemiş taş havuzundan** gelir, hiçbir taş 2 kopyayı geçmez | 22 |
| Okey yüzü | Yıldız + OKEY yazısı | **Gerçek okey taşı boş yüzlüdür** (hiç desen yok). Sahte okey yonca simgesi | 23 |
| Yere açılan perler | Her oyuncunun önünde dağınık | **Masanın ortasında ortak tahta** (8 satır × 20 sütun), deste ve gösterge sola kayar | 23 |
| İşleme | Sadece İşle düğmesi | Taş seçilince/sürüklenince altın "+" ve yeşil "okey al" göstergeleri; okey perin iki ucuna işlenebilir | 22, 23 |
| El açma puanı | İşledikçe artıyordu | **Açılış puanı sabit kalır** | 22 |
| Geri alma | Yok | **Geri topla**: bu turda açılan/işlenen her şey ıstakaya döner | 22 |
| Cezalar | Kısmi | Hatalı açma (+101), işlek taş atma (+101) | 22 |
| Soldan alınan taş | Kullanmak zorunlu | **Geri bırak** düğmesi ya da sol halkaya sürükleme | 22 |
| Elden bitiş | Yok | **Elden bit** (bomba) düğmesi + nükleer patlama animasyonu, rakiplere ×2 ceza | 22, 23 |
| Hamle süresi | Sabit | Aç / Çift aç / Yere aç / İşle / okey alma süreyi **baştan başlatır** | 22 |
| Ayarlar → Görüntü | Gezegen teması satırı vardı | Gezegen teması satırı **kaldırıldı**; Görüntü modu, Çözünürlük (monitöre göre), Kare hızı eklendi | 19 |
| Masadan kalk | Masada kalıyordu | Onaylanınca **lobiye** döner | 20 |

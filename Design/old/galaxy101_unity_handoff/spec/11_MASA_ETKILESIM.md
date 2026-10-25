# 11 · Oyun Masası — Istaka Etkileşimi (sürükle-bırak, toplu taşıma)

Tüm girdiler 3D'dir: ekran → ışın (raycast). Benim ıstakamın düzlemi (ıstaka yerel Z=0) ve masa düzlemi (y = TY) kullanılır. Kaynak: `table3d.js` → `onDown / onMove / onUp`, `singlePlan`, `groupPlan`, `planRow`.

## 1. Seçilebilir hedefler
- Istakamdaki taşlar (oyun dışı aşamalar hariç her zaman).
- Çekme aşamasında: deste kutusu ve Burak'ın son atığı.
- Fare imleci: hedef üstünde `pointer`, sürüklerken `grabbing`.
- Üzerine gelinen taş 0.8 cm yükselir (yerel Y), seçili taş 2.2 cm (yumuşak, 14/sn).

## 2. Tıklama
- **Tek tık** (5 px'den az hareket): taşı seç / seçimi kaldır. Deste ya da yerdeki taş → çek (otomatik yerleştir).
- **Çift tık** (aynı taş, 380 ms, "Çift tıkla at" açık, atma aşaması): taşı at.
- Seçili taş varken atma halkasına tıklamak: seçili taşı at.

## 3. Tek taş sürükleme
- 5 px hareketle başlar. Taş imleci izler: ıstaka bölgesindeyse ıstaka düzleminde 5 cm önde (x −0.43…0.43, y 0.04…0.135), dışındaysa masa düzleminde 5 cm yukarıda, yüzü yukarı. Konum takibi 22/sn, dönüş 14/sn yumuşatma.
- **Canlı önizleme**: imleç dolu bir yuvanın üstüne geldiğinde, bırakılınca oluşacak dizilim hemen gösterilir (komşu taşlar 0.2 sn'de kayar). İmleç ıstakadan çıkarsa hepsi geri döner.
- **Yerleştirme kuralı (`singlePlan`)**:
  1. Taş eski yerinden kaldırılır.
  2. Hedef yuva boşsa oraya konur.
  3. Doluysa aynı sırada hedefin solundaki ve sağındaki **ilk boş yuva** aranır; **hangisi daha yakınsa** (eşitse sağ) o yöne doğru, hedef ile o boşluk arasındaki taşlar birer yuva kayar.
  4. Bir tarafta boşluk yoksa (ıstaka kenarı) diğer tarafa kayılır.
  5. Sırada hiç boşluk yoksa yerinden çıkan taş diğer sıradaki en yakın boş yuvaya gider.
  6. Taşlar asla yer değiştirmez (swap yok) ve üst üste binmez.
- Bırakma: atma halkası üstünde (atma aşaması) → at; masadaki bir per taşının 7.5 cm yakınında (açtıysam) → işle; ıstaka yuvası → yerleştir; başka yer → yerine dön.

## 4. Per toplu taşıma
- **Başlatma**: geçerli bir perin (seri ya da grup, yeşil çizgili) herhangi bir taşına **360 ms basılı tut** ya da **Shift + sürükle**. Bu süre dolmadan 5 px hareket edilirse normal tek taş sürükleme olur (tek taş davranışı korunur).
- Başlayınca: perin tüm taşları birlikte kalkar (%5 büyür, yeşil çizgi), tuttuğun taşa göre göreli konumlarını korur; toast "Per taşınıyor · ıstakada istediğin yere bırak".
- **Canlı önizleme**: imleç başka bir yuvaya geçtiği her an ıstakadaki diğer taşlar yeni dizilime 0.2 sn'de kayar.
- **Yerleştirme kuralı (`groupPlan` + `planRow`)**:
  1. Per taşları eski yerlerinden kaldırılır. Per n taş, hedef sıra r, başlangıç `st = clamp(hedefYuva − tutulanİndeks, 0, 15 − n)`.
  2. **Boşluk**: perin solunda ve sağında birer boş yuva bırakılır; per ıstakanın kenarına dayanıyorsa o tarafta bırakılmaz.
  3. Sıradaki diğer taşlar **sıralarını koruyarak** sola ya da sağa itilir. Her olası bölme noktası k için (ilk k taş sola, kalanlar sağa): sol grup sağdan sola, her taş `min(eskiYer, sınır)` yuvasına; sağ grup soldan sağa `max(eskiYer, sınır)` yuvasına yerleştirilir (önce o taraftaki boş yuvalar dolar, taşlar mümkün olduğunca az hareket eder).
  4. Puan = taşmayan taş sayısı·1000 + vazgeçilen boşluk·100 + **bitişik grubu bölme cezası 60** + toplam kayma. En düşük puanlı çözüm seçilir. Önce iki boşlukla, sığmazsa tek taraf, sonra boşluksuz denenir.
  5. Yine de sığmayan taşlar diğer sıradaki en yakın boş yuvalara geçer (sıralarıyla).
  6. Önizlemede görülen dizilim bırakınca **birebir** uygulanır (bırakma anında hesaplanan plan oyun durumuna yazılır).
- Istaka dışında bırakılırsa hiçbir şey değişmez, herkes yerine döner.

## 5. Desteden / yerden sürükleyerek çekme
- Deste sürüklenince sırtı yukarı bir "hayalet" taş çıkar, imleci izler; ıstaka yuvasına bırakılınca yeni taş hayaletin olduğu yerden yuvaya kayar (tek taş yerleştirme kuralıyla). Yuva dışında bırakılırsa otomatik yerleştirilir.
- Yerdeki taş da aynı şekilde sürüklenir.

## 7. Birim test örnekleri (per toplu taşıma)
Üst sıra `A B C D . E F G . H I J . K L`, alt sıra `m n . o p q . r . s t . u v .` (`.` boş).

1. `H I J` → üst sıra yuva 2: üst `* * *` bloğu 2–4'e değil, A-B-C-D bitişik grubu bölünmesin diye sağa itilir; sonuç: per iki yanında birer boşlukla, A B C D E F G sırası korunarak sağa kaymış, K L yerinde; hiçbir taş alt sıraya geçmez ya da en az sayıda geçer.
2. `A B C D` → alt sıra yuva 6 (sıra dolu: 10 taş + 4 + 2 boşluk > 15): önce bir taraftaki boşluktan vazgeçilir; `m n o p q r` sola, `s t u v` sağa sıkışır; taşma olmaz.
3. `E F G` → üst sıra yuva 0: per 0–2, sağında 1 boşluk, A B C D 4–7'ye itilir (solda kenar olduğu için boşluk yok).

Kabul ölçütü: her senaryoda (a) taşlar üst üste binmez, (b) itilen taşların göreli sırası korunur, (c) kenar değilse perin iki yanı boştur, (d) önizleme = bırakma sonucu.

## 6. Hareket sonrası
- Istaka içi kaymalar: efektsiz, 0.18–0.34 sn (mesafeye göre) yumuşak kayma; zıplama, parıltı ya da ölçek efekti **yoktur**.
- Istakaya dışarıdan gelen taş (çekme, alma): yaylı uçuş + iniş "pop" + 1.6 sn sarı "yeni" çizgisi (bkz. `12_MASA_ANIMASYONLAR.md`).

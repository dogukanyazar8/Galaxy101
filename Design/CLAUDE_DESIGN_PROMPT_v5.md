# Claude Design'a verilecek prompt — Galaxy 101 teslim paketi v0.5.0

Çizgiden sonrasını olduğu gibi Claude Design'a yapıştır. En alttaki "Claude Code tarafı" bölümü sana not; onu verme.

---

## Bağlam

Galaxy 101'in (101 Okey, 4 oyuncu) v0.4.0 teslim paketi (`Galaxy101_ClaudeCode/`, `prototip/galaxy101_handoff_v4/`) Claude Code tarafından Unity 6 (URP, UI Toolkit) istemcisine ve .NET 10 sunucusuna eksiksiz taşındı.

v0.4.0 paketinde işe yarayanlar:
- `rules.js` satır satır C#'a çevrildi; 106 kural vektörü ve 26 sunucu akışı testlerde aynen koşuyor.
- `protocol.json`'daki 53 C2S, 45 S2C mesaj ve 94 hata kodu sunucuda birebir uygulandı.
- `states.json`'daki 144 durum Unity'de `-g101State` ile açılıyor. Prototiple aynı tohum, tema, dil ve donma anında yan yana çekilip metin kutusu (±3 px) ve piksel farkıyla otomatik karşılaştırılıyor.
- Metinler, temalar, fontlar, ikonlar, rozetler, imleçler, sesler, müzik, animasyon kataloğu ve 103 GLB doğrudan içe aktarıldı.

Oyunun v0.5.0 sürümünü seninle tasarladık: ekranlar yeni içerik, animasyon, seçenek ve özelliklerle yenilendi. Şimdi **v0.5.0 için aynı biçimde, eksiksiz bir teslim paketi** istiyorum. Claude Code bu paketle mevcut v0.4.0 kodunu hiçbir değeri tahmin etmeden v0.5.0'a getirecek.

İki temel kural:
- **Tasarımı değiştirme.** Paket, tasarladığımız v0.5.0'ın kendisi olsun; paketlerken yeni karar verme.
- **Bilmediğin değeri uydurma.** `OPEN_QUESTIONS.md`'ye yaz ve bana sor.

## A. v0.4.0 sözleşmesini aynen koru

Claude Code'un araçları bu sözleşmeye bağlı. Bir şeyi değiştirmen gerekiyorsa `CHANGELOG.md`'de "sözleşme değişikliği" başlığıyla gerekçesini yaz.

1. **Klasör düzeni.** v4 ile aynı düzen; teslim klasörü `galaxy101_handoff_v5/`:
   - Belgeler: `README.md`, `RULES.md`, `DECISIONS.md`, `OPEN_QUESTIONS.md`, `REMOVED.md`, `DEMO_LIST.md`.
   - Klasörler: `logic/`, `protocol/`, `tests/`, `states/`, `export/`, `anim/`, `strings/`, `assets/`, `sound/`, `screenshots/`.
   - Kök `START.md` ve `prototip/`.
2. **Durum adresi.** `?state=ID&seed=42&theme=saturn&lang=tr&ft=…` aynı anlamda çalışsın; v0.4.0'daki parametrelerin hiçbiri kaldırılmasın.
   - Ortak saat (`g101-clock.js`), seed'li mulberry32 RNG, `Stream(name)` ayrımı ve donma mekanizması aynı kalsın.
3. **Kimlikler kalıcı.**
   - Durumlar: S01–S15, G01–G34, T01–T51 ve 01–97 aynı kimlikle kalsın. Anlamı değişen durum aynı kimliği korur ve `CHANGELOG`'da "değişti" diye işaretlenir.
   - Yeni durumlar yeni kimlik alsın; hiçbir kimlik başka bir anlam için yeniden kullanılmasın.
   - Kaldırılan durumlar `states.json`'da `removed: true` ve `REMOVED.md` kaydıyla kalsın (v4'teki T33, T40, T41, T44, T45, T46, T48 gibi).
   - Aynı kural, kural (R-xx), karar (D-xx), test vektörü, sunucu akışı (SRV-xx), metin anahtarı, `data-g101-id`, ses olayı, animasyon, ikon ve malzeme kimlikleri için de geçerli.
4. **Kural motoru saf kalsın.** `logic/rules.js` ve `logic/bots.js`'de DOM, zamanlayıcı ve `Math.random` olmasın. Durum yalnız `logic/mockServer.js` mesajlarıyla değişsin.
   - Kural davranışı değiştiyse `ruleVersion`'ı yükselt (`101-v5`) ve eski kayıtların hangi sürümle açılacağını yaz.
   - RNG ya da karma algoritması değiştiyse kimliğini değiştir (`mulberry32-v4` → yeni ad).
5. **Sürüm.** Pakette, prototipin `/info` karşılığında ve `START.md`'de sürüm `0.5.0` olsun.

## B. Değişiklikleri makine tarafından okunabilir yaz

Claude Code v0.4.0'dan v0.5.0'a farkı bu belgelerden çıkaracak. Prototipi tekrar tersine mühendislikle taramak zorunda kalmamalı.

1. **`CHANGELOG.md`.** v0.4.0'a göre her değişiklik bir satır olsun: ne, neden, etkilenen durum kimlikleri, `data-g101-id`'ler, kurallar, protokol mesajları, metin anahtarları, animasyonlar, sesler ve varlıklar. Bölümler:
   - Yeni ekranlar
   - Yenilenen ekranlar
   - Yeni özellikler
   - Yeni seçenekler / ayarlar
   - Kural değişiklikleri
   - Protokol değişiklikleri
   - Sunucu davranışı
   - Ekonomi
   - Animasyon
   - Ses
   - 3D
   - Metin
   - Kaldırılanlar
   - Sözleşme değişiklikleri
2. **`changes.json`.** Aynı bilgi makine biçiminde: `{ kind, id, change: added|changed|removed|renamed, from, to, refs[] }`. Yeniden adlandırmalarda eski ve yeni kimlik birlikte bulunsun.
3. **`FEATURES.md`.** v0.5.0'daki her özellik için tek satırlık iz:
   - özellik
   - açan durumlar
   - C2S/S2C mesajları
   - kurallar
   - metin anahtarları
   - animasyonlar
   - ses olayları
   - test vektörleri / sunucu akışları
   - DEMO kaydı (varsa)

   İzi eksik özellik kalmasın.
4. **`OPTIONS.md` + `options.json`.** Oyundaki her seçenek ayrı satırda:
   - Kapsam: ayarlar paneli, masa aç formu, lobi filtreleri, profil, mağaza, dereceli, masa içi menüler, görüntü/ses/imleç/masa görünümü ve diğerleri.
   - Her seçenek için: kimlik, olası değerler, varsayılan, nerede saklandığı (yerel / sunucu), oyunda tam olarak neyi değiştirdiği, hangi durumda görüldüğü, metin anahtarları.
   - Bir seçeneğin varsayılanı iki yerde farklı olmasın (v4'te müzik 60 ↔ 55 farkı vardı).
5. **`MIGRATION.md`.**
   - Yerel kayıt (ayarlar, son giriş, dil, tema) ve sunucu verisinin (hesap, bakiye, envanter, lig, istatistik, geçmiş) v0.4.0'dan nasıl taşınacağı.
   - Eski istemcinin yeni sunucuyla konuşup konuşamayacağı, yeni anahtarların varsayılanları.
   - Kaldırılan alanlara ne olacağı.

## C. Eksiksiz kapsam

v0.5.0'da tasarladığımız her şey pakette olsun. "Ekranda var ama pakette yok" durumu kalmasın.

1. **Durum kataloğu.** `states.json`'a v0.5.0'ın her ekranı, penceresi, sekmesi, alt sekmesi, diyaloğu, tostu, boş/yükleniyor/hata/çevrimdışı hali, açılır menüsü, seçili/üzerinde/basılı/pasif hali ve her masa anı girsin.
   - Her durum için şunlar olsun: açıklama, açma yolu (gerçekten çalışan), ilgili `data-g101-id` kökü, kullanılan seed ve donma anı.
   - Her durum adresle açılabilsin. Açılmayan yol kalmasın: v4'te S14/S15 yolu `wait.friend/invTab` yazıyor, prototip `wait.inv {tab}` okuyordu; davet paneli hiç açılmıyordu.
2. **Kalıcı öğe kimlikleri.** v0.5.0'ın her görünür öğesinde `data-g101-id` olsun; yeni öğelerde de, 3D sahnedeki seçilebilir objelerde de (obje adı).
3. **Yerleşim dökümü.** `export/layout-all.html` ve `?dump=1` yeni durumları da kapsasın.
   - Döküm şunları içersin: kutu, hesaplanmış stiller, degrade katmanları, gölgeler, harf aralığı (px), filtreler, z-index.
4. **Animasyon kataloğu.** `anim/css.json`, `anim/tween3d.json` ve `anim/easings.json` yeni ve değişen bütün animasyonları içersin.
   - Her animasyon için: tetikleyici, hedef, özellik, keyframe'ler, süre, gecikme, eğri, tekrar, kademe.
   - Çakışma kuralı da yazılsın: animasyon sürerken yeni olay gelirse ne olur.
   - Yeni süreler `protocol/timings.md`'de sunucu ve istemci sütunlarıyla yer alsın.
5. **Metinler.** `strings/strings.json` + `strings.csv` v0.5.0'ın bütün metinlerini TR ve EN içersin: hata, tost, boş durum, ipucu, mağaza, görev, sezon, bildirim ve sistem satırları dahil.
   - HTML'de gömülü metin kalmasın.
   - Her anahtar tekil olsun (v4'te 23 satır içi anahtar birden çok metne gidiyordu).
   - Koşullu metinlerin her dalı ayrı anahtar olsun. Örnekler: ısmarladı / ısmarladın / gönderdi / gönderdin; "seat/seats"; `n = 0` hali; v4'teki "NaN/4 dolu" hatası.
   - EN'de çevrilmemiş metin kalmasın (v4'te "Masa" sekmesi, "SV 24", " sn" birimi, İmleç satırı).
   - Sayı, para, yüzde ve süre biçimleri `strings/formats.md`'de olsun, "son görülme · N dk" gibi kısa süreler dahil.
6. **Ses.** `sound/events.json` yeni olayları içersin: tetikleyici, kanal, ses düzeyi, süre, kesme/tekrar kuralı.
   - Yeni efekt ve müzikler WAV olarak gelsin; tetiklenme anı tek anlamlı olsun (v4'te `finish.nuke` üç yerde üç farklı tanımlıydı).
7. **Botlar ve sunucu provası.** v0.5.0'da yeni ya da değişen her sunucu davranışı `mockServer.js`'de çalışsın ve `tests/server.tests.js`'de bir akışla test edilsin.
   - Kapsam: zamanlayıcılar, ekonomi, ödüller, görevler, sezon, eşleşme, davet, hediye, sohbet.
   - Yeni kurallar `tests/rules/*.json` vektörleriyle gelsin.
   - Vektör kimlikleri tekil olsun (v4'te MELD-19, MELD-20, FIN-09 iki kez kullanılmıştı). `rules.test.html` ve `runner.js` tamamen yeşil koşsun.

## D. v0.4.0 paketinde bulunan hataları kapat

Taşıma sırasında pakette şu tutarsızlıklar bulundu. Her biri için ya düzelt ya da `DECISIONS.md`'de tek anlamlı karar yaz:

1. **Donma anı.** D-61 "100000 + ft ms" diyor; `g101-state.js` `G101Clock.freeze()`'i argümansız çağırıyor ve bütün kareler 100000 ms'de donuyor. Tek kural yaz ve referans kareleri ona göre üret.
2. **Determinizm.** Aynı adres iki açılışta aynı kareyi vermiyor:
   - Yıldız tuvali (parıltı, kayan yıldız) ve gezegen dönüşü ortak saate bağlanmamış.
   - Mağaza, günlük ödül ve görev geri sayımları duvar saatine bağlı (`Date.now` = gerçek yükleme anı).
   - S01 açılış yüzdesi yükleme olaylarına bağlı.

   Hepsi ortak saatten ve seed'den türesin; durum karesinde sabit bir "şimdi" (örneğin `now=` parametresi) kullanılsın.
3. **Protokol ↔ motor uyumsuzlukları:**
   - `gAdd.side`: protokolde `L|R`, motorda `pre|post`.
   - `E_SEAT_NOT_BOT` RULES.md hata listesinde yok.
   - `rules.js` iki bozuk girdide hata kodu yerine `TypeError` fırlatıyor: aralık dışı `swap.index` ve `tiles`'sız per.
   - R-59 "3 / 5 / 7 el" diyor; motor her değeri kabul ediyor.
   - `vsBotsStart.hands` geçerli değerleri yok.
   - `tableView` "her commit sonrası" diyor (eski terim).
4. **Sunucu provası hataları:**
   - SRV-25 kazanan ortağa 4000 bekliyor; ödenen bahis 2000.
   - `seatKick` iade edip `betsPaid`'den düşmüyor.
   - `gameView` törende göstergeyi erken gösteriyor.
   - SRV-02 ile R-02 örneği dağıtanda çelişiyor.
5. **Varsayılanlar ve depolar.**
   - Müzik varsayılanı menüde 60, masada 55.
   - Sohbet sesi iki anahtar istiyor (`chatSnd` + `notifSnd`); menü ve masa farklı anahtarları tutuyor.
   - Tek ayar deposu ve tek anahtar listesi yaz.
6. **Kimlik/katalog farkları:**
   - Çerçeve kimlikleri üç yerde farklı: menü 0 + 10–15, protokol 0/1/2.
   - Maç geçmişi mod adlarını eski mod kimliklerinden okuyor (Eşli satırı "Dereceli" görünüyor).
   - Hesap silme kayıpları sabit "125.400 çip / 340 elmas".
   - 84 "Bağlantı kopyalandı" durumu yanlış düğmeyi kuruyor.
   - Günlük ödül rozeti "1" sabit; ödül alındıktan sonra da görünüyor.
   - 61 "Gün toplandı" yolu ödülü eklemiyor.
7. **Lobi:**
   - Kodla katılımda şifreli masada PIN penceresi için masa bilgisi protokolde yok.
   - Şifreli masayı izlerken PIN soruluyor ama `tableSpectate` PIN taşımıyor.
   - Bekleme odası 3-2-1 geri sayımı prototipte yalnız misafirde, protokolde herkese.
   - Masa aç formundaki iki ceza anahtarı (`okey`, `isle`) protokol kurallarıyla birebir değil.
   - `inviteCancel` için gereken `inviteId` yalnız ack'te geliyor.
8. **Dereceli:**
   - Kabulden masaya geçiş prototipte 900 ms, `timings.md`'de 1800 ms.
   - `rankedSearch.filled` rakip sayısı değil, partinin kendi koltuk sayısı.
   - Galaksi sırası (#87), sezon numarası ve kalan gün sabit.
9. **3D:**
   - `themes.json` `table3d` bloğunda `tile/leather/glow/sky` eşlemesi yok.
   - `lighting.json`'da `key.shadow.normalBias` yok.
   - `materials.json`'da bazı değerler oyundakinden farklı: kurucu değer verilmiş, çalışma anı değeri değil (`atma_alani` opaklık 0.4 ↔ 0.16; `led` 2.4 ↔ 2.2 + sin(t·1.2)·0.25).
   - Kullanılmayan kaplamalar var (`randro/rwood/rneon`).
   - GLB'lerde aynı adlı farklı malzemeler (`hale`, `atma_alani`, `led`) ve adsız malzemeler var.
   - `sahne_tam` döngünün son durumunu taşıyor; varsayılan masa/ıstaka/deste değil.
   - Işıklar GLB'de yönsüz: hedef ayrı boş düğümde.
   - Zar modelinde yüz yönü aynalanınca 2 ve 5 yer değiştiriyor.
10. **Metin/font:**
    - Galaksi rozetindeki "★" (U+2605) paketteki hiçbir fontta yok.
    - Paket ikonlarının bir kısmı sıra numarasıyla adlandırılmış (`icon-20` … `icon-69`) ve `usedBy` listesi boş.

## E. v0.4.0'da tasarımı/tanımı olmayanlar

Şunlar v0.4.0'da yoktu. v0.5.0'da tasarladıysan pakete ekle; tasarlamadıysan `OPEN_QUESTIONS.md`'de "tasarım yok" diye listele (uydurma):

- Gelen davet kartı (`inviteIn`) ve sunucuya bağlanılamadığında lobi görünümü.
- Masadan çıkarılma, bağlantı kopması ve bakiye yetmediği için kalkma tostlarının metinleri.
- Taşıma hatalarının metinleri: `offline`, `timeout`, bilinmeyen kod. Sessiz geçilen dereceli hataları da (`E_ALREADY_QUEUED`, `E_MODE`, `E_PARTY_SIZE`, `E_NOT_QUEUED`).
- Dereceli parti daveti ve onay adımı; takım arkadaşının reddi metni.
- Dereceli maç sayısı, birincilik sayısı, son maçlar (lig + LP farkı).
- Kopup dönünce ekrana dönüş:
  - `snapshot.screen` değerlerinin tam listesi;
  - her ekranın snapshot alanları;
  - dereceli arama, bulundu ve yükleniyor ekranlarına dönüş.
- Sunucu yeniden başlarsa süren masalara ne olacağı.
- Bot ad havuzu.
- Oyuncu adı ve masa adı uzunluk sınırları; ad benzersizliği.
- Görev kimlikleri ve mağaza SKU listesi: tek katalog, sunucuyla aynı adlar.
- Lig çerçevesi halkasının (`ring()`) görseli: her lig ve sezon çerçevesi için dosya.
- Galaksi rozeti üstündeki dinamik sıra etiketi.
- Öğe başına imleç kuralları (`pointer / not-allowed / progress`): hangi `data-g101-id`, hangi imleç.
- Pakette tanımsız sunucu uçları: `skinSet` / `E_NOT_OWNED`, `friendRemoved`, `E_REQUEST_PENDING`, Galaksi çerçevesi (seviye 30) kuralı.
- "Son oynadıkların" listesinde birlikte oynama zamanı: protokol alanı.
- Masa görünümü: "Yarı yüksek" görüş masa ayarlarında da var mı?

## F. Unity'ye doğrudan girecek varlıklar

Unity SVG filtre ve `<text>` çizemiyor; UI Toolkit'te CSS degrade metin, `backdrop-filter`, `mix-blend-mode` ve `conic-gradient` yok. Bu yüzden:

1. **Raster karşılıklar.** Filtreli, metinli ya da degradeli her SVG'nin (lig rozetleri, madalyalar, imleçler, karmaşık ikonlar, başlık rozeti, sezon çerçeveleri) yanında PNG olsun:
   - @1x, @2x ve @4x;
   - şeffaf arka plan;
   - dosya adı SVG ile aynı.

   İmleçler temanın vurgu rengini izliyorsa her tema için ayrı set gelsin (5 gezegen).
2. **Kısıt listesi.** `UNITY_KISITLARI.md`: bu efektlerin her kullanım yeri `data-g101-id` ile ve tam parametreleriyle listelensin. Önceden hazırlanmış sprite varsa adı da yazılsın. Kapsanacak efektler:
   - `backdrop-filter`, `filter`, `mix-blend-mode`;
   - `conic-gradient`, metin degradesi, `mask`, `clip-path`;
   - metin gölgesi, kabartma yazı.
3. **Fontlar.** Kullanılan her aile ve ağırlık TTF olarak lisansıyla gelsin. Arayüzde görünen her glif (★, ×, ·, ok ve madalya sembolleri, TR karakterler) paketteki bir fontta bulunsun; `fonts.json` glif kapsamını listelesin.
4. **3D.**
   - Her GLB dosya olarak gelsin; araçla indirme gerekmesin. Kapsam: yeni ve değişen modeller, 5 tema, ortak dosyalar.
   - Dosya adları v4 ile aynı; yeni dosyalar `CHANGELOG`'da.
   - Her GLB'nin yanında `materials.json` kaydı olsun. Kayıtta çalışma anındaki değerler bulunsun (animasyonlu değerler formülüyle) ve `KHR_materials_iridescence` / `KHR_materials_sheen` kullanan malzemeler için bu eklentiler olmadan aynı görünümü veren yedek parametreler.
   - Işıklar yönüyle ya da hedef koordinatıyla verilsin.
   - Zar yüzlerinin eksen eşlemesi tablo olarak yazılsın (+X, −X, +Y, −Y, +Z, −Z → hangi sayı).
   - Prosedürel dokular (çuha, gezegen, taş yüz atlası, holo halka) üretim parametreleriyle ve hazır PNG olarak gelsin.
5. **İkonlar.** Her ikon anlamlı adla gelsin; `icons.json`'da `usedBy` listesi (`data-g101-id`) dolu olsun.

## G. Referans görüntüleri

1. Her etkin durum için **kayıpsız PNG** gelsin. Varsayılan Satürn + TR olsun; donma anı D'deki tek kurala uysun.
   - 1280×720 (`<ID>@1x.png`) ve 2560×1440 (`<ID>@2x.png`).
2. **Ek setler:**
   - her durumun EN hali (`<ID>.en@1x.png`);
   - teması değişen her durumun 5 gezegen hali (`<ID>.<tema>@1x.png`);
   - hover, basılı, pasif gibi hal durumlarının kareleri.
3. **Animasyon kareleri.** Her yeni ya da değişen animasyon için başlangıç, %50 ve bitiş anlarında kare olsun (`anim/<id>/<ms>.png`). Hangi durumda ve `ft` ile çekildiği yazılsın.
4. `screenshots/INDEX.md` + `index.json`: her dosya için durum, tema, dil, seed, donma anı, çözünürlük. Görüntüyle aynı adres iki kez açıldığında aynı kare çıkmalı.
5. **Platform.** Hedef en-boy oranları (16:9, 16:10, 21:9) ve her oranda yerleşim kuralı yazılsın (letterbox / genişleme), birkaç örnek kareyle. Klavye kısayolları ve dokunmatik hedefler (varsa) de belgelensin.

## H. Kullanıcı kararları (pakette bunlarla çelişme; değiştirdiysen CHANGELOG'da açıkça yaz)

v0.4.0 taşınırken şu kararları verdim:
- **Bekleme odası:** sahip çıkınca sahiplik kalan ilk insana geçer; insan kalmazsa masa kapanır.
- **3D modeller:** mevcut Blender cilalı modeller kullanılır (bevel, LOD, 2K doku); paketten yalnız renk, malzeme ve yerleşim değerleri alınır. Yeni modellerde aynı yaklaşım.
- **Taş sesleri:** gerçek kayıttan üretilmiş örneklerdir. Paket olay/kanal/zaman sözleşmesini verir; WAV'lar yeni olaylar için yine gelsin.
- **"Botlara karşı":** v0.4.0'da açıldı. v0.5.0'da durumunu açıkça yaz.
- **Ürün kuralları:**
  - Kopan oyuncu maç bitene kadar dönebilir; 2 zaman aşımından sonra sunucu oynar.
  - Hesap kilidi: 3 yanlış şifrede 5 dakika.
  - Günlük seri 7 günde döner; gün sınırı Europe/Istanbul.
  - Beraberlikte sıralama paylaşılır (1, 1, 3); ödeme, LP ve haftalık puan bölünür.
  - Eşli masada kalkanın yerine bot oturur; kazanırsa ödülü masadaki ortak alır.
  - Elden bitişte açılış barajı (101 / 5 çift) aranır.

## I. Paket ve teslim

```
Galaxy101_ClaudeCode/
  START.md                          v0.5.0; okuma sırası, çalıştırma, Claude Code'a ilk mesaj
  MANIFEST.json                     her dosya: yol, boyut, sha256, tür
  galaxy101_3d_tum_temalar/         bütün GLB'ler (ortak + 5 tema klasörü)
  prototip/                         tek güncel prototip, vendor/ dahil, çevrimdışı çalışır
    galaxy101_handoff_v5/
      README.md  CHANGELOG.md  changes.json  FEATURES.md  OPTIONS.md  options.json
      MIGRATION.md  RULES.md  DECISIONS.md  OPEN_QUESTIONS.md  REMOVED.md  DEMO_LIST.md
      UNITY_KISITLARI.md
      logic/ protocol/ tests/ states/ export/ anim/ strings/ assets/ sound/ screenshots/
```

- Kopya modül olmasın: her JS modülünün tek sürümü bulunsun. v0.4.0'dan kalan, artık kullanılmayan dosya da olmasın; gerekiyorsa `archive/` altına.
- `// DEMO:` işaretli her kod `DEMO_LIST.md`'de olsun, gerçek sunucudaki karşılığıyla.
- Paket internet gerektirmesin.

## J. Teslimden önce doğrula ve raporla

Teslimden önce şunları doğrula:
- `states.json`'daki her etkin durum adresle açılıyor; aynı adres iki kez **piksel olarak aynı** kareyi veriyor. Yıldız alanı, gezegen ve geri sayımlar dahil.
- Görünür her öğenin `data-g101-id`'si var; `layout-all.html` bütün durumları döküyor.
- `rules.test.html` / `runner.js` ve `server.tests.js` tamamen yeşil.
- Gömülü metin yok. Seed'li RNG dışında `Math.random` yok. `mockServer.js` dışında durum değişikliği yok. EN'de çevrilmemiş anahtar yok.
- `FEATURES.md`'deki her özelliğin izi tam; `OPTIONS.md`'deki her seçeneğin varsayılanı tek.
- `MANIFEST.json` paketteki dosyalarla birebir. `states.json` durum sayısı = `screenshots/index.json` durum sayısı.

Rapor şu sayıları versin: durum (etkin / yeni / değişen / kaldırılan), kural, vektör, sunucu akışı, protokol mesajı ve hata kodu, metin anahtarı, animasyon, ses olayı, ikon, rozet, imleç ve GLB. Raporda ayrıca D bölümünün her maddesinin sonucu, E bölümünün her maddesinin durumu (eklendi / tasarım yok) ve `OPEN_QUESTIONS.md` özeti bulunsun.

---

## Claude Code tarafı (sana not — Claude Design'a verme)

Paket gelince eskisini silmeden `Design/Galaxy101_ClaudeCode/` yerine koy; v0.4.0 git geçmişinde duruyor. Sonra Claude Code'dan bir entegrasyon promptu iste (`PROMPT_v4_OTONOM_GELISTIRME.md`'nin v5 hali). İçeriği kabaca şu sırada olacak:

1. `CHANGELOG.md`, `changes.json`, `MIGRATION.md` ve `REMOVED.md` okunur, mevcut kodla fark matrisi çıkarılır.
2. Kural motoru `ruleVersion`'a göre güncellenir; yeni vektörler geçirilir.
3. Protokol ve sunucu güncellenir; yeni akışlar xUnit'e eklenir.
4. Yeni referans kareleri çekilir ve durum yönlendiricisi yeni durumlara genişletilir.
5. Ekranlar ekran ekran eşitlenir.
6. Ses, 3D, metin ve tema varlıkları içe aktarılır.
7. Uçtan uca test, ardından rapor.

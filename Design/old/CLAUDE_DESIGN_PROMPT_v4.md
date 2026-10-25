# Claude Design'a verilecek prompt — Galaxy 101 teslim paketi v4

Aşağıdaki çizgiden sonrasını olduğu gibi Claude Design'a yapıştır. Sonundaki "Claude Code tarafı" bölümü sana not; onu Claude Design'a verme.

---

## Bağlam

Galaxy 101 (101 Okey, 4 oyuncu) için yaptığın çalışan prototipi (`Galaxy 101 - Oyun.dc.html` + `G101-Menu`, `G101-Lobi`, `G101-Masa`, 3D modüller) Claude Code, Unity 6 (URP, UI Toolkit) istemcisine ve sunucu otoriteli bir .NET sunucusuna taşıyor. Önceki teslim paketi (`galaxy101_unity_handoff`, v1→v3) Unity'de **farklı görünen ve farklı işleyen** bir oyun üretti. Nedenleri:

1. Spec dosyaları prototipin kayıplı özeti; değerler HTML'de, spec ile HTML çelişiyor, Claude Code 195 KB'lık HTML'den tersine mühendislik yapıyor.
2. Ekran görüntüleri 924×540 JPG (0,72 ölçek, kayıplı), bir kısmı HTML'in eski sürümünden. Piksel karşılaştırması yapılamıyor.
3. Prototipteki oyun mantığı gerçek bir oyun değil: rakiplerin gerçek ıstakası yok, bot atışları o an "görülmemiş taş havuzundan" rastgele üretiliyor (`poolTake`, `_gone`), açmış rakibin el puanı sabit 34, XP sabit (+340/+60), `Math.random` her yerde. Gerçek sunucu kurulunca davranış doğal olarak farklılaşıyor.
4. Sunucu tarafı hiç tanımlı değil ("sunucu yok, mock"). Zamanlayıcılar, gizli bilgi, botlar, ekonomi, lobi, davet Claude Code tarafından tahminle kuruldu (ör. dağıtım töreni toplam süresi spec'te yok; sunucu 22 sn varsaydı).
5. Sürümler üst üste katmanlandı: v3 "en yeni belge geçerli" diyor ama bir kuralı anmamak onu kaldırmak mı belli değil (spec/17'de 5 ceza + ×8'e kadar çarpanlar var, spec/22'de 2 ceza + yalnız elden bitiş ×2). spec/22'deki puan formülü çarpanı iki kez uyguluyor (bitiren −404 çıkıyor), kod −202 veriyor.
6. v3 metinleri (giriş, lobi, dağıtım, cezalar, görüntü ayarları) CSV'de yok, yalnız HTML'e gömülü. Aynı JS modülünün iki farklı sürümü var (`reference/table/table3d.js` 73 KB, `reference/game/table3d.js` 100 KB). Ses ayarları var, ses olayları tanımı yok.

**Amaç:** Claude Code'un hiçbir değeri tahmin etmeden, prototiple Unity'yi otomatik olarak yan yana ölçerek birebir kurabileceği, sunucuyu da prototipin sahte sunucusundan birebir türetebileceği **tek kaynaklı, makine tarafından okunabilir** bir v4 paketi üret. Tasarımı değiştirme; prototipi bu kurallara göre yeniden düzenle ve paketle.

## A. Prototipi gerçek oyun yap (mantık)

1. **Kural motorunu ayır.** Tüm 101 kuralları tek bir saf modülde olsun: `logic/rules.js` (DOM, three.js, zamanlayıcı, `Math.random` yok; rastgelelik dışarıdan verilen seed'li RNG ile). Arayüz yalnızca bu modülü çağırır. Claude Code bu dosyayı satır satır C#'a çevirecek, o yüzden saf fonksiyonlar ve düz veri yapıları kullan.
2. **Sahte mantığı kaldır.** 106 taş gerçekten karılıp dağıtılsın; her rakibin gerçek ıstakası olsun; botlar kendi elinden çeksin, atsın, açsın, işlesin, bitirsin. `poolTake`, `_gone`, sabit el puanı 34, sabit XP, "botlar %60 işlek taş atar" gibi kısayolları kaldır. Test senaryoları için gereken hazır eller `logic/scenarios/*.json` dosyalarından yüklensin. Bir demo kısayolu kalmak zorundaysa koda `// DEMO:` yaz ve `DEMO_LIST.md`'ye ekle.
3. **Sahte sunucu sınırı.** Arayüz durumu asla doğrudan değiştirmesin. Hesap, ekonomi, mağaza, lobi, masa aç/katıl/izle, bekleme odası, davet, arkadaşlar, liderlik, günlük ödül/görevler, sohbet, hediye, masa hamleleri, el/maç sonucu dahil her işlem `logic/mockServer.js` üzerinden **mesajla** gitsin. Bu dosya gerçek sunucunun prova sürümü: zamanlayıcılar, botlar, dağıtım, zar sonucu, puanlama, bakiye hareketleri orada çalışsın. İstemci yalnızca gelen olayları canlandırsın.
4. **Protokol kataloğu.** `protocol/protocol.md` + `protocol/protocol.json`: her istemci niyeti (C2S) ve sunucu olayı (S2C) için ad, alanlar ve tipleri, kimin gönderdiği, kime gittiği, hata kodları ve her hatanın arayüzde nasıl göründüğü (hangi durum kimliği). Gizli bilgi kuralı açık olsun: rakip ıstakaları, deste sırası, zar sonucu atılmadan önce istemciye gitmez; her oyuncuya kendi görünümü gönderilir. Kopma/yeniden bağlanma: tam durum anlık görüntüsü mesajı ve hangi ekrana dönüleceği.
5. **Zamanlama sözleşmesi.** `protocol/timings.md`: sunucu süreleri (tur süresi, uyarı, dağıtım töreninin toplam süresi ve adımları, el sonu bekleme "Sonraki el · 15", davet 30 sn, bekleme odası geri sayımı, bağlantı kopma toleransı, ardışık zaman aşımında otomatik oyun) ile istemci animasyon süreleri ayrı sütunlarda. Animasyon sürerken yeni olay gelirse ne olur (kuyruğa al / hızlandır / atla) her animasyon için yazılsın.
6. **Bot davranışı.** `logic/bots.js` (yine saf): ne zaman açar, neyi işler, hangi taşı atar, düşünme süreleri, sohbet olasılıkları. Botlar sunucuda aynı kural modülünü kullanacak.
7. **Altın test vektörleri.** `tests/rules/*.json`: her biri başlangıç durumu + seed + hamle dizisi + beklenen sonuç (izin/ret ve hata kodu, ıstaka, perler, tahta yerleşimi, puanlar, cezalar). En az şunlar: dağıtım (dağıtan, balyalar, iki zar, gösterge, sahte okey göstergesi, fazla taş, 22/21), 2 kopya sınırı, seri/grup/çift tanıma (okey ve sahte okey dahil, 12-13-1 sarması), okeyin iki uca işlenmesi, okey alma, 101 açma, 5 çift açma, hatalı açma, işlek taş atma, geri topla, soldan alıp geri bırakma, elden bitiş, normal bitiş, taş bitti, eşli puanlama, süre dolunca otomatik oyun, masadan kalkma, maç sonu sıralaması ve berabere. `tests/rules.test.html` hepsini koşup yeşil/kırmızı göstersin. Claude Code aynı JSON'ları C# NUnit ve sunucu testlerinde koşacak.
8. **Kural belirsizliklerini kapat.** `RULES.md` tek karar tablosu; her satırda kural, sayılı örnek ve test vektörü kimliği. Mutlaka cevapla:
   - El puanı formülü tam olarak: çarpanlar (elden ×2, okeyle bitiş, çift açan, çiftle bitiş) nasıl birleşir, cezalar çarpılır mı, bitiren kaç alır.
   - spec/17'deki cezalar (okey atma, açtıran taş ×10, alınan okey, eksik açılış) v3'te geçerli mi? Geçersizse "kaldırıldı" diye yaz.
   - Elde kalan okeyin ve sahte okeyin puan değeri; açmış oyuncunun kalan taş toplamı nasıl hesaplanır.
   - Soldan alınan taşla açma zorunluluğu var mı; açmadan soldan alma koşulları.
   - Aynı turda işle + bitir; yalnız bir taşla bitiş; ilk oyuncunun 22 taşla atışı.
   - Eşli masa: takım puanı, ortak tahta, ortağa işleme.
   - Masadan kalkan oyuncunun puanı, bahsi, yerine giren botun kimin adına oynadığı.
   - Katlamalı ve "ceza kuralları", "işleme", "izleyici", "sohbet (açık/hızlı/kapalı)" masa seçeneklerinin oyunda tam olarak neyi değiştirdiği.
   - Ekonomi: bahsin ne zaman düşüldüğü, havuz, XP ve seviye eğrisi, ödül miktarları.

## B. Görsel birebirlik (ölçülebilir)

1. **Durum kataloğu.** `states/states.json`: her ekran, pencere, sekme, diyalog, hata/boş/yükleniyor durumu ve masa anı için kalıcı kimlik (mevcut S01–S15, G01–G34, T…, 01–97 kimliklerini koru, yenilerini ekle), açıklama ve açma yolu. Her durum URL ile doğrudan açılsın: `Galaxy 101 - Oyun.dc.html?state=G15&theme=saturn&lang=tr&seed=42&t=1200`.
2. **Deterministik ve dondurulabilir.** Bütün animasyonlar (CSS, JS tween, three.js, yıldız alanı, gezegen dönüşü) tek bir ortak saatten beslensin; `t` parametresi o milisaniyede kareyi dondursun. `Math.random` yalnız seed'li RNG üzerinden. Aynı URL her açılışta piksel olarak aynı kareyi vermeli. Tweaks paneli kapalı açılsın (`?tweaks=0`) ve kesinleşmiş değerler varsayılan olsun.
3. **Kalıcı öğe kimlikleri.** Anlamlı her öğeye `data-g101-id` ver (ör. `menu.playNow`, `menu.playNow.label`, `lobby.card[0].seat[2]`). Claude Code Unity'de eleman adlarını bunlarla aynı yapacak.
4. **Yerleşim dökümü.** `?state=…&dump=1` açılınca her `data-g101-id` öğesi için JSON indir: ekran kutusu (`getBoundingClientRect`, 1280×720 tasarım birimi), hesaplanmış stiller (renk, degrade katmanları, font ailesi/ağırlığı/boyutu, satır yüksekliği, harf aralığı px, padding, border, radius, box-shadow katmanları, opacity, transform, z-index, filter). Hepsini toplu indiren `export/layout-all.html` sayfası olsun.
5. **Animasyon dökümü.** `anim/*.json`: her animasyon için tetikleyici olay, hedef (`data-g101-id` ya da 3D obje adı), özellik, keyframe'ler, süre, gecikme, eğri (cubic-bezier sayıları), tekrar, kademe. CSS `@keyframes`, JS tween'ler ve three.js animasyonları hepsi.
6. **Unity'ye çevrilemeyen CSS.** `UNITY_KISITLARI.md`: `backdrop-filter`, `filter` (blur, drop-shadow, saturate, brightness), `mix-blend-mode`, `conic-gradient`, metin degradesi, `mask`, `clip-path`, `grid`/`gap`, metin gölgeleri — her kullanım yerini `data-g101-id` ile listele ve tam parametrelerini yaz. Mümkünse tasarımı bu efektler olmadan aynı görünecek şekilde çiz (katmanlı öğe, önceden hazırlanmış sprite); olmuyorsa beklenen görünümü tarif et.
7. **Ekran görüntüleri.** Her durum için 1280×720 ve 2560×1440 **kayıpsız PNG**, varsayılan Satürn + TR; ek olarak her durumun EN hali ve seçili durumlarda diğer 4 tema. Dosya adı durum kimliği. Eski JPG'ler `archive/` altına.
8. **Varlıklar ayrı dosya.** Tüm ikonlar `assets/icons/<id>.svg` (HTML'e gömülü değil), sprite/degrade görselleri @2x PNG, font listesi (aile, ağırlık, sürüm, glif seti), 3D: dışa aktarma sayfası `?autoexport=all` ile tek komutla (insan tıklaması olmadan) 28 GLB'yi indirsin; malzeme parametreleri `assets/materials.json`; prosedürel dokular (gezegen, çuha, holo halka, taş yüz atlası) seed ve parametreleriyle üretilebilir olsun.
9. **Metinler.** Tek `strings/strings.csv`: anahtar, tr, en, bağlam, durum kimliği, yer tutucular (`{name}`, `{n}`), en fazla uzunluk. Prototipteki bütün metinler buradan okunsun; HTML'de gömülü metin kalmasın (v3 giriş, lobi, dağıtım, ceza, görüntü ayarları metinleri dahil). Sayı/para/yüzde biçimleri `strings/formats.md`.
10. **Ses.** `audio/audio_events.csv`: olay kimliği, tetikleyici (protokol olayı ya da animasyon anı), kanal (müzik/efekt/taş/mesaj, ayarlardaki kaydırıcılarla eşleşen), süre, karakter tarifi. Dosya yoksa yer tutucu olarak işaretle.
11. **Platform.** Hedef cihazlar ve en-boy oranları (16:9, 16:10, 21:9; mobil hedefse 19.5:9 ve 4:3), mobil/dokunmatik varsa ayrı durumlar, güvenli alan, dokunma hedef boyutları, klavye kısayolları.

## C. Paket düzeni

```
galaxy101_handoff_v4/
  README.md            okuma sırası, prototip nasıl çalıştırılır
  SPEC.md              tek birleşik belge (v1/v2/v3 katmanı yok, çelişki yok)
  RULES.md             kural karar tablosu (A.8)
  CHANGELOG.md         v3'e göre her değişiklik + etkilenen durum ve data-g101-id'ler
  REMOVED.md           kaldırılan her özellik/kural/metin açıkça (anılmamak kaldırılmak değildir)
  DECISIONS.md         senin seçtiğin, kullanıcıya sorulmamış değerler
  OPEN_QUESTIONS.md    kullanıcının karar vermesi gerekenler
  DEMO_LIST.md         yalnız prototipe ait sahte veri/kısayollar (1234 şifresi, GX-7Q2M, demo adlar, sahte gecikmeler)
  prototype/           tek güncel prototip (kopya modül yok)
  logic/               rules.js, bots.js, mockServer.js, scenarios/
  protocol/            protocol.md, protocol.json, timings.md
  tests/               rules/*.json, rules.test.html
  states/              states.json
  export/              layout-all.html, 3D dışa aktarma
  anim/  strings/  audio/  assets/
  screenshots/         <durum>@1x.png, <durum>@2x.png
  archive/             v1–v3 (yalnız tarihçe)
```

## D. Teslimden önce kendini doğrula ve raporla

- `states.json`'daki her durum URL ile açılıyor, aynı URL iki kez aynı kareyi veriyor.
- `dump=1` ve `layout-all.html` çalışıyor; her görünür öğenin `data-g101-id`'si var.
- `rules.test.html` tamamen yeşil.
- `strings.csv` dışında metin, seed'li RNG dışında `Math.random`, `mockServer.js` dışında durum değişikliği yok.
- `DEMO_LIST.md` dışında demo kısayolu yok.
- Raporda: tamamlananlar, yapılamayanlar ve nedeni, `OPEN_QUESTIONS.md` özeti.

Bir değeri bilmiyorsan uydurma: `OPEN_QUESTIONS.md`'ye yaz ve bana sor.

---

## Claude Code tarafı (sana not — Claude Design'a verme)

v4 paketi gelince Claude Code'a kabaca şunu ver:

> `Design/galaxy101_handoff_v4/` geldi. Önce `CHANGELOG.md`, `REMOVED.md`, `RULES.md`, `protocol/`'ü oku ve mevcut kodla farkı çıkar. Sonra: (1) `tests/rules/*.json` vektörlerini `com.okey101.core` NUnit ve sunucu testlerine bağla, kırmızıları düzelt; (2) `protocol.json`'u `Shared/Galaxy101.Contracts` ile eşle, sunucuyu `mockServer.js` + `timings.md` davranışına getir, botları `bots.js`'ye göre; (3) `states.json`'daki her durum için prototipi `?state&seed&t` ile Playwright'ta, Unity'yi aynı durumda çekip piksel farkı ve `layout` dökümüyle eleman eleman karşılaştıran otomatik bir karşılaştırma koş; (4) `strings.csv`'yi Localization'a, `anim/*.json`'u tween tanımlarına aktar. Uydurma değer yok; eksikleri `IMPLEMENTATION_NOTES.md`'ye ve bana bildir.

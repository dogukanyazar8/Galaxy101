# Okey 101 — İlk oynanabilir Windows sürümü tasarımı

Tarih: 2026-10-20. Durum: Tasarım ve teslim aşamaları kullanıcı tarafından topluca onaylandı; uygulama başlamadı.

Onaylı oyun sözleşmesi: [101-v1.md](../../rules/101-v1.md).

Dosya ve test adımları: [Windows uygulama planı](../plans/2026-10-20-windows-first-playable.md).

## 1. Amaç ve başarı ölçütü

Windows'ta bir insan ve üç yerel botun, onaylı 101-v1 kurallarıyla baştan sona bir el oynayabildiği; çekme, dizme, açma, işleme, bitiş ve sonuçları kullanıcı tarafından denenebilen sürüm. Masa 3D, arayüz 2D. Sonraki hedef internet üzerinden özel oda, ardından Android ve iOS.

Başarı, yalnızca sahnenin açılması değildir: doğru puanlanmış tamamlanmış el, bitirensiz deste sonu, dört çift iptali, tekrar üretilebilir hata senaryosu ve gerçek Windows çalıştırılabilir dosyası gerekir.

Kesin kararlar: platform sırası, yerel botlar, tek el, 3D masa + 2D arayüz, 101-v1.
Onaylı tasarım kararları: aşağıdaki görsel yön, yerleşim, teknik ayrım ve teslim aşamaları. Yeni oyun kuralı eklemez.

## 2. Çalışma alanı ve teknoloji

Yerel gözlem: proje klasörü başlangıçta boştu; Git deposu yok. Unity Hub altında 6000.6.6f1 kurulu; Unity.exe sürümü doğrulandı. .NET SDK 10.0.102 kurulu. Unity lisansı, Windows build modülü, URP paketlerinin erişilebilirliği ve cihaz performansı henüz doğrulanmadı.

- Başlangıç motoru: kurulu Unity 6000.6.6f1; sürüm değişimi gerekirse önce somut derleme/çalışma engeli gösterilir.
- Görüntüleme: URP, tek sabit kamera, Windows x64 ve başlangıçta Direct3D 11.
- Arayüz: uGUI; tek çalışma zamanı UI sistemi. Girdi Unity Input System üzerinden eylemlere çevrilir.
- Çekirdek: UnityEngine bağımlılığı olmayan C#; Unity .NET Standard 2.1 API yüzeyine uygun.
- İlk test altyapısı: Unity Test Framework; kural testleri EditMode, etkileşim testleri PlayMode.
- Motor ve paket sürümleri oluşturulan projede ProjectVersion.txt, manifest.json ve packages-lock.json ile sabitlenir. Sürümler tahmin edilerek yazılmaz; kurulu editörle uyumlu resmi şablondan çözülür.
- İlk sürümde ağ SDK'sı, veritabanı, hesap sistemi, ücretli eklenti veya yeni tween bağımlılığı yoktur.

Seçenekler: Unity sahnesine gömülü kurallar hızlı ilk görüntü sağlar fakat test ve sunucu geçişini zorlaştırır. Saf C# çekirdek + Unity sunumu daha net doğrulama sağlar; seçilen yaklaşım budur. Şimdiden ağ sunucusu kurmak yerel masa kilometre taşına gereksiz kapsam ekler.

## 3. Görsel yön ve ekranlar

Önerilen yön: koyu yeşil keçe, sıcak koyu ahşap, kırık beyaz taşlar, sınırlı pirinç vurgular. Taş sayıları ve seçim durumu dekorun önünde gelir. Parlak efektler, kamera dolaşması, insan karakterleri ve hareketli çevre ilk sürüme dahil değildir.

Ekran akışı: Ana Menü / Yerel Oyna, Masa, Duraklat-Ayarlar katmanı, Sonuç. Test paneli yalnızca geliştirme yapısında açılır.

| Ekran | İçerik ve temel davranış |
|---|---|
| Ana menü | Yerel Oyna, Ayarlar, Çıkış; hesap/lobi taklidi yok |
| Masa | Dört oyuncu, açılış türleri, taş adetleri, gösterge, deste adedi, son atıklar, ayrı seri/çift alanları |
| Duraklat | Devam, ses, pencere/tam ekran, animasyon azaltma, Ana Menü; bekleyen bot işi de durur |
| Sonuç | Bitiş nedeni, N/J/P/B/M kırılımı, her oyuncunun sonucu, Yeniden Oyna, Ana Menü |
| Test paneli | Senaryo seç, dağıtım kimliği, bot adımlat, otomatik oynat/durdur, yerel hata kaydı dışa aktar |

1920×1080 referans yerleşim:

- Üst yaklaşık %8: el bilgisi, gösterge, sıra durumu, duraklatma.
- Orta yaklaşık %60: masa grupları; seri/sayı grupları ve çift alanı görünür biçimde ayrılır. Bot profilleri karşı ve yan kenarlardadır.
- Alt yaklaşık %24: insanın iki sıralı ıstakası; her sıra 12 yuvalı, toplam 24 yuva, en fazla 22 elde taş.
- Alt yaklaşık %8: Diz, Açılışı Hazırla, İşle, Düzenlemeyi Geri Al, Turu Onayla eylemleri. Atılacak taş açıkça işaretlenir.
- Çok grup oluştuğunda merkez alan kontrollü kaydırma ve grup yakın görünümü sunar; taşları okunamayacak kadar küçültmez. Kamera yönü değişmez.

Taşlar, ıstaka ve masa grupları 3D nesnelerdir. Düğmeler, seçim/uyarı katmanları ve araç ipuçları uGUI'dir. Kamera hafif eğimli, sabit ortografik olur; kesin açı gerçek sahnede okunabilirlik kontrolüyle ayarlanır.

1280×720, 1920×1080, 2560×1440 ve 2560×1080 kontrol edilir. 720p'de insan taşının sayı yüksekliği en az 24 ekran pikseli hedeflenir. Renk ayrımına küçük, tutarlı renk simgeleri de eşlik eder. Sonuç/uyarılar yalnızca renkle anlatılmaz. Türkçe karakterler kontrol edilir.

Blender: ortak taş ağı, ıstaka, masa ve yakın dekor. İlk mekanik sürüm Unity temel geometrileriyle çıkar. Sonrasında düşük ayrıntılı modeller FBX olarak aktarılır; birim ölçeği, eksenler ve malzemeler Unity'de doğrulanır. Blender kurulumu bu incelemede kontrol edilmedi. Varlıklar için üretim kaynağı/lisans kaydı tutulur.

## 4. Etkileşim ve kesinleştirme

- İlk kullanıcı sırası birden fazla tık bekleten öğretici yerine kısa görev metni gösterir: Taş al, hamleni hazırla, taş at.
- Taşlar tıklayarak seçilebilir; sürükleme yanında tıkla-seç/tıkla-hedef yöntemi de bulunur. Klavye kısayolları tek erişim yolu değildir.
- Dizme yalnızca ıstaka yerleşimini değiştirir; otomatik açılış veya bağlayıcı oyun hamlesi yapmaz.
- Açılış önizlemesi toplam/çift adedini ve her okey karşılığını gösterir. Geçersiz hedefte kısa neden görünür; taş kaybolmaz.
- Kapalıdan veya yandan çekiş doğrulanıp kesinleşir. Bundan sonraki masa işlemleri tur taslağı üzerinde hazırlanır.
- Taslakta açılış, işleme ve okey değişimleri sırasıyla doğrulanır; aynı tur açıldıktan sonra işleme mümkündür.
- Turu Onayla, seçilmiş atış dahil bütün taslağı tek işlem olarak doğrular. Başarısızlık kalıcı oyun durumunu değiştirmez. Çekiş geri alınmaz; masa taslağı düzeltilebilir veya sıfırlanabilir.
- Yandan alma yalnızca R10'u karşılayan en az bir tamamlanabilir tur varsa kullanılabilir. Bu uygunluk strateji tahminiyle değil kesin kural aramasıyla belirlenir. Geri alınan okeyin elde kalması, yandan alınan fiziksel taş masaya konmuşsa uygundur.
- Cezalı atışta ceza miktarı gösterilir; onay olmadan tur tamamlanmaz. Geçersiz hamleye onay seçeneği sunulmaz.
- Animasyon geçerli durum değişimini gösterir; geçerlilik kaynağı değildir. Tur geçişi sırasında çift giriş engellenir. Azaltılmış animasyonda aynı sonuç oluşur.
- Başlangıç hareket süreleri çekme/yerleştirmede 150–220 ms aralığıdır; sahnede his testiyle ayarlanır. Giriş kilidi sabit bekleme süresine değil animasyonun tamamlanmasına bağlanır.
- Devam eden elde Ana Menü veya Yeniden Başlat seçilirse mevcut elin kaybolacağı açıkça belirtilip onay alınır. Duraklat-Devam ilerlemeyi korur; uygulamayı kapatıp devam etme desteği vaat edilmez.

## 5. Çekirdek mimarisi

Tek yerel oyun oturumu bütün özel duruma sahiptir. İnsan arayüzü ve botlar yalnızca kendi görünümlerini alır; oyun motoruna aynı yetki kontrollerinden geçen komutlar gönderir.

| Birim | Sorumluluk |
|---|---|
| Core/Model | Fiziksel TileId, taş yüzü, oyuncu, açılış türü, grup, sabit okey karşılığı |
| Core/Rules | Grup doğrulama, açılış, işleme, okey değişimi, yandan alma uygunluğu |
| Core/Turns | Sıra/evre, çekiş, tur taslağı, atomik onay, işleme bütçesi, bitiş önceliği |
| Core/Scoring | N/J/P/B/M kırılımı; bitiş, deste sonu ve iptal |
| Core/Random | Sürümlü deterministik karıştırma ve tekrar oynatılabilir dağıtım |
| Application | Yerel oturum, oyuncuya özel görünüm, komut sırası, bot yaşam döngüsü, tekrar kayıtları |
| Bots | Yalnızca oyuncuya özel görünümle karar üretimi |
| Presentation | 3D görünüm, uGUI, giriş, ses ve animasyon; kural kararı vermez |

Her fiziksel taşın ayrı kimliği vardır. Aynı yüzlü iki taş farklı kimlik taşır. Çift işlem, çoğaltılmış kimlik, masadan izinsiz taş alma ve aynı taşa iki kullanım reddedilir.

Özel durum dışarıya değiştirilebilir referanslarla verilmez. Komutlar oyuncu kimliği, beklenen durum sürümü ve benzersiz komut kimliğiyle değerlendirilir. Önceki tura ait veya yinelenen komut ikinci kez uygulanmaz. Bu, ilk sürümde yereldir; ağ hizmeti kurulmaz.

Mantıksal durumlar: çekiş bekleniyor, tur planlanıyor, sonuçlandı, iptal. İlk oyuncunun 22 taşlık ilk turu doğrudan planlamayla başlar. Duraklatma uygulama düzeyindedir; kural durumunu değiştirmez.

Tur onayında son atışla bitiş önce kontrol edilir; bitiş varsa cezalı atış veya dört çift iptali uygulanmaz. Bitiş yoksa tur cezası yazılır; dört çift varsa iptal; değilse son kapalı taş turu ise bitirensiz sonuç; aksi halde sonraki oyuncu.

Sürüm etiketli başlangıç durumu/dağıtım ve kabul edilmiş komutlar tekrar kaydına girer. Sadece rastgele seed kaydetmek yeterli kabul edilmez: deste dizilimi, başlangıç oyuncusu, kural ve karıştırma sürümü de korunur. Görsel rastgelelik bu akışı etkilemez. Kayıtlar yerel geliştirme verisidir; sonraki online sürümde özel deste bilgisi istemciye gönderilmeyecektir.

## 6. Bot davranışı

Amaç güçlü rakip değil, kuralları bozmadan kendi elini azaltabilen üç rakiptir.

- Önce tamamlanabilir ve faydalıysa yandan al; değilse kapalıdan çek.
- Geçerli açılış adayları, yeni grup, işleme ve okey değiştirme adayları üret.
- Bitiren geçerli turu önceliklendir. Sonra kalan ceza değerini azalt; eşitliklerde sabit, tekrar üretilebilir seçim sırası kullan.
- Açılış türünü ilk açılacağı turda seç. Otomatik ve rastgele tür değiştirme yok.
- Seçilen tam turu kural motoruna doğrulat; strateji araması kural doğrulamasını atlayamaz.
- Strateji araması aday/düğüm sayısıyla sınırlanabilir; kesin geçerlilik denetimi zaman aşımı nedeniyle geçersiz hamleyi kabul edemez veya insanın geçerli hamlesini reddedemez.
- Kapalıdan çekişten sonra açmadan atmak geçerli geri dönüş yoludur. Yandan çekişte zorunlu kullanımı sağlayan önceden doğrulanmış tur planı korunur.
- Arama ana görüntü döngüsünü bloke etmez. Duraklatma, yeni el ve menüye dönüşte eski karar işi iptal edilir; durum sürümü uyuşmayan sonuç uygulanmaz.

Botun minimum yeterliliği ayrı senaryolarda seri açma, çift açma, iki taş bütçesine uyma, yandan aldığı taşı kullanma ve son taşla bitmedir. Rastgele atış yapan bot bu ölçütü sağlamaz.

## 7. Hedef dosya düzeni

Bu ağaç gelecekte oluşturulacak yapıyı gösterir; henüz ürün dosyası oluşturulmadı.

```text
Assets/Okey101/
  Application/
  Bots/
  Presentation/{Input,Table,UI,Audio}/
  Scenes/{Bootstrap,Table}.unity
  Prefabs/{Tiles,Table,UI}/
  Art/{Models,Materials,Textures}/
  Tests/{EditMode,PlayMode}/
  Editor/BuildWindows.cs
Packages/com.okey101.core/
  Runtime/{Model,Rules,Turns,Scoring,Random}/
  Tests/Editor/
docs/rules/101-v1.md
docs/superpowers/specs/2026-10-20-windows-first-playable-design.md
```

Saf C# çekirdek tek kaynak kümesidir; Unity referansı kapalı assembly tanımıyla ayrılır. İleride sunucuya geçerken kurallar kopyalanıp ikinci kez yazılmaz. İlk sürümde ağ/kalıcılık için boş servis katmanları oluşturulmaz.

## 8. Teslim aşamaları

| Aşama | Kullanıcının deneyebileceği çıktı | Geçiş koşulu |
|---|---|---|
| A0 — Temel masa | Windows'ta açılan geçici 3D masa; dağıtım, ıstaka dizme, çekme/atma | 106 taş korunuyor; dört oyuncu sırası; ilk 22 taş turu; temel giriş çalışıyor |
| A1 — Kurallı masa | Hazır ellerle açılış, işleme, değişim, bitiş ve puanlama | R01–R17 test eşlemesi tamam; sınır durumları doğru; hatalı tur atomik reddediliyor |
| A2 — İnsan + 3 bot | Menünün başlattığı, doğal biçimde tamamlanabilen tek el | Bot minimum yeterlilikleri; gizli bilgi sınırı; 1.000 sabit dağıtımda kurallara uygun bitiş/iptal/deste sonu |
| A3 — Masa hissi | Son görsel yönle tek masa, ses, animasyon ve okunaklı ekranlar | Çözünürlük kontrolleri; sürükleme/tıklama; animasyonda kayıp/çift hamle yok; ilk profil ölçümü |
| A4 — Windows test paketi | Çalıştırılabilir dosya, sürüm bilgisi, sonuç ekranı, test senaryoları, hata kayıt yolu | EditMode ve PlayMode geçişi; paketlenmiş oyunda elle tam el; 30 dakika kararlılık ve profil kontrolü |

A0 ve A1 ara sürümdür; tüm onaylı kurallar bitmeden ilk oynanabilir sürüm tamamlandı denmez. Her aşama mevcut doğrulamaya eklenir; yalnızca yeni değişiklik veya hata gerekçesi varsa pahalı kontroller tekrarlanır.

Bu aşama planı teslim sırasıdır. Dosya ve test adımı düzeyindeki uygulama planı, yazılı tasarım incelemesinden sonra ayrı belgeye çıkarılır.

## 9. Doğrulama ve kabul

Kural test matrisi: her R kimliği en az bir ilgili senaryo/test grubuyla eşleşir. Test adları ve sabit başlangıç durumları ayrıntılı uygulama planında verilir.

Öncelikli örnekler:

- Açılış 100 reddedilir, 101 kabul; dört çift reddedilir, beş kabul. İşlenen taş açılışa sayılmaz.
- 12-13-1 ve yinelenen renkli sayı grubu reddedilir; iki joker ve iki jokerden çift kabul edilir.
- Seri oyuncu çift alanına işlemeden önce masada çift açılışı gerekir; açılış türü değişmez.
- Çift oyuncunun farklı gruplara toplam üçüncü taşı reddedilir. Değişim hakkı tüketir, çift ekleme tüketmez.
- Aynı yüzlü yanlış fiziksel taşı elde bırakmak R10'u sağlamaz. Yandan alınan gerçek fiziksel taş kullanılmış olmalıdır.
- Okey karşılığı sabit kalır; değişim sonrası elde bekleyebilir. Geçersiz değişimde hiçbir taş kaybolmaz.
- Son taşı atmadan sıfıra inme reddedilir; bitiş taşı işlek olsa da ceza yoktur.
- R12 örnekleri ve ×1/×2/×4/×8 birleşimleri; P'nin çifte açma ve bitiş çarpanlarına doğru sırayla girmesi.
- Son kapalı taşla aynı tur bitiş, bitirensiz son, dört çift iptali öncelikleri.
- Yinelenen komut, eski bot sonucu, çift tık, duraklatılmış animasyon, menüye dönüş ve yeni el.
- Her adımda 106 benzersiz taşın tek konumda bulunması; gösterge, deste, eller, masa ve atık toplamının korunması.
- Aynı başlangıç durumu ve kabul edilmiş komutlar aynı son durum/puanı üretir; animasyon hızı sonucu değiştirmez.
- Bot görünümünde rakip taş yüzü veya kapalı deste sırası bulunmaz.

Yerel 60 FPS / 1080p hedeflenir; kanıt başlangıçta seçilecek test bilgisayarının CPU/GPU/RAM, kalite ayarı ve paketlenmiş yapısında kaydedilir. Garanti veya mobil kabulü sayılmaz. Dağıtım sonrası ısınmış 10 dakikalık oyunda p95 kare süresi hedefi 16,7 ms'dir; kalıcı takılmalar ayrıca incelenir. A0'da temel donanım kaydı alınır; bu incelemede performans ölçümü yapılmadı.

1.000 dağıtımlık otomatik simülasyon doğruluğun tek kanıtı değildir; hedefli kural testlerine ek tamamlanma ve korunum kontrolüdür. Normal bitiş, bitirensiz son ve iptal sayıları ayrı raporlanır.

Windows build testleri, Editor testleri, görsel kontrol ve gelecek Android/iOS kabulü ayrı raporlanır. İlk Windows paketinin kabulü mobil veya online hazır olduğu anlamına gelmez.

## 10. AI ile kontrollü çalışma

- Her iş: ilgili kural kimlikleri, küçük dosya kapsamı, beklenen davranış, kabul örneği ve doğrulama komutu.
- Önce davranışı yakalayan anlamlı test; sonra uygulama; sonra gözlenen sonuç. Grafik düzenlemelerinde düşük değerli şekil testleri yerine sahne ve cihaz kontrolü.
- Bir seferde tek teslim edilebilir görev. İlişkisiz düzenleme ve bağımlılık ekleme yok.
- Her görev sonunda değişiklik özeti, geçen/başarısız/çalıştırılmayan kontroller ve denenebilir çıktı yolu.
- Botlu hata senaryosu başlangıç durumu ve komut kaydıyla paylaşılır; bir hata için rastgele yeniden deneme döngüsü kullanılmaz.
- Git deposu şu anda yoktur. Sürüm kontrolü ayrı, açık bir başlangıç adımıdır; bu tasarım çalışması init/commit/push yapmaz.

## 11. Sınırlar ve sonraki aşama

İlk sürüme dahil olmayanlar: online özel oda, hesap, arkadaş sistemi, sohbet, mağaza, gerçek para/çip ekonomisi, sıralama, turnuva, çok elli maç, uygulamayı kapatıp oyuna devam etme ve Android/iOS dağıtımı. Ayarlar yerel tutulabilir; oturum kurtarma ilk sürüm sözü değildir.

Online özel oda aşaması A4 sonrasında ayrı tasarımdır: aynı çekirdek sunucuda çalışır; oyuncuya özel görünüm, kimlik doğrulama, yetki, oda yaşam döngüsü, yeniden bağlanma, süre ve kalıcı sonuçlar o aşamada uygulanır. Yerel PRNG ve özel test kayıtları ağ güvenliğinin yerine geçmez.

## 12. Kaynaklar ve mevcut doğrulama

- [Unity 6000.6.6f1 sürüm notları](https://unity.com/releases/editor/whats-new/6000.6.6f1).
- [Unity 6.6 UI karşılaştırması](https://docs.unity3d.com/6000.6/Documentation/Manual/UI-system-compare.html): uGUI çalışma zamanı seçeneklerinden biridir; seçimi proje için bir mühendislik kararıdır.
- [Unity .NET API uyumluluğu](https://docs.unity3d.com/6000.6/Documentation/Manual/dotnet-profile-support.html): .NET Standard 2.1 yüzeyi ve uyum sınırları.

Yapılan kontroller: çalışma alanı envanteri, Git deposu yokluğu, kurulu Unity.exe sürümü, .NET SDK envanteri, resmi sürüm/API dokümantasyonu, belgelerin kural kapsamı ve çelişki incelemesi. Oyun kodu, derleme, otomatik oyun testi, görsel sahne veya performans kontrolü henüz yoktur.

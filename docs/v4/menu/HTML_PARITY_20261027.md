# Galaxy 101 — güncel HTML ve native istemci eşlemesi

Tarih: 27 Ekim 2026. Başlangıç commit'i `d4ed5165ddb8b1179e9b30e5c48553d1614cf583`; çalışma ağacı başlangıçta kirliydi. Mevcut auth, imleç, arka plan ve tasarım değişiklikleri korunarak çalışıldı. Commit, push, deployment ve üretim hesabı işlemi yapılmadı. Test turunun üç canlı UI senaryosu loopback oyun sunucusunda test misafir hesaplarını kullandı.

## Doğrulanan kaynak

`http://localhost:3000/Galaxy%20101%20-%20Ger%C3%A7ek%20Ana%20Oyun.dc` tarafından dönen dosya, `Design/Galaxy101_ClaudeCode/prototip/Galaxy 101 - Gerçek Ana Oyun.dc.html` ile SHA256 olarak aynı: `4a88b75db3b9bfecc1e17bbd6eb5932d0d16a1c4fcb2b2bcd42840543dd9992d`.

`START.md` v0.5.0 kaynağını `prototip` olarak gösteriyor. Bu klasörde 425 dosya var; `full project/Galaxy101_ClaudeCode/prototip` aynı dosyaların kopyası. Dış `full project` ile ortak 405 dosya aynı; farklı 12 ortak dosyada bağımlılık/font URL'leri değişiyor. Yerel sunucunun menüsü `vendor/fonts.css` kullanıyor. Çalışan sayfalar ve gerçek düğme akışları, eski teslim kataloglarıyla çatıştığında esas alındı.

## Ekran ve davranış haritası

Tasarım tuvali 1280×720. Sığdırma oranı ekran boyutlarının en küçüğü; tuval dışı alanlar boş kalır.

| Alan | HTML kaynağı | Mevcut native karşılık |
| --- | --- | --- |
| Açılış, giriş, kayıt, misafir | Gerçek Ana Oyun | AuthFlow; mevcut WIP korundu |
| Ana menü, üç mod, gezegen seçimi | G101-Menu | MenuView.Main; Klasik/Dereceli/Botlara karşı |
| Profil, düzenleme, mağaza, arkadaşlar, liderlik, günlük ödül, kurallar | G101-Menu | Mevcut ayrı panel/controller'lar |
| Ortak altı sekmeli ayarlar | G101-Menu settings-only | SettingsController + SettingsMasa; aynı bileşen masa üzerinde de açılır |
| Lobi, filtreler, masa kurma, bekleme, davet | G101-Lobi | LobbyScreen partial'ları; mevcut gerçek servisler korunur |
| Solo/Duo/Squad, lig ve eşleşme kabulü | G101-Dereceli | RankedScreen; mevcut gerçek servisler korunur |
| Masa, sohbet, ikram, puan, el sonucu, kalkış | G101-Masa | TableGame + TableHud; kurallar ve oturumlar korunur |
| Düz masa | table2d.js | Native TableHud.TwoDimensional; aynı TableGame komutları |
| Gezegen/yıldız kompoziti | planet-renderer/starfield | Mevcut native background + yeni PlanetTransition |
| Üç seyir gemisi, iki avcı | ships3d.js | Özgün GLB'ler + native sabit adımlı uçuş/efekt katmanı |
| Genel imleç ve FPS | g101-cursor / Gerçek Ana Oyun | Yeniden kullanılan animasyonlu imleç; 500ms FPS örneklemesi |

## Bu çalışmadaki ana kararlar

- UI Toolkit, URP, mevcut controller'lar ve sunucu bağlantıları korunur. HTML demo kimlik doğrulama, ödeme veya rastgele hesap verileri üretim davranışı olarak taşınmaz.
- Gerçek masa ayar düğmesi, HTML shell'deki gibi ortak altı sekmeli pencereyi Masa sekmesinde açar. Eski dört sekmeli `OpenSettings` yalnız mevcut durum/diagnostic çağrılarının uyumluluğu için kalır.
- Oyun/ses tercihleri anında uygulanır; görüntü tercihleri ve imleç modu `Kaydet` bekler. Kaynakta görünen footer yalnız `Vazgeç` ve `Kaydet` içerir; kullanılmayan `applyDisp` API'si için ek düğme üretilmedi. Görüntü taslağı kapatılınca atılır. Menü ve masa aynı kanonik oyun/ses tercihlerini okur. Varsayılanlar menüdekidir: `dbl=false`, `music=60`.
- 2D görünüm ayrı oyun veya kural motoru açmaz. Kendi elindeki ve masada açık olan taşları kullanır; rakiplerin gizli elleri yalnız kapalı taş sayısı olarak çizilir. Perspektif dönüşünde oturum ve ıstaka düzeni korunur.
- Beş gemi, oyunun kendi `Araç - 3D Varlık Dışa Aktarma` ekranından GLB olarak alındı. Resources altındaki `.bytes` dosyaları özgün GLB içerikleridir; yaklaşık yeni geometri çizilmedi.
- Native uçuşların ayrı deterministik rastgele akışı vardır. HTML'nin bütün uygulamayla paylaşılan `Math.random` tüketimiyle kare kare aynı rota iddia edilmez. Kamera, kaynak ölçüler, çekim ve geçiş bandı esas alınır.
- Gezegen geçişi kaynak sürelerini kullanır: çıkış950ms, giriş1150ms/gecikme320ms, halka900ms/gecikme900ms. CSS blur/box-shadow native Gaussian yaklaşımıdır; piksel eşitliği ölçüm gerektirir.
- 2D zar, dağıtım, kutlama, ikram ve dört bitiş efekti kaynak zaman çizelgelerini kullanır. Arka plan hareketi tercihi bu oyun efektlerini kapatmaz. Görünüm değişimi ve odak kaybı kendi animasyon yaşam döngüsünü sonlandırır/duraklatır; kaynak CSS `screen` karışımı native alpha yaklaşımıdır.
- Uygulama, build ve tanılama metaverisi kaynak sürümü `0.5.0` ile eşleşir; protokol ve oyun kural sürümleri korunur.
- Lobi/dereceli ekran üstteyken ana menü görünmez tutulur; filtreli ağaç sökülmez. Native Unity hata kaydı ve render-tree sahiplik izi, `display:none` sonrasında iç içe gölge komutlarının bırakılmış texture kimliklerini kullandığını gösterdi. `visibility:hidden`, gizli animasyonların durmasıyla birlikte aynı görünümü korur. Ayrılma cevabı da olayın zaten kurduğu listeyi ikinci kez kurmaz.
- Görüntü ayarlarının ileri/geri okları kaynakta şeffaftır. Eksik `.g-step-arrow` stili tamamlandı; Unity varsayılan gri düğme yüzeyi kaldırıldı.

## Eski katalogla karıştırılmaması gerekenler

Handoff kataloğu151 kayıt,144 etkin,7 kaldırılmış durum içeriyor. `01/06` başlıklarındaki Tekli/Eşli, güncel menünün Klasik/Dereceli modlarıyla aynı anlamda değildir. Tekli/Eşli seçimi Klasik lobidedir.

`S14/S15` eski `wait.friend/invTab` alanlarını kuruyor; güncel davet modeli `wait.inv` kullanıyor. `G32/T15` eski yerel masa ayar penceresini açıyor; gerçek gear düğmesi ortak pencereyi açıyor. Yeni 2D görünüm, gemiler ve `planetSwap` eski animasyon/durum manifestlerinde eksik.

`T33,T40,T41,T44,T45,T46,T48` kaldırılmıştır. Ayrı maç sonu ekranı yerine lobi bildirimi ve dereceli sonuç akışı korunur.

## Kanıt sınırı

Kaynak eşlemesi, bir ekranın piksel veya cihaz üzerinde doğrulandığı anlamına gelmez. Eski `tools/v4/compare_states.py` tam ekran arka plan katmanlarını maskeler; gemi veya gezegen doğruluğu için yeterli değildir. Yeni kanıtlar `artifacts/design-inspection/20261027-html-parity` altında ayrı XML/log/PNG olarak tutulur. Native 2D efektlerinin işleyişi ve zaman sınırları test edildi; her animasyon karesinde CSS/WebGL ile piksel eşitliği doğrulanmadı. Logo girişinin CSS perspektifi artık native perspektif kamera/WorldSpace UI ile çizilir; köşe ve gerçek GPU testleri aşağıdadır. Bütün ekranlarda ve bütün animasyon karelerinde eşitlik beyanı yapılmaz.

## Doğrulama sonuçları

| Kontrol | Sonuç | Kanıt |
| --- | --- | --- |
| Editor: gezegen zaman çizelgesi, shader, gerçek GLB çizimi, uçuş, iptal/temizleme, 2D yerleşim/FX, ortak tercih kuralları | İlk geçişte 77/77 geçti | `editor-verified.xml` |
| Geniş UI turu, yüksek DPI dahil | 135 test; 134 geçti, 1 eski `display:none` beklentisi başarısız | `playmode-green.xml` |
| Aynı görünürlük beklentisi düzeltildikten sonra tüm dereceli testleri | 9/9 geçti | `ranked-final.xml` |
| Benzersiz UI testlerinin en son sonuçları | 135/135 geçti; atlanan test yok | `verification-summary.json` |
| Gerçek loopback sunucu + authored sahne geçişleri | 3/3 geçti: masa kurma/ayrılma, eşli/katlamalı seçenekler, bir bot eli boyunca oyun ve sonuçtan lobiye dönüş | `ClientLiveFlowTests`, geniş UI XML'i |
| Yeni Unity asset metaverisi | İlk geçişte 109 yeni `.meta` GUID'i 32 hex karakter; authored/Resources stil kopyaları aynı | terminal doğrulaması |
| PlayMode kaynak derlemesi | 0 hata | `compile-final` |

İlk başarısızlıklar gizlenmedi veya log-ignore ile bastırılmadı. Lobi hatasının başarısız tekrarları, render-tree/texture sahibi izleri ve yeşil izole tekrar `lobby-*.xml/log`, `ui-owners.xml/log` dosyalarında korunur. Slider testleri anında ses tercihi modeline, ranked test fixture'ı mevcut oturum generation/epoch sözleşmesine güncellendi. 2D test fixture'ındaki açık taşla fiziksel ID çakışması düzeltildi; mevcut el korunur.

`native` altında ana menü, görüntü ayarları, ortak masa ayarları, boş ve gerçek eldeki 2D masa PNG'leri; `high-dpi` altında dokuz 2000×1125 UI görüntüsü bulunur. HTML ana ekranı/görüntü sekmesi ve native karşılıkları görsel incelendi. 144 etkin durumun tümünde piksel karşılaştırması veya WAN/üretim sunucusu testi yapıldığı iddia edilmez.

İlk Windows paketinde runtime glTF shader'larının build sırasında çıkarıldığı bulundu. Beş özgün GLB'nin kullandığı altı shader varyantı Resources malzemeleriyle tutulur; `ShipBuildMaterials.RequireCurrent` kaynak hash'ini ve asset varlığını build öncesi denetler. GLB değişince `Galaxy101/Rebuild Ship Material Variants` çalıştırılır. URP asset güncellemesinin eklediği premultiply bayrağı da kaynak SrcAlpha davranışına eşlenir. `20261027-html-parity-verified` Development paketi 0 hata/0 uyarıyla üretildi. Strict shader eşleşmesi açık pakette `01,12,S09,G13` 1280×720 yakalamaları ve hata kaydı kontrolü geçti; `player-verified` kanıttır. Projenin geçici doğrulama ayarı geri alındı.

## İkinci doğruluk geçişi — devam ediyor

Güncel tarayıcı referansları `fidelity-pass2/ref` altında yeniden çıkarılıyor; 26 Ekim tarihli 144 görüntü güncel kaynak kanıtı olarak kullanılmıyor. Tarayıcı viewport'u 1280×720, devicePixelRatio 1.5; DOM ölçüleri ve normalize PNG'ler birlikte saklanıyor. Native baseline ile ilk ayar/profil karşılaştırmaları, metin kaybı olmadan başlık ve satır yerleşiminde 3–5 px fark gösterdi. Ayar başlığının eksik 1.15 satır yüksekliği, satır adlarının 1.25 yüksekliği ve reset düğmesinin fazla padding'i düzeltiliyor.

Satürn'ün aktif `ringAt` kodu kaldırılmış C halkasını çizmiyor; eski native profil hâlâ çiziyordu. Güncel çift hassasiyetli profil ortak küçük LUT'ye taşındı ve piksel ayak iziyle filtreleniyor. Logo girişine gerçek native WorldSpace UI/perspektif kamera eklendi. Yeni 17 Editor regresyonunun önce başarısızlığı, sonra tamamının geçişi gözlendi. Native perspektif köşe hatası 0.02 px altında; gerçek yazı/gölge framebuffer'ı ve kamera izolasyonu test edildi (`logo-composite.xml`, 3/3).

28 Ekim yerel saatli devam turunda 144 etkin HTML durumunun tamamı yeniden yakalandı. Her durumun PNG, metin ve yerleşim dosyası var; hepsi 1280×720. Kaynak hash'leri ve çıktı hash'leri `fidelity-pass2/reference-manifest.json` içinde. Eski `capture-report.json` önceki başarısız hazırlık denemelerini de içerir; tamamlanmış kapsam için yeni manifest esas alınır.

İzole doğrulama projesi `artifacts/validation-projects/20261027-html-parity-pass2` altındadır; ana Unity Editor ve kullanıcının açık oyunu kapatılmadı. Assets/Packages/ProjectSettings fiziksel kopyadır; Library paylaşılmaz. Özgün altı ıstaka yeniden dışa aktarıldı. GLB'nin kendi Canvas PNG baytları, mip/trilinear/16× anisotropic ve NPOT boyutları korunarak native materyallere bağlandı; yeni geometri çizilmedi. Kaynak/native board yerleşimi 2400 ardışık snapshot'ta aynı sonucu verdi.

3D eylem paneli kaynak dikey düzen, sabit 2×2 komut ızgarası, rack'e göre ölçek, gösterge ve dört bitiş seçimini kullanır. Pasif düğmeler ve panel dolgusu 3D tıklamalarını engeller. “İşle” artık önce açılma moduna uygun perleri yere açar, son taşı korur, sonra işlenebilir taşları ekler; gerçek okey otomatik işlenmez. İlk başarılı hamleden alınan board anchor snapshot'ı geri toplamada ve soldan alınan taşı iade etmede geri yüklenir. Kural motoru ve sunucu ACK'si yetkili kalır; başarısız Lay ACK'sinden sonra Add zinciri durur.

Puan tablosu ortak el sekmeleri ve tüm eller matrisi kullanır. Puan/ceza/toplam sunucu modelinden gelir. Gözlenmiş el açma notları ayrı bir yerel sunum önbelleğinde korunur; kanonik History/View protokolüne alan eklenmez. Yeni maç, farklı masa, silinen geçmiş veya uyuşmayan skor eski notu taşımaz. Gerçek yeniden bağlantıda bilinmeyen geçmiş için “açtı/açmadı” tahmin edilmez. İlgili 28 TableClient testi geçti (`history-tests/history-client-contracts-green.trx`).

Nükleer, kara delik, süpernova ve meteor ayrı kaynak zaman çizelgeleriyle çizilir. Özgün `table3d.js` doğrudan ayrı test sahnesinde çalıştırılarak referans efekt PNG'leri alındı; kanonik HTML değiştirilmedi. Unity HDR içinde biriktirip en son ACES uygularken kaynak her fragmenti ACES/sRGB'ye çevirip 8 bit hedefte biriktiriyordu. Bitiş çizimine özel encoded sRGB pass bu farkı giderir; sadece canlı bitiş düğümleri/partikülleri varken çalışır. Opaque depth korunur. Normal ve additive alpha, özgün vendor'ın One/OneMinusSrcAlpha ve One/One kurallarını kullanır. GPU testi beyaz %50 normal veya iki %25 additive örneğinde 128/255 RGB ve alpha sonucunu, ayrıca opaque engellemeyi doğrular. Paylaşılan zar/ikram efektleri bu değişime dahil değildir.

Native yazıya üç duraklı dikey gradient, SDF glyph quad'larını orta durakta bölerek uygulanır; ölçülen/named Label korunur. Üst/orta/alt gerçek piksel renkleri, gölge, ölçü, metin dump'ında çoğalmama ve temizleme testleri geçti. Mağaza kartındaki eksik üst flex boşluğu, 22px/1.15 satır kutusu, alt yazı/fiyat yüksekliği ve fiyat rengi düzeltildi; giriş animasyonu tamamlandıktan sonra üç sekmenin tüm kartları ölçüldü.

| İkinci tur kontrolü | Gözlenen sonuç | Kanıt |
| --- | --- | --- |
| Ring/logo/rack/board/flight dahil Editor regresyonu | 132/132 geçti | `fidelity-pass2/editor-pass2-green.xml` |
| Yeni development efekt görüntüleme argümanları | 10/10 geçti | `fidelity-pass2/preview-options.xml` |
| Geniş UI turu | 171:169 geçti,1 float zaman hassasiyeti beklentisi,1 isteğe bağlı audit env'si atlandı | `fidelity-pass2/playmode-pass2-final.xml` |
| Oyun/board/mağaza/efekt ve encoded blend odaklı turu | 30/30 geçti | `fidelity-pass2/finish-blend-green4.xml` |
| Native glyph gradient ve gerçek efekt framebuffer'ı | 3/3 geçti | `fidelity-pass2/gradient-finish-gpu.xml` |
| El geçmişi kanonik sözleşmesi ve notlar | 28/28 geçti | `fidelity-pass2/history-tests/history-client-contracts-green.trx` |

Geniş turdaki yeni şok testi, uzun test oturumunda `Time.time` float ULP'si yüzünden 0.000015 alpha farkıyla başarısız oldu. Test, toleransı büyütmek yerine renderer'a geçen temsil edilebilir gerçek elapsed değerini kullanacak şekilde düzeltildi; odaklı tekrar bekleniyor. Atlanan yüksek DPI audit'i ayrıca kendi env'siyle tekrar çalıştırılacak. Yeni Windows paketi ve bütün native durumların karşılaştırması henüz bu tabloya eklenmedi; önceki paketin sonuçları ikinci tur değişikliklerinin kanıtı değildir.

Kaynak katalogdaki `G25…G29` demo “elden bit” eli 89 puanla eşik altında kalır; başlığına rağmen düğme/efekt yolunu üretmez. Bazı `open` demo taşları da motorsuz örnek/tekrar eden fiziksel taş içerir. Gerçek motorun 101 eşiği veya 106 taş bütünlüğü, demo görüntüsüne uymak için gevşetilmez. Efekt görünürlüğü bu sebeple ayrı renderer sahnesinde ve gerçek native GPU testlerinde ölçülür. Rastgele parçacık konumları, bütün sahne ışığı ve bütün animasyon kareleri için piksel eşitliği bu testlerden çıkarılamaz.

## Additional observations

- `medium` — `SettingsController.Account`: mevcut hesap sekmesi sabit `4821 9930` oyuncu kimliği, maskeli demo e-postası ve Google `Bağlı` etiketi içeriyor. Bu etiketler, gerçek sunucudan doğrulanmış hesap bağlantısı kanıtı değildir. Yeni görsel taşıma gerçek Google hesabı bağlama veya ödeme işlemi eklemez; bu mevcut veri bağlama açığı ayrıca ele alınmalıdır.
- `minor` — Teslim durum kataloğunun bazı yolları ve animasyon listeleri güncel çalışan HTML'yi kapsamıyor. Eski katalog üzerinden otomatik ekran açma, gerçek düğme akışını temsil etmeyebilir; üstteki kaynak çatışmaları esas alınmalıdır.

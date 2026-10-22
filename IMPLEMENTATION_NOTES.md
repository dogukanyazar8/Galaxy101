# Galaxy 101 — Unity 1a uygulama notları

Kaynak: `Design/galaxy101_unity_handoff/` içindeki PROMPT, README, yedi spec, JSON/CSV, 54 referans ve 1a HTML/JS. Görsel çelişkilerde HTML 1a esas alındı. 1b/1c kapsam dışı.

## Uygulama

- Unity 6000.6.6f1 / mevcut URP projesi. Yeni menü native UI Toolkit UXML/USS/C#; webview yok. `Assets/Okey101/Scenes/Bootstrap.unity` Galaxy menüsünü açar, mevcut `Table.unity` korunur.
- Yedi panel, beş dialog denetleyicisi; Settings/Shop/Friends/Leaderboard/DailyReward/Rules/Profile ve profiledit/accountconfirm/done/addfriend/share alt durumları. Merkezi modal yığını, Esc/backdrop, Tab sınırı, odak iadesi ve taslak koruması.
- Beş tema; kaynak SVG yollarından native Painter2D ikonlar; Montserrat/Baloo2/Nunito900 statik ağırlıklar ve Türkçe karakterler. Fontların OFL lisansları Resources/Galaxy101/Fonts altında.
- Beş 2048×1024 gezegen dokusu kaynak seed/noise algoritmasından editörde üretildi. HLSL küresel örnekleme, atmosfer/limb, Satürn halkası ve karşılıklı gölgeler; 242 yıldız ve kayan yıldızlar. Hareket/focus durdurma, sahip olunan GPU kaynaklarının temizliği.
- Unity Localization TR/EN native string tabloları. CSV yinelenen anahtarları ayrı satır anahtarlarıyla korundu. Kaynak HTML'deki ek metinler ayrı importer girdileriyle tamamlandı. Bağlama göre belirsiz çeviriler açık TR/EN çiftleriyle seçilir.
- Hesap, ekonomi, arkadaş, liderlik, ödül ve paylaşım servisleri handoff gereği yerel mock. Gerçek ödeme, Google oturumu, hesap silme veya ağ mesajı yapılmaz. Paylaşım hedefleri bağlantıyı panoya kopyalar; dış uygulama açılmaz.
- Kullanıcı onayı: Botlara karşı mevcut bir insan + üç bot masamızı açar. Diğer beş mod seçim ve `PlayRequested` sınırında kalır; online eşleşme bu paketin dışında.
- Ayar/profil/tema/seçili mod JSON ile kalıcı; diğer mock bakiyeler/claim/arkadaş durumları oturumluk. Dil, çözünürlük ve tema anında; diğer ayarlar Kaydet/Vazgeç taslağı. Ortak ana ses seviyesi masa ayarlarıyla eşlenir, eski fullscreen/reduced-motion değerleri korunur.
- Normal build artık sahneleri yeniden üretmez. İlk Bootstrap ve değiştirilen build kaynaklarının yedeği `artifacts/galaxy101/source-backup/` altında. Ürün adı Galaxy 101, sürüm 0.2.0, kural sürümü 101-v1.

## Tasarım düzeltmeleri — 21 Ekim 2026

- Gezegen seçicinin pill zemini, halkaları ve grup aralıkları; kaynak SVG mağaza ikonunun yayları/beyaz dolgusu; Nunito900 indirim etiketi; CDX/GAMES/sürüm fontları, harf ve grup boşlukları düzeltildi.
- Ana sahne, panel ve dialog ayrı katmanlarda korunur. Panel içi sekme/veri değişimi açık pencereyi yeniden oluşturmaz veya giriş animasyonunu tekrarlamaz; odak, imleç ve hesap silme taslağı korunur. Açılış eğrisi kaynak `cubic-bezier(.2,1.2,.4,1)` ile eşlendi.
- Modal bulanıklığı açık bir alt katmana uygulanır. Ana ekranın kaybolduğu gri swapchain örneklemesi kaldırıldı. Tuval dışı alan tema rengini izler; alt koyu çerçeve kaynağı giderildi.
- Menü, CSS ile aynı gamma renk uzayında kendine ait UNorm hedefe çizilir; ayrı URP kamerası görüntüyü bir kez linear uzaya dönüştürerek sunar. Böylece düşük opaklıklı beyaz kenarlar/glow gereğinden açık görünmez. Menüye ait kamera/hedef/panel kopyası yeniden boyutlandırılır ve sahne kapanışında serbest bırakılır; mevcut masanın linear render ayarı korunur.
- Mağaza coin/gem/tile çizimleri, kesikli kenarlar, iç gölgeler, glow, popüler etiketleri, fiyat gemleri ve kaynak shine ölçüleri/eğrisi eşlendi. Shine zamanı sekme değişiminde korunur.
- Ek karşılaştırma düzeltmeleri: seçili olmayan modlarda fazla siyah katman kaldırıldı; kaynak saturation/brightness filtresi tüm karta uygulanır. Profil kart yükseklikleri/rozetler/çerçeve halkaları/düzenle ikonu; arkadaş ekle ikonu ve çevrimiçi noktası; liderlik renkleri/glow; ayar slider aralıkları ve paylaşım radyal gradyanı düzeltildi.

## HTML denetimi — 21 Ekim 2026 (ikinci tur)

- Referans: `Design/.../Galaxy101_AnaMenu_1a.dc.html` canlı render'ı (Playwright, 1280×720, 54 durum). Klasördeki PNG'ler HTML'in eski sürümünden; çelişkide HTML esas.
- UI Toolkit `letter-spacing` değerini px değil font boyutunun yüzde biri olarak uygular. `MenuView.Tracking(element, px, fontSize)` CSS px değerini çevirir; tüm harf aralıkları buna taşındı.
- Hemen Oyna: skewX parlama (`SkewBand`), CSS pulseG halkası, hover/active tüm gölge yığınını taşır. SVG ikonlarda saat yönü tersi yaylar kübik eğrilerle çizilir.
- Ayarlar/Kurallar seçili satırı kenarlık yerine inset çubukla çizilir (metin kaymaz). Input'larda sentetik kalın kaldırıldı. Buton ölçüleri HTML padding'ine göre (`PanelUi.CssButton`). Kesikli kod çerçeveleri `DashedOutline`.
- Bilinçli farklar: profil mod çubukları gerçek oranı gösterir (HTML `%44` geçersiz CSS → tam dolu). EN durumda üst avatar "LV 24" ve sayılar EN biçimli (HTML "SV 24"/"125.400" sabit). Chrome metin rasterizasyonu Unity SDF'ten bir miktar kalın görünür; font/ağırlık aynı (Montserrat/Baloo 700).

## 3D oyun masası — faz 1 (sahne temeli), 21 Ekim 2026

- Modeller: `Design/.../assets_src/blender_out` (Blender cilalı, bkz. `assets_src/BLENDER_CILALAMA.md`) → `Assets/Galaxy101/Table/Models`, glTFast 6.20.0 ile içe aktarılır.
- `Galaxy 101 > Build Table Assets` (`TableAssetBuilder`): 27 prefab (4 masa, 6 ıstaka, 5 taş seti, 10 ikram/hediye, koltuk, oda), `X`/`X_LOD1` → LODGroup, ıstakalara 30 `Slot_{satır}_{i}`, katalog (`Resources/Galaxy101/Table/TableAssetCatalog`), sahne `Assets/Galaxy101/Table/Scenes/GalaxyTable.unity` (build listesinde).
- glTFast malzemeleri prefab kurulumunda URP Lit / Complex Lit (clearcoat) / Unlit'e dönüştürülür (`TableMaterialConverter`); adlar korunur, emissive_strength HDR emisyona katlanır, glTF MR → URP metallic-gloss.
- Koordinatlar: glTFast X eksenini aynalar; JS yerleşim kodu three uzayında port edilir ve tek noktada aynalanır (`ThreeSpace`). Kamera/ışık/yuva/deste/gösterge/atma/per formülleri `table3d.js` ile aynı.
- Taş yüzleri: prototipin `tiles3d.js faceMat()` çizimiyle pişirilen atlas (5 görünüm × 53 hücre, 256×360; `tools/blender/bake_tile_faces.py`). GLB'lerdeki taş başına yüz dokuları glTFast'te aynı ada çarptığı için kullanılmaz (spec de atlas ister).
- `SkinService`: gezegen renklerini malzeme adına göre uygular (led/şerit → a1, neon tüp2 → a2, masa_kenar → rim, deri → pn, eloksal → rim×0.7, klasik taş → tf1).
- Masa sahnesi URP asset'inde gölge mesafesini 4.5 m / tek kaskad ve MSAA 4× yapar, çıkışta eski değerleri geri yükler.
- Doğrulama: `TableAssetBuilder.Capture` (edit-mode render, 16:9). Oyuncu gözü ve kuşbakışı `screenshots/table/T05` ve `T22` ile kadraj/yerleşim olarak eşleşiyor. Not: aynı editör tikinde oluşturulan malzemeler SRP Batcher'a ilk gönderimde yüklenmediği için yakalama 3 kez render eder.
- Henüz yok (sonraki fazlar; arka plan ve HUD faz 2'de eklendi): pencereler, oyun akışı/kurallar/botlar, ıstaka etkileşimi, tween/efektler, ikram sistemi, ayarlar ve tema değiştirici arayüzü, Satürn dışı gezegenler için çuha dokuları. Menüdeki "Botlara karşı" hâlâ eski `Table.unity`yi açar.

## 3D oyun masası — faz 2 (arka plan + HUD), 21 Ekim 2026

- Katman yığını (`TableHud`, 1280×720 tuval, menünün `MenuGammaPresenter`'ı ile CSS bayt uzayında): galaksi arka planı → 3D görünüm → vinyet (`ellipse 80% 70% at 50% 58%`) → çapalı katman → üst gölge (120 px) → HUD.
- Arka plan: `GalaxyBackground.UseTableLayout(pg)` — degrade `ellipse 90% 75% at 28% 12%`, `pg` halesi (closest-side, %70'te saydam) ve gezegen (300,118) ×0.82, sabit −16°, 40 sn'de bir tur; menü parıltısı/kenar ışığı/vinyeti yok. Shader düzen parametreleri (`_Shape`, `_HaloShape`, `_Placement`) varsayılan olarak menü değerlerini taşır.
- 3D: ana kamera saydam temizlemeyle `RenderTexture`'a çizer (`TableScene.RenderInto`); oda penceresinden arka plan görünür. URP `allowPostProcessAlphaOutput` PC/Mobile asset'lerinde açıldı. Saydam malzemeler alfa için "over" (`_SrcBlendAlpha` One / `_DstBlendAlpha` OneMinusSrcAlpha, eklemeli: Zero/One) kullanır; yoksa taş yüzlerinin saydam kenarları RT'de delik açıyordu.
- HUD: üst bar (çıkış, GALAXY 101, oda/bahis, deste sayısı, çip, ayarlar), tur toast'u, oyuncu paneli (çerçeveli avatar + tur halkası, durum, sohbet/hediye), Seri/Çift diz, açılış göstergesi (x/101) ve Aç düğmesi. Rakip avatarları ve isim plakaları `table3d.js anchorWorld` noktalarından her karede projekte edilir (clampA: x 84…1196, y 150…580); BURAYA AT / GÖSTERGE etiketleri de çapalıdır. Veri `TableHud.State` üzerinden; şimdilik T05 demo durumu.
- Bilinen yaklaşım: GALAXY 101 yazısı CSS'te metne kırpılmış dikey degradedir; UI Toolkit tek renk aldığı için orta durak (a1) + sert `lS` gölgesi kullanılır.
- Doğrulama: `TableHudRuntimeTests` (PlayMode; `GALAXY_TABLE_HUD_CAPTURE=<.png>` ile 1280×720 kare yazar). T05 ile yerleşim eşleşiyor. EditMode 418/418, PlayMode 106/106.
- Sonraki fazlar: oyun akışı/kurallar/botlar, ıstaka etkileşimi, tween/efektler, ikram/sohbet/ayarlar pencereleri, menüdeki "Botlara karşı"nın yeni sahneye bağlanması.

## 3D oyun masası — faz 3 (oyun akışı + ıstaka etkileşimi), 21 Ekim 2026

- Kurallar, deste, dağıtım (başlayan 22, diğerleri 21, gösterge), okey/sahte okey, açma (seri ≥101, 5 çift), işleme, cezalar, puanlama ve bot kararları paylaşılan motordan gelir: `com.okey101.core` + `Okey101.Application.LocalSession` + `Okey101.Bots.BotPlanner` (`Galaxy101.Runtime` bunlara başvurur).
- `TableGame`: dağıt → turlar → bitiş / taş bitti → 5 sn sonra yeni el. Bot turu spec/10 §4 zamanlamasıyla (aktif +0.12 sn, çek +0.72, perler önce masaya, at); planlayıcı `Task.Run` üzerinde. Benim turum 30 sn; son 5 sn'de halka kırmızı + "Süren bitiyor!"; süre bitince desteden çek + çekileni at, kural engeli olursa planlayıcı oynar. Aç / Yere aç / Çift aç / işle hamleleri atışa kadar "staged" tutulur ve `TableEvaluator` ile doğrulanır; atışla birlikte tek `CommitTurnCommand` gider. İşlenebilir taş atılırsa önce uyarı, aynı taşı tekrar atınca 101 ceza kabul edilir.
- `RackGrid` (saf, birim testli): `singlePlan`, `groupPlan`+`planRow`, `place`, `firstFree` (önce alt sıra), otomatik yerleştirme, Seri/Çift diz. `HandEvaluation`: boşluksuz ıstaka dilimlerinden geçerli seri/grup/çift (okey bağlamaları motorun aday üreticisinden) → açılış ölçeri.
- `TableBoard`: her fiziksel taş tek nesne; durum her komuttan sonra mutabakatla eşlenir (deste ↔ ıstaka ↔ atık ↔ per). Rakip taşları yüzü gizli anonim nesnelerdir; atınca/perlenince yüzü açılır. `TileView` tween'leri: kayma 0.18–0.34 sn, uçuş 0.42–0.85 sn yaylı, dağıtım Bézier girdabı 0.78 sn ±360°, iniş "pop", seçim/üzerine gelme kalkması.
- `RackInput` (Input System): tık seç, çift tık (380 ms) ya da seç + halkaya tık at, sürükle-bırak canlı önizlemeli, 360 ms basılı tut / Shift ile per taşıma, deste/yerdeki taşa tık ya da sürükleyerek çek, masadaki pere 7.5 cm içinde bırakınca işle. HUD düğmelerine basış UI'da kalır.
- HUD canlı: deste sayısı, taş sayıları, "Açtı · n" / "n çift", açılış ölçeri, Aç / Yere aç / Çift aç · n/5 (etkinse nabızlı), uyarı ve kural mesajları, el sonu satırı.
- Testler: `RackGridTests` (spec/11 §7 senaryoları, ölçer + okey), `TableGameRuntimeTests` (botlar bir eli bitirir ve her motor sürümünde tahta = motor; benim at/çek/at turum; ekran→ışın eşlemesi; gerçek fare olaylarıyla sürükleme ve çift tıkla atma). EditMode 426/426, PlayMode 109/109.
- Menü bağlantısı: ana menüde "Botlara karşı" + Hemen oyna artık `GalaxyTable` sahnesini açar (`MainMenuController.StartLocalAsync`, varsayılan `GalaxyTableScene`). Masadaki çıkış düğmesi `Bootstrap` (ana menü) sahnesine döner; el sürerken ilk basış uyarır, 2.6 sn içinde ikinci basış çıkar (tam T25 onay kartı sonraki faz). Eski masa (`Table`) yalnızca tanı argümanlarıyla (`-okeyPractice`, `-okeyA3Output`, `-okeyA4Output`) ve eski kabul testlerinde `LegacyTableScene` ile açılır.
- Henüz yok: el sonu tablosu penceresi (şimdilik toast satırı), bitiş/dağıtım efektleri (kıvılcım, halka dalgası, deste dönüşü, konfeti), "yeni" sarı çizgi ve geçerli per yeşil çizgisi, bekleme/geri sayım, masadan kalk onayı, sohbet/ikram/ayarlar pencereleri. Dağıtılan ıstaka sırasızdır; ölçer, taşlar dizilene (ya da Seri diz'e basılana) kadar 0 görünür.

## 3D oyun masası — faz 4 (döngüyü kapatan pencereler), 21 Ekim 2026

- Ürün kararı: bot masası çevrimdışıdır ve **yalnızca XP** verir (eli alan +340, diğerleri +60; çip yok). Üst bar oda hapı "Botlara karşı · 101 • Antrenman · XP".
- Bekleme (T01–T02): ilk elde lobi kamerası, deste masada; Ayşe 1.3 sn, Zeynep 2.7 sn, Burak 4.1 sn'de oturur; boş koltukta kesikli halka + yanıp sönen nokta + "Davet et" (koltuğu hemen doldurur); "MASA HAZIRLANIYOR · n/4" kartı ve dolum çubuğu; masa dolunca 0.9 sn sonra 3-2-1 (countPop) ve kamera 3 sn'de oyuncu görünümüne iner. Sonraki eller doğrudan dağıtımla başlar.
- Bitiş anı (T23): bitiren için 3.8 sn şerit (dönen altın halkalı avatar, "AYŞE Y. BİTİRDİ!" / "BİTİRDİN!", alt satırda gerçek el puanı; çiftle bitişte "el puanlanmadı"); deste bitince 2.4 sn "TAŞ BİTTİ" şeridi.
- El sonu tablosu (T24): satırlar el puanına göre, motorun gerçek puanları (cezalar ve çarpanlar dahil), not (bitirdi / açtı / çift açtı / açmadı), birikimli toplam, eli alan altın çerçeveli; sağ üstte XP; satırlar 90 ms arayla kayarak girer; "Sonraki el · 15" dolum çubuğuyla geri sayar (bitince ya da basınca yeni el); "Masadan kalk" çıkış kartını açar.
- Masadan kalk (T25): çıkış düğmesi kartı açar; "Masada kal", arka plana tık ve Esc kapatır; "Kalk" yeşil bildirim + 1.7 sn sonra ana menü. Metin XP kararına göre: "El bitmeden kalkarsan bu elin XP'sini alamazsın ve yerine bot oynar."
- Bağlantı koptu (T26) çevrimiçi masaya (S8) taşındı; bot masasında ağ yok.
- XP şimdilik oturum içinde tutulur; profile kalıcı yazılması S2 (profil) ile gelecek.
- Testler: oturma + davet + geri sayım, bitiş şeridi → tablo (4 satır) → sonraki el, çıkış kartı → menü. Ekran karşılaştırmaları: `docs/testing/table_T01_bekleme.png`, `docs/testing/table_T24_el_sonu.png`. EditMode 428/428, PlayMode 110/110.

## Düzeltme — Release'de botlara karşı masa siyah ekran, 21 Ekim 2026

- Neden: menü ve masa `UIDocument`'ları sahne yüklenirken önce yetkili `Resources/Galaxy101/UI/PanelSettings` asset'ine bağlanır, sonra `MenuGammaPresenter` onları kendi kopyasına taşır. Asset'in ekran paneli boş olarak kalır; asset `clearColor = true` olduğu için her karede sunulan görüntüyü siyaha temizliyordu. 3D masa ve arayüz doğru çiziliyor, son aşamada siliniyordu.
- Düzeltme: yetkili asset artık temizlemez (`m_ClearColor: 0`, `MenuAssetBuilder` da öyle üretir). Temizleme yalnızca sunucunun sahip olduğu kopyada açıktır (`ownedPanel.clearColor = true`).
- Doğrulama: Acceptance (Release + tanı) derlemesinde `-galaxyTableProbe <png>` menüden "Botlara karşı"yı açıp sunulan kareyi okur. Eski davranış `-galaxyLegacyPanelClear` ile taklit edildiğinde kare tamamen siyah (ortalama piksel ≈ 0); düzeltmeyle masa ve HUD görünür (`docs/testing/release_bot_table_fixed.png`). Gerileme testi: `PanelSettingsTests`. EditMode 429/429, PlayMode 110/110.

## Sunucu hattı — S1–S8, 22 Ekim 2026

Dal `server/line`. Ayrıntı, çalıştırma, Railway ve protokol: `Server/README.md`.

- **Yapı:** `Server/` (.NET 10 ASP.NET Core minimal API, EF Core + Npgsql, snake_case şema, tek migration), `Shared/Galaxy101.Contracts` (netstandard2.1 DTO, Unity uyumlu C# 9). Kural motoru ve `BotPlanner.cs` Unity kaynaklarından kopyasız derlenir (`Server/src/Galaxy101.Rules`); masa hattı motoru değiştirdikçe sunucu aynı kodu kullanır.
- **S1 kimlik:** misafir hesap (başlangıç bakiyesi defterle), Google/Apple id token ile giriş ve bağlama (OIDC anahtarlarıyla doğrulama), HS256 erişim token'ı (15 dk), SHA-256 ile saklanan dönüşümlü yenileme token'ı ve tekrar kullanımda aile iptali, WebSocket için 30 sn'lik tek kullanımlık bilet. Hız sınırı, `X-Forwarded-*` (Railway), `/health`, Dockerfile + `railway.json` (tek replika).
- **S2:** profil (istemciyle aynı doğrulama), istatistik, rozet (istatistikten türetilir), geçmiş, KVKK sürüm onayı, revizyonlu bulut ayarları (çözünürlük cihazda kalır), gezegen, hesap silme (kişisel satırlar silinir; satın alma ve maç kayıtları anonimleşir; canlı kuyruk/oda/maç/soket önce kapanır).
- **S3–S4:** `Ledger` tek bakiye yazarı (negatife düşmeyen koşullu UPDATE + idempotent değişmez defter). Katalog sunucuda (`catalog.json`, menü referans içeriğinden üretildi). Gem ile kaplama alımı tek işlemde. Günlük seri/görevler sunucu gününe (Europe/Istanbul) göre; görev ilerlemesi yalnız sunucu maçlarından.
- **S5:** Google Play (`purchases.products.get`, hizmet hesabı OAuth) ve App Store (StoreKit 2 JWS, sabitlenmiş Apple Root CA G3 zinciri) doğrulaması; makbuz `(platform, işlem)` başına bir kez; tekrar deneme aynı cüzdanı döndürür, başka hesap reddedilir.
- **S6–S7:** WebSocket (hesap başına tek soket, sınırlı tek yazar kuyruğu, taşma koruması), arkadaş kodu/isteği/kabul/silme, çevrimiçi durumu arkadaşlara itme, ad/kod arama, paylaşım bağlantısı ve `/p/{kod}` sayfası; haftalık / sezon / arkadaş liderliği (eşitler aynı sırada).
- **S8 çevrimiçi masa:** maç başına tek iş parçacıklı aktör; hamle önce kaydedilir sonra geçerli olur, yeniden başlatmada tohum + hamle günlüğü aynı masayı kurar. Koltuğa özel görünüm, sürüm denetimi, 30 sn hamle süresi; zaman aşımı/kopan/ayrılan koltuğu çevrimdışı masadaki bot oynar (yasal yedek hamle garantili). Mod+bahis kuyruğu, şifreli özel oda ve arkadaş daveti, başlangıçta bahis emaneti, kurtarılamayan masada iade. Uzlaşma tek işlemde: sıralama (eşli takım toplamı, ayrılan sona), pot, LP, haftalık puan, XP, istatistik/seri, görev, geçmiş. Masada emoji (hız sınırlı) ve ücretli ikram.
- **Doğrulama:** `cd Server && dotnet test` → **48/48** (gerçek PostgreSQL 17 konteyneri; iki ardışık koşu). Uçtan uca: dört oyuncu kuyruktan masaya, gizli eller, yanlış oyuncu/eski sürüm/bozuk hamle reddi, boş masanın sunucu tarafından 2 el oynatılıp uzlaşması (çip korunumu, istatistik, liderlik, görev), yeniden oynatmanın aynı el ve sürümü vermesi, şifreli oda + davet. Docker imajı derlendi; konteyner Postgres'e karşı `/health` ve misafir girişiyle duman testi geçti. Railway'e dağıtım yapılmadı.
- **Açık / yapılmadı:** Unity tarafı (`Http*Service`, async arayüz, mock yer değişimi, ağ oturumu) ayrı entegrasyon adımı. Ürün varsayılanları (maç 4 el, pot dağıtımı, LP, XP, bahisler, ligler) `Server/README.md` tablosunda, onay bekliyor. Katlamalı kural farkı tanımsız (şimdilik yalnız bahis). Gerçek mağaza/kimlik bilgileri, KVKK metni yok.

## 3D oyun masası — faz 5 (ek durumlar T28–T51), 22 Ekim 2026

Kaynak: `Design/galaxy101_unity_handoff/galaxy101_ek_durumlar` (spec/17, metin tablosu, T28–T51). Kararlar: maç şimdilik sabit 5 el (lobide seçici yok); XP eğrisi 500 × seviye; README açık soruları varsayılanlarla (okey atıp açtırma yalnız +101, çiftle bitiş puansız, önerilen XP değerleri); **cezalar çarpanla katlanmaz: el sonucu B × M + P** (kural belgesi R14 güncellendi). Değerler `Resources/Galaxy101/Table/table_rules.json` içinde.

- Motor (`com.okey101.core`): yerden taş her zaman alınabilir, `ReturnDiscardCommand` ile geri bırakılır (sonra yalnız deste); `ShortOpeningCommand` eksik açılışta +101; attığı taşla sonraki oyuncu açarsa taş değeri × 10; masadaki okeyi başkası alırsa sahibine +101; puan B × M + P. Birim testleri `Spec17RuleTests`.
- Mesajlar (T40–T41): 6 tür, süreler, kuyruk (+n sayaç, ceza/hata öne, aynı metin süreyi yeniler), rakip olayları avatar balonu. Metinler tasarımın CSV'sinden (`TableStrings`).
- Cezalar (T34–T36): her komuttan sonra kimin cezası arttığı ve nedeni çıkarılır; bana ceza toast'ı (tutar hapıyla), rakibe balon, avatarda o elin toplamı rozeti.
- Yerden alma (T38–T39): YERDEN etiketi + çerçeve, GERİ BIRAK halkası (dokun ya da sürükle), sonra "Sıra sende · desteden çek"; alınan taşı atma hatası.
- Okey (T33, T47–T49): masadaki her okeyde temsil rozeti + "Okey · Mavi 7 yerine" ipucu; sürüklenen gerçek taşla alınabilecek okeyler parlar, bırakınca takas; yanlış taşta kırmızı çerçeve + hata; 500 ms basılı tutma ipucu.
- Bot başlığı (T28, T30): "Botlara karşı · 101 · ANTRENMAN · Çipsiz · XP", el sayacı (nokta + "n / 5. el"), XP hapı (seviye dairesi, çubuk, XP), rakiplerde BOT etiketi, bot çıkış kartı metni.
- El sonu v2 (T42–T44): CEZA sütunu (tutar + neden), çarpan rozetleri (okeyle / çiftle / elden ×2, çift açan "el ×2"), açıklamalı el puanı ("−101 × 2 = −202"), puansız el kutusu, yalnız XP; son elde düğme maç sonucuna götürür.
- Maç sonu (T45–T46): sıralama (madalyalar), XP dökümü + sıra bonusu, seviye çubuğu (seviye atlamada dolar → parlar → yeni seviye + "SEVİYE ATLADIN"), el el puan tablosu, kazanınca konfeti; Lobiye dön / Tekrar oyna. XP menü profilinde kalıcı (`MenuModel.Level/Xp`); menüdeki seviye rozetleri artık bu değeri gösterir.
- Yükleme (T50–T51): menüden masaya geçerken tam ekran yükleme (gezegen sağda, ilerleme %, rastgele ipucu); 15 sn içinde bitmezse "Masa açılamadı" kartı (Lobiye dön / Yeniden dene).
- Ekran karşılaştırmaları: `docs/testing/faz5_*.png`. Testler: EditMode 436/436, PlayMode 114/114.

Kabul listesi (spec/17):
- [x] Bot maçında bahis/çip metni yok; XP hapı ve el sayacı (T28, T30). Sapma: el seçici yok (karar: sabit 5).
- [x] Okey seçici yok, en yüksek değer otomatik, masada temsil rozeti (T33). Sapma: kendi ıstakamdaki okeylerde altın nokta/iç çerçeve işareti yok (3D taş yüzü değişikliği gerektiriyor).
- [x] 5 ceza kuralı doğru tutarla, Aç 101 altında basılabiliyor, toast + rozet + balon (T34–T36). Sapma: T34'teki "ceza doğuran taşın 1.2 sn parlaması" yok.
- [x] YERDEN etiketi, geri bırakma, sonraki oyuncuya atılamama (T38–T39). Sapma: geri dönüş uçuşu 300 ms değil, standart taş uçuşu (0.42–0.85 sn).
- [x] 6 mesaj türü, süreler, kuyruk (T40–T41).
- [x] El sonu v2 çarpanlar, açıklama, nedenli ceza, puansız el, XP (T42–T44). Sapma: çevrimiçi çip varyantı (T43) yok — çevrimiçi masa S8'de.
- [x] Maç sonu sıralama, XP dökümü, seviye atlama, mini tablo (T45–T46).
- [x] Okeyi alma: parlayan hedef, takas, ipucu, geçersiz, sahibine +101 (T47–T49). Sapma: okeyin ıstakaya dönüşü 450 ms özel yay değil, standart uçuş.
- [x] Yükleme ve hata ekranı (T50–T51).
- [ ] EN dilde metin taşması kontrolü yapılmadı (yalnız TR karelerle karşılaştırıldı).

## Tekrar üretim

1. Unity Hub üzerinden bu kökü Unity 6000.6.6f1 ile açın; Bootstrap sahnesini çalıştırın.
2. Canonical UXML/USS veya metinler düzenlenince `Galaxy 101 > Update Menu Assets` çalıştırın. Araç Resources kopyalarını, tabloları, font/PanelSettings ayarlarını günceller.
3. Windows release: `pwsh -File scripts/Build-Windows.ps1 -Configuration Release`. Her derleme benzersiz klasöre `Galaxy101.exe` ve `build-report.json` yazar. Eski paketler üzerine yazılmaz.
4. Testler: `pwsh -File scripts/Test-Unity.ps1 -Platform EditMode -Graphics`; `pwsh -File scripts/Test-Unity.ps1 -Platform PlayMode`. Editor bu kökü açık tutarken batch başlatmayın.
5. Kabul görüntüleri için `scripts/Capture-GalaxyMenu.ps1 -Presentation` kullanılabilir. Eşdeğer Acceptance komutu: `Galaxy101.exe -batchmode -force-d3d11 -galaxyCaptureOutput C:\absolute\captures -galaxyCapturePresentation -logFile C:\absolute\captures\player.log`. Yalnız uygulamanın kendi UI render hedefleri okunur; masaüstü görüntülenmez. 54 durum + iki çözünürlük, manifest, izole kayıt ve otomatik çıkış. Bu tanılama kodu Release paketinden çıkarılır.

## Kaynakta olmayan zorunlu hata durumları

Başarısız atomik kayıt: “Değişiklikler kaydedilemedi. Tekrar dene.” / “Could not save changes. Try again.”. Yetersiz mock bakiye: “Yetersiz elmas” / “Not enough gems”. Mock satın alma bildirimi: “İşlem tamam” / “Done”. Orijinal başarı/hesap ekranı metinleri korunur. Gerçek online servisler yokken tasarımın Google/hesap açıklamaları yalnız prototip metnidir.

## Doğrulama durumu — son düzeltme paketi

- EditMode: `artifacts/test-results/20261021T130621959Z-0752986d/results.xml`, **414/414 geçti**, skip yok. Kural/regresyonlar, DX11 renk/gradient/gezegen, SVG yayları ve CSS açılış eğrisi dahil.
- PlayMode: `artifacts/test-results/20261021T130805196Z-dd2632b2/results.xml`, **105/105 geçti**, skip yok. Tüm sekmelerde aynı kart/backdrop/ana sahne, nested modal/klavye odağı/taslak, gerçek UI piksel renkleri, gamma sunumu/yön/input eşleşmesi/kaynak ömrü ve mevcut bot masasına giriş/dönüş dahil.
- Acceptance: `artifacts/builds/20261021T130701166Z-788556f1/`, **Succeeded, 0 hata / 0 derleme uyarısı**. Altı tanılama fixture'ı mevcut.
- Paketlenmiş kamera görüntüleri: `artifacts/galaxy101/captures/correction-delivery/manifest.json`, **54 referans durumu + 1920×1080/2560×1440 ana ekran = 56 görüntü**. 47 modal durumunda açıkta kalan ana ekran için ayrı kontrast/parlaklık kontrolleri geçti. Yalnız uygulamanın kendi hedefleri okundu; masaüstü/input/pano kullanılmadı. Headless player normal kamera döngüsünü başlatmadığı için tanılama, Unity'nin desteklediği `RenderPipeline.StandardRequest` API'siyle aynı üretim kamerasını çizdirir. Bu çağrı Release'e dahil değildir.
- Son karşılaştırmalar: `artifacts/galaxy101/comparisons/correction-delivery/compare-1.jpg`–`compare-6.jpg`; 54 durum karşılaştırıldı, son profil/liderlik/arkadaş/ayar düzeltmeleri yeniden görsel kontrol edildi. `evidence.json`: ilk menünün UI hedefi ile kamera çıktısı arasında RGB bayt farkı **0**. Bu karşılaştırma sunum dönüşümünün doğruluğunu gösterir; HTML ile tüm piksellerin aynı olduğu anlamına gelmez.
- Release: `artifacts/builds/20261021T130948504Z-f70b1bcd/Galaxy101.exe`, **Succeeded, 0 hata / 0 derleme uyarısı**, Mono/Direct3D11, v0.2.0 / 101-v1. Release tanılama/fixture temizliği doğrulandı: fixture sayısı sıfır.
- Aynı Release başlangıcı: `artifacts/galaxy101/release-corrections-verified/evidence.json`; `GALAXY101_MENU_READY version=0.2.0` görüldü, runtime exception yok. Bu kontrol başlangıç kontrolüdür; masaüstünde görsel kullanıcı kabulü değildir. Eski script/serialization uyarıları aşağıda ayrıca kayıtlıdır.
- Dağıtım: `artifacts/releases/Galaxy101-v0.2.0-Windows-x64-UI-Fix-20261021.zip`, **48.901.664 bayt**, ZIP bütünlük/gerekli dosya kontrolleri geçti. SHA256: `12e7b19c1a25a5f358cd72d4c343aa384ebef74d3ea3b0d03d5697e11d985dc3`. Yanında `.sha256` dosyası var. ZIP'in tamamını çıkarıp `Galaxy101.exe` çalıştırın. Önceki paket korunur.

## Kabul sınırları

- Kaynak HTML TR profil çubuklarına `%44` gibi geçersiz CSS genişlikleri üretir; referans görüntüde bunlar tam dolu görünür. Unity doğru `44%`, `38%` gibi sayısal oranları gösterir. Kaynağın bu hatası kopyalanmadı.
- Kaynak/test kapsamı ve tamamlanan 54 durum karşılaştırması, tüm piksellerin birebir eşleştiği iddiası değildir.
- Rastgele yıldızlar/gezegen dönme açısı referans ekran görüntüsünün tek karesiyle birebir aynı olamaz. Kabul capture'ları tekrarlanabilir sıfır animasyon zamanı kullanır; sayaç metinleri normalleştirilir.
- 84 kopyalandı ekranı görsel fixture olarak üretilir; capture sırasında kullanıcı panosuna yazılmaz. Gerçek pano ve harici paylaşım hedeflerinin kullanıcı kabulü ayrıca yapılmalıdır.
- Native drop-shadow/blur ile CSS rasterizasyonu ve text shaping arasında kenar/piksel farkları olabilir; HLSL halka mikro çizgilerinde float/double farkı vardır.
- Bu değişiklik mevcut masa UI'sini yeniden tasarlamaz veya İngilizceye taşımaz; handoff kapsamı 1a menü ve pencereleridir. Auto-sort, per vurgusu, çift tıkla atış, masa sohbeti, uyarı süresi, taş boyutu, müzik/efekt/bildirim ve hesap bildirim seçenekleri prototipin mock ayar kaydını değiştirir; mevcut masa bu yeni seçeneklerin tamamını henüz tüketmez. Bu pakette uygulanan çalışma zamanı bağlantıları ana ses seviyesi, menü arka plan hareketi/kalitesi, tema/dil ve çözünürlüktür. Müzik varlığı sağlanmadı; yeni ses tasarımı eklenmedi.
- 30 dakikalık performans testi, hedef donanımda 60 FPS kabulü, fiziksel Android/iOS ve ses hissi testi yapılmadı. Kullanıcının önceki kararı gereği uzun test ve ses çalışması ayrı tutuldu. Railway yayını yapılmadı.

## Additional observations

- **medium — Assets/Okey101/Editor/ProjectSetup.cs:18, CreateOrUpdate:** eski “Okey 101 > Create or update local game” komutu Bootstrap/Table sahnelerini ve prefabları sıfırdan üretir; yeni menüyü ve elle düzenlenmiş sahneleri ezebilir. Normal Windows build bundan ayrıldı. Mevcut proje üzerinde bu eski sıfırdan kurulum komutunu kullanmayın; menü güncellemesi için `Galaxy 101 > Update Menu Assets` kullanın. Eski aracın bağımsız davranışı bu görevde değiştirilmedi.

- **medium — artifacts/galaxy101/release-corrections-verified/player.log:27:** önceki Release logunda da bulunan `The referenced script (Unknown) on this Behaviour is missing!` ve serialization-layout uyarıları sürüyor. Log ilgili kaynak asset/type adını vermiyor; menü başlangıcı, 56 paket kamera görüntüsü ve 105 PlayMode kontrolü başarılı. Kaynak kesinleştirilmeden render assetleri değiştirilmedi. Acceptance kamera isteği ayrıca kullanılmayan/strip edilmiş URP post-process pass uyarılarını açığa çıkarıyor; menü kamerasında post-processing kapalı. Bu uyarılar sıfır derleme uyarısı beyanından ayrıdır.

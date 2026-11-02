# Galaxy 101 · v4 otonom geliştirme görevi (Railway öncesi tam oyun)

Bu dosyanın çizgiden sonraki kısmı Claude Code'a olduğu gibi verilir. Çalışma klasörü: `C:\GD\Okey 101`.

---

## Görev

Sen bu deponun kıdemli Unity istemci ve .NET sunucu geliştiricisisin. `Design/Galaxy101_ClaudeCode/` klasöründeki Galaxy 101 (101 Okey) teslim paketi v4'teki çalışan prototipi, Unity istemcisinde ve sunucuda **birebir ve eksiksiz** hale getireceksin: aynı kurallar, aynı ekranlar ve durumlar, aynı yerleşim, renk, yazı, animasyon, süre, metin ve ses olayları, aynı sunucu davranışı.

Hedef: Railway'e çıkmadan önce oyunun tamamı. Railway dağıtımı, push ve canlıya çıkış bu görevin dışında.

Otonom çalış. İşleri bağımlılıklarına göre sırayla ya da paralel yürüt. Kullanıcıya ara soru sorma; belirsizliği kayda geçir ve sonraki işe geç. Bittiğinde tek bir rapor ver.

## Kaynaklar ve öncelik sırası

Çelişkide üstteki geçerli:

1. `prototip/galaxy101_handoff_v4/RULES.md` (R-01…R-98), `tests/rules/*.json` (106 vektör), `tests/server.tests.js` (26 akış). Paket bunların hepsinin geçtiğini söylüyor; Faz 0'da kendin doğrula.
2. `OPEN_QUESTIONS.md` "Cevaplananlar" tablosu (kullanıcının cevapları), `DECISIONS.md` (D-01…D-62), `REMOVED.md`.
3. `protocol/protocol.json` + `protocol.md`, `protocol/timings.md`.
4. `states/states.json` (151 kimlik: 144 etkin, 7 kaldırılmış; etkinlerin hepsi `?state=` ile açılır), `export/layout-all.html` (canlı yerleşim + hesaplanmış stil dökümü), `anim/css.json`, `anim/tween3d.json`, `anim/easings.json`, `strings/strings.json`, `assets/*.json` (`themes`, `fonts`, `materials`, `lighting`, `icons`, `ranks`, `cursors`) + `assets/fonts/` (TTF) + `icons/` (69) + `rank/` (75) + `cursors/` (11), `sound/events.json` + `sound/sfx/` (28 WAV) + `sound/music/` (2 döngü WAV).
5. Çalışan prototip: `prototip/Galaxy 101 - Gerçek Ana Oyun.dc.html` ve `G101-*.dc.html`, `g101-*.js`, 3D modüller, `vendor/` (three.js r184, JSZip, yerel font tanımı; internet gerekmez). Referanstır, kodu aynen kopyalanmaz; değerler aynen kullanılır.
6. `galaxy101_3d_tum_temalar/`: 103 GLB. 5 tema × 18 (masa 4, ıstaka 6, taş seti 5, tek taş, oyun işaretleri, ışıklı tam sahne `sahne_tam`) ve 13 ortak (zar, koltuk, oda, 10 ikram/hediye). Yeniden üretmek için `prototip/Araç - 3D Varlık Dışa Aktarma.html`.
7. `screenshots/`: 144 etkin durumun hepsi, 924×540. Yalnız göz kontrolü için. Ölçüm için bunları kullanma, referansı kendin çek (Faz 0.3).

`START.md`'deki "belirsiz bir nokta olursa sor" cümlesi bu görevde geçerli değil: sorma, kayda geçir (aşağıdaki kurallar).

Kapsam dışı: `DEMO_LIST.md` ve kodda `// DEMO:` ile işaretli her şey (DEMO-01…18). Deneme PIN'i, deneme oda kodu, sahte oyuncular, sahte gecikmeler, düz metin şifre, sabit sıfırlama kodu, doğrulanmayan Google girişi ve anında verilen mağaza paketi gerçek oyuna girmez.

Paketten önceki proje belgeleri (`docs/rules/101-v1.md`, `Server/README.md` "Ürün kararları", silinmiş eski `Design/galaxy101_unity_handoff/spec`) artık ikincil. v4 bir konuda sessizse mevcut kod davranışı kalır, fark matrisinde "pakette yok" diye işaretlenir.

## Değişmez çalışma kuralları

- **Uydurma yok.** Her renk, ölçü, süre, metin, kural ve sayı pakette bir kaynağa dayanmalı: dosya + anahtar, satır ya da durum kimliği. Kaynak bulunamazsa değer uydurma. Ya o parçayı yapma ya da mevcut davranışı koru, ve `docs/v4/EKSIKLER.md`'ye yaz. Her kayıtta şunlar olsun: kimlik (EK-001…), ne eksik, nerelere bakıldı, etkisi, şu anki geçici durum.
- **Sıfırdan yazma yok.** Mevcut kod temeldir: UI Toolkit menü ve pencereler, 3D masa (`Assets/Galaxy101/Table`), kural motoru `Packages/com.okey101.core`, botlar, `Server/`, `Packages/com.galaxy101.contracts`. Farkları kapat; bir parçayı baştan yazman gerekiyorsa gerekçesini `docs/v4/KARARLAR.md`'ye yaz.
- **Dış dünyaya giden işlem yok:** push, deploy, Railway, gerçek ödeme, e-posta, mağaza. Yalnız bunlarda dur. Yerel commit serbest. Yerel Docker PostgreSQL test konteynerleri serbest.
- **Her adım kanıtla biter:** derleme temiz, ilgili testler yeşil, görsel karşılaştırma yapılmış, yerel commit atılmış. "Bitti" demeden önce komut çıktısıyla doğrula.
- **İlerleme günlüğü:** `docs/v4/ILERLEME.md`'yi her kilometre taşında güncelle (faz, biten, sıradaki, riskler). Bağlam sıfırlanırsa buradan devam et.
- **Gizli bilgi** (protocol.md "Gizli bilgi kuralı") sunucuda uygulanır. İstemci rakip ıstakasını ve deste sırasını hiçbir zaman almaz.
- **Unity tek süreç:** aynı anda yalnız bir Unity editörü ya da batch işlemi çalışabilir (proje kilidi). Unity'ye dokunan iş yalnız ana çalışma kopyasında ve sırayla yapılır.
- **Unity'siz derleme ve test** (hafızadaki "verify-without-unity" notu): çekirdek, bot ve uygulama kodu geçici bir SDK NUnit projesiyle koşar. Unity csproj'larının mutlak yollu kopyalarıyla istemci derlenir; Unity C# 9 kullanır, `LangVersion 9.0` ile derle. Sunucu testleri `-p:OutDir=<geçici>` ve Docker ile koşar. Unity içi testler `scripts/Test-Unity.ps1` ile, editör kapalıyken koşar.

## Faz 0 · Hazırlık (sıralı, tek oturum)

**0.1 Taban commit.**
- Çalışma ağacında yüzlerce commit edilmemiş değişiklik var: önceki oturumların sunucu v4–v7, dereceli, katlamalı işleri; eski handoff klasörünün silinmesi; yeni paket.
- Önce mevcut testleri koş: çekirdek NUnit, sunucu xUnit (Docker), mümkünse Unity EditMode.
- Sonucu "v4 öncesi taban" olarak tek yerel commit'le kaydet. `Design/Galaxy101_ClaudeCode` de bu commit'e girer; GLB ve PNG dosyaları `.gitattributes` gereği LFS'e gider.
- Kırmızı test varsa düzeltmeden önce kayda geçir.

**0.2 Paketi oku.**
- Sırayla tamamını oku: `START.md`, v4 `README.md`, `RULES.md`, `OPEN_QUESTIONS.md`, `DECISIONS.md`, `REMOVED.md`, `DEMO_LIST.md`, `protocol/*`, `states/README.md`, `anim/README.md`, `strings/README.md`, `sound/README.md`, `logic/*.js`, `tests/*`.
- Prototipi çalıştır: `cd Design/Galaxy101_ClaudeCode/prototip && npx serve .`
- `tests/rules/*.json` vektörlerini ve `tests/server.tests.js` akışlarını Node'da koş; 106/106 ve 26/26 olduğunu gör (25 Ekim 2026 kontrolünde böyleydi). Koşucu: bir `vm` bağlamına sırayla `logic/rules.js`, `logic/bots.js`, `logic/mockServer.js`, `tests/runner.js`, `tests/server.tests.js` yüklenir; her vektör `G101TestRunner.run(G101Rules, G101Bots, vektör)` ile, akışlar `G101ServerTests.run()` ile koşar (`tests/rules.test.html` ile aynı).

**0.3 Referans yakalama.**
- Playwright ile `removed:true` olmayan her durumu aç. URL şablonu: `Galaxy 101 - Gerçek Ana Oyun.dc.html?state=<ID>&seed=42&theme=saturn&lang=tr&ft=<ms>`.
- Her durum için 1280×720 PNG ve `dump=1&nodl=1` ile `window.G101Dump` yerleşim JSON'u kaydet: `artifacts/v4/ref/<ID>.png`, `<ID>.layout.json`.
- Ek olarak her durumun EN halini ve metin ya da renk taşıyan durumların diğer 4 temasını çek.
- `tools/ref_capture.py` eski prototip adını kullanıyor; yeni prototipe ve `?state` yönlendiricisine göre güncelle.
- Sürükleme isteyen durumlar artık yönlendiricinin `scDrag` / `scSwap` adımlarıyla açılıyor; Unity yönlendiricisi de aynı adımları desteklemeli.
- Aynı URL iki kez aynı kareyi vermiyorsa EKSIKLER'e yaz.

**0.4 Fark matrisi.** `docs/v4/FARK_MATRISI.md`. Her satır: kimlik, pakette ne diyor (kaynak), projede ne var (dosya:satır), durum (var / farklı / eksik / pakette yok). Kapsam:
- R-01…R-98, D-01…D-62
- protocol.json'daki her C2S, S2C mesajı ve hata kodu
- timings.md'deki her süre
- 144 etkin durum (kaldırılan 7'si de: Unity'de varsa kaldırılacak)
- themes, fonts (TTF), materials, lighting, 69 ikon, 75 lig rozeti, 11 imleç
- strings anahtarları, 28 ses olayı + 28 efekt + 2 müzik dosyası, 103 GLB

Sonraki fazların iş listesi bu matristir.

**0.5 Mimari kararlar.** `docs/v4/KARARLAR.md`'ye gerekçeli yaz, sonra uygula. Varsayılanlar:
- **Kural motoru:** `com.okey101.core` korunur ve RULES.md'ye uydurulur. 106 vektör NUnit'te bir vektör adaptörüyle koşar:
  - D-01 string taş kimliği (`R5a` …) ↔ `TileId`
  - rules.js eylem, olay ve durum yolları ↔ motor
  - Adaptör bir vektörü ifade edemiyorsa bu bir motor model farkıdır: motoru düzelt, adaptöre vektöre özel kod yazma.
  - Farklar motorun yeniden yazılmasını gerektirecek kadar büyükse `rules.js`'yi C#'a satır satır taşı ve bu kararı gerekçesiyle yaz.
- **RNG ve dağıtım:** mulberry32 ve D-02 / R-02…R-05 sırası birebir. Mevcut `xorshift32-fy-v1` ile kaydedilmiş maçlar ve replay'ler bozulmasın; `ShuffleVersion` sürümlenir, eski kayıt eski RNG ile açılır.
- **Botlar:** `logic/bots.js` politikası (D-20, D-21, R-80) C#'a taşınır. Sunucu botları ve "koltuğu sunucu oynar" (R-66) bunu kullanır. Otomatik oyun R-70 / R-71'e göre yapılır. Mevcut `BotPlanner` yalnız paketin tarif ettiği bir yerde kalır.
- **Protokol:** mevcut taşıma (HTTP + WebSocket `{type,id,data}`, Contracts) korunur.
  - protocol.json'daki her mesaj, alan ve hata kodu bir karşılığa eşlenir: `docs/v4/PROTOKOL_ESLEME.md`. Eksikler eklenir.
  - 26 sunucu akışının her biri bir xUnit entegrasyon testi olur.
  - Eşleme mümkün değilse protocol.json taşımasına geçiş kararını gerekçesiyle yaz.
- **Unity durum yönlendirici:** `g101-state.js` adımlarının karşılığı, yalnız `UNITY_EDITOR || DEVELOPMENT_BUILD` içinde.
  - Komut satırı: `-g101State=G13 -g101Seed=42 -g101Theme=saturn -g101Lang=tr -g101Ft=<ms>`.
  - Ortak saat ve seed (D-60, D-61): aynı durum, aynı kare.
  - Öğe adları `data-g101-id` ile aynı (D-62), böylece yerleşim dökümleri eleman eleman karşılaştırılır.

## Faz 1 · Paralel hatlar (Unity çalıştırmaz)

Dört hattı ayrı oturum ya da alt ajan olarak başlat. Her biri kendi git worktree'sinde ve dalında çalışır, yalnız kendi dosyalarına dokunur. Birleştirme sırası A → B → D → C; her birleşmeden sonra bütün testler koşulur.

**Hat A · Kural motoru ve botlar** — dal `v4/rules`. Dosyalar: `Packages/com.okey101.core/**`, `Assets/Okey101/Bots/**`, `Assets/Okey101/Application/**`, testleri.
- Çıktı: 106/106 vektör NUnit'te yeşil, bots.js politikası, RNG, mevcut testler yeşil. Kurala göre değişen bir test varsa gerekçesi commit mesajında olur.
- Bilinen fark adayları (matrisle doğrula):
  - R-53: elden bitişte açılış eşiği aranır; şu an `TableEvaluator.FinishesFromHand` eşiği atlıyor, "en az 15 taş" şartı da kalkıyor.
  - R-57: bitirenin ortağı 0 + kendi cezası alır.
  - R-24: seri açan, çiftleri her zaman yere açabilir.
  - D-10: geri bırak, turdaki bütün işlemleri geri alır.
  - D-15: okey atışında ayrıca işlek cezası yazılmaz.
  - D-16, D-17, D-18: bekleyen cezalar.
  - R-70, R-71: otomatik oyun.
  - R-55: taş bitince puanlama.

**Hat B · Sunucu** — dal `v4/server`. Dosyalar: `Server/**`, `Packages/com.galaxy101.contracts/**`, `Shared/**`.
- Çıktı: protokol eşlemesi, timings.md, ekonomi, dereceli ve 26 akış testi tamam; mevcut 99 test yeşil. Kural değişiklikleri için Hat A'nın dalını birleştirir.
- Kapsam:
  - Süreler: dağıtım töreni sunucu takvimi D-24 (21 686 ms; şu an `Game:DealSeconds` 22 sn), zar bekleme D-23, hazır düğmesi D-25.
  - Ekonomi: R-90…R-98 ve D-11, D-26, D-27, D-34, D-37. Dikkat: R-90'a göre bahis oturunca düşülür ve bekleme odasından çıkınca iade edilir; şu an maç başında düşülüyor.
  - Paylaşılan sıra D-40 (en yakın tama yuvarlama; şu an `MidpointRounding.ToZero`) ve eşli dört konum D-41 (şu an takım yeri 1/2, XP bonusu 500/200).
  - Dereceli: R-94, D-29, D-30.
  - Sohbet 120 karakter (D-32); kopma ve dönme R-66, D-35, D-36.
- Pakette karşılığı olmayan sunucu özelliklerini silme. Örnek: hızlı eşleşme kuyruğu `queue.join` solo/pairs/doubling, `Game:HandsPerMatch` 4, `Game:Stakes`. Bunları matriste "pakette yok" diye işaretle ve rapora koy.

**Hat C · 3D varlıklar** — dal `v4/assets3d`. Dosyalar: `galaxy101_3d_tum_temalar` (103 GLB) ile `Assets/Galaxy101/Table/Models`, `tools/blender/**`.
- GLB'leri mevcut modellerle karşılaştır.
- Malzeme adlarını `assets/materials.json` ile eşle. Işık ve görüntü ayarları `assets/lighting.json`'dan (glTF'e geçmeyen ışıklar için dosyadaki Unity karşılıkları); `sahne_tam` GLB'si yerleşim ve ışık karşılaştırması için referans.
- Gerekirse Blender ile cila: Blender MCP bağlıysa onunla, değilse `blender -b -P tools/blender/<betik>.py`.
- Prefab ve katalog üretimi (`Galaxy 101 > Build Table Assets`) Unity gerektirir. O kısım Faz 2'de ana kopyada yapılır; bu hat yalnız dosyaları ve fark raporunu hazırlar.

**Hat D · Veri varlıkları** — dal `v4/data`. Dosyalar: `scripts/**`, `tools/**`, `Assets/Galaxy101/Resources/**` altındaki veri dosyaları.
- Kapsam: strings (TR/EN ve hata metinleri), themes.json, fonts.json + `assets/fonts/*.ttf` (mevcut font asset'leriyle karşılaştır), 69 SVG ikon, 75 lig rozeti, 11 imleç, sound/events.json + 28 efekt + 2 müzik WAV.
- Dönüştürücü betikleri (`scripts/import-galaxy-ui-assets.py` vb.) yeni paket yollarıyla güncelle, Unity'nin okuyacağı dosyaları üret.
- Unity içe aktarma Faz 2'de yapılır.

## Faz 2 · Unity istemci (ana kopya, sıralı)

**Sıra:**
1. Altyapı: durum yönlendirici, ortak saat ve seed, Hat C/D çıktılarının Unity'de içe aktarılması (tema, font, metin, ikon, imleç, rozet, 3D prefab ve katalog), ses olay sistemi.
2. S01–S16: açılış, giriş, menü, lobi, dereceli girişleri.
3. 01–97: menü pencereleri.
4. Lobi, masa aç, bekleme odası, davet, izleme, oyuna katıl.
5. Dereceli ekranları (`G101-Dereceli.dc.html`, `g101-rank.js`, `assets/rank`).
6. Masa G01–G34: dağıtım töreni ve oyun.
7. Masa T01–T51.
8. İstemci–sunucu bağlantısı: protokol eşlemesi; yeniden bağlanma "Deneme n/5", 3 sn, sonra "Lobiye dön"; `snapshot.screen` ile dönüş.

**Her durum için döngü:**
1. Prototipte durumun yolunu, yerleşim dökümünü, `anim/css.json` ve `easings.json` kayıtlarını, 3D içinse `tween3d.json` ve `materials.json` değerlerini, metin anahtarlarını oku.
2. Unity'de uygula; öğe adları `data-g101-id` ile aynı olsun.
3. Unity'de aynı durumu yönlendiriciyle aç: 1280×720 kare + UiDump.
4. Karşılaştır: `tools/compare_ui.py` (metin kutuları ±3 px), yerleşim dökümü (kutu, renk, font, harf aralığı, köşe, gölge) ve piksel farkı. Kullandığın eşikleri `docs/v4/DURUMLAR.md`'de belirt.
5. Kabul: fark eşik içinde, ya da nedeni `docs/v4/UNITY_FARKLARI.md`'de (UI Toolkit kısıtı ve seçilen yöntem) veya EKSIKLER'de kayıtlı. `docs/v4/DURUMLAR.md` tablosunda durumu işaretle.

**Durumlar ve kısıtlar:**
- Kaldırılan durumlar (T33, T40, T41, T44, T45, T46, T48) ve REMOVED.md'deki her şey Unity'de varsa kaldırılır.
- Sürükleme isteyen durumlar (`scDrag`, `scSwap`) PlayMode testinde girdi benzetimiyle açılır.
- 3D nesneler yerleşim dökümünde yok; onları `tween3d.json`, `materials.json`, `lighting.json`, `sahne_tam` GLB'si ve referans karesiyle doğrula.

**Ses:**
- `sound/events.json`'daki 28 olay: tetikleyici, kategori, öncelik, eş zamanlı kopya sınırı, süre, dosya; müzik geçişi ve kısma kuralları. Dosyalar `sound/sfx/` (28 WAV) ve `sound/music/` (menü 32 sn, masa 40 sn kesintisiz döngü).
- Kanallar ayarlardaki kaydırıcılara bağlanır: ana ses, efektler, müzik, taş sesleri, sohbet/bildirim sesleri.
- Motor olayı → ses eşlemesi `sound/README.md`'deki tablodan (drew → tile.draw, discarded → tile.discard, …). Menüde menü müziği, masada masa müziği.

## Faz 3 · Uçtan uca doğrulama

- Docker + yerel sunucu (`scripts/Start-LocalServer.ps1`) ve Windows build (`scripts/Build-Windows.ps1`).
- Gerçek akışlar: misafir → menü → lobi → masa aç → bekleme odası → maç → maç sonu → lobi. Ayrıca eşli, katlamalı (ortak seçeneği açık ve kapalı), dereceli, izleme, oyuna katıl, kopma ve dönme, masadan kalk, hediye, sohbet, mağaza, günlük ödül, arkadaş, liderlik.
- Gerçek ödeme, Google ve e-posta sağlayıcısı yoksa yalnız sınırına kadar test et, kalanını EKSIKLER'e yaz. Ses: her olay doğru anda, doğru kanalda ve doğru düzeyde çalıyor; müzik ekran geçişlerinde kuralına göre değişiyor.
- 1280×720, 1920×1080, 2560×1440 ve 21:9'da keskin ve kırpılmasız; 1080p'de 60 FPS.
- Bütün paket testleri (106 vektör, 26 akış), mevcut testler ve 144 durumluk tablo son kez koşulur.

## Faz 4 · Rapor

`docs/v4/RAPOR.md` yaz ve kullanıcıya özetini ver:
- Ne yapıldı ve hangi commit'lerde.
- Test sayıları, gerçek komut çıktılarıyla.
- Durum tablosu özeti (144 durum): kaç durum eşleşti, kaçı bilinçli farklı, kaçı eksik.
- Bilinçli farklar.
- EKSIKLER, gruplanmış: tasarımda yok / kullanıcı kararı gerekiyor / dış hizmet gerekiyor (Google, mağaza, e-posta) / Unity kısıtı.
- "Pakette yok" sunucu özellikleri.
- Railway'e hazırlık listesi (dağıtım yapmadan): ortam değişkenleri, migration'lar, sağlık ucu, gizli anahtarlar, CORS, Docker imajı.

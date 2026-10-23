# Claude Code / Codex için görev metni

Aşağıdaki metni olduğu gibi Claude Code veya Codex'e verin. Paketin tamamı (bu klasör) Unity projesinin kökünde `Design/galaxy101_unity_handoff/` altında bulunmalıdır.

---

## GÖREV

Sen deneyimli bir Unity UI ve oyun istemcisi geliştiricisisin. **Galaxy 101** adlı 101 Okey oyununun **ana menü ekranını (1a) ve bu ekrandan açılan tüm pencereleri** Unity'de üretim kalitesinde kuracaksın.

Tasarımın tamamı `Design/galaxy101_unity_handoff/` klasöründedir. İşe başlamadan önce sırayla şunları **tamamen oku**:

1. `README.md`
2. `spec/01_TASARIM_SISTEMI.md` … `spec/07_KABUL_KONTROL_LISTESI.md` (hepsi)
3. `spec/data/themes.json`, `spec/data/content.json`, `spec/data/strings_tr_en.csv`
4. `screenshots/INDEX.md` ve ekran görüntülerinin hepsi
5. `reference/Galaxy101_AnaMenu_1a.dc.html` — kesin değerler (renk, ölçü, gölge, metin) için birincil kaynak. Spec ile HTML çelişirse **HTML'deki 1a değerleri geçerlidir**. 1b ve 1c bölümlerini yok say.
6. `reference/planet-renderer.js` ve `reference/starfield.js`

## TEKNİK KARARLAR (değiştirme)

- Unity **6 (6000.x) LTS** veya 2022.3 LTS; render pipeline **URP**.
- Arayüz: **UI Toolkit** (UXML + USS + C#). uGUI kullanma.
- Yazı: UI Toolkit Text (TextCore) + **Montserrat** (logo, başlık, buton) ve **Baloo 2** (gövde) font asset'leri; Türkçe glifler dahil.
- Tema: 5 gezegen teması, USS custom property'leri (`--bg1`, `--a1` …) ile; tema değişince kök elemana tema class'ı (`theme-saturn` vb.) atanır. Değerler `themes.json` ile birebir aynı.
- Dil: **Unity Localization** paketi; tablolar `strings_tr_en.csv`'den. TR varsayılan, EN tam.
- Gezegen: `planet-renderer.js` algoritmasının **HLSL shader** karşılığı (equirectangular doku + küresel arama + ışık/limb/atmosfer + Satürn halkası ve gölgeleri). Doku üretimi editör aracıyla bir kez yapılıp PNG olarak kaydedilebilir.
- Yıldızlar: **ParticleSystem** veya hafif bir C# mesh/shader çözümü; 3 katman paralaks, parıldama, kayan yıldız.
- Çözünürlük: Referans **1280×720**. PanelSettings → Scale Mode: *Scale With Screen Size*, Reference Resolution 1280×720, Match 0.5. 1920×1080 ve 2560×1440'da bulanıklık olmamalı (vektör/yüksek çözünürlük varlık).
- Mimari: tek `MainMenuScreen` + her pencere için ayrı `Controller` sınıfı; modal yığını (stack) servisi; `IEconomyService`, `IFriendsService`, `ILeaderboardService`, `IRewardsService`, `IProfileService`, `ISettingsService` arayüzleri ve `content.json`'dan beslenen **mock** uygulamaları.
- Ayarlar `PlayerPrefs` (veya JSON dosyası) ile kalıcı.

## YAPILACAKLAR

1. Proje iskeleti ve klasör yapısı (`spec/06_UNITY_UYGULAMA_REHBERI.md`).
2. Tasarım token'ları: `Theme.uss` (ortak) + 5 tema USS'i; tipografi class'ları; buton/kart/sekme/toggle/slider/girdi bileşenleri (`spec/01`).
3. Ana menü 1a: tüm öğeler, hover/press/seçili durumları (`spec/02`).
4. Arka plan: gezegen shader'ı, halka, glow, dönüş (16 sn/tur) ve yıldız alanı (`spec/04`).
5. Pencereler ve alt pencereler; aç/kapa animasyonları, Esc ve dışarı tıklama ile kapanma (`spec/03`, `spec/05`).
6. TR/EN yerelleştirme; sayı biçimi TR `125.400`, EN `125,400`; yüzde TR `%41`, EN `41%`; para `₺249,99` / `₺249.99`.
7. Mock servisler ve kalıcı ayarlar.
8. Performans: gezegen ve yıldızlar ekran görünmezken/arka plan hareketi kapalıyken durur; 60 FPS hedef.
9. `spec/07_KABUL_KONTROL_LISTESI.md` içindeki her maddeyi doğrula; ekran görüntüleriyle yan yana karşılaştır.

---

## GÖREV 2 — 3D OYUN MASASI

Ana menü bittikten sonra **3D oyun masası ekranını** eksiksiz kur: tüm oyun akışı, ıstaka etkileşimi, ikram/hediye sistemi, sohbet, oyuncu kartı, masa ayarları, temalar, görüş açıları, el sonu ve hata durumları.

### Okuma sırası
1. `spec/08_OYUN_MASASI_GENEL.md` … `spec/16_MASA_VARLIK_TESLIMI.md` (hepsi)
2. `spec/data/table_scene.json`, `spec/data/table_content.json`, `spec/data/table_strings_tr_en.csv`
3. `screenshots/table/INDEX.md` ve T01–T27
4. `reference/table/table3d.js`, `racks3d.js`, `tiles3d.js`, `gifts3d.js` — **3D değerlerin birincil kaynağı** (sahne, 6 ıstaka profili, 5 taş görünümü + rakam yolları, 10 ikram)
5. `reference/table/Galaxy101_OyunMasasi_3D.dc.html` — arayüz, akış ve mantık (`class Component`); prototipi yerel sunucuyla çalıştırıp her aşamayı dene
6. `assets_src/` — modeller: önce `blender_out/` (cilalı), yoksa `glb/` (ham). 28 dosyanın tamamı olmalı; eksikse dur ve bildir.

### Teknik kararlar (masa)
- URP + Bloom; **glTFast** ile `assets_src/` GLB'lerini içe aktar (`spec/16` D). Parça/malzeme isimlerini koru; gezegen renklerini malzeme adına göre çalışma anında uygula.
- Istaka modellerini yeniden modelleme; GLB'yi kullan. Taş kalınlığı 10 mm sabit.
- Taş yüzleri: `tiles3d.js → GLYPHS` SVG yollarından editör aracıyla pişirilen tek atlas (5 görünüm × [52 + sahte okey]). Rakamlar için font kullanma.
- Arka plan (gradyan + yıldız + gezegen) ayrı bir arka plan kamerasında; 3D masa ana kamerada üstüne.
- Animasyonlar kodla (tween); süreler ve eğriler `spec/12` ve `table_scene.json` ile birebir. Roket yolu ekran NDC noktalarından uçuş başında hesaplanır.
- Oyun modeli görünümden ayrı: her taşın kalıcı id'si; görünüm farkı tween'ler (`spec/14` §3).
- Istaka yerleştirme algoritmaları (`spec/11` §3–4) birim testleriyle doğrulanmalı (örnek dizilimler `spec/11`).
- Iridescence ve cam için Shader Graph (`spec/14` §4).
- Sunucu yok: `ITableService` mock'u prototipteki bot zamanlamalarını uygular (`spec/10` §4, `table_scene.json → timings_ms`).

### Yapılacaklar (masa)
1. Sahne hiyerarşisi, kamera görünümleri, ışıklar, oda/masa/ıstaka/koltuk prefab'ları; 4 masa + 6 ıstaka + 5 taş görünümü değiştirici (`SkinService`).
2. Taş sistemi: havuz, yüz atlası (Galaxy 101 Numerals, 5 görünüm, sahte okey dahil), 2 malzemeli gövde (yüz + kenar), deste instancing, gösterge, atma yığınları, açılan per alanı.
3. Kurallar ve tur sistemi: dağıtım (22/21), çekme/atma, süre ve uyarı, otomatik oyun, bot rakipler, Aç / Çift aç / Yere aç / İşle, bitirme, taş bitti, puanlama.
4. Istaka girdisi: hover/seçim/çift tık, tek taş sürükleme (en yakın boşluğa kaydırma), per toplu taşıma (360 ms / Shift, iki yan boşluk), canlı önizleme, desteden/yerden sürükleyerek çekme, perlere sürükleyerek işleme.
5. Efektler: dağıtım karması + girdap, iniş pop'ları, halka dalgaları, bitiş kutlaması, kamera sarsıntısı.
6. İkram/hediye: 10 model, panel (3 sekme, Herkes ×3), uçuş/iniş/idle, roket ekran turu, alıcı köşesi yuvaları, bildirim + teşekkür, emoji uçuşları.
7. HUD ve pencereler: üst bar, toast, oyuncu paneli, aksiyonlar, çapalı avatarlar ve balonlar, oyuncu kartı, sohbet, masa ayarları (4 sekme, hepsi işlevli ve kalıcı; Görünüm'de görüş açısı, masa, ıstaka, taş görünümü), bekleme/geri sayım, bitiş şeridi, el sonu tablosu, masadan kalk, bağlantı koptu.
8. TR/EN; 1280×720 referans, 1080p ve 2K keskin.
9. `spec/15_MASA_KABUL_LISTESI.md` maddelerini görüntülerle karşılaştırarak doğrula.

## ÇALIŞMA KURALLARI

- Hiçbir metni değiştirme, kısaltma veya yeniden yazma; `strings_tr_en.csv` ve HTML'deki metinleri birebir kullan.
- Değer uydurma. Spec'te veya HTML'de bulamadığın bir değer olursa HTML'den hesapla; yine yoksa en yakın benzer öğeyi örnek al ve bunu `IMPLEMENTATION_NOTES.md` dosyasına yaz.
- Her adımdan sonra derlenebilir durumda bırak. Büyük adımları ayrı commit'lerle yap.
- Bittiğinde `IMPLEMENTATION_NOTES.md` içinde: yapılanlar, spec'ten sapmalar ve gerekçeleri, bilinen eksikler.

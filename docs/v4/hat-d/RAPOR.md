# Hat D · veri varlıkları · rapor

Dal `v4/data` (taban `753287e`), worktree `C:/GD/Okey101-v4/data`. Unity çalıştırılmadı (tek süreç kilidi); bu dal Unity'nin okuyacağı dosyaları, dönüştürücü betikleri, fark matrisini ve Unity fazının yapacağı işlerin listesini hazırlar. Push yok.

Paket kökü `Design/Galaxy101_ClaudeCode/prototip/galaxy101_handoff_v4/` (aşağıda `pkg/`); çıktı kökü `Assets/Galaxy101/Resources/Galaxy101/` (aşağıda `Res/`).

## 1. Özet

| Alan | Paket | Sonuç |
|---|---|---|
| Metinler | 751 tablo + 241 satır içi + 49 hata metni (94 hata kodu) | 1017 tekil anahtar `Res/Strings/strings_v4.csv` + `.json`. Projeyle karşılaştırma: **691 var, 40 farklı, 284 yeni (eksik), 0 eksik metin** (2 anahtar pakette bilinçli boş: örneği olmayan kural bölümleri). Masa HUD tablosuna 60 anahtar eklendi, 10 metin bilinçli olarak eski haliyle kaldı (EK-403, EK-412). |
| Temalar | 5 gezegen | 395 değer projeyle aynı; tek ekleme `loadingRingOpacity` (pakette `ui.ro`, yükleme ekranı halkası). Masa renkleri (`table3d` ↔ `table_content.json planets_table`) aynı. |
| Fontlar | 11 TTF + 3 OFL | 7 sabit ağırlık paket dosyasıyla değişti (GUID korundu), `Nunito-700.ttf` yeni; OFL'ler aynı; 3 değişken font kopyalanmadı. FontAsset'ler yeniden üretilecek. |
| İkonlar | 69 SVG | `Res/Icons/v4/<id>.txt` (GalaxyIcon) + `svg/<id>.svg` + `icons.json`. 35 eski ikonla aynı, 3 çizgi kalınlığı farklı, 31 yeni; 63'ü GalaxyIcon ile çizilir, 6'sı çizilemez (EK-407). |
| Lig rozetleri | 75 SVG (63 rozet + 12 madalya) | `Res/Rank/<ad>.png` (dosya adındaki boyut) + `@2x.png` + `svg/` + `ranks.json`. |
| İmleçler | 11 SVG | `Res/Cursors/<ad>.png` 32 px + `@2x` 64 px (Texture Type Cursor) + `svg/` + `cursors.json` (sıcak noktalar). |
| Ses | 28 efekt + 2 müzik WAV, `events.json` | `Res/Audio/Sfx/*.wav`, `Res/Audio/Music/*.wav`, `Res/Audio/sound_events.json` (olaylar, kanal, öncelik, poly, kısma, geçiş, motor olayı eşlemesi, ekran müziği). Hepsi 44,1 kHz / 16 bit / stereo; müzik 32,0 sn ve 40,0 sn. |

Fark matrisi: `docs/v4/matris/hat-d.md` (627 satır + 40 farklı metin eki). Tüm anahtarların karşılaştırması: `docs/v4/hat-d/strings_compare.csv`. Eksikler: `docs/v4/hat-d/EKSIKLER.md` (EK-401…EK-413).

## 2. Commit'ler

| Commit | İçerik |
|---|---|
| `b400721` | Dönüştürücü (`scripts/import-v4-data.py`), güncellenen `scripts/import-galaxy-ui-assets.py`, matris üreticisi (`tools/v4/hat_d_matrix.py`) ve bütün `Res/` çıktıları (885 dosya, `.meta`'larla) |
| bu raporu içeren commit | Koşullu metin düzeltmesi (`gave`/`gaveAll` masa tablosunda eski metinde), matris, RAPOR, EKSIKLER, `strings_compare.csv` (kimlik: `git log v4/data`) |

## 3. Çıktılar ve istemcinin okuma şekli

| Dosya (`Res/` altı) | Kaynak (`pkg/`) | Biçim | İstemci |
|---|---|---|---|
| `Strings/strings_v4.csv` | `strings/strings.json` | UTF-8 BOM, `"key","tr","en"`, 1017 satır | `LocalizationCatalog.Parse` ile birebir okunur (3 sütun, başlık `key,tr,en`; doğrulandı). Unity Localization CSV içe aktarımına da uygun. |
| `Strings/strings_v4.json` | `strings/strings.json` | `strings{anahtar:{tr,en,params?,unusedParams?,pkgKey?,uses?}}`, `errors{kod:{ui,state?,key?,source?,params?}}`, `format`, `keyRules`, `notes` | Faz 2'de anahtar tabanlı yükleyici (§6.3). `errors` protokol hata kodunu UI türüne (toast/inline/shake/dialog/screen/silent/resync) ve metin anahtarına bağlar. |
| `Table/Strings/table_strings.csv` | `strings.json` `table.STR.*` | mevcut biçim; 60 anahtar eklendi | `TableStrings.Get` (değişiklik gerekmez). |
| `themes.json` | `assets/themes.json` + `strings.json menu.STR.pn/pd` | mevcut biçim + `loadingRingOpacity` | `MenuModel.Themes` (değişiklik gerekmez); yükleme halkası için yeni alan. |
| `Fonts/*.ttf` | `assets/fonts/*.ttf` | aynı yollar, `Nunito-700.ttf` yeni | `MenuView.Font()` `Resources.Load<Font>`; FontAsset'ler yeniden (§6.2). |
| `Icons/v4/<id>.txt`, `Icons/v4/svg/<id>.svg`, `Icons/v4/icons.json` | `assets/icons.json` + `icons/*.svg` | `.txt` = SVG, C2PA `<metadata>` bloğu çıkarılmış (geometri aynı) | `view.Icon("v4/<id>", …)` → `GalaxyIcon` (`Resources.Load<TextAsset>("Galaxy101/Icons/" + ad)`, kod değişmeden çalışır). |
| `Rank/<ad>.png`, `Rank/<ad>@2x.png`, `Rank/svg/<ad>.svg`, `Rank/ranks.json` | `assets/ranks.json` + `rank/*.svg` | PNG resvg ile; `ranks.json` = paket dosyası + `emblems{ad:{kind,league,tier,size,divisionMarks?,png,png2x,svg}}` | `Resources.Load<Texture2D>(emblems[ad].png)`; lig renkleri/çubuk/çerçeve (`leagues`) kodla çizilen parçalar için. |
| `Cursors/<ad>.png`, `@2x`, `svg/`, `cursors.json` | `assets/cursors.json` + `cursors/*.svg` | `cursors{ad:{png,png2x,size,hotspot,hotspot2x,svg}}`, `rules` | `Cursor.SetCursor(tex, new Vector2(hx, hy), CursorMode.Auto)`. |
| `Audio/Sfx/*.wav`, `Audio/Music/*.wav`, `Audio/sound_events.json` | `sound/` | §6.8 | Faz 2 ses servisi. |

Her yeni dosya ve klasörün `.meta`'sı var (437 meta): GUID `uuid4().hex`; içe aktarıcı bloğu projedeki benzerinden kopya — klasör `Icons.meta`, metin `Icons/close.txt.meta`, TTF `Fonts/Nunito-900.ttf.meta`, PNG `Artwork/logo-saturn.png.meta`, WAV `Assets/Okey101/Art/Audio/draw.wav.meta`; SVG için projede örnek olmadığından iki satırlık biçim. İmleç PNG'lerinde aynı PNG bloğu, `Cursor.SetCursor`'ın istediği üç alanla: `textureType: 7` (Cursor), `isReadable: 1`, `enableMipMap: 0`. Mevcut `.meta`'lara dokunulmadı; bütün projede yinelenen GUID yok (kontrol edildi).

## 4. Komutlar ve çıktılar

```
uv run --no-project --with resvg-py --with fonttools --with pillow python scripts/import-v4-data.py
  strings: uniqueKeys 1017, byGroup {table 751, inline 241, error 25}, duplicateInlineKeys 23, errorConflicts 0
  themes: changes 5 (loadingRingOpacity × 5), packageProblems 0, tableChanges 0
  fonts 14 dosya · icons 69 (GalaxyIcon uyumsuz 6) · ranks 75 (63 rozet, 12 madalya) · cursors 11
  sound: events 28, music 2, wav 30, offFormat 0
  885 file(s) written
... --check  →  0 file(s) would change   (ikinci koşu aynı çıktıyı verir; .meta GUID'leri sabit)

uv run --no-project --with fonttools --with pillow python scripts/import-galaxy-ui-assets.py
  Fonts: 9 static weights with Turkish glyphs.
  Out of date: Artwork/logo-*.png (5)   ← USS güncel (renkler değişmedi); logolar §5.4

uv run --no-project --with fonttools python tools/v4/hat_d_matrix.py
  Temalar var 395 · eksik 6 · farklı 3 | Fontlar farklı 7 · eksik 5 · var 6 · pakette yok 1
  İkonlar var 35 · farklı 3 · eksik 31 · pakette yok 4 | Lig rozetleri eksik 75 | İmleçler eksik 11
  Ses eksik 34 · farklı 8 · var 3 | Metinler var 691 · farklı 40 · eksik 284 · boş 2 · masa anahtarı pakette yok 85
  docs/v4/matris/hat-d.md: 627 rows + 40 changed strings; strings_compare.csv: 1017 keys
```

Unity'siz doğrulamalar (betikler `scratchpad`'de, sonuçlar burada):
- `strings_v4.csv` C# `LocalizationCatalog.Parse` mantığının Python kopyasıyla: 1017 satır, hepsi 3 sütun, JSON ile birebir; 49 hata metni anahtarı çözülüyor. Masa tabloları `TableStrings` okumasıyla 343 anahtar (283 + 60).
- Yerel tablo taklidi (`LocalizationCatalog.Load` sırası): katalog 322 satır, native tablo 312; 53 TR uyuşmazlığı `content.row0259` "Bronz III" ↔ "Elmas I" ile başlıyor (EK-410).
- Rozet/imleç PNG'leri boyut denetimi (her biri beklenen piksel boyutu) ve gözle kontrol (temas sayfası).

## 5. Bulgular

### 5.1 Metinler
- Anahtar şeması paketten aynen: `<ekran>.<tablo>.<yol>`; satır içi anahtarlar `<ekran>.inline.<alan>@<satır>`. 23 tekrarlı satır içi anahtar `#2`, `#3` aldı (EK-401). Hata metinleri: 24 kod oyunda zaten görünen metnin anahtarını kullanır (`errors[kod].key`), 22 yeni metin `err.E_*`, 3 masa kuralı metni `table.STR.engErr.E_SIDE_RETURNED` / `E_MELD_MIX` / `E_ISLE_DISABLED` (yalnız hata listesinde vardı).
- Karşılaştırma yöntemi: `table.STR.<k>` ↔ masa anahtarı `<k>`; geri kalanı TR+EN metin çifti üzerinden (menü yerelleştirmesi Türkçe metinle aranıyor: `LocalizationBridge.Text(tr, en)`). Kaynaklar: `strings_tr_en.csv`, `additional_strings.csv`, `content/themes/supplemental/table_content.json`, C# `L("tr","en")` / `Text("tr","en")`.
- 40 farklı metnin çoğu EN üslup farkı (örn. `app.STR.howCont` "How do you want…" ↔ C# "How would you like…"), birkaçı anlam/biçim: `shared.rank.diamond` EN "Diamond" ↔ eski satır içi listede "Gems" (bağlam farkı), `ranked.STR.cancel` "İptal" ↔ "Vazgeç", `lobby.STR.tierOk` "Uygun" ↔ "Kullanılabilir", `table.STR.diceCap` "DIE" ↔ "DICE". Tam liste matris Ek A.
- Menü metinleri C#'ta sabit (`L("…","…")`); CSV'yi değiştirmek ekranda bir şey değiştirmez. v4 metinleri ekranlara Faz 2'de anahtar tabanlı yükleyiciyle girer (§6.3). Bu yüzden `strings_tr_en.csv` ve `additional_strings.csv`'ye dokunulmadı (testler 194 satır bekliyor).
- Masa HUD (`TableStrings`): v4 metni aynı anahtarda değişen 10 kaydın hiçbiri güvenle değiştirilemedi: 8'i liste sırası kayması (EK-403), 2'si koşullu fonksiyonun tek dalı (EK-412). Yer tutucu uyuşmazlığı 0. 60 yeni anahtar eklendi (dağıtım töreni `ds*`, `bomb*`, `penCode.*`, `engErr.*`, `roll`, `undo*`, `return*` …); 85 proje anahtarının pakette karşılığı yok (silinmedi, HUD kullanıyor; matris Ek C).
- `app.STR.tag` tek satır sonlu metin. Depo `text=auto` + Windows `autocrlf`: CSV içindeki satır sonu çalışma kopyasında `\r\n` olur; JSON'da `\n` kalır. Yükleyici JSON'u okursa ya da `\r`'yi atarsa fark olmaz.

### 5.2 Temalar
- `ui` renkleri, `rings`, `atmosphere`, `logo`, `menu.orb`, `pf` projeyle bire bir aynı; TR/EN adlar ve açıklamalar `strings.json menu.STR.pn.*` / `pd.*` ile aynı.
- Pakette aynı adlı iki ayrı `ro` var: `ui.ro` ana oyunun yükleme ekranındaki halka opaklığı (`Galaxy 101 - Gerçek Ana Oyun.dc.html:279`, `ld.ro`), `rings.ro` menü gezegen halkası (G101-Menu `RINGS`). Projedeki `cssVars.ro` ikincisi; birincisi `loadingRingOpacity` olarak eklendi (Satürn 0,85 · Neptün 0,35 · Jüpiter 0,25 · Mars/Venüs 0).
- `vars.fL/fH/fB` gezegen başına değer taşımıyor (fonts.json rolleri); `transition` (CSS değişkenleri 0,6 sn, gezegen 1 sn `cubic-bezier(.6,0,.2,1)`) animasyon verisi, Faz 2'de `anim/easings.json cssPlanetMove` ile.
- `Theme.*.uss` güncel (renk değişmedi); `import-galaxy-ui-assets.py --write-uss` gerekmedi.

### 5.3 Fontlar
- Paketteki değişken Montserrat ve Baloo 2 dosyaları, projenin eski sabit ağırlıklarını kestiği kaynaklarla aynı (SHA-256 `0f7b311b…`, `d47a6852…`, eski betikte `PINNED`). Paketteki sabit ağırlıklar ise yayıncının statik sürümleri: 85 örnek karakterde ilerleme genişlikleri birebir aynı; Montserrat'ta çizim noktaları farklı (statik sürüm, ttfautohint), Baloo 2'de çizimler aynı, glif sayısı 1601 → 1585 ve 3 karakterin glif indeksi farklı. Eski dosyaların ad tablosu "Montserrat Thin" / "Baloo 2 Regular" idi; paket dosyaları doğru aile/alt aile adı taşıyor.
- `Nunito-700.ttf` (fonts.json `versionTag`, `app.version` öğesi) yeni. Projedeki `Nunito-900.ttf` pakette yok (yalnız menüdeki indirim rozeti kullanıyor; Faz 2 menüsüyle değişir).
- Türkçe ve özel karakterler (`çğıİöşüÇĞÖŞÜ₺×·—…`) bütün sabit dosyalarda var.

### 5.4 Logolar
`Artwork/logo-*.png` Montserrat 700 ile önceden çizilmiş "GALAXY" yazısı. Tema renkleri değişmedi; font dosyası değiştiği için yeniden çizim 1503 kenar pikselinde fark veriyor. Yeniden üretilmedi (görsel karar Faz 2 karşılaştırmasına kalsın): `scripts/import-galaxy-ui-assets.py --logos`.

### 5.5 İkonlar
- Paket ikonları kullanım yerine göre ayrı dosya (aynı çizim farklı `stroke-width` ile birkaç kez). Geometri eşleşmesiyle 38 ikonun eski karşılığı bulundu (35 aynı, 3 çizgi kalınlığı farklı: `app-splash-tip-box` 2.4 ↔ `tip-bulb` 2.2, `lobby-t-name` 3 ↔ `lock` 2.6, `icon-30` ↔ `crown`); 4 eski ikonun (`back`, `eye`, `table-tip`, `table-wifi-off`) pakette karşılığı yok. Eski adlar (`Icons/*.txt`) dokunulmadan duruyor; çağıranlar Faz 2'de v4 kimliklerine geçer.
- SVG'lerdeki C2PA içerik kimliği bloğu `.svg` kopyalarda aynen, `.txt` kopyalarda çıkarılmış (çizime etkisi yok, dosya başına ~8 KB).

### 5.6 Lig rozetleri ve imleçler
- Rozetler filtre ve metin içerdiği için Unity'nin SVG içe aktarıcısıyla birebir çizilemez (EK-404); resvg 0.48.1 (resvg-py 0.5.0) ile PNG, metin paketteki Montserrat (Black 900), galaksi taşındaki ★ için Segoe UI Symbol (EK-405). Boyutlar dosya adından (`_24/_52/_160`, madalya `_16/_24`), `@2x` iki katı (1280×720 referans panelin 2560×1440 ölçeği). Kademe taşı sayısı `g101-rank.js emblem()`: n = 4 − kademe (I → 3, II → 2, III → 1).
- İmleçler 32 px (`cursors.json size`) ve 64 px; sıcak nokta paket değeri, `@2x` için iki katı.

### 5.7 Ses
- `sound_events.json` alanları ve kaynakları: `events` (cat, prio, poly, ms, clip, trigger — events.json), `channels` / `toggles` (`g101-sound.js` `CAT`, `gainFor`: tile → `tileSnd`, chat → `chatSnd` **ve** `notifSnd`), `volume.defaults` (`S()`: master 80, sfx 90, music 55), `volume.opponentDrawVolume` 0,7 (G101-Masa `sfx()`), `mixing.duck` (öncelik ≥ 5 → müzik ×0,4, 0,3 sn iniş, 2,4 sn'de geri), `crossfadeOutSeconds` 1,2 / `fadeInSeconds` 1,5 (`setMusic()`), `sync` (dice 1450, nuke 1750, roket 5600 ms), `engineEvents` (README tablosu + tetikleyiciler), `screenMusic` (masa → `music.table`, diğer → `music.menu`) ve `screenChange` (`ui.screen`, açılıştan çıkarken çalmaz).
- WAV'lar `ms` değerinden uzun (sentez kuyruğu: `render()` süresi `ms/1000 + 0,6`, en az 0,3 sn); `poly` sayımı `ms` ile yapılmalı, klip uzunluğuyla değil.
- Ayar varsayılanları menüde ve masada farklı (EK-402); taş sesleri ürün kararı (EK-413).

## 6. Unity fazının yapacakları

### 6.1 İçe aktarma
1. `v4/data` ana kopyaya birleşince Unity ~430 yeni varlığı içe aktarır (LFS: 172 PNG, 30 WAV, 8 TTF).
2. SVG'ler (`Icons/v4/svg`, `Rank/svg`, `Cursors/svg`) iki satırlık `.meta` ile gelir. İkonlar için içe aktarma türü **UI Toolkit Vector Image** seçilmeli (yalnız GalaxyIcon'un çizemediği 6 ikon için gerçekten gerekir). Rozet/imleç SVG'leri çalışma anında kullanılmıyor (PNG var); içe aktarıcı `<text>`/`filter` için uyarı verirse bu SVG'ler `Resources` dışına taşınabilir — çizim farkı olmaz.
3. Müzik WAV'ları WAV bloğuyla geldi (Decompress On Load, Vorbis). 32/40 sn stereo için **Load Type = Streaming** önerilir (bellek); efektler olduğu gibi kalabilir.

### 6.2 Fontlar (FontAsset yeniden üretimi)
- `Fonts/Montserrat-600/700/800/900.asset`, `Fonts/Baloo2-600/700/800.asset`: kaynak TTF değişti. GUID'i korumak için her biri için `FontAsset.ClearFontAssetData(true)` ve ardından `MenuAssetBuilder`'daki karakter kümesiyle `TryAddCharacters` (silip yeniden oluşturmak `TextSettings.asset` → `Baloo2-700.asset` bağını koparır).
- `MenuAssetBuilder.cs:38` listesine `"Nunito-700"` eklenip `Nunito-700.asset` üretilmeli (`app.version` etiketi, Nunito 700 10 px).
- `Nunito-900` (pakette yok) indirim rozeti v4 menüsüne geçince kaldırılabilir.

### 6.3 Metinler
- **Kırmızı test (EK-410):** Unity'de `Galaxy 101/Import TR-EN Localization` (`LocalizationImporter.Import`). 53 kaymış satır ve 10 eksik anahtar yazılır; "Bronz III" testi geçer. (`AddEntry` var olan anahtarın değerini güncellemiyorsa üç tablo varlığı — `Galaxy101 UI_tr/en.asset`, `Shared Data` — silinip yeniden üretilmeli; `Locale-*.asset` ve `LocalizationSettings.asset` kalır.)
- **v4 metinleri:** önerilen C# (Faz 2): `Strings/strings_v4.json`'u okuyan anahtar tabanlı yardımcı (`V4Text.Get(key, lang, params object[] args)`, `string.Format` ile `{0}`…), ya da `strings_v4.csv`'den ikinci bir StringTableCollection ("Galaxy101 v4", anahtarlar paketle aynı). Hata gösterimi `errors[kod]` (`ui` türü + `key`); `silent`/`resync` metinsiz.
- `unusedParams` taşıyan 4 kayıt (EK-412) metin değil kod: fiil/şifre dalı C#'ta.
- Masa listeleri (EK-403): üç görünüm (`seat`, `mid`, `top`) ve 11 akış Faz 2 kodunda; ardından `views.1.*` ve `flows.4…9` metinleri v4'e çekilir (elle ya da `update_table_strings` içindeki kayma denetimi bu anahtarlar için kapatılarak; denetim eski metni paket listesinde başka sırada gördükçe metni korur).

### 6.4 Test yolları (EK-411, 31 test)
Veri tarafı hazır; C# değişikliği:

| Dosya:satır | Şimdi | Önerilen |
|---|---|---|
| `Tests/EditMode/MenuServicesTests.cs:24,25` · `NativePanelTests.cs:24` · `FramePacingTests.cs:77` | `Design/galaxy101_unity_handoff/spec/data/content.json`, `…/themes.json` | `Assets/Galaxy101/Resources/Galaxy101/content.json`, `…/themes.json` (eski dosyadan farkı: 24 lig adı, `loadingRingOpacity`; testlerin baktığı değerler aynı) ya da birebir eski kopya `Design/old/galaxy101_unity_handoff/spec/data/…` |
| `Tests/EditMode/MenuServicesTests.cs:129` | `…/spec/data/strings_tr_en.csv` | `Assets/Galaxy101/Resources/Galaxy101/strings_tr_en.csv` (bayt bayt aynı, 194 satır) |
| `Tests/Editor/GalaxyCaptureTests.cs:18` | `../Design/galaxy101_unity_handoff/screenshots` | `../Design/old/galaxy101_unity_handoff/screenshots` (57 PNG, `GalaxyCaptureDriver.ReferenceFiles` ile aynı küme); v4 durum listesine geçince `pkg/screenshots` |

### 6.5 İkonlar
`view.Icon("v4/<id>", boyut, renk)` bugünkü `GalaxyIcon` ile çalışır (yol birleştirme). 6 ikon için (EK-407) GalaxyIcon'a `<g>` içine inme ve gradyan desteği ya da `Icons/v4/svg/<id>` VectorImage. Hangi öğenin hangi ikonu kullandığı `icons.json` `usedBy` (`data-g101-id`) ve `uses`.

### 6.6 Lig rozetleri
`ranks.json emblems[<lig>[_<I|II|III>]_<boyut>]`: `png` / `png2x` (`Resources.Load<Texture2D>`), panel ölçeği > 1 ise `@2x`. Galaksi sıra etiketi (`#n`) çalışma anında (EK-406). Lig adları `strings_v4` `shared.rank.*`; renk/çubuk/çerçeve/LP tabloları `ranks.json` (`leagues`, `lpDeltas`, `rewardChips`, `steps`).

### 6.7 İmleçler
`cursors.json`: `Cursor.SetCursor(Resources.Load<Texture2D>(c.png), new Vector2(c.hotspot[0], c.hotspot[1]), CursorMode.Auto)`; yüksek DPI'da `png2x` + `hotspot2x`. Öncelik kuralları `rules` (öğe `data-cursor` > metin alanı > satır içi > varsayılan; meşgulken kilit; taş sürüklerken grab → grabbing → drop) UI Toolkit işaretçi olaylarına bağlanır.

### 6.8 Ses
- Yeni ses servisi `Audio/sound_events.json`'u okur: olay → klip (`Resources.Load<AudioClip>(clip)`), kanal kazancı `master/100 × (music | sfx)/100`, kategori anahtarı kapalıysa 0, `poly` kadar eş zamanlı kopya (süre `ms`), öncelik ≥ 5'te müzik kısma, menü ↔ masa müzik geçişi (1,2 sn sönme, 1,5 sn açılma), rakip taş çekişi ×0,7.
- Ayarlar: menü ayar modeli (`content.json settingsDefaults`) `chatSnd` içermiyor, masa (`table_content.json`) `notifSnd` içermiyor; ses servisi ikisini de `true` varsayar (`volume.defaults`). Müzik varsayılanı EK-402.
- Motor olayı eşlemesi `engineEvents` (drew, discarded, opened, laid, added, okeySwapped, penalty, finished, handEnd, turn, dealStart, dealDie, dealStep, gift, chat). `Okey101 TableAudio` (3 kayıt klibi) bu servisle değişir (EK-413).

### 6.9 Temalar
`loadingRingOpacity` yükleme ekranındaki halkanın opaklığı (`AuthFlow` splash; prototipte `ld.err` iken 0,4). Diğer tema değerleri değişmedi; USS yeniden üretimi gerekmez.

## 7. Betikler

| Betik | Ne yapar |
|---|---|
| `scripts/import-v4-data.py` | Bütün veri bölümleri (`--only strings,themes,fonts,icons,ranks,cursors,sound`, `--check`). Yalnız içeriği değişen dosyayı yazar; yeni dosyaya `.meta` ekler, var olana dokunmaz. Rapor: `docs/v4/hat-d/import_report.json`. |
| `scripts/import-galaxy-ui-assets.py` | v4 yollarına güncellendi: font denetimi; `--write-uss` tema USS, `--logos` logo PNG, `--legacy-icons` eski 44 ikon (`Design/old/…`). Varsayılan yalnız denetler. Eski Google Fonts indirmesi kalktı (fontlar paketten). |
| `tools/v4/hat_d_matrix.py` | Matris + `strings_compare.csv`; "projede ne vardı" `git show 753287e:` ile okunur (LFS dahil), tekrar üretilebilir. |

Gereken araçlar `uv` ile geçici ortamda: `resvg-py` (rasterleme), `fonttools`, `pillow`.

## 8. Riskler
- FontAsset'ler yeniden üretilmeden yeni karakter eklenirse yanlış glif (EK-409) — §6.2 ilk iş olmalı.
- `Resources` altına eklenen ~430 dosya build'e girer (WAV 19 MB ham; Vorbis ile küçülür; PNG 3,3 MB; SVG 1,1 MB). Rozet/imleç SVG'leri çalışma anında gerekmiyorsa `Resources` dışına alınabilir.
- Galaksi ★ rasterlemesi Windows fontuna bağlı (EK-405); başka makinede betik uyarır, PNG'ler depoda olduğu için yeniden üretmek gerekmez.

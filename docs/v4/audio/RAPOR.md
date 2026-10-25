# Faz 2 · Ses ve imleç · rapor

Dal `v4/audio` (taban `main` 59aa81c + `v4/data` birleşimi), worktree `C:/GD/Okey101-v4/audio`. Unity çalıştırılmadı; derleme `tools/v4/unity_compile.py` ile, kural mantığı ayrıca Unity dışında bir .NET konsol koşusuyla doğrulandı (§8). Push yok.

Kaynaklar: paket `Design/Galaxy101_ClaudeCode/prototip/galaxy101_handoff_v4/sound/` (`events.json`, `README.md`), prototip `g101-sound.js`, `g101-cursor.js`, `g101-settings.js`, `G101-Masa.dc.html` `sfx()`, `Galaxy 101 - Gerçek Ana Oyun.dc.html` `go()`/`flash()`; veri hat D'nin `Resources/Galaxy101/Audio/sound_events.json` ve `Resources/Galaxy101/Cursors/cursors.json` dosyaları. Kodda kaynaksız sayı yok; her sabitin yanında kaynağı yazılı.

## 1. Ne yapıldı

Yeni klasör `Assets/Galaxy101/Scripts/Audio/` (ad alanı `Galaxy101.Audio`, derleme `Galaxy101.Runtime`):

| Dosya | İçerik |
|---|---|
| `SoundCatalog.cs` | `sound_events.json` okuyucu: 28 olay (kategori, öncelik, poly, ms, klip), 2 müzik, kanal/anahtar eşlemesi, varsayılanlar, kısma, geçiş süreleri, `sync`, `engineEvents`, `screenMusic`, `screenChange`. Tutarlılık denetimi (bilinmeyen kategori/ses/müzik hata verir). |
| `SoundLevels.cs` | Ayarlar (master, sfx, music 0–100; tileSnd, chatSnd, notifSnd) ve kural: `kazanç = master/100 × (müzikse music, değilse sfx)/100`, kategori anahtarlarından biri kapalıysa 0 (`g101-sound.js gainFor`). `With(JObject)` bir ayar nesnesindeki anahtarları üst üste yazar. |
| `ParamCurve.cs` | WebAudio AudioParam'ın prototipte kullanılan kısmı: `setValueAtTime`, `linearRampToValueAtTime`, `setTargetAtTime`, `cancelScheduledValues`. |
| `SoundEngine.cs` | Ses çıkışı olmayan motor (Unity'siz test edilir): `Play` (günlük → sessiz mi → sıfır kazanç mı → öncelik ≥ 5 ise müziği kıs → poly sınırı → çal), müzik geçişi (eski 1,2 sn'de sessize, yeni 1,5 sn'de açılır), kısma (0,3 sn'de %40, 2,4 sn'de geri), ayar değişiminde 0,1 sn zaman sabiti, ekran değişimi (`ui.screen`, açılıştan çıkarken değil; müzik `screenMusic`). |
| `TableSounds.cs` | Masa için motor/protokol olayı → ses eşlemesi (§5). |
| `GalaxyAudio.cs` | Oyunun ses girişi (prototipteki `G101Sound`): `Play`, `Toast`, `ApplySettings`, `SetLevels`, `TrackScreens`, `Log`, `Muted`, `Table`. |
| `GalaxyAudioHost.cs` | Sahneler arası kalan "Galaxy audio" nesnesi: efekt kaynak havuzu, müzik kaynakları (eğriyi her kare uygular), gerekirse `AudioListener`, ekran izleme, `ui.tap`, imleç sürücüsü. Sessiz koşularda AudioSource/dinleyici açmaz; Galaxy ekranı yokken kaynaklarını bırakır. |
| `CursorSet.cs` | `cursors.json`: 11 imleç, @1x/@2x, sıcak noktalar; mod (Normal / Büyük ×1,3 / Sistem) ve ekran ölçeğine göre boyut ve kaynak seçimi. |
| `CursorRules.cs` | `g101-cursor.js resolve()` UI Toolkit karşılığı ve `ui.tap` kuralı (§3). |
| `GalaxyCursor.cs` | İmleç girişi (mod, dünya imleci, sürükleme tutuşu, `Mark`, `Lock`) ve `Cursor.SetCursor` sürücüsü (yeniden örnekleme dahil). |
| `UiPicker.cs` | Ekran noktasının altındaki UI öğesi, bütün `UIDocument` panellerinde (`elementFromPoint` karşılığı). |

Testler: `Tests/EditMode/AudioServiceTests.cs` (Galaxy101.Service.Tests; 19 test) ve `Tests/Editor/TableSoundsTests.cs` (Galaxy101.Editor.Tests, `Okey101.Core` başvurusu olan EditMode derlemesi; 5 test).

Paket bildirimi değişmedi: `UnityEngine.AudioModule` derlemede var (`com.unity.ugui` → `com.unity.modules.audio`, `Packages/packages-lock.json`), `Unity.InputSystem` Galaxy101.Runtime'ın başvurusunda.

## 2. Ses kuralları (kaynakları)

| Kural | Değer | Kaynak |
|---|---|---|
| Kanal | ui, game, tile, chat → sfx; music → music | `g101-sound.js CAT`, `sound_events.json channels` |
| Kategori anahtarı | tile → tileSnd; chat → chatSnd **ve** notifSnd | `gainFor`, `toggles` |
| Varsayılan | master 80, sfx 90, music 55, anahtarlar açık | `S()`, `volume.defaults` (menü 60 farkı EK-901) |
| Duyulmaz eşik | kazanç ≤ 0,001 çalmaz | `play()` |
| Rakip taş çekişi | ×0,7 | `volume.opponentDrawVolume` |
| Poly | aynı olaydan en çok `poly` kopya, her kopya `ms` boyunca sayılır, doluysa yeni kopya düşer | `play()` `live[id]` |
| Kısma | öncelik ≥ 5 (penalty, finish, finish.nuke): müzik 0,3 sn'de %40, 2,4 sn'de geri; poly sınırına takılan da kısar (prototip sırası) | `mixing.duck`, `duck()` |
| Müzik geçişi | eski 1,2 sn doğrusal 0,0001'e, yeni 0,0001'den 1,5 sn'de kazanca; aynı müzik tekrar istenirse bir şey olmaz | `setMusic()`, `crossfadeOutSeconds`, `fadeInSeconds` |
| Ayar değişimi | müzik `setTargetAtTime(kazanç, t, 0,1)` | `G101Settings.on` |
| Ekran müziği | "game" → music.table, diğer her ekran → music.menu | `screenMusic`, `go()` |
| Ekran sesi | ekran değişince `ui.screen`, açılıştan (splash) çıkarken çalmaz | `screenChange`, `go()` |
| Tost | altın → ui.reward, kırmızı → ui.error | `flash()` |
| Günlük | son 200 olay (id, zaman, koltuk); sessizken de yazılır | `log` |

## 3. İmleç

- Mod: `GalaxyCursor.Mode` = `Normal` / `Büyük` / `Sistem`, PlayerPrefs `galaxy101.cursor` = `{"mode":"…"}` (prototipin localStorage anahtarı ve biçimi). Menüde satırı yok (EK-904).
- Boyut: 32 px × `Screen.dpi/96` × (Büyük ise 1,3). 32 → @1x, 64 → @2x birebir; diğerleri yeniden örneklenir (UF-913).
- Seçim sırası (`CursorRules.Resolve`, prototipteki `resolve()`): sürükleme tutuşu (`GalaxyCursor.Hold`) → `ew`/`ns` üstünde basılıyken o imleç → en yakın kilit sınıfı `g-cursor-lock-<ad>` (`data-cursor-lock`, örn. `wait`) → hedeften yukarı ilk kuralı olan öğe: kaydırma çubuğu (`Scroller` yönüne göre ns/ew) → `g-cursor-<ad>` sınıfı (`data-cursor`, `none` gizler) → metin alanı (`text`; pasif/salt okunur `no`) → kaydırıcı (`GalaxySlider`, `Slider`… `ew`) → düğme/anahtar/açılır liste (`pointer`; pasifse `no`) → `default`. UI yoksa (3D masa) `GalaxyCursor.World`. Basılıyken `grab` → `grabbing`.
- `ui.tap`: basılan öğe ya da en yakın 5 üstünden biri kendi kuralıyla `pointer` ise (`g101-sound.js`: 6 öğeye bakar). 3D alan (UI yok) çalmaz (prototipte `CANVAS`'ta durur). Fare sol/sağ/orta ve dokunma.

## 4. Paylaşılan dosyalara eklenen satırlar (diğer hatlar korumalı)

| Dosya (sahibi) | Satır | Ne yapar |
|---|---|---|
| `Scripts/Screens/MainMenuController.cs` (menü) | `Initialize()`, `Model.Changed+=ApplySettings;` satırının altına (45): `Galaxy101.Audio.GalaxyAudio.TrackScreens(this,()=>Auth!=null&&Auth.Visible?Auth.Screen:Lobby!=null&&Lobby.Visible?"lobby":ranked!=null&&ranked.Visible?"ranked":"menu");` | Ekran kaynağı: açılış/giriş alt ekranları (`AuthFlow.Screen`), lobi, dereceli kuyruğu, menü. Ekran değişimi → `ui.screen` + müzik. Ses nesnesini de kurar. |
| `Scripts/Screens/MainMenuController.cs` (menü) | `ApplySettings()` (205): `if(!offscreen)AudioListener.volume=…master…/100f;` yerine `Galaxy101.Audio.GalaxyAudio.ApplySettings(Model.Settings);` (kullanılmayan `offscreen` satırı silindi) | Menü ses ayarları → ses servisi (master artık kaynak başına, UF-908). |
| `Scripts/Core/MenuView.cs` (menü) | `Flash()` içinde `if(toastLayer==null)return;` altına (278): `Galaxy101.Audio.GalaxyAudio.Toast(kind);` | Kabuk tostu: `gold` → ui.reward, `bad` → ui.error. |
| `Scripts/Screens/LobbyScreen.cs` (lobi) | `Flash()` içinde `if (toastLayer == null) return; LastToast = text;` altına (484): `Galaxy101.Audio.GalaxyAudio.Toast(kind);` | Lobinin kırmızı tostu → ui.error (katalog kuralı; prototip lobisi sessizdi, EK-906). |
| `Scripts/Components/GalaxyFilters.cs` (ortak) | `PressSink()` başına (193): `Galaxy101.Audio.CursorRules.Mark(element,"pointer");` | Basılınca çöken her öğe (giriş/lobi `Tap()`, masa HUD `Button(…)`) pointer imleci gösterir ve `ui.tap` çalar. |

Ekran adları önemli değil, yalnız `"splash"` (sessiz çıkış) ve `"game"` (masa müziği) anlamlı. Dereceli hattı tam ekran bir Dereceli ekranı kurarsa ilk satırdaki ifadeye `"ranked"` dalını kendi `Visible`'ıyla eklemeli.

## 5. Masa entegrasyonu (orkestratör) — çağrılacaklar

Masa dosyalarına dokunulmadı. Ses nesnesi menüden gelir ve sahneler arası kalır; `GalaxyTable` sahnesi etkin olunca ekran kendiliğinden "game" olur (masa müziği, `ui.screen`). Masa:

1. **Ayarlar**: masa ayarları açılınca ve her `TableSettings.Changed`'de `GalaxyAudio.SetLevels(new SoundLevels(settings.Int("master"), settings.Int("sfx"), settings.Int("music"), settings.Bool("tileSnd"), settings.Bool("chatSnd"), GalaxyAudio.Engine.Levels.NotificationSounds))` (ya da bu anahtarları taşıyan bir `JObject` ile `GalaxyAudio.ApplySettings`). Menü ayarı bağlı; masa kendi deposunu bağlamazsa masadaki kaydırıcılar sese yansımaz (EK-902).
2. **Kural motoru olayları** (`G101Rules.Apply(...).Events` ya da protokol `game {ev}`): `GalaxyAudio.Play(GalaxyAudio.Table.ForEngine(events, mySeat, dealing))`. Tek olay: `ForEngine(ev.e, seat, mySeat, nuke, dealing)`. drew → tile.draw (rakipse ×0,7) · discarded → tile.discard · opened/laid → meld.lay · added → meld.add · okeySwapped → okey.swap · penalty → penalty · finished → finish / finish.nuke (aynı partide `handEnd.result` elden ya da okey bitişse; EK-908) · handEnd → hand.end · turn (koltuk = ben, dağıtım bitmişse) → turn.mine. Diğerleri (handStart, openShort, undone, auto…) sessiz.
3. **Zamanlama**: ses animasyonun başladığı karede çalınmalı. `hand.end` el sonu tablosu **açılınca** (timings.md: panel bitişten sonra açılır) — partideki `handEnd` ipucunu o ana kadar bekletin. `dice.roll` 3D zarla (1450 ms), `finish.nuke` fırlatmayla (1750 ms), `gift.rocket` roketle (5600 ms) başlar (`Catalog.SyncMs`).
4. **Dağıtım töreni / protokol**: `dealStart` → `Table.DealStart()`; `dealDie` → `Table.DealDie()`; `dealStep` → `Table.DealStep(step)` (indicator, distribute; diğer adımlar sessiz); tören bitince sıra bendeyse `Table.Turn(seat, mySeat, dealing: false)`.
5. **Masa olayları**: geri sayım `Table.Countdown(n)` (3-2-1 tik, bitince `null` → count.go) · tur uyarısı eşiği `Table.TurnWarning()` · ıstakada yer değişimi `Table.RackMove()` · hediye uçuşu `Table.GiftFlight(gift, from)` (roket `"rocket"`) · emoji uçuşu `Table.EmojiFlight(from)` · bana ikram kartı göründü (ikram uyarısı açıksa) `Table.GiftReceived(from)` · sohbet satırı `Table.Chat(seat, mySeat)` (benimki sessiz, sistem satırı −1 çalar) · hediye/sohbet/ayarlar/kalk onayı açıldı/kapandı `Table.Panel(open)` · kırmızı sistem satırı ya da protokol hatası `Table.ErrorUi(ui)` (toast/shake/inline → ui.error) veya doğrudan `GalaxyAudio.Play("ui.error")`.
6. **İmleç**: 3D alanda `GalaxyCursor.World = "grab"` (oynanabilir taşın üstü) / `"default"`; taş sürüklenirken `GalaxyCursor.Hold("grabbing")`, geçerli bırakma alanında `Hold("drop")`, bırakınca `GalaxyCursor.Release()`. HUD'un saydam bölgeleri `PickingMode.Ignore` olmalı (yoksa 3D alan "UI" sayılır). HUD'daki özel tıklanırlar `GalaxyCursor.Mark(e, "pointer")` ya da `PressSink` (EK-909); meşgulken kök `GalaxyCursor.Lock(root, "wait")`.
7. Eski `Okey101 TableAudio` (3 kayıt klibi) Galaxy masasında kullanılmıyor; taş sesi seçimi EK-413 (hat D).

## 6. Diğer hatların yapacakları

- **Menü**: (a) Ayarlar › Görüntü'ye "İmleç" satırı (Normal · Büyük · Sistem) → `GalaxyCursor.Mode` (EK-904). (b) Günlük ödül/görev bildirimleri `gold` türüyle (EK-907). (c) Avatar (`profile-open`) gibi `Button` olmayan tıklanırlar: `GalaxyCursor.Mark(e, "pointer")` (EK-909). (d) Menüde `chatSnd` yok (EK-903) — ses ayarları tek depoya inerse `GalaxyAudio.ApplySettings` çağrısı yeterli. (e) §4'teki iki `MainMenuController` satırı ve `MenuView.Flash` satırı yeni menü koduna taşınmalı.
- **Lobi**: `LobbyScreen.Flash` satırı (§4) korunmalı ya da EK-906 kararıyla silinmeli; `Tap()` → `PressSink` zinciri korunursa pointer/ui.tap kendiliğinden.
- **Dereceli**: tam ekran Dereceli ekranı `TrackScreens` ifadesine eklenmeli (§4).
- **Durum yönlendiricisi**: `-g101State` koşuları sessiz; çalınan olaylar `GalaxyAudio.Log` (id, zaman, koltuk) ile karşılaştırılabilir (prototipte `G101Sound.log`).

## 7. İçe aktarma ayarları

| Dosyalar | Değişiklik | Neden |
|---|---|---|
| `Resources/Galaxy101/Audio/Music/*.wav.meta` (2) | `loadType: 2` (Streaming) | 32/40 sn stereo döngüler bellekte açılmasın (hat D RAPOR §6.1 önerisi). Vorbis kalite 1 aynı. |
| `Resources/Galaxy101/Audio/Sfx/*.wav.meta` (28) | `preloadAudioData: 1` | Klip verisi varlıkla yüklensin; ilk `ui.tap` gecikmesin (ses nesnesi ayrıca ilk seste bütün efektleri `Resources.Load` eder). Decompress On Load / Vorbis aynı. |

## 8. Doğrulama

```
python tools/v4/unity_compile.py --repo C:/GD/Okey101-v4/audio --csproj-from "C:/GD/Okey 101" --scratch <scratchpad>/compile --project <P>
  Galaxy101.PlayMode.Tests: OK (0 errors)
  Galaxy101.Service.Tests: OK (0 errors)
  Galaxy101.Editor.Tests:  OK (0 errors)
```

Unity'siz kural koşusu (scratchpad'de .NET 10 konsolu; `SoundCatalog`, `SoundLevels`, `ParamCurve`, `SoundEngine`, `TableSounds`, `CursorSet` kaynak olarak, `UnityEngine.CoreModule` + `Newtonsoft.Json` + `Okey101.Core` başvurusuyla): paket `events.json` ile 28 olay birebir, 30 WAV mevcut, kazanç kuralı, anahtarlar, poly (28 olayın hepsi), kısma eğrisi (10,15 / 10,3 / 11,35 / 12,4 sn), çapraz geçiş, ayar zaman sabiti, ekran akışı, sessiz koşu ve günlük sınırı, eşleme kapsamı (`engineEvents`'in her girdisi), nuke partisi ve gerçek motor (tohum 42: `handStart,turn` → turn.mine; `discarded,turn` → tile.discard + turn.mine), imleç boyutları/sıcak noktaları → `ALL OK`.

Unity EditMode testleri (Unity açılmadığı için koşulmadı; derleniyor):
- `AudioServiceTests`: katalog = paket; karıştırma sayıları; bütün klipler `AudioClip` olarak içe aktarılmış; kazanç kuralı; kategori anahtarları; menü ayarlarından düzey; poly; ses payı ve sessizlik eşiği; kısma; sessiz koşu; müzik açılış/geçiş; ayar zaman sabiti; ekran sesi ve müzik; `ParamCurve`; protokol nuke sonucu; imleç seti (dokular, @2x okunabilir); imleç boyutu; imleç kuralları (düğme, pasif, metin, salt okunur, kaydırıcı, kaydırma çubuğu, anahtar, açılır liste, işaret, kilit); `ui.tap` (6 öğe sınırı, pasif düğme, `PressSink`).
- `TableSoundsTests`: README eşlemesi; protokol/masa olayları; `engineEvents` kapsamı ve aday listesi; nuke partisi; gerçek motor turu.

Unity'de bakılacaklar (koşulmadı): ses nesnesi menüde müziği açıyor mu, masaya geçişte çapraz geçiş, düğmelerde `ui.tap` ve pointer, UI Toolkit'in metin alanı imleci (`cursor: text` varsayılan temada varsa Unity çalışma anında doku ister ve uyarı yazar; sürücü kendi imlecini korur), `Screen.dpi` ile @2x seçimi.

## 9. Kalan farklar

- Eksikler: `docs/v4/audio/EKSIKLER.md` (EK-901…EK-910).
- Unity farkları: `docs/v4/audio/UNITY_FARKLARI.md` (UF-901…UF-915).

## 10. Commit'ler

| Commit | İçerik |
|---|---|
| `5b45de0` | `v4/data` birleşimi (WAV'lar, `sound_events.json`, imleç PNG'leri ve `cursors.json`) |
| `18ce75e` | Ses servisi, müzik, imleç, masa eşlemesi, hook satırları, testler, WAV içe aktarma ayarları |
| (bu commit) | RAPOR, EKSIKLER, UNITY_FARKLARI |

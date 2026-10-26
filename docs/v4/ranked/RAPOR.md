# Faz 2 · Dereceli · rapor

Dal `v4/ranked`, worktree `C:/GD/Okey101-v4/ranked`. Unity bu hatta çalıştırılmadı; derleme `tools/v4/unity_compile.py --project Galaxy101.PlayMode.Tests` ile her commit öncesi **OK (0 hata)**. Orkestratörün Unity koşusu (main `9606a80`, `artifacts/v4/unity/run5/S16*`) S16'yı referansla kutu kutu eşleşmiş buldu; tek sapma `ranked.text.seasonTag` (x 150 / 145,78) idi, `c372219` ile düzeltildi (UF-715). Push yok.

- Birleştirilenler: `v4/data` (`68a044d`), sonra main `74b9ebe` (içinde `v4/server`, `v4/assets3d`, `v4/audio`, bu hattın ilk teslimi) ve main `6565e6a` (`v4/menu`, tek `Galaxy101.Audio.GalaxyCursor`).
- Ekran artık **v4 dereceli protokolüyle** konuşur (`315d829`); eski `queue.join` / `queue.leave` / `match.start` yolu kaldırıldı.

Referans: `Design/Galaxy101_ClaudeCode/prototip/G101-Dereceli.dc.html` (aşağıda `Dereceli`), `g101-rank.js`, paket `galaxy101_handoff_v4/` (`ranks.json`, `strings` `ranked.*`, `anim/css.json` `rk*`, `protocol/timings.md`, R-94, D-29, D-30, D-62), S16 referansı `artifacts/v4/ref/S16.{png,layout.json,text.json}`; sunucu `docs/v4/hat-b/ISTEMCI_NOTU.md`, `docs/v4/PROTOKOL_ESLEME.md`, `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs`.

## 1. Yapılanlar

UI Toolkit, kodla kurulan tek katman (lobi deseni: `view.Decorate`, `view.Text`, `MenuView.Place`, `Tap`, `GalaxyFilters`, fontlar anahtarla). Menü tuvalinin `modal-layer`'ı altına eklenir, `main-layer` gizlenir; geri ile menüye döner. Öğe adları prototipin `data-g101-id`'leri (D-62): `ranked.back`, `ranked.v.pick[0]`, `ranked.m.pick[1]`, `ranked.z.ini[0]`, `ranked.cta.go`, `ranked.res.close#2` …

| Görünüm | Prototip | Unity |
|---|---|---|
| Üst çubuk | 86 px: geri, 52 px turkuaz rozet (laurel + taş), "Dereceli" + "Sezon N · K gün kaldı · Solo / Duo / Squad", bakiye hapı, Maç ara / İlerleme | `RankedScreen.Home.cs` `BuildHeader`, `Badge` |
| Ana ekran (S16) | 360 px lig kartı (sezon etiketi, 150 px yüzen amblem, lig adı, LP çubuğu, "Altın I'e 28 LP", düşme koruması, MAÇ / 1.LİK / 1.LİK ORANI, "Dereceli ilerlemem"); Solo / Duo / Squad kartları (seçili: −4 px, 6 + 9 px halka, 0 14 30 parıltı; diğerleri saturate .7 brightness .78); "Masan" 4 koltuk (SEN / RAKİP / ARKADAŞ / TAKIM; dolu, davet, bekleyen, "?" rakip + arama halkası, takımdan çıkar); eşleşme aralığı (±1, 30 sn sonra ±2; Squad: takım aralığı); MAÇ ARA / MASAYI KUR (rkPulse 2,2 sn) ya da "Rakip aranıyor" saat + İptal | `BuildHome`, `MeCard`, `ModeCard`, `TablePanel`, `Seat`, `Cta`, `SearchBox` |
| İlerleme | Lig yolu (6 lig, BURADASIN, kademe hapları, ulaşılmamış lig gri), Sezon ödülleri (çerçeveli avatar, çip, Ulaşıldı / Kazanacağın / Kilitli), Son maçlar + LP kuralı "1. +30 · 2. +10 · 3. −10 · 4. −20" | `RankedScreen.Progress.cs` |
| Davet (Duo / Squad) | 520 px pencere: arkadaşlar, lig amblemi, çevrim içi noktası, Davet et / Gönderildi / Takımda / Çevrimdışı / Aralık dışı (±1 lig) | `BuildInvite` |
| Maç bulundu | 640 px: MAÇ BULUNDU / SQUAD HAZIR, 4 oyuncu (kabul rozeti, TAKIM A / B — D-29 Duo 0 + 2, SEN), Reddet / "Kabul et · 10" (10 sn çubuk), kabulden sonra "Oyuncular bekleniyor…" → "Masa hazırlanıyor…" (rankedReady 1800 ms) | `BuildFound` |
| Sonuç | 520 px: DERECELİ · SOLO, yer kutusu, Galibiyet! / "N. oldun", ±LP, amblem + LP çubuğu (350 ms sonra .9 sn; kademe geçişinde dolup boşalır), not (düştün / koruma / ayrıldın), Tamam·Devam / Tekrar ara; yükselişte promosyon (ışınlar, rkPop amblem, KADEME / LİG ATLADIN, ZİRVEYE ULAŞTIN, Devam) | `BuildResult`, `BuildPromo`, `Rays` |
| Tost | üst orta hap, ok / bad / info, 2,6 sn | `RankedScreen.Flash` |

Veri ve kurallar (`RankedData.cs`): `RankedLadder` (`ranks.json` ligleri, `at`, `label`, `leagueOf`, `deltas`, sunucu kodları b3…m1 / gx, amblem dosyası), `RankedRules.Apply` (Ana Oyun `applyRanked()`, D-30) ve `RankedRules.FromChange` (`rankChange` → sonuç kartı olayı), `RankedProfile` / `RankedPlayer` / `RankedSearch` / `RankedFound` / `RankedResult` / `RankedStart`.

Menü (`MainMenuController`, yalnız dereceli kısmı): HEMEN OYNA + Dereceli → `OpenRanked()`; MAÇ ARA kuyruğu başlatır; maç başlayınca `OnRankedMatched` maçı masa hattına bırakır (§4); dereceli masadan dönüşte (`FromRanked`) ekran yeniden açılır ve sonuç kartını gösterir (`RankedScreen.Pending`); oturum açılınca `HookRanked` bir parti liderinin kuyruğa soktuğu oyuncuda ekranı açar.

Görseller: başlık rozeti (`icon-50`) ve 6 sezon çerçevesi (`g101-rank.js ring()`), `docs/v4/ranked/ranked_art.py` ile prototip kaynağından @2x PNG (`Assets/Galaxy101/Scripts/Screens/Ranked/Resources/Galaxy101/Ranked/`).

## 2. Sunucu mu, demo mu

| Parça | Çevrimiçi (`RankedOnlineBackend`, v4 sunucu) | Demo (`G101DemoRanked`, yalnız editör / development, durum yönlendiricisi) |
|---|---|---|
| Lig, LP, koruma, en iyi lig, sezon, kalan gün, Galaksi sırası | `ProgressResponse` (League, DivisionLp, Protected, BestLeague, Season, SeasonEndsAt, SeasonRank); ekran açılınca ve `rankChange`'ten sonra yenilenir | prototipin `rankedOf()` varsayılanı: Altın II 72 LP, 0 maç; Sezon 3, 18 gün |
| Maç / 1.lik sayısı, son maçlar | sunucu tutmuyor → 0 / "—" / boş (**EK-703 açık**) | 0 (S16 böyle) |
| Arkadaşlar, Duo / Squad | `/v1/friends` (lig, çevrim içi); parti liderin `rankedQueue {party}`'siyle kurulur, onay adımı yok (**EK-702 kısmen**) | `FRIENDS`, 1,3–2,2 sn'de katılır |
| MAÇ ARA / İptal | `rankedQueue {mode, party}` / `rankedCancel`; hatalar paket metinleriyle (E_PARTY_RANGE, E_ALREADY_SEATED tost; E_ALREADY_QUEUED, E_MODE, E_PARTY_SIZE, E_NOT_QUEUED sessiz) | rakipler 1,5 sn + 1–3,2 sn'de bir |
| Arama | `rankedSearch {mode, range, filled}`: ±1 / ±2 (30 sn, sunucu söyler); parti üyesi modu buradan öğrenir; rakip sayısı bilinmez (EK-714) | ±1, 30 sn sonra ±2, rakipler koltuklarda |
| Maç bulundu | `rankedFound` (pencere = `expiresAt − at`, 10 sn), `rankedAccept`, `rankedAccepted`; `E_MATCH_EXPIRED` ve aramaya geri dönüş (`rankedSearch` maç beklerken) → "Eşleşme süresi doldu · aramaya dönülüyor"; `rankedCancelled` timeout → "Maç zamanında kabul edilmedi", cancel → "Arama iptal edildi", eşin reddi sessiz (EK-712) | diğerleri 0,6–3,2 sn'de kabul eder, 10 sn'de timeout |
| Masa hazırlanıyor → başla | `rankedReady {table, loadingMs: 1800}` → aynı masanın `matchStart` → `OnRankedMatched` (§4) | 1800 ms → yerel masa |
| Sonuç kartı | `rankChange {before, after, delta, place, tierUp, leagueUp, down, protectedLeft}` → `RankedRules.FromChange`; ekran yoksa `RankedScreen.Pending` | `G101DemoRanked.Finish(place)` |

`RankedOnlineBackend` oturum başına tektir (`For(online)`) ve oturum boyu dinler: masa açıkken gelen `rankChange` kaybolmaz, ekran kapalıyken gelen arama / maç (`Incoming`) menüye ekranı açtırır, açılan ekran süren aramayı ya da bekleyen maçı (`Queue`, `Pending`) devralır. Ekran paneli giderken (sahne değişimi) dinlemeyi bırakır.

## 3. Durum yönlendirici

`G101StateRouter.Shell.cs` `case "S16"` → `G101StateRouter.Ranked.cs` `RankedState`: states.json yolu `acct → go ranked → waitFor Dereceli` = hesabı aç (Doğukan Y., #FF8A5B, 10.000 çip), menüyü kur, `MainMenuController.RankedBackend` = demo, `OpenRanked()`, ekran görünene dek bekle. Kare 100 000 ms'de donar (main `74b9ebe`, EK-005; UF-709 çözüldü).

```
Galaxy101.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -g101State=S16 -g101Seed=42 -g101Out=C:\out
```

## 4. Masaya devir: `OnRankedMatched` (masa hattı `v4/table` için)

`matchStart` geldiğinde `MainMenuController.OnRankedMatched(RankedStart)`:

1. `MainMenuController.PendingRanked = start` — `start.Match` sunucunun **`V4MatchStart`**'ı (`Table`: id, `Seats[0..3].User` {id, name, color, rankIdx}, `Mode` "solo" / "pairs" (Duo), `Hands` 5, `TurnSec` 30, `Bet` 0, `Ranked` true, `Rules` {spectate false, chat "quick", gifts false}; `CountdownMs` 3000), `start.Seat` senin koltuğun (0–3), `start.Mode` solo / duo / squad, `start.Players` koltuk sırasıyla oyuncular. Demo maçında `PendingRanked` null.
2. `TableGame.PendingRoom = "Dereceli · Solo|Duo|Squad"`, `FromRanked = true`, masa sahnesi açılır (`StartLocalAsync(GalaxyTableScene)`).

Masa hattının yapması gerekenler:

- Sahne açılırken `PendingRanked`'ı alıp temizlemek ve masayı **v4 adım protokolüyle** kurmak (yerel motor değil): `TableClient` aynı soketin `dealStart` … `turn` / `game` / `legal` / `handWait` / `matchResult` olaylarını dinler, hamleleri `g*` mesajlarıyla gönderir (ISTEMCI_NOTU §4).
- `dealStart` `matchStart`'tan `countdownMs` (3000 ms) sonra gelir; sahne yüklemesi bunu kaçırabilir: yüklenince `resync` gönderip `snapshot {screen: "table", game}` ile masayı animasyonsuz kurmak (sunucu `Hub.Snapshot` bunu verir).
- Maç sonunda `matchResult`'ın ardından gelen `rankChange`'i masa işlemez: `RankedOnlineBackend` yakalar ve `RankedScreen.Pending`'e koyar. Masa kapanırken `MainMenuController.ReturnToLobby = true` vermek yeter; menü `FromRanked` ile Dereceli ekranını açar ve sonuç kartını gösterir.
- Dereceli masada sohbet yalnız hazır mesaj, hediye ve izleyici kapalı (masa kuralları `Table.Rules`).

Masa hattı bunu bağladı (main `46aab1c`, EK-713): `OnRankedMatched` açık `V4TableSession.Current`'ı `TableGame.Pending = TableLaunch{Session, Room}` ile masaya verir; demo maçında oturum yok, masa yerel motorla açılır.

## 5. Testler

`Assets/Galaxy101/Tests/PlayMode/RankedTests.cs` (bu hatta Unity'de koşulmadı, derleniyor; orkestratör koşusunda yalnız `HemenOynaOpensTheDereceliHomeAsS16` seasonTag x'inden düşmüştü):

- `LadderFollowsTheRankScript`, `LpRulesMatchApplyRanked`: lig etiketleri, kodlar, LP tabloları, ödüller, amblemler; kademe / lig / koruma / düşüş / Galaksi / taban.
- `HemenOynaOpensTheDereceliHomeAsS16`: HEMEN OYNA → ekran, metinler ve S16 kutuları (±1,5 px; ortalanmış metinde ±3–4 px), geri.
- `SoloSearchFoundAcceptAndStart`, `DeclineTimeoutAndCancelFlash`, `DuoNeedsAFriendInRange`, `ResultCardThenPromotion`: senaryolu arka uçla akışlar.
- `OpeningOnAFoundMatchAndFallingBackToTheSearch`: ekran bekleyen maçla açılır, maç düşünce aramaya döner (tost, Duo modu), liderin iptali.
- `OnlineBackendSpeaksTheV4RankedMessages`: gerçek `GalaxyRealtime` kutusuna v4 çerçeveleri (`rankedSearch`, `rankedFound`, `rankedAccepted`, `rankedReady`, başka masanın ve dereceli masanın `matchStart`'ı, `rankChange`, `rankedCancelled`) verilir; olaylar, koltuk, mod, 10 sn pencere ve `Incoming` denetlenir; soketsiz `rankedQueue` "offline" döner.

## 6. Kalan farklar

- Eksikler `docs/v4/ranked/EKSIKLER.md`: EK-701 **çözüldü**; açık olanlar EK-702 (onaylı parti daveti yok), EK-703 (çevrimiçi maç sayısı / geçmiş), EK-706 (yükleme ekranının dereceli başlığı), EK-712 (eşin reddi metni), EK-713 (v4 masa oturumu, §4), EK-714 (aramada rakip sayısı yok), EK-715 (kopunca `snapshot` ile dönüş).
- Unity farkları `docs/v4/ranked/UNITY_FARKLARI.md` (UF-701…UF-715): amblem / rozet / çerçeve PNG, box-shadow karşılıkları, saturate yaklaşımı, ışınlar, imleç durumları, **UF-715** letter-spacing düzeltmesi; UF-709'un donma farkı çözüldü.

## 7. Unity koşusu düzeltmeleri (main `46aab1c` sonrası)

- `HemenOynaOpensTheDereceliHomeAsS16`: `ranked.z.ini[0]` y 380 (beklenen 381,91 ± 1,5). 1280×720 yakalamada öğe doğru yerde (`artifacts/v4/unity/run9/S16.layout.json`: y 382, ad 453, "DY" 400 = referans). 1,91'lik kayma koltuk sütununun merkezi çevresinde 1,0315 ölçeğe denk geliyor (x 1 px kayar, ±1,5'te kalır): ölçüm anında `rkSeat` girişi (0,45 sn, gerçek zaman; eğri 1,08'den 1'e aşarak iner) bitmemiş ya da artık ölçek bırakmış. Öğe düzeltmesi: her giriş (`rkFade`, `rkSeat`, `rkModal`, `rkPop`, `rkUp`) bitince öğenin ölçek / dönme / kayma / saydamlık değerleri silinir (`RankedScreen.Rest`, CSS `both` son karesi "none"); test ölçümden önce oyun zamanı .6 sn yerine gerçek zaman 1 sn bekler (S16'nın 900 ms tutuşu gibi). Tolerans değişmedi.
- `ranked.text.seasonTag`: `c372219`'un sağ dolgusu işledi (genişlik 118 → 120), boşluklara verilen zengin metin `<space>` işlemedi; yerine `word-spacing` (UF-715). Beklenen genişlik 124,4, x 145,8.

## 8. Commit'ler

| Commit | İçerik |
|---|---|
| `68a044d` | `v4/data` birleştirmesi |
| `ae326ba` | Dereceli ekranı (ana, İlerleme, davet, bulundu, sonuç), `IRankedBackend`, ilk çevrimiçi bağdaştırıcı, `G101DemoRanked`, S16 yolu, menü girişi, `RankedQueue` kaldırıldı, rozet / çerçeve PNG'leri |
| `c51e1dc` | `RankedTests` (7 test); dereceli başlangıcı `OpenMatchTable` kapalıyken masa açmaz |
| `d725a7d` | Paketin hata metinleri (sessiz kodlar), `E_MATCH_EXPIRED`, koltuk girişi görünüm başına |
| `b5d30da` | Bakiye / dil / tema değişince yeniden çizim; EKSIKLER, UNITY_FARKLARI |
| `286b21a` | İlk RAPOR |
| `315d829` | **v4 protokolü**: `rankedQueue` / `rankedAccept` / `rankedCancel`, `rankedSearch` / `rankedFound` / `rankedAccepted` / `rankedCancelled` / `rankedReady` / `matchStart` / `rankChange`; oturum başına arka uç, `HookRanked`, `PendingRanked`; 2 yeni test |
| `c62c8c4` | main `6565e6a` birleştirmesi (`v4/menu`, tek `GalaxyCursor`) |
| `c372219` | letter-spacing Chrome gibi (boşluk ve son karakter): S16 `seasonTag` x 145,78 (UF-715) |
| `dd6b031` | RAPOR, EKSIKLER (EK-701 çözüldü, EK-702 kısmen, EK-703 açık, EK-712…715), UNITY_FARKLARI (UF-709 çözüldü, UF-715) |
| (birleştirme) | main `46aab1c` (`v4/table`: `OnRankedMatched` `V4TableSession.Current`'ı okur, `PendingRanked` yanında kalır) |
| (bu commit) | Giriş animasyonları bitince öğe kendi kutusuna döner (`Rest`), aralık `word-spacing` ile, S16 testi gerçek zamanla bekler; §7 |

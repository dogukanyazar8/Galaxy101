# Faz 2 · Dereceli · rapor

Dal `v4/ranked` (taban `main` `59aa81c`), worktree `C:/GD/Okey101-v4/ranked`. Unity çalıştırılmadı; derleme `tools/v4/unity_compile.py --project Galaxy101.PlayMode.Tests` ile her commit öncesi **OK (0 hata)**. Push yok.

- `v4/data` birleştirildi (`68a044d`): lig amblemleri (`Resources/Galaxy101/Rank/*.png` + `ranks.json`), v4 metinleri, ikonlar, Nunito-700.
- `v4/server` **birleştirilmedi**: `docs/v4/hat-b/RAPOR.md` yok (hat sürüyor). Ekran bugünkü sunucuya bağlı; paket protokolüne geçiş için ayrı bir bağdaştırıcı yüzeyi bırakıldı (§4).

Referans: `Design/Galaxy101_ClaudeCode/prototip/G101-Dereceli.dc.html` (aşağıda `Dereceli`), `g101-rank.js`, paket `galaxy101_handoff_v4/` (`ranks.json`, `strings` `ranked.*`, `anim/css.json` `rk*`, `protocol/timings.md`, R-94, D-29, D-30, D-62), S16 referans karesi `artifacts/v4/ref/S16.{png,layout.json,text.json}`.

## 1. Yapılanlar

UI Toolkit, kodla kurulan tek katman (lobi deseni: `view.Decorate`, `view.Text`, `MenuView.Place`, `Tap`, `GalaxyFilters`, fontlar anahtarla). Menü tuvalinin `modal-layer`'ı altına eklenir, `main-layer` gizlenir; geri ile menüye döner. Öğe adları prototipin `data-g101-id`'leri (D-62): `ranked.back`, `ranked.v.pick[0]`, `ranked.m.pick[1]`, `ranked.z.ini[0]`, `ranked.cta.go`, `ranked.res.close#2` …

| Görünüm | Prototip | Unity |
|---|---|---|
| Üst çubuk | 86 px: geri, 52 px turkuaz rozet (laurel + taş), "Dereceli" + "Sezon N · K gün kaldı · Solo / Duo / Squad", bakiye hapı, Maç ara / İlerleme | `RankedScreen.Home.cs` `BuildHeader`, `Badge` |
| Ana ekran (S16) | 360 px lig kartı (sezon etiketi, 150 px yüzen amblem, lig adı, LP çubuğu, "Altın I'e 28 LP", düşme koruması, MAÇ / 1.LİK / 1.LİK ORANI, "Dereceli ilerlemem"); Solo / Duo / Squad kartları (seçili: −4 px, 6 + 9 px halka, 0 14 30 parıltı; diğerleri saturate .7 brightness .78); "Masan" 4 koltuk (SEN / RAKİP / ARKADAŞ / TAKIM; dolu, davet, bekleyen, "?" rakip + arama halkası, takımdan çıkar); eşleşme aralığı (±1, 30 sn sonra ±2; Squad: takım aralığı); MAÇ ARA / MASAYI KUR (rkPulse 2,2 sn) ya da "Rakip aranıyor" saat + İptal | `BuildHome`, `MeCard`, `ModeCard`, `TablePanel`, `Seat`, `Cta`, `SearchBox` |
| İlerleme | Lig yolu (6 lig, BURADASIN, kademe hapları, ulaşılmamış lig gri), Sezon ödülleri (çerçeveli avatar, çip, Ulaşıldı / Kazanacağın / Kilitli), Son maçlar (yer, mod, lig, ±LP; boşsa açıklama) + LP kuralı "1. +30 · 2. +10 · 3. −10 · 4. −20" | `RankedScreen.Progress.cs` |
| Davet (Duo / Squad) | 520 px pencere: arkadaşlar, lig amblemi, çevrim içi noktası, Davet et / Gönderildi / Takımda / Çevrimdışı / Aralık dışı (±1 lig, D-29 eşli koltuk) | `BuildInvite` |
| Maç bulundu | 640 px: MAÇ BULUNDU / SQUAD HAZIR, 4 oyuncu (kabul rozeti, TAKIM A / B, SEN), Reddet / "Kabul et · 10" (10 sn çubuk), kabulden sonra "Oyuncular bekleniyor…" → "Masa hazırlanıyor…" (rankedReady, 1800 ms) | `BuildFound` |
| Sonuç | 520 px: DERECELİ · SOLO, yer kutusu (altın / gümüş / bronz), Galibiyet! / "N. oldun", ±LP, amblem + LP çubuğu (350 ms sonra .9 sn, kademe geçişinde dolup boşalır), not (düştün / koruma / ayrıldın), Tamam·Devam / Tekrar ara; yükselişte promosyon (ışınlar, rkPop amblem, KADEME / LİG ATLADIN, ZİRVEYE ULAŞTIN, alt metin, Devam) | `BuildResult`, `BuildPromo`, `Rays` |
| Tost | üst orta hap, ok / bad / info, 2,6 sn | `RankedScreen.Flash` |

Veri ve kurallar (`RankedData.cs`): `RankedLadder` (`ranks.json`'dan ligler, `at`, `label`, `leagueOf`, `deltas`, sunucu kodları b3…m1 / gx, amblem dosyası seçimi), `RankedRules.Apply` (Ana Oyun `applyRanked()`: kademe, lig, Galaksi, D-30 3 maç koruma), `RankedProfile` / `RankedPlayer` / `RankedSearch` / `RankedFound` / `RankedResult` / `RankedStart`.

Menü (`MainMenuController`, yalnız dereceli girişi): HEMEN OYNA + Dereceli → `OpenRanked()` ekranı açar (eskiden doğrudan kuyruk); MAÇ ARA kuyruğu başlatır; maç başlayınca masa "Dereceli · Solo" adıyla açılır; dereceli masadan dönüşte (`FromRanked`) ekran yeniden açılır ve sonuç kartını gösterir (`RankedScreen.Pending`). Eski `RankedQueue` katmanı kaldırıldı.

Görseller: başlık rozeti (`icon-50`) ve 6 sezon çerçevesi (`g101-rank.js ring()`), `docs/v4/ranked/ranked_art.py` ile prototip kaynağından @2x PNG (`Assets/Galaxy101/Scripts/Screens/Ranked/Resources/Galaxy101/Ranked/`).

## 2. Sunucu mu, demo mu

| Parça | Çevrimiçi (`RankedOnlineBackend`, bugünkü sunucu) | Demo (`G101DemoRanked`, yalnız editör / development) |
|---|---|---|
| Lig, LP, koruma, en iyi lig, sezon, kalan gün, Galaksi sırası | `ProgressResponse` (League, DivisionLp, Protected, BestLeague, Season, SeasonEndsAt, SeasonRank) | prototipin `rankedOf()` varsayılanı: Altın II 72 LP, koruma 0, en iyi Altın II, 0 maç; Sezon 3, 18 gün |
| Maç / 1.lik sayısı, son maçlar | yok → 0 / "—" / boş (EK-703) | 0 (S16 böyle) |
| Bakiye | `MenuModel.Chips` (cüzdan) | 10.000 (durum hesabı) |
| Arkadaşlar, Duo / Squad daveti | yok (EK-702) | `FRIENDS` (7 kişi), 1,3–2,2 sn sonra katılır |
| MAÇ ARA / İptal | `queue.join {mode: ranked}` / `queue.leave` (yalnız Solo) | 1,5 sn + 1–3,2 sn'de bir rakip (`OPP`, ±1 lig, 30 sn sonra ±2) |
| Maç bulundu, 10 sn kabul, reddet, zaman aşımı | yok: `match.start` gelince masa açılır (EK-701) | diğerleri 0,6–3,2 sn'de kabul eder, 10 000 ms'de "timeout" |
| Masa hazırlanıyor → başla | `match.start` → `NetworkTableSession` | `rankedReady` 1800 ms → yerel masa |
| Sonuç kartı | `match.end` yerleşimi + LP farkı → `RankedRules` (D-30) → menüye dönüşte | `G101DemoRanked.Finish(place)` |

## 3. Durum yönlendirici

`G101StateRouter.Shell.cs` `case "S16"` → `G101StateRouter.Ranked.cs` `RankedState`: states.json yolu `acct → go ranked → waitFor Dereceli` = hesabı aç (Doğukan Y., #FF8A5B, 10.000 çip), menüyü kur, `MainMenuController.RankedBackend` = demo, `OpenRanked()`, ekran görünür olana dek bekle.

```
Galaxy101.exe -screen-width 1280 -screen-height 720 -screen-fullscreen 0 -g101State=S16 -g101Seed=42 -g101Out=C:\out
```

S16 kutuları `RankedTests.HemenOynaOpensTheDereceliHomeAsS16` içinde `S16.layout.json` değerleriyle (±1,5 px; ortalanmış metinlerde ±3–4 px) denetlenir: geri, başlık, sekmeler, sezon etiketi, lig adı, LP, istatistik, ilerleme düğmesi, üç mod kartı (seçili −4 px), masa başlığı, koltuk etiketi, SEN avatarı (492, 381.91), rakip etiketi, aralık, MAÇ ARA (932, 612, 300×64). Hesap: lig kartı 28…388, sağ sütun 408…1252, kartlar 272 px, koltuklar 192 px (287…598), alt satır 612.

## 4. Paket protokolüne bağlama (Hat B bitince)

Ekran yalnız `IRankedBackend` görür; arayüz paket mesajlarıyla birebir:

| Paket mesajı | `IRankedBackend` | Ekranda |
|---|---|---|
| c2s `rankedQueue {mode, party}` | `QueueAsync(mode, party)` (hata kodu döner) | MAÇ ARA / MASAYI KUR |
| c2s `rankedCancel` | `CancelAsync()` | İptal, geri |
| c2s `rankedAccept {matchId, accept}` | `AcceptAsync(matchId, accept)` (`E_MATCH_EXPIRED` → tost) | Kabul et / Reddet |
| s2c `rankedSearch {mode, since, range, filled}` | `Searching(RankedSearch{Range, Filled})` | aralık ±1/±2, "n/3 bulundu" |
| s2c `rankedFound {found}` | `Found(RankedFound{MatchId, Players, Accepted, Seconds})` (oyuncu sırası koltuk sırası; Duo'da 0 ve 2 sen + eşin, D-29) | Maç bulundu |
| s2c `rankedAccepted {index, accepted}` | `Accepted(accepted)` | kabul rozetleri |
| s2c `rankedCancelled {reason}` | `Cancelled(reason)` | "timeout" / "declined" tostları |
| s2c `rankedReady {table, loadingMs}` | `Ready(loadingMs)` | "Masa hazırlanıyor…" |
| s2c `matchStart` | `Started(RankedStart{Mode, Players, Match})` | masa açılır |
| s2c `rankChange {before, after, delta, place, tierUp, leagueUp, down, protectedLeft}` | `RankChanged(RankedRules.FromChange(...))` | sonuç kartı, promosyon |

Yapılacak: `v4/server` birleşince `RankedOnlineBackend`'in `QueueAsync` / `CancelAsync` / `AcceptAsync` gövdelerini `V4Messages.RankedQueue` / `RankedCancel` / `RankedAccept` isteklerine çevirmek, s2c mesajlarını `online.Realtime.On<V4…>` ile yukarıdaki olaylara bağlamak (`V4RankedFound.Players[i].User` → `RankedPlayer`, lig adımı `User.League`'den), `rankChange`'i masa açıkken `RankedScreen.Pending`'e yazmak. Ekran koduna dokunmak gerekmez.

## 5. Testler

`Assets/Galaxy101/Tests/PlayMode/RankedTests.cs` (Unity'de koşulmadı, derleniyor):

- `LadderFollowsTheRankScript`: etiketler, kodlar (gidiş-dönüş), LP tabloları, ödül çipleri, amblem dosyaları.
- `LpRulesMatchApplyRanked`: kademe, lig (+3 koruma), korumalı düşmeme, lig düşüşü, Galaksi, Bronz III tabanı.
- `HemenOynaOpensTheDereceliHomeAsS16`: HEMEN OYNA → ekran, metinler ve S16 kutuları, geri.
- `SoloSearchFoundAcceptAndStart`, `DeclineTimeoutAndCancelFlash`, `DuoNeedsAFriendInRange`, `ResultCardThenPromotion`: senaryolu arka uçla akışlar.

## 6. Kalan farklar

- Eksikler: `docs/v4/ranked/EKSIKLER.md` (EK-701…EK-711) — başlıcaları Hat B protokolü (EK-701), dereceli takım daveti mesajı (EK-702), çevrimiçi maç sayısı / geçmiş (EK-703), yükleme ekranının dereceli başlığı (EK-706).
- Unity farkları: `docs/v4/ranked/UNITY_FARKLARI.md` (UF-701…UF-714) — amblem / rozet / çerçeve PNG, box-shadow karşılıkları, saturate yaklaşımı, ışınlar, saatler (yönlendiricinin 100 000 + ft donması), imleç yok.
- Görsel karşılaştırma yapılmadı (Unity yok): orkestratörün S16 yakalaması bekleniyor. Dikkat edilecekler: rozet / amblem PNG ölçeklemesi, seçili mod kartının halka katmanları, MAÇ ARA nabız fazı (UF-709).

## 7. Commit'ler

| Commit | İçerik |
|---|---|
| `68a044d` | `v4/data` birleştirmesi |
| `ae326ba` | Dereceli ekranı (ana, İlerleme, davet, bulundu, sonuç), `IRankedBackend`, `RankedOnlineBackend`, `G101DemoRanked`, S16 yolu, menü girişi, `RankedQueue` kaldırıldı, rozet / çerçeve PNG'leri |
| `c51e1dc` | `RankedTests` (7 test); dereceli başlangıcı `OpenMatchTable` kapalıyken masa açmaz |
| `d725a7d` | Paketin hata metinleri (sessiz kodlar), `E_MATCH_EXPIRED`, koltuk girişi görünüm başına |
| `b5d30da` | Bakiye / dil / tema değişince yeniden çizim; EKSIKLER, UNITY_FARKLARI |
| (bu rapor) | RAPOR |

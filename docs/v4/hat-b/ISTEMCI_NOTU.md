# İstemci notu · v4 protokolüne geçiş (Hat B → Unity)

Unity istemcisinin yeni sunucuyla konuşması için değişmesi gerekenler. Ayrıntılı eşleme: `docs/v4/PROTOKOL_ESLEME.md`. Sözleşme sınıfları: `Packages/com.galaxy101.contracts/Runtime/V4Messages.cs` (bütün mesaj adları `V4Messages.*`, bütün hata kodları `V4Errors.*`) ve `V4Payloads.cs` (`V4TableCreateRequest`, `V4Table`, `V4DealStep`, …). C# 9, öznitelik yok; Newtonsoft camelCase ile bağlanır.

## 1 · Bağlantı

1. Giriş HTTP'de kalır: `POST /v1/auth/guest | register | login | external`, `POST /v1/auth/refresh` (paketteki `guest/register/login/oauth` karşılığı). Hata gövdesinde yeni `error` alanı paket kodudur (`E_PASS_WRONG` + `detail.left`, `E_ACCOUNT_LOCKED` + `detail.until`, …); metin anahtarı `err.<KOD>`.
2. `POST /v1/realtime/ticket` → `wss://…/v1/ws?ticket=…`.
3. Soket açılınca **`hello`** gönder (`{token}` alanı yok sayılır). Gelen: `session {me, serverVersion:"0.4.0", serverTime}` ve `snapshot {me, screen, table?, game?, ranked?, spectator?}`. Ekranı `snapshot.screen`'e göre aç: `menu` / `waitroom` / `table` / `ranked.search` / `ranked.found` / `ranked.loading`. Masa `snapshot.game` ile animasyonsuz kurulur; dağıtım sürüyorsa `game.deal.step` ve `game.deal.elapsed` törenin kaldığı yeri verir.
4. Çerçeve: `{type, id, data}`. Her v4 mesajına tam bir `ack`: `data = {id, ok, code, detail?, at, …dönüş}` — `code` paketteki `error`; dönüş alanları (`table`, `seat`, `inviteId`, `expiresAt`, `serverTime`, …) aynı nesnede.
5. Her sunucu olayında `data.at` (Unix ms). Sayaçlar `turn.deadline`, `handWait.nextAt`, `inviteIn.expiresAt`, `rankedFound.found.expiresAt` mutlak değerlerinden; saat farkı `ping` (id ile) ack'indeki `serverTime`'dan.

## 2 · Kopma (R-66, Q-10)

- Soket koparsa: "Bağlantı koptu · Deneme n/5", denemeler **3 sn** arayla (`TableTimings.ClientRetries` = 5, `ClientRetryMs` = 3000). Her deneme: gerekirse token yenile → bilet → soket → `hello`. 5. deneme de olmazsa **"Lobiye dön"** düğmesi.
- Maçta koltuk tutulur; diğerleri `seatConnection{seat, connected:false}` görür, üst üste 2 zaman aşımından sonra `seatConnection{autoplay:true}` ("Otomatik" rozeti). `hello` ya da bir hamle sunucunun oynamasını bitirir; sıra sendeyse süre baştan başlar (yeni `turn` olayı ya da `snapshot.game.turn.deadline`).
- Bekleme odasında kopma: oda bırakılır, bahis iade (`balance{reason:"betRefund"}`), dönünce `snapshot.screen = "menu"`.

## 3 · Lobi ve masa

| Eski (kaldır) | Yeni |
|---|---|
| `lobby.subscribe` / `lobby.tables` | `lobbySubscribe` → `lobby{tables}`, sonra `lobbyTable{table}` / `lobbyTableRemoved{id}` |
| `table.create {options}` | `tableCreate {name, mode:"solo"\|"pairs", private, pin, hands 3\|5\|7, bet 1000\|5000\|10000\|50000\|100000, katlama, turnSec 15\|20\|30\|45, rules{spectate, chat:"open"\|"quick"\|"off", gifts, isle, penalties}, katlamaPartner?}` → ack `{table}` |
| `table.join {tableId\|code}` | `tableJoin {tableId, pin?, seat?}` · `tableJoinCode {code:"GX-XXXX", pin?}` → ack `{table, seat}`; oynanan masada koltuk devralma (R-65) + `snapshot{screen:"table"}` |
| `table.watch` | `tableSpectate {tableId}` → `snapshot{spectator:true}` |
| `table.leave` / `match.leave` | `tableLeave` → `leftTable{reason, toLobbyIn}` (maçta 1300 ms sonra lobi) |
| `table.addBot` / `table.kick` / `table.start` | `seatAddBot{seat}` / `seatKick{seat}` / `tableStart` → `countdown{n:3,2,1, startsIn}` |
| `table.state` | `tableState{table}`, `tableClosed{id, reason:"ownerLeft"}`, `kicked{tableId}` |
| `POST /v1/friends/{id}/invite` | `inviteSend{userId, seat?, recent?}` → `inviteIn` (alıcı), `inviteRespond{inviteId, accept}`, `inviteCancel`, `inviteResult` / `inviteExpired` (30 sn) |
| çevrimdışı botlara karşı | `vsBotsStart{mode, hands?}` (çevrimiçi) ya da çevrimdışı §6 |
| `queue.join{mode:ranked}` | `rankedQueue{mode:"solo"\|"duo"\|"squad", party:[]}` → `rankedSearch` → `rankedFound` (10 sn) → `rankedAccept{matchId, accept}` → `rankedAccepted` / `rankedCancelled` → `rankedReady{table, loadingMs:1800}` → `matchStart` |

Bahis **oturunca** düşülür (`balance{reason:"bet"}`), maç başlamadan çıkınca iade (`betRefund`). Sahip bekleme odasından çıkarsa masa kapanır.

## 4 · Maç (adım protokolü)

Eski tur-bütün `match.command {kind: commit, plan}` yerine her adım ayrı mesaj; sunucu doğrular, hata ack'te (`E_NOT_YOUR_TURN`, `E_PHASE`, `E_MELD_INVALID`, … — motor kodları, `detail` ile).

- Gelen sıra: `matchStart{table, countdownMs:3000}` → `dealStart{handNo, dealer, buildMs, humanDealer}` → `dealStep{step:"dice", rollWaitMs}` → `dealDie{index, value, rollMs}` ×2 → `dealStep:indicator{indicator, okey, stack, level, …}` → `extra` → `distribute{plan}` → `center{stacks, deck}` → `handDealt{rack (yalnız senin), counts, deck, indicator, okey, starter}` → `turn{seat, phase, n, turnMs, deadline}`. Bot dağıtıcıda `dealStart`→`turn` 21 686 ms; olaylar sunucu saatine göre gelir, animasyon onlara sığdırılır (timings §2).
- Dağıtan sensen (`humanDealer:true`): her zar için **`gRollDice`** (≤ 15 sn; atmazsan sunucu atar).
- Hamleler: `gDraw{src:"deck"\|"side"}`, `gReturnSide`, `gOpen{melds:[{kind:"run"\|"group"\|"pair", tiles}]}`, `gLay{melds}`, `gAdd{tile, meld, side}` (`side`: `"L"`/`"R"` ya da motorun `"pre"`/`"post"`; grupta null), `gSwap{tile, meld, index}`, `gUndo`, `gDiscard{tile}`, `gRackOrder{tiles}` (yalnız doğrulama; `E_RACK_MISMATCH` → `resync`).
- Olaylar: `game{ev}` — `ev.e` rules.js adları: `handStart` (dice/indicator/okey boş), `drew` (desteden çekilen taş yalnız sana; başkasında `tile:null`), `sideReturned`, `openShort` (taşlar yalnız deneyene), `opened`, `laid`, `added`, `okeySwapped`, `undone`, `discarded`, `finished`, `penalty`, `auto`, `turn`, `seatLeft`, `seatTaken`, `handEnd` (açık ıstakalar), `matchEnd`. Düğmeler: `legal{draw, side, discard, open, undo, returnSide, add}` (yalnız sıradakine). Her adımdan sonra kendi `tableView{view}` (= `snapshot.game` biçimi).
- El sonu: `handWait{nextHandIn:15000, nextAt, handNo, hands}`; "Sonraki el" = **`gReady`** → `readyState{ready}`; bütün insanlar hazırsa hemen dağıtılır.
- Maç sonu: `matchResult{ranking, totals, history, pool, rake:0, payouts, xpBonus, lp, weekly}`, ardından `balance{reason:"win"}`, `xp{reason:"match"}`, dereceli `rankChange{before, after, delta, place, tierUp, leagueUp, down, protectedLeft}`. Her el sonunda `xp{reason:"hand"}` (+340 bitiren / +60). Masa kapanır, lobiye dönülür.
- Sohbet: `chatSend{text ≤ 120}` ya da `chatSend{quick:"quick.1…8"\|"emoji.0…9"}` → `chat{seat, user, text, quick, bot?, spectator?}`; masa kuralı `rules.chat` (`E_CHAT_OFF`, `E_CHAT_QUICK_ONLY`). Hediye: `giftSend{gift, to: koltuk \| "all"}` → `gift{from, to[], gift, cost}` ("all" = fiyat × alıcı).

## 5 · Ad eşlemeleri (HTTP tarafı)

- Görevler: `q.win3`→`winHands`, `q.openPairs`→`pairsOpenings`, `q.play5`→`handsPlayed`, `q.okeyFinish`→`okeyFinishes`, `q.add40`→`tilesAdded`.
- Mağaza: `skin.galaxy/gold/neon/marble` → `skin-0/1/2/3`; `chips.50k/250k/1m/5m` → `chips-0…3` (bonus eklenir, D-27); `gems.100/550/1200/2500` → `gems-0…3`; `deal.weekend` → `deal`.
- Arkadaşlık isteği arkadaş koduyla (`{code}`), `userId` ile değil; kabul/ret `…/requests/{id}/accept|decline`.

## 6 · Çevrimdışı masa: `Packages/com.galaxy101.table`

Sunucunun maç akışı Unity'de de aynen koşar (botlara karşı, durum yönlendirici, seed 42 kareleri). Paket: `com.galaxy101.table`, asmdef `Galaxy101.Table.Runtime` (`Okey101.Core`, `Galaxy101.Contracts`; `noEngineReferences`). API (`Galaxy101.Table`):

```csharp
public interface ITableHost
{
    long Now { get; }                                   // ms; zamanlanmış iş kendi zamanını görür
    object Schedule(long atMs, Action action);          // ana iş parçacığında, Now = atMs iken çalıştır
    void Cancel(object handle);
    void Send(int seat, string type, object payload);   // seat 0–3 (insan) ya da -1 (izleyici); type = V4Messages.*
    void Applied(TableRuntime table, IReadOnlyList<GameEvent> events);  // XP, görev sayaçları
    void MatchEnded(TableRuntime table);                // matchResult'ı host gönderir (TableSettlement.Compute)
}

var table = new TableRuntime(host, new TableSetup { Seed = 42, Options = new MatchOptions { Mode = "solo", Hands = 5 },
    TurnMs = 30000, Chat = "open" }, bots: new[] { false, true, true, true }, chatRng: new RngState(42));
table.Start(v4Table);                       // matchStart, 3 sn sonra dağıtım
table.RollDice(seat);                       // gRollDice → hata kodu ya da null
table.Move(seat, new GameAction { T = "draw", Src = "deck" }, out var detail);   // g* hamleleri
table.ReadyUp(seat); table.RackOrder(seat, tiles);
table.Leave(seat); table.TakeSeat(seat);    // R-60 / R-65
table.Reconnect(seat); table.AnnounceSeat(seat); table.Disconnect(seat);
table.GameView(seat);                       // snapshot.game; seat -1 = izleyici
table.BotsHeard(fromSeat);                  // oyuncu yazınca bot yanıtı (%60, 1400 ms)
BotChat.Thanks(host, botSeat, gift, () => true);
TableSettlement.Compute(ranking, person, pool, lpTable, weekly, TableEconomy.XpPlace);
```

`TableTimings` (bütün sunucu süreleri, timings.md §1) ve `TableEconomy` (bahis/el/tur seçenekleri, XP, haftalık puan, 120 karakter, hazır mesaj anahtarları) sabitleri tek kaynaktır. Unity'de `Now`/`Schedule` `GameClock` (D-60) üzerine kurulur; `Send` olayı sunucudan gelmiş gibi aynı işleyiciye verir, böylece çevrimiçi ve çevrimdışı masa aynı kodu kullanır. Durum yönlendirici için ilk el `TableSetup.FirstHandRig` ile sabitlenebilir (`G101Rules.RigFromHands`, DEMO-17: yalnız test/ekran).

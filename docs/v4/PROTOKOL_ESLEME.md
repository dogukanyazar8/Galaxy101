# v4 · protokol eşlemesi (Hat B)

Kaynak: `pkg/protocol/protocol.json` 0.4.0 (53 C2S, 45 S2C, 94 hata kodu), `protocol.md`, `timings.md`. Karar: `docs/v4/KARARLAR.md` K-04 (mevcut taşıma korunur). Bu belgedeki tablolar kaynak koddan aranarak üretildi; `dosya:satır` değerleri `v4/server` dalının bu belgenin işlendiği halini gösterir.

Durum: **var** (önceden de vardı, aynı) · **eklendi** (v4 ile eklendi) · **farklı** (karşılığı var, adı/biçimi farklı; not açıklar) · **eksik** (pakette var, sunucuda yok; EKSIKLER'de) · **pakette yok** (sunucuda var, pakette yok; sondaki bölüm).

## Taşıma

| Paket | Sunucu | Not |
|---|---|---|
| Tek WebSocket | `GET /v1/ws?ticket=…` (`Server/src/Galaxy101.Server/Realtime/Realtime.cs`) | Bilet `POST /v1/realtime/ticket` (30 sn, tek kullanım) — giriş HTTP'de olduğu için soket her zaman girişli açılır. |
| `{t, rid, …alanlar}` | `{type, id, data:{…alanlar}}` | `t`→`type`, `rid`→`id` (string, ≤ 64), alanlar `data` içinde. |
| `ack{rid, ok, error, detail, …dönüş}` | `{type:"ack", data:{id, ok, code, detail?, at, …dönüş}}` | `error`→`code`. Her v4 C2S için tam bir ack (`id` gönderilmese de). Eski mesajlar (`table.*`, `match.*`, `queue.*`) eski biçimde: ack yalnız `id` varsa, `{id, ok, code}`. |
| S2C `at` | `data.at` | Sunucu saati, Unix ms. Hub mantıksal saatle çalışır: zamanlayıcıyla gelen olayın `at`'ı tam takvim zamanıdır (D-24 tablosu milisaniyesi milisaniyesine). |
| Hesap/ekonomi/sosyal | HTTP uçları | K-04 izni: aynı alanlar, hata gövdesinde `error` = paket kodu (+ `detail`). Mevcut `code` alanı değişmedi (eski istemci). |
| Masa ve oyun | WebSocket, adım adım | `g*` mesajları `Packages/com.galaxy101.table` `TableRuntime` → v4 kural motoru (`Okey101.Core.V4`, Hat A). Eski tur-bütün `match.command` yalnız eski protokolün maçları için kalır. |

Sunucu tarafı: `Server/src/Galaxy101.Server/V4/` (Hub, Store) + `Packages/com.galaxy101.table/Runtime/` (maç akışı, taşıma yok; Unity istemcisi çevrimdışı aynı kodu koşar). Hiçbir eşleme imkânsız çıkmadı; bu yüzden protocol.json taşımasına geçme kararı gerekmedi.

## Gizli bilgi (`protocol.json` hiddenInfo)

| Kural | Uygulama | Test |
|---|---|---|
| Rakip ıstakaları hiçbir mesajda yok; yalnız `counts` (el sonunda `handEnd.racks` açık) | `TableRuntime.GameView`, `Dealt`: yalnız kendi `rack`; `racksRevealed` yalnız `result` varken | SRV-03, SRV-11, `TableRuntimeTests.Views_carry_only_the_own_rack_and_the_spectator_none` |
| Deste sırası gönderilmez; yalnız `deck` sayısı | `GameView` `deck` = sayı; motor ağacındaki `hand.deck` hiç kopyalanmaz | aynı |
| Zar `dealDie` ile atış anında; gösterge/okey `dealStep:indicator` ile; `handStart` boşaltılmış | `TableRuntime.Roll`, `DealRest`; `ViewEvent` `handStart` → dice/indicator/okey null; görünümde tören `build/dice/afterDice` adımlarında gösterge yok, zarlar yalnız atılanlar | SRV-02, SRV-03, `Dice_and_indicator_are_told_only_when_rolled_and_revealed` |
| Desteden çekilen taş yalnız çekene; soldan alınan herkese | `ViewEvent`: `drew` `src=deck` başkasına `tile=null` | SRV-03 |
| `openShort` taşları yalnız deneyene | `ViewEvent`: `openShort` başkasına `tiles=null` | `A_short_opening_shows_its_tiles_only_to_the_player_who_tried` |
| İzleyici (seat -1) ıstakasız, açık bilgi | `TableHost.Send(-1)` izleyicilere; `GameView(-1)` rack/legal null | aynı |
| Her oyuncuya kendi görünümü (`snapshot.game`) | `Snapshot` → `GameView(seat)`; `tableView` her adımda kişiye özel | SRV-11 |

## Kopma ve yeniden bağlanma (`protocol.json` reconnect)

| Adım | Uygulama |
|---|---|
| Maçta kopma kalkma değil, `seatConnection{connected:false}` | `Hub.DisconnectedAsync` → `TableRuntime.Disconnect` (SRV-16) |
| Tur süresi işler, R-70; kopukken üst üste 2 zaman aşımı → sunucu oynar (`autoplay:true`) | `TableRuntime.Timeout` (D-35: bağlı oyuncuya yalnız R-70) |
| `hello` → `session` + `snapshot{screen:"table"}`, sunucu bırakır, sıra ondaysa süre baştan | `Hub.HelloAsync` → `TableRuntime.Reconnect` + `AnnounceSeat` (D-36). Hamle gönderen oyuncu için de (`Move`), yeni süre `turn` olayıyla gelir. |
| Bekleme odasında kopma → hemen çıkar, bahis iade, `leftTable{reason:"disconnect"}`; sahibiyse masa kapanır | `Hub.LeaveWaitRoomAsync` (SRV-17). Dereceli masa hazırlanırken kopma maç sayılır, koltuk korunur. |
| İstemci "Deneme n/5", 3 sn, sonra "Lobiye dön" | İstemci işi; süreler `TableTimings.ClientRetries` / `ClientRetryMs` (ISTEMCI_NOTU) |
| `snapshot.screen`: menu / waitroom / table / ranked.search / ranked.found / ranked.loading | `Hub.Snapshot` (SRV-09, SRV-10, SRV-11, SRV-17) |

## İstemci → sunucu (C2S, 53)

| Paket mesajı | Alanlar → dönüş | Sunucu karşılığı | Durum | Not |
|---|---|---|---|---|
| `hello` | `token`: string → `token`: string | ws `hello` → `Server/src/Galaxy101.Server/V4/Hub.cs:259` | eklendi | Soket zaten biletle girişli (`/v1/realtime/ticket`); `token` kabul edilir, değeri kullanılmaz (DEMO-11 imzasız belirteç yok). Başarıda `session` + `snapshot`; masadaysa sunucunun oynaması biter (D-36). Hatalar: E_SESSION_EXPIRED. |
| `register` | `email`: string<br>`pass`: string<br>`name`: string → `token`: string | HTTP `POST /v1/auth/register` `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:33` | farklı | `pass`→`password`, ek alan `acceptTerms` (mevcut kural); dönüş `token` yerine `TokenResponse` (access + refresh). Hata: gövdede `error` = paket kodu. Adlar benzersiz değil: `E_NAME_TAKEN` üretilmez (EK-205). Hatalar: E_EMAIL_INVALID E_PASS_SHORT E_NAME_INVALID E_EMAIL_EXISTS E_NAME_TAKEN. |
| `login` | `email`: string<br>`pass`: string → `token`: string | HTTP `POST /v1/auth/login` `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:42` | farklı | `pass`→`password`. `E_PASS_WRONG` `detail.left`, `E_ACCOUNT_LOCKED` `detail.until` (Unix ms). 3 yanlış → 5 dk (R-98). Hatalar: E_ACCOUNT_UNKNOWN E_PASS_WRONG E_ACCOUNT_LOCKED. |
| `guest` | `name`: string?<br>`color`: string? → `token`: string | HTTP `POST /v1/auth/guest` `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:22` | var | Ad ≤ 16, renk; 10.000 çip + 20 elmas (R-96). Boş ad için paketteki `Misafir####` yerine mevcut ad üretimi. Hatalar: E_NAME_INVALID. |
| `passwordResetRequest` | `email`: string → — | HTTP `POST /v1/auth/password/forgot` `Server/src/Galaxy101.Server/Auth/PasswordReset.cs:93` | var | Hesap var/yok belli edilmez (D-38). Kod e-postayla (DEMO-15 sabit 123456 yok). Hatalar: —. |
| `passwordReset` | `email`: string<br>`code`: string<br>`pass`: string → — | HTTP `POST /v1/auth/password/reset` `Server/src/Galaxy101.Server/Auth/PasswordReset.cs:118` | farklı | `pass`→`password`; başarıda kilit kalkar (R-98) ve bütün oturumlar kapanır (mevcut). Hatalar: E_RESET_CODE E_PASS_SHORT. |
| `oauth` | `provider`: "google"<br>`email`: string<br>`name`: string?<br>`idToken`: string → `token`: string | HTTP `POST /v1/auth/external` `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:57` | farklı | `{provider, idToken}`; `email`/`name` alınmaz, kimlik belirteci doğrulanır (DEMO-16 yok). Yeni hesap 25.000 + 20. Hatalar: E_OAUTH_FAILED. |
| `logout` | — → — | HTTP `POST /v1/auth/logout` `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:98` | farklı | Gövde `{refreshToken}`; soket istemcide kapatılır. Hatalar: —. |
| `accountDelete` | `confirm`: string → — | HTTP `DELETE /v1/account` `Server/src/Galaxy101.Server/Profile/ProfileEndpoints.cs:115` | farklı | "SİL / DELETE" onayı istemcide (`E_CONFIRM_MISMATCH` istemci hatası). Maçtaki hesap silinirken maç terk edilir (R-60); `E_IN_GAME` üretilmez (mevcut davranış). Hatalar: E_CONFIRM_MISMATCH E_IN_GAME. |
| `resync` | — → — | ws `resync` → `Server/src/Galaxy101.Server/V4/Hub.cs:270` | eklendi | `snapshot` gönderir. Hatalar: —. |
| `ping` | — → `serverTime`: int | ws `ping` `Server/src/Galaxy101.Server/Realtime/Realtime.cs:124` | var | Mevcut `pong` sürer; `id` varsa ayrıca `ack{serverTime}` (Unix ms). Hatalar: —. |
| `profileUpdate` | `name`: string?<br>`status`: string?<br>`color`: string?<br>`frame`: int? → — | HTTP `PUT /v1/profile` `Server/src/Galaxy101.Server/Profile/ProfileEndpoints.cs:26` | farklı | Bütün alanlar birlikte (ad, renk, durum, çerçeve 0/1). Hata `error`: `E_NAME_INVALID`, `E_STATUS_LONG`, `E_FRAME_LOCKED` (çerçeve 0/1 dışı). Galaksi çerçevesi seviye 30 kuralı yok (EK-206). Hatalar: E_NAME_INVALID E_STATUS_LONG E_FRAME_LOCKED. |
| `shopBuy` | `sku`: string → — | HTTP `POST /v1/shop/purchase` (elmasla) `Server/src/Galaxy101.Server/Economy/ShopEndpoints.cs:31` · `POST /v1/shop/store-purchase` (gerçek para) `Server/src/Galaxy101.Server/Economy/ShopEndpoints.cs:57` | farklı | SKU adları sunucu kataloğundan (`skin-0…3`, `chips-0…3`, `gems-0…3`, `deal`; paket `skin.galaxy` …). Gerçek para paketi mağaza makbuzu doğrulanınca verilir (DEMO-10 anında verme yok). Bonus yüzdesi eklenir (D-27). Hatalar: E_SKU_UNKNOWN E_ALREADY_OWNED E_NO_GEMS. |
| `skinSet` | `skin`: string → — | — | eksik | Seçili taş görünümü sunucuda tutulmuyor; `me.skin` ayarlar JSON'undaki `skin` alanından okunur (EK-207). Hatalar: E_NOT_OWNED. |
| `dailyGet` | — → — | HTTP `GET /v1/daily` `Server/src/Galaxy101.Server/Economy/Daily.cs:121` | farklı | Yanıt `DailyResponse` (streak[7], quests[], nextReset, wallet); paketteki `daily{day,claimedToday,amounts,msToReset}` aynı bilgiyi başka biçimde taşır. Hatalar: —. |
| `dailyClaim` | — → `amount`: int | HTTP `POST /v1/daily/claim` `Server/src/Galaxy101.Server/Economy/Daily.cs:124` | farklı | Dönüş `amount` yok: yanıttaki cüzdan farkı. 1.000…15.000, gün Europe/Istanbul (R-97). `E_DAILY_CLAIMED` gövdede. Hatalar: E_DAILY_CLAIMED. |
| `questClaim` | `questId`: string → — | HTTP `POST /v1/daily/quests/{id}/claim` `Server/src/Galaxy101.Server/Economy/Daily.cs:127` | farklı | Görev kimlikleri: `q.win3`→`winHands`, `q.openPairs`→`pairsOpenings`, `q.play5`→`handsPlayed`, `q.okeyFinish`→`okeyFinishes`, `q.add40`→`tilesAdded` (miktarlar aynı). Hatalar: E_QUEST_UNKNOWN E_QUEST_INCOMPLETE E_QUEST_CLAIMED. |
| `friendsGet` | — → — | HTTP `GET /v1/friends` `Server/src/Galaxy101.Server/Social/FriendsEndpoints.cs:17` | farklı | Yanıt `{friends, incoming, outgoing}`. Hatalar: —. |
| `friendFind` | `query`: string → `result`: "friend"\|"player"\|"none"<br>`user`: User? | HTTP `GET /v1/players/search?q=` `Server/src/Galaxy101.Server/Social/FriendsEndpoints.cs:109` | farklı | Kod ya da ad öneki; `result` yerine liste. Hatalar: —. |
| `friendRequest` | `userId`: string → — | HTTP `POST /v1/friends/requests` `Server/src/Galaxy101.Server/Social/FriendsEndpoints.cs:39` | farklı | `userId` yerine arkadaş kodu `{code}`. Tekrar istek 204 döner: `E_REQUEST_PENDING` üretilmez (EK-208). Hatalar: E_USER_UNKNOWN E_ALREADY_FRIENDS E_REQUEST_PENDING. |
| `friendRespond` | `userId`: string<br>`accept`: bool → — | HTTP `POST /v1/friends/requests/{id}/accept` · `/decline` `Server/src/Galaxy101.Server/Social/FriendsEndpoints.cs:74` | farklı | `accept` alanı yerine iki uç. Hatalar: E_REQUEST_UNKNOWN. |
| `friendRemove` | `userId`: string → — | HTTP `DELETE /v1/friends/{id}` `Server/src/Galaxy101.Server/Social/FriendsEndpoints.cs:101` | var | `E_NOT_FRIENDS` gövdede. Hatalar: E_NOT_FRIENDS. |
| `leaderboardGet` | `board`: "weekly"\|"season"\|"friends" → — | HTTP `GET /v1/leaderboard/{weekly|season|friends}` `Server/src/Galaxy101.Server/Social/LeaderboardEndpoints.cs:20` | farklı | Satır `{rank, player, points, me}`; 30/15/5/1, pazartesi 00:00 İstanbul, sezon = LP (R-95). Hatalar: —. |
| `lobbySubscribe` | — → — | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:153` | eklendi | Listelenen masalar: dereceli, botlara karşı ve özel değil. Hatalar: —. |
| `lobbyUnsubscribe` | — → — | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:160` | eklendi |  Hatalar: —. |
| `tableCreate` | `name`: string<br>`mode`: "solo"\|"pairs"<br>`private`: bool<br>`pin`: string(4)?<br>`hands`: 3\|5\|7<br>`bet`: 1000\|5000\|10000\|50000\|100000<br>`katlama`: bool<br>`turnSec`: 15\|20\|30\|45<br>`rules`: {spectate:bool,chat:"open"\|"quick"\|"off",gifts:bool,isle:bool,penalties:bool}<br>`katlamaPartner`: bool? (yalnız eşli + katlamalı; varsayılan true) → `table`: Table | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:171` | eklendi | Bahis oturunca düşülür (R-90). Ad ≤ 28 ve küfür maskesi (mevcut kural). Hatalar: E_ALREADY_SEATED E_TABLE_NAME E_PIN_FORMAT E_TABLE_OPTIONS E_NO_CHIPS. |
| `tableJoin` | `tableId`: string<br>`pin`: string?<br>`seat`: int? → `table`: Table<br>`seat`: int | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:241` | eklendi | Oynanan masada R-65 devralma: `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:266`. Hatalar: E_TABLE_CLOSED E_ALREADY_SEATED E_PIN_WRONG E_TABLE_FULL E_NO_CHIPS E_TABLE_PLAYING. |
| `tableJoinCode` | `code`: string<br>`pin`: string? → `table`: Table<br>`seat`: int | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:251` | eklendi | GX-7Q2M (DEMO-02) yok: kod sunucuda üretilir. Hatalar: E_CODE_FORMAT E_CODE_UNKNOWN E_PIN_WRONG E_TABLE_FULL E_NO_CHIPS E_ALREADY_SEATED. |
| `tableSpectate` | `tableId`: string → — | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:310` | eklendi | `snapshot{spectator:true}`; görünüm seat -1. Hatalar: E_TABLE_CLOSED E_SPECTATE_OFF E_ALREADY_SEATED. |
| `tableLeave` | — → — | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:327` | eklendi | Bekleme odası: iade (D-11), sahibi çıkarsa masa kapanır. Maçta R-60. Hatalar: E_NOT_SEATED. |
| `seatAddBot` | `seat`: int → — | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:411` | eklendi | Bot adları mevcut sunucunun ("Bot · Ayşe" …), paketteki adlar DEMO-06. Hatalar: E_NOT_SEATED E_NOT_OWNER E_STATUS E_SEAT_TAKEN. |
| `seatKick` | `seat`: int → — | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:423` | eklendi | Çıkarılan insana iade + `kicked`. Hatalar: E_NOT_SEATED E_NOT_OWNER E_STATUS E_SEAT_EMPTY. |
| `tableStart` | — → — | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:441` | eklendi | 3-2-1 (waitRoomCountdown). Hatalar: E_NOT_SEATED E_NOT_OWNER E_STATUS E_TABLE_NOT_FULL. |
| `inviteSend` | `userId`: string<br>`seat`: int?<br>`recent`: bool? → `inviteId`: string<br>`expiresAt`: int | ws → `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:24` | eklendi | 30 sn (inviteTtl). `recent:true` son 20 maçta birlikte oynamış olmayı sunucuda doğrular. Hatalar: E_NOT_SEATED E_STATUS E_USER_UNKNOWN E_NOT_FRIENDS E_USER_OFFLINE E_USER_BUSY E_TABLE_FULL. |
| `inviteRespond` | `inviteId`: string<br>`accept`: bool → — | ws → `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:52` | eklendi |  Hatalar: E_INVITE_EXPIRED E_TABLE_FULL E_NO_CHIPS E_TABLE_CLOSED. |
| `inviteCancel` | `inviteId`: string → — | ws → `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:78` | eklendi |  Hatalar: E_INVITE_EXPIRED. |
| `vsBotsStart` | `mode`: "solo"\|"pairs"<br>`hands`: int? → `table`: Table | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:490` | eklendi | `hands` 1–7 (paket testleri 1 ve 2 kullanır), çip yok, XP var (D-42). Hatalar: E_ALREADY_SEATED. |
| `rankedQueue` | `mode`: "solo"\|"duo"\|"squad"<br>`party`: string[] → — | ws → `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:60` | eklendi | Gerçek kuyruk (DEMO-09 bot rakip yok): solo 4×1, duo 2×2 (D-29), squad 1×4; ±1 lig, 30 sn sonra ±2. Hatalar: E_ALREADY_SEATED E_ALREADY_QUEUED E_MODE E_PARTY_SIZE E_PARTY_RANGE. |
| `rankedAccept` | `matchId`: string<br>`accept`: bool → — | ws → `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:150` | eklendi | 10 sn; reddeden çıkar, diğerleri aramaya döner. Hatalar: E_MATCH_EXPIRED. |
| `rankedCancel` | — → — | ws → `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:168` | eklendi |  Hatalar: E_NOT_QUEUED. |
| `gRollDice` | — → — | ws → `TableRuntime.RollDice` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:130` | eklendi | D-23: her zar ≤ 15 sn; gönderilirse hemen. Hatalar: E_NOT_SEATED E_SPECTATOR E_PHASE E_NOT_DEALER. |
| `gDraw` | `src`: "deck"\|"side" → — | ws → `TableRuntime.Move` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:308` | eklendi | Eski tur-bütün `match.command{kind:draw}` yerine adım. Hatalar: ENGINE. |
| `gReturnSide` | — → — | ws → `TableRuntime.Move` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:308` | eklendi | R-32, D-10. Hatalar: ENGINE. |
| `gOpen` | `melds`: [{kind:"run"\|"group"\|"pair",tiles:string[]}] → — | ws → `TableRuntime.Move` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:308` | eklendi | Eksik açılış = başarılı `open` + `openShort` (R-23). Hatalar: ENGINE. |
| `gLay` | `melds`: [{kind,tiles}] → — | ws → `TableRuntime.Move` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:308` | eklendi |  Hatalar: ENGINE. |
| `gAdd` | `tile`: string<br>`meld`: string<br>`side`: "L"\|"R"\|null → — | ws → `TableRuntime.Move` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:308` | eklendi | `side`: paket `"L"\|"R"\|null`, motor `pre\|post`: ikisi de kabul (`L`→`pre`, `R`→`post`; EK-201). Hatalar: ENGINE. |
| `gSwap` | `tile`: string<br>`meld`: string<br>`index`: int → — | ws → `TableRuntime.Move` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:308` | eklendi | R-26. Hatalar: ENGINE. |
| `gUndo` | — → — | ws → `TableRuntime.Move` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:308` | eklendi | R-34. Hatalar: ENGINE. |
| `gDiscard` | `tile`: string → — | ws → `TableRuntime.Move` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:308` | eklendi | Bitiş dahil (R-36). Hatalar: ENGINE. |
| `gReady` | — → — | ws → `TableRuntime.ReadyUp` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:350` | eklendi | D-25. Hatalar: E_NOT_SEATED E_SPECTATOR E_PHASE. |
| `gRackOrder` | `tiles`: (string\|null)[] → — | ws → `TableRuntime.RackOrder` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:364` | eklendi | Yalnız taş kümesi doğrulanır. Hatalar: E_RACK_MISMATCH. |
| `chatSend` | `text`: string?<br>`quick`: string? → — | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:511` | eklendi | ≤ 120 (D-32), R-63 kuralları, küfür maskesi ve engelleme (mevcut). Eski `match.chat` 80 karakter → 120. Hatalar: E_NOT_SEATED E_CHAT_OFF E_CHAT_QUICK_ONLY E_CHAT_LENGTH E_CHAT_QUICK_UNKNOWN. |
| `giftSend` | `gift`: string<br>`to`: int\|"all" → — | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:532` | eklendi | "all" = fiyat × alıcı sayısı (D-26; eski yol 3 × fiyat). Hatalar: E_NOT_SEATED E_GIFTS_OFF E_GIFT_UNKNOWN E_GIFT_TARGET E_NO_CHIPS. |

## Sunucu → istemci (S2C, 45)

| Paket olayı | Kime | Alanlar | Sunucu karşılığı | Durum | Not |
|---|---|---|---|---|---|
| `ack` | gönderen | `rid`: int<br>`ok`: bool<br>`error`: string?<br>`detail`: object? | ws `ack` `Server/src/Galaxy101.Server/Realtime/Realtime.cs:177` | farklı | `rid`→`id`, `error`→`code`; `detail`, `at` ve dönüş alanları aynı nesnede. v4 mesajlarının her biri tam bir ack alır. |
| `session` | kullanıcı | `me`: Me<br>`serverVersion`: string<br>`serverTime`: int | ws `session` `Server/src/Galaxy101.Server/V4/Hub.cs:264` | eklendi |  |
| `snapshot` | kullanıcı | `me`: Me<br>`screen`: "menu"\|"waitroom"\|"table"\|"ranked.search"\|"ranked.found"\|"ranked.loading"<br>`table`: Table?<br>`game`: GameView?<br>`ranked`: Queue?<br>`spectator`: bool? | ws `snapshot` `Server/src/Galaxy101.Server/V4/Hub.cs:284` | eklendi |  |
| `profile` | kullanıcı | `me`: Me | HTTP `GET /v1/profile` yanıtı | farklı | Profil değişince WS olayı yok; istemci HTTP yanıtını kullanır. |
| `balance` | kullanıcı | `chips`: int<br>`gems`: int<br>`dChips`: int<br>`dGems`: int<br>`reason`: "bet"\|"betRefund"\|"win"\|"daily"\|"quest"\|"purchase"\|"shop"\|"gift"<br>`ref`: string? | ws `balance` `Server/src/Galaxy101.Server/V4/Hub.cs:207` + HTTP yanıtları | farklı | Hub hareketleri (bet, betRefund, win, gift) WS `balance`. daily / quest / purchase / shop: HTTP yanıtındaki `wallet` (eski WS `wallet` olayı sürer). |
| `xp` | kullanıcı | `gained`: int<br>`xp`: int<br>`xpNext`: int<br>`level`: int<br>`levelUp`: int<br>`reason`: string | ws `xp` `Server/src/Galaxy101.Server/V4/Hub.Match.cs:85` | eklendi |  |
| `daily` | kullanıcı | `daily`: {day:int,claimedToday:bool,amounts:int[7],msToReset:int}<br>`quests`: [{id,cur,goal,reward,gem,claimed}] | HTTP `GET /v1/daily` yanıtı | farklı |  |
| `presence` | arkadaşlar | `user`: User | ws `presence` `Packages/com.galaxy101.contracts/Runtime/Realtime.cs:30` | farklı | `{accountId, presence: offline\|online\|playing}`; paket `user` kartı gönderir. |
| `friends` | kullanıcı | `friends`: User[]<br>`requests`: User[] | HTTP `GET /v1/friends` yanıtı | farklı |  |
| `friendRequestIn` | alıcı | `from`: User | ws `friend.request` `Packages/com.galaxy101.contracts/Runtime/Realtime.cs:31` | farklı | Ad farklı; içerik `{player, sentAt}`. |
| `friendRequestResult` | iki taraf | `user`: User<br>`accepted`: bool | ws `friend.accepted` `Packages/com.galaxy101.contracts/Runtime/Realtime.cs:32` | farklı | Yalnız kabul bildirilir; ret sessiz. |
| `friendRemoved` | iki taraf | `userId`: string | — | eksik | Arkadaşlıktan çıkarma bildirilmez (EK-209). |
| `leaderboard` | kullanıcı | `board`: "weekly"\|"season"\|"friends"<br>`rows`: [{pos,user,rankIdx,lp,pts}]<br>`mine`: row?<br>`resetsAt`: int? | HTTP `GET /v1/leaderboard/{scope}` yanıtı | farklı |  |
| `lobby` | abone | `tables`: Table[] | ws `lobby` `Server/src/Galaxy101.Server/V4/Hub.cs:29` | eklendi |  |
| `lobbyTable` | abone | `table`: Table | ws `lobbyTable` `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:150` | eklendi |  |
| `lobbyTableRemoved` | abone | `id`: string | ws `lobbyTableRemoved` `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:150` | eklendi |  |
| `tableState` | masadakiler | `table`: Table | ws `tableState` `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:144` | eklendi |  |
| `tableClosed` | masadakiler | `id`: string<br>`reason`: "ownerLeft" | ws `tableClosed` `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:389` | eklendi |  |
| `kicked` | çıkarılan | `tableId`: string | ws `kicked` `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:433` | eklendi |  |
| `countdown` | masadakiler | `n`: int<br>`startsIn`: int<br>`cancelled`: bool? | ws `countdown` `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:463` | eklendi |  |
| `seatConnection` | masadakiler | `seat`: int<br>`connected`: bool<br>`autoplay`: bool? | ws `seatConnection` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:276` | eklendi |  |
| `leftTable` | kalkan | `reason`: "leave"\|"disconnect"<br>`toLobbyIn`: int | ws `leftTable` `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:356` | eklendi |  |
| `inviteIn` | alıcı | `inviteId`: string<br>`from`: User<br>`table`: Table<br>`expiresAt`: int | ws `inviteIn` `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:48` | eklendi |  |
| `inviteExpired` | alıcı | `inviteId`: string | ws `inviteExpired` `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:46` | eklendi |  |
| `inviteResult` | gönderen | `inviteId`: string<br>`userId`: string<br>`result`: "accepted"\|"declined"\|"expired"\|"failed"<br>`seat`: int?<br>`error`: string? | ws `inviteResult` `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:45` | eklendi |  |
| `rankedSearch` | kullanıcı | `mode`: string<br>`since`: int<br>`range`: 1\|2<br>`filled`: int<br>`found`: null | ws `rankedSearch` `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:57` | eklendi |  |
| `rankedFound` | kullanıcı | `found`: {matchId,players:[{user,bot}],expiresAt,accepted:bool[4]} | ws `rankedFound` `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:110` | eklendi |  |
| `rankedAccepted` | kullanıcı | `index`: int<br>`accepted`: bool[4] | ws `rankedAccepted` `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:163` | eklendi |  |
| `rankedCancelled` | kullanıcı | `reason`: "timeout"\|"declined"\|"cancel" | ws `rankedCancelled` `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:140` | eklendi |  |
| `rankedReady` | masadakiler | `table`: Table<br>`loadingMs`: int | ws `rankedReady` `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:196` | eklendi |  |
| `rankChange` | kullanıcı | `before`: {idx,lp}<br>`after`: {idx,lp}<br>`delta`: int<br>`place`: int<br>`tierUp`: bool<br>`leagueUp`: bool<br>`down`: bool<br>`protectedLeft`: int | ws `rankChange` `Server/src/Galaxy101.Server/V4/Hub.Match.cs:157` | eklendi |  |
| `matchStart` | masadakiler | `table`: Table<br>`countdownMs`: int | ws `matchStart` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:101` | eklendi |  |
| `dealStart` | masadakiler | `handNo`: int<br>`dealer`: int<br>`stacks`: 15<br>`perStack`: 7<br>`buildMs`: int<br>`humanDealer`: bool | ws `dealStart` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:116` | eklendi |  |
| `dealStep` | masadakiler | `step`: "dice"\|"indicator"\|"extra"\|"distribute"\|"center"<br>`dealer`: int?<br>`rollWaitMs`: int?<br>`stack`: int?<br>`level`: int?<br>`rolledLevel`: int?<br>`fakeSkip`: bool?<br>`indicator`: string?<br>`okey`: {c,n}?<br>`plan`: [{seat,stacks:int[]}]?<br>`stacks`: int[]?<br>`deck`: int? | ws `dealStep` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:124` | eklendi |  |
| `dealDie` | masadakiler | `index`: 0\|1<br>`value`: 1..6<br>`rollMs`: int | ws `dealDie` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:148` | eklendi |  |
| `handDealt` | her oyuncuya ayrı | `handNo`: int<br>`rack`: string[]\|null<br>`counts`: int[4]<br>`deck`: int<br>`indicator`: string<br>`okey`: {c,n}<br>`starter`: int | ws `handDealt` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:195` | eklendi |  |
| `turn` | masadakiler | `seat`: int<br>`phase`: "draw"\|"play"<br>`n`: int<br>`turnMs`: int<br>`deadline`: int | ws `turn` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:220` | eklendi |  |
| `legal` | sıradaki oyuncu | `legal`: {draw,side,discard,open,undo,returnSide,add} | ws `legal` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:227` | eklendi |  |
| `game` | masadakiler (görünüm süzgeçli) | `ev`: EngineEvent | ws `game` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:472` | eklendi |  |
| `handWait` | masadakiler | `nextHandIn`: int<br>`nextAt`: int<br>`handNo`: int<br>`hands`: int | ws `handWait` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:345` | eklendi |  |
| `readyState` | masadakiler | `ready`: int[] | ws `readyState` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:358` | eklendi |  |
| `matchResult` | masadakiler | `ranking`: Unit[]<br>`totals`: int[4]<br>`history`: array<br>`pool`: int (ödenen bahislerin toplamı)<br>`rake`: 0<br>`payouts`: int[4]<br>`xpBonus`: int[4]<br>`lp`: (int\|null)[4]<br>`weekly`: int[4] | ws `matchResult` `Server/src/Galaxy101.Server/V4/Hub.Match.cs:127` | eklendi |  |
| `chat` | masadakiler | `seat`: int<br>`user`: string?<br>`text`: string?<br>`quick`: string?<br>`bot`: bool?<br>`spectator`: bool? | ws `chat` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:261` | eklendi |  |
| `gift` | masadakiler | `from`: int<br>`to`: int[]<br>`gift`: string<br>`cost`: int | ws `gift` `Server/src/Galaxy101.Server/V4/Hub.cs:34` | eklendi |  |
| `tableView` | masadaki her insan oyuncu ve izleyici (kendi görünümü) | `view`: GameView (snapshot.game ile aynı biçim): status, handNo, dealer, totals, deck, rack (yalnız kendi), counts, discards, melds, opened, openMode, pen, turn{seat,phase,n,sideTake,sideMust,deadline}, legal, result | ws `tableView` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:481` | eklendi |  |

## Hata kodları (94)

| Kod | ui | Sunucuda nerede | Durum | Not |
|---|---|---|---|---|
| `E_BAD_MESSAGE` | silent | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:414` | var | v4 mesajının gövdesi çözülemezse. |
| `E_UNKNOWN_MESSAGE` | silent | — | farklı | Bilinmeyen `type` için mevcut ack kodu `not_found` (eski istemciler bunu bekliyor). |
| `E_AUTH_REQUIRED` | screen | — | farklı | HTTP 401 / soket bileti yoksa yükseltme 401; ack kodu olarak gelmez. |
| `E_SESSION_EXPIRED` | screen | `Server/src/Galaxy101.Server/V4/Hub.cs:261` | var | `hello` hesabı bulamazsa; HTTP `/v1/auth/refresh` geçersizse gövdede. |
| `E_EMAIL_INVALID` | inline | `Server/src/Galaxy101.Server/Auth/Passwords.cs:54` | eklendi (HTTP `error`) | alan altı kırmızı |
| `E_PASS_SHORT` | inline | `Server/src/Galaxy101.Server/Auth/PasswordReset.cs:121` | eklendi (HTTP `error`) |  |
| `E_NAME_INVALID` | inline | `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:25` | eklendi (HTTP `error`) |  |
| `E_EMAIL_EXISTS` | inline | `Server/src/Galaxy101.Server/Auth/Passwords.cs:60` | eklendi (HTTP `error`) |  |
| `E_NAME_TAKEN` | inline | — | eksik | Adlar benzersiz değil (arkadaş kodu ayırt eder) — EK-205. |
| `E_ACCOUNT_UNKNOWN` | inline | `Server/src/Galaxy101.Server/Auth/Passwords.cs:74` | eklendi (HTTP `error`) |  |
| `E_PASS_WRONG` | inline | `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:48` | eklendi (HTTP `error`) | "N deneme hakkın kaldı" (detail.left) |
| `E_ACCOUNT_LOCKED` | inline | `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:49` | eklendi (HTTP `error`) | "5 dakika sonra tekrar dene" (detail.until), "Şifremi unuttum" bağlantısı vurgulu |
| `E_CONFIRM_MISMATCH` | inline | — | farklı | Onay metni istemcide denetlenir. |
| `E_IN_GAME` | toast | — | farklı | Hesap silmede maç terk edilir (mevcut). |
| `E_STATUS_LONG` | inline | `Server/src/Galaxy101.Server/Profile/ProfileEndpoints.cs:30` | eklendi (HTTP `error`) |  |
| `E_FRAME_LOCKED` | toast | `Server/src/Galaxy101.Server/Profile/ProfileEndpoints.cs:30` | farklı | Çerçeve yalnız 0/1; seviye 30 kuralı yok (EK-206). |
| `E_SKU_UNKNOWN` | toast | `Server/src/Galaxy101.Server/Economy/ShopEndpoints.cs:34` | eklendi (HTTP `error`) |  |
| `E_ALREADY_OWNED` | toast | `Server/src/Galaxy101.Server/Economy/ShopEndpoints.cs:36` | eklendi (HTTP `error`) |  |
| `E_NO_GEMS` | toast | `Server/src/Galaxy101.Server/Economy/ShopEndpoints.cs:41` | eklendi (HTTP `error`) | eksik miktar detail.missing |
| `E_NOT_OWNED` | toast | — | eksik | `skinSet` yok (EK-207). |
| `E_DAILY_CLAIMED` | silent | `Server/src/Galaxy101.Server/Economy/Daily.cs:135` | eklendi (HTTP `error`) | düğme zaten pasif |
| `E_QUEST_UNKNOWN` | silent | `Server/src/Galaxy101.Server/Economy/Daily.cs:134` | eklendi (HTTP `error`) |  |
| `E_QUEST_INCOMPLETE` | silent | `Server/src/Galaxy101.Server/Economy/Daily.cs:136` | eklendi (HTTP `error`) |  |
| `E_QUEST_CLAIMED` | silent | `Server/src/Galaxy101.Server/Economy/Daily.cs:135` | eklendi (HTTP `error`) |  |
| `E_USER_UNKNOWN` | inline | `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:28` | eklendi | "bulunamadı" |
| `E_ALREADY_FRIENDS` | inline | `Server/src/Galaxy101.Server/Social/FriendsEndpoints.cs:52` | eklendi (HTTP `error`) | "zaten arkadaşsınız" |
| `E_REQUEST_PENDING` | inline | — | eksik | Tekrar istek 204 (EK-208). |
| `E_REQUEST_UNKNOWN` | silent | `Server/src/Galaxy101.Server/Social/FriendsEndpoints.cs:87` | eklendi (HTTP `error`) |  |
| `E_NOT_FRIENDS` | silent | `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:29` | eklendi |  |
| `E_ALREADY_SEATED` | toast | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:173` | eklendi |  |
| `E_TABLE_NAME` | inline | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:175` | eklendi | Oluştur pasif |
| `E_PIN_FORMAT` | inline | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:176` | eklendi |  |
| `E_TABLE_OPTIONS` | silent | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:180` | eklendi |  |
| `E_NO_CHIPS` | toast | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:182` | eklendi | "Yetersiz bakiye · X çip eksik" (detail.missing) |
| `E_TABLE_CLOSED` | dialog | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:229` | eklendi |  |
| `E_PIN_WRONG` | shake | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:231` | eklendi | PIN kutuları sallanır + hata |
| `E_TABLE_FULL` | dialog | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:233` | eklendi |  |
| `E_TABLE_PLAYING` | toast | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:269` | eklendi | dereceli / botlara karşı masaya katılım |
| `E_CODE_FORMAT` | inline | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:254` | eklendi |  |
| `E_CODE_UNKNOWN` | inline | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:256` | eklendi |  |
| `E_SPECTATE_OFF` | toast | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:314` | eklendi |  |
| `E_NOT_SEATED` | silent | `Server/src/Galaxy101.Server/V4/Hub.cs:312` | eklendi |  |
| `E_NOT_OWNER` | silent | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:406` | eklendi |  |
| `E_STATUS` | silent | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:407` · motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:281` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_SEAT_TAKEN` | toast | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:415` | eklendi |  |
| `E_SEAT_EMPTY` | silent | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:426` | eklendi |  |
| `E_TABLE_NOT_FULL` | silent | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:444` | eklendi | Başlat pasif |
| `E_USER_OFFLINE` | silent | `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:30` | eklendi | satır düğmesi pasif |
| `E_USER_BUSY` | silent | `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:32` | eklendi |  |
| `E_INVITE_EXPIRED` | toast | `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:54` | eklendi |  |
| `E_ALREADY_QUEUED` | silent | `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:62` | eklendi |  |
| `E_MODE` | silent | `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:64` | eklendi |  |
| `E_PARTY_SIZE` | silent | `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:69` | eklendi | HEMEN OYNA pasif |
| `E_PARTY_RANGE` | toast | `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:74` | eklendi | ±1 lig dışı |
| `E_MATCH_EXPIRED` | toast | `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:152` | eklendi |  |
| `E_NOT_QUEUED` | silent | `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:170` | eklendi |  |
| `E_SPECTATOR` | silent | `Server/src/Galaxy101.Server/V4/Hub.cs:313` | eklendi |  |
| `E_DEALING` | silent | `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:312` | var | Tören sürerken g* hamleleri. |
| `E_NOT_DEALER` | silent | `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:134` | eklendi |  |
| `E_RACK_MISMATCH` | resync | `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:366` | eklendi | resync gönder |
| `E_CHAT_OFF` | silent | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:514` | eklendi | sohbet paneli gizli |
| `E_CHAT_QUICK_ONLY` | silent | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:515` | eklendi | yazı alanı gizli |
| `E_CHAT_LENGTH` | inline | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:516` | eklendi |  |
| `E_CHAT_QUICK_UNKNOWN` | silent | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:517` | eklendi |  |
| `E_GIFTS_OFF` | silent | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:535` | eklendi | hediye düğmesi gizli |
| `E_GIFT_UNKNOWN` | silent | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:538` | eklendi |  |
| `E_GIFT_TARGET` | silent | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:543` | eklendi |  |
| `E_NOT_YOUR_TURN` | silent | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:500` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_PHASE` | silent | `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:133` · motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:522` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_HAND_OVER` | silent | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:497` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_TILE_NOT_IN_RACK` | resync | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:585` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_SIDE_EMPTY` | silent | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:529` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_SIDE_RETURNED` | toast | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:527` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_SIDE_DISCARD` | toast | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:759` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_SIDE_MUST_OPEN` | toast | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:646` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_RETURN_INVALID` | silent | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:552` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_DECK_EMPTY` | silent | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:536` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_MELD_INVALID` | toast | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:572` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_MELD_MIX` | toast | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:594` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_MELD_NOT_FOUND` | resync | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:700` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_ALREADY_OPENED` | silent | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:643` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_NOT_OPENED` | toast | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:678` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_OPEN_MODE` | toast | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:681` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_LAST_TILE` | toast | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:595` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_ISLE_INVALID` | silent | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:703` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_ISLE_DISABLED` | toast | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:695` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_SWAP_INVALID` | toast | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:727` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_NO_UNDO` | silent | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:744` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_LEAVE_INVALID` | silent | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:334` · motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:818` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_ACTION` | silent | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:493` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |
| `E_SPEC_DUP` | silent | — | var (motor, yalnız test) | Motor `rigFromHands` (Hat A); sunucu mesajı değil. |
| `E_RESET_CODE` | inline | `Server/src/Galaxy101.Server/Auth/PasswordReset.cs:122` | eklendi (HTTP `error`) | kod yanlış |
| `E_OAUTH_FAILED` | toast | `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:61` | eklendi (HTTP `error`) |  |
| `E_SEAT_NOT_BOT` | silent | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:807` | eklendi (motor) | Kural motoru (Hat A) üretir; `TableRuntime.Move` ack'e aynen koyar (`detail` dahil). |

## Pakette yok (sunucuda kalan, silinmeyen)

| Sunucu | Ne | Not |
|---|---|---|
| ws `queue.join {mode: solo\|pairs\|doubling\|ranked, stake}` / `queue.leave` / `queue.status` | Hızlı eşleşme kuyruğu (4 kişi bulunca maç) | Pakette hızlı eşleşme yok (lobi masaları + dereceli var). Eski motorla oynar. |
| ws `room.create` / `room.join` / `room.leave` / `room.state` | Kodlu özel oda (eski) | Paketteki özel masa `tableCreate{private:true}` + `tableJoinCode` ile karşılandı. |
| ws `table.create` / `table.join` / `table.watch` / `table.leave` / `table.addBot` / `table.kick` / `table.start`, `lobby.subscribe`, `lobby.tables`, `table.state` | v3–v7 lobi masaları | v4 karşılıkları `tableCreate` … ; eski istemci için duruyor. Eski masa dolunca 5 sn'de kendiliğinden sayar (paket: yalnız sahibi bot/demo olan masada 900 ms). |
| ws `match.command` (`draw\|return\|shortOpening\|commit`), `match.sync`, `match.leave`, `match.emote`, `match.chat`, `match.gift`, `match.state`, `match.start`, `match.handEnd`, `match.end`, `match.seats` | Tur-bütün eski motor | Yeni maçlar v4 adım protokolüyle; eski kayıtlar (`xorshift32-fy-v1` / `101-v1`) okunabilir kalır. |
| ws `session.replaced` | Aynı hesap ikinci cihazdan bağlanınca eski soket kapanır | Pakette tek bağlantı varsayımı. |
| `Game:HandsPerMatch` (4) | Hızlı eşleşme maçının el sayısı | v4 masaları masa seçeneği (3/5/7), dereceli 5 (R-94). |
| `Game:Stakes` (solo/pairs/doubling/ranked/private) | Hızlı eşleşme bahis seviyeleri | v4 masa bahisleri paketten: 1.000 / 5.000 / 10.000 / 50.000 / 100.000. |
| `Game:SoloPayout`, `Game:RakePercent` | Eski yol pot dağılımı | v4: kesinti 0 (R-91), kazanan(lar) havuzun tamamı. |
| İzleyici sınırı 20 (eski `MatchSession.MaxSpectators`) | | v4 hub'da sınır yok (paket belirtmiyor). |
| Sohbet hız sınırı 1 sn, emoji 2 sn (eski `MatchSession`) | | v4'te yok (soket taşma koruması 40/20 sn kalır). |
| Küfür maskesi, engelleme, şikayet, moderatör uçları | v4 sohbetinde de uygulanır | Pakette yok; mevcut güvenlik özelliği. |
| Sezon ödülü, sezon başı LP düşüşü, Galaksi ligi | `Profile/Ranked.cs` | Pakette sezon geçişi tanımlı değil; korunur. |
| Gerçek para mağazası (Google Play / App Store makbuzu) | `POST /v1/shop/store-purchase` | Paket: DEMO-10 anında verme. |
| Hesap bağlama (`/v1/auth/link`), Apple girişi, token yenileme | | Pakette yalnız Google ve imzasız belirteç (DEMO-11, DEMO-16). |
| Profil istatistik / rozet / geçmiş, bulut ayarları, KVKK onayı, paylaşım bağlantısı | | Pakette karşılığı yok. |
| Yeniden başlatmada eski motor maçlarının hamle günlüğünden geri kurulması | `MatchManager.StartAsync` | v4 masaları geri kurulmaz: açılışta tuttukları bahis geri verilir (`Store.AbortOpenAsync`, EK-210). |

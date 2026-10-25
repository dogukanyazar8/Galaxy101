# v4 · Hat B matrisi (sunucu)

Paket (`pkg/`) ↔ proje karşılaştırması. "Projede ne vardı" = v4 öncesi taban `753287e`; "v4 sonrası" = bu dalın hali (`dosya:satır`). Mesaj ve hata satırları `docs/v4/PROTOKOL_ESLEME.md` ile aynı kaynaktan üretildi.

Kısaltmalar: `TT` = `Packages/com.galaxy101.table/Runtime/TableTimings.cs`, `TR` = `Packages/com.galaxy101.table/Runtime/TableRuntime.cs`, `TS` = `Packages/com.galaxy101.table/Runtime/TableSettlement.cs`, `HT` = `Server/src/Galaxy101.Server/V4/Hub.Tables.cs`, `HM` = `Server/src/Galaxy101.Server/V4/Hub.Match.cs`, `HR` = `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs`, `HI` = `Server/src/Galaxy101.Server/V4/Hub.Invites.cs`, `ST` = `Server/src/Galaxy101.Server/V4/Store.cs`. Eski dosyalar `Server/src/Galaxy101.Server/` altından kısa yazıldı.

Kanıt: `dotnet test` → 130/130 (26 paket akışı + SRV-02b + 4 çalışma zamanı testi + 99 mevcut); komutlar `docs/v4/hat-b/RAPOR.md`.

## 1 · Protokol mesajları (53 C2S + 45 S2C)

| Kimlik | Pakette ne diyor (kaynak) | Projede ne vardı (dosya:satır) | Durum (var / farklı / eksik / pakette yok) | v4 sonrası |
|---|---|---|---|---|
| C2S `hello` | Yeniden bağlanma. Başarıda session + snapshot. (protocol.json) | yeniden bağlanma = yeni bilet + `GET /v1/matches/current` + ws `match.sync` (`Packages/com.galaxy101.contracts/Runtime/Realtime.cs:20`) | farklı | ws `hello` → `Server/src/Galaxy101.Server/V4/Hub.cs:259` |
| C2S `register` | S04 Kayıt ol. 25.000 çip + 20 elmas. (protocol.json) | HTTP `POST /v1/auth/register` (`Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:33`), hata kodu `validation`/`email_taken` | farklı | HTTP `POST /v1/auth/register` `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:33` |
| C2S `login` | S03 Giriş yap. E_PASS_WRONG.detail.left = kalan hak; 3. yanlışta 5 dk kilit, E_ACCOUNT_LOCKED.detail.until. (protocol.json) | HTTP `POST /v1/auth/login` (`AuthEndpoints.cs:42`), `wrong_password` + `remainingAttempts`, 423 `account_locked` | farklı | HTTP `POST /v1/auth/login` `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:42` |
| C2S `guest` | S05 Misafir olarak oyna. 10.000 çip + 20 elmas. (protocol.json) | HTTP `POST /v1/auth/guest` (`AuthEndpoints.cs:22`) | var | HTTP `POST /v1/auth/guest` `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:22` |
| C2S `passwordResetRequest` | Şifremi unuttum: kod e-postayla gönderilir. Hesabın var olup olmadığı yanıtta belli edilmez. (protocol.json) | HTTP `POST /v1/auth/password/forgot` (`Auth/PasswordReset.cs:93`) | var | HTTP `POST /v1/auth/password/forgot` `Server/src/Galaxy101.Server/Auth/PasswordReset.cs:93` |
| C2S `passwordReset` | Yeni şifre; başarıda giriş kilidi kalkar (Q-14). (protocol.json) | HTTP `POST /v1/auth/password/reset` (`Auth/PasswordReset.cs:118`) | farklı | HTTP `POST /v1/auth/password/reset` `Server/src/Galaxy101.Server/Auth/PasswordReset.cs:118` |
| C2S `oauth` | S02 Google ile devam et. Yeni hesap 25.000 çip + 20 elmas. (protocol.json) | HTTP `POST /v1/auth/external` (`AuthEndpoints.cs:52`) | farklı | HTTP `POST /v1/auth/external` `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:57` |
| C2S `logout` | Çıkış yap. (protocol.json) | HTTP `POST /v1/auth/logout` (`AuthEndpoints.cs:93`) | farklı | HTTP `POST /v1/auth/logout` `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:98` |
| C2S `accountDelete` | Hesabı sil (SİL / DELETE). (protocol.json) | HTTP `DELETE /v1/account` (`Profile/ProfileEndpoints.cs:112`) | farklı | HTTP `DELETE /v1/account` `Server/src/Galaxy101.Server/Profile/ProfileEndpoints.cs:115` |
| C2S `resync` | Tam durum anlık görüntüsü ister → snapshot. (protocol.json) | ws `match.sync` (`Packages/com.galaxy101.contracts/Runtime/Realtime.cs:20`) yalnız maç | farklı | ws `resync` → `Server/src/Galaxy101.Server/V4/Hub.cs:270` |
| C2S `ping` | Saat eşitleme. (protocol.json) | ws `ping` → `pong` (`Packages/com.galaxy101.contracts/Runtime/Realtime.cs:13`) | var | ws `ping` `Server/src/Galaxy101.Server/Realtime/Realtime.cs:124` |
| C2S `profileUpdate` | Profili düzenle. (protocol.json) | HTTP `PUT /v1/profile` (`ProfileEndpoints.cs:26`), `validation` | farklı | HTTP `PUT /v1/profile` `Server/src/Galaxy101.Server/Profile/ProfileEndpoints.cs:26` |
| C2S `shopBuy` | Mağaza satın alma. SKU listesi mockServer ECON.shop. (protocol.json) | HTTP `POST /v1/shop/purchase` / `store-purchase` (`Economy/ShopEndpoints.cs:31`, `:57`) | farklı | HTTP `POST /v1/shop/purchase` (elmasla) `Server/src/Galaxy101.Server/Economy/ShopEndpoints.cs:31` · `POST /v1/shop/store-purchase` (gerçek para) `Server/src/Galaxy101.Server/Economy/ShopEndpoints.cs:57` |
| C2S `skinSet` | Taş görünümü seç. (protocol.json) | yok | eksik | — |
| C2S `dailyGet` | → daily. (protocol.json) | HTTP `GET /v1/daily` (`Economy/Daily.cs:121`) | farklı | HTTP `GET /v1/daily` `Server/src/Galaxy101.Server/Economy/Daily.cs:121` |
| C2S `dailyClaim` | Günlük ödülü al. (protocol.json) | HTTP `POST /v1/daily/claim` (`Daily.cs:124`) | farklı | HTTP `POST /v1/daily/claim` `Server/src/Galaxy101.Server/Economy/Daily.cs:124` |
| C2S `questClaim` | Görev ödülünü al. (protocol.json) | HTTP `POST /v1/daily/quests/{id}/claim` (`Daily.cs:127`) | farklı | HTTP `POST /v1/daily/quests/{id}/claim` `Server/src/Galaxy101.Server/Economy/Daily.cs:127` |
| C2S `friendsGet` | → friends. (protocol.json) | HTTP `GET /v1/friends` (`Social/FriendsEndpoints.cs:17`) | farklı | HTTP `GET /v1/friends` `Server/src/Galaxy101.Server/Social/FriendsEndpoints.cs:17` |
| C2S `friendFind` | Arkadaş ekle araması (kod AB1234 → AB-1234, sonra ad içerir). (protocol.json) | HTTP `GET /v1/players/search` (`FriendsEndpoints.cs:109`) | farklı | HTTP `GET /v1/players/search?q=` `Server/src/Galaxy101.Server/Social/FriendsEndpoints.cs:109` |
| C2S `friendRequest` | İstek gönder. (protocol.json) | HTTP `POST /v1/friends/requests {code}` (`FriendsEndpoints.cs:39`) | farklı | HTTP `POST /v1/friends/requests` `Server/src/Galaxy101.Server/Social/FriendsEndpoints.cs:39` |
| C2S `friendRespond` | İsteği kabul/ret. (protocol.json) | HTTP `…/accept` · `…/decline` (`FriendsEndpoints.cs:74`, `:94`) | farklı | HTTP `POST /v1/friends/requests/{id}/accept` · `/decline` `Server/src/Galaxy101.Server/Social/FriendsEndpoints.cs:74` |
| C2S `friendRemove` | Arkadaşlıktan çıkar. (protocol.json) | HTTP `DELETE /v1/friends/{id}` (`FriendsEndpoints.cs:101`) | var | HTTP `DELETE /v1/friends/{id}` `Server/src/Galaxy101.Server/Social/FriendsEndpoints.cs:101` |
| C2S `leaderboardGet` | → leaderboard. weekly: 30/15/5/1 puan (özel oda ve botlara karşı hariç), pazartesi 00:00 İstanbul sıfırlanır · season: dereceli LP · friends: arkadaşlar arasında sezon LP. (protocol.json) | HTTP `GET /v1/leaderboard/{scope}` (`Social/LeaderboardEndpoints.cs:20`) | farklı | HTTP `GET /v1/leaderboard/{weekly|season|friends}` `Server/src/Galaxy101.Server/Social/LeaderboardEndpoints.cs:20` |
| C2S `lobbySubscribe` | Masa listesi + canlı güncellemeler (lobbyTable, lobbyTableRemoved). (protocol.json) | ws `lobby.subscribe` (`Packages/com.galaxy101.contracts/Runtime/Lobby.cs:10`) | farklı | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:153` |
| C2S `lobbyUnsubscribe` | Canlı güncellemeyi bırak. (protocol.json) | ws `lobby.unsubscribe` (`Packages/com.galaxy101.contracts/Runtime/Lobby.cs:11`) | farklı | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:160` |
| C2S `tableCreate` | S10 Masa aç. Bahis oturunca düşülür (R-90). Katlamalı (R-64): yalnız açılış eşiği yükselir. (protocol.json) | ws `table.create {options}` (`Packages/com.galaxy101.contracts/Runtime/Lobby.cs:12`); bahis maç başında (`Games/MatchStore.cs:21`) | farklı | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:171` |
| C2S `tableJoin` | Otur. Şifreli masada pin zorunlu (S11). Masa oynanıyorsa "Oyuna katıl" (R-65): botun ya da kalkan oyuncunun koltuğu devralınır, bahis o an ödenir, koltuğun toplamıyla devam edilir; ardından snapshot{screen:"table"}. (protocol.json) | ws `table.join {tableId, pin, seat}` (`Packages/com.galaxy101.contracts/Runtime/Lobby.cs:13`), oynanan masada devralma (`Games/MatchSession.cs:216`) | farklı | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:241` |
| C2S `tableJoinCode` | S12 Kodla katıl (GX-XXXX). (protocol.json) | ws `table.join {code}` (`Packages/com.galaxy101.contracts/Runtime/Lobby.cs:13`) | farklı | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:251` |
| C2S `tableSpectate` | İzle → snapshot{spectator:true}. (protocol.json) | ws `table.watch` (`Packages/com.galaxy101.contracts/Runtime/Lobby.cs:15`) | farklı | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:310` |
| C2S `tableLeave` | Bekleme odasında: bahis iade. Maçta: R-60 (bahis kaybı, son sıra, yerine bot) → leftTable. (protocol.json) | ws `table.leave` / `match.leave` (`Packages/com.galaxy101.contracts/Runtime/Lobby.cs:16`, `Packages/com.galaxy101.contracts/Runtime/Realtime.cs:21`) | farklı | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:327` |
| C2S `seatAddBot` | Sahip: boş koltuğa bot. (protocol.json) | ws `table.addBot` (`Packages/com.galaxy101.contracts/Runtime/Lobby.cs:17`) | farklı | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:411` |
| C2S `seatKick` | Sahip: oyuncuyu/botu çıkar (bahis iade). (protocol.json) | ws `table.kick` (`Packages/com.galaxy101.contracts/Runtime/Lobby.cs:18`) | farklı | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:423` |
| C2S `tableStart` | Sahip: Başlat → countdown 3-2-1. (protocol.json) | ws `table.start` (`Packages/com.galaxy101.contracts/Runtime/Lobby.cs:19`), dolu masa 5 sn kendiliğinden (`Games/Tables.cs:22`) | farklı | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:441` |
| C2S `inviteSend` | S14/S15 Davet. 30 sn geçerli. (protocol.json) | HTTP `POST /v1/friends/{id}/invite {roomCode}` (`Games/GamesEndpoints.cs:21`), süresiz | farklı | ws → `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:24` |
| C2S `inviteRespond` | Gelen daveti yanıtla. (protocol.json) | yok (davet edilen kodla katılır) | eksik | ws → `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:52` |
| C2S `inviteCancel` | Gönderilen daveti geri çek. (protocol.json) | yok | eksik | ws → `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:78` |
| C2S `vsBotsStart` | Botlara karşı: 3 bot, çip yok, doğrudan matchStart. (protocol.json) | yok (botlara karşı istemcide çevrimdışı) | eksik | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:490` |
| C2S `rankedQueue` | Arama başlat. Duo: party 1 kişi, Squad: 3 kişi. (protocol.json) | ws `queue.join {mode:ranked}` (`Packages/com.galaxy101.contracts/Runtime/Realtime.cs:14`), yalnız tekli | farklı | ws → `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:60` |
| C2S `rankedAccept` | Maç bulundu: kabul / reddet (10 sn). (protocol.json) | yok (bulunca hemen maç) | eksik | ws → `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:150` |
| C2S `rankedCancel` | Aramayı iptal et. (protocol.json) | ws `queue.leave` (`Packages/com.galaxy101.contracts/Runtime/Realtime.cs:15`) | farklı | ws → `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:168` |
| C2S `gRollDice` | Dağıtan insan oyuncu zarı atar (her zar ayrı). 15 sn içinde atmazsa sunucu atar. (protocol.json) | yok (zar istemcide, sunucu 22 sn bekliyordu) | eksik | ws → `TableRuntime.RollDice` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:130` |
| C2S `gDraw` | Desteden çek / soldan al. (protocol.json) | ws `match.command {kind:draw}` (`Packages/com.galaxy101.contracts/Runtime/Match.cs:11`) | farklı | ws → `TableRuntime.Move` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:308` |
| C2S `gReturnSide` | Soldan alınan taşı geri bırak (R-32). (protocol.json) | ws `match.command {kind:return}` (`Packages/com.galaxy101.contracts/Runtime/Match.cs:11`) | farklı | ws → `TableRuntime.Move` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:308` |
| C2S `gOpen` | Elini aç (101 / 5 çift). (protocol.json) | tur-bütün `match.command {kind:commit, plan}` içinde (`Packages/com.galaxy101.contracts/Runtime/Match.cs:11`) | farklı | ws → `TableRuntime.Move` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:308` |
| C2S `gLay` | Açmış oyuncu ek per yere açar. (protocol.json) | `commit` planı içinde (`Packages/com.galaxy101.contracts/Runtime/Match.cs:11`) | farklı | ws → `TableRuntime.Move` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:308` |
| C2S `gAdd` | İşle. (protocol.json) | `commit` planı içinde (`Packages/com.galaxy101.contracts/Runtime/Match.cs:11`) | farklı | ws → `TableRuntime.Move` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:308` |
| C2S `gSwap` | Masadaki okeyi al (R-26). (protocol.json) | `commit` planı içinde (`Packages/com.galaxy101.contracts/Runtime/Match.cs:11`) | farklı | ws → `TableRuntime.Move` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:308` |
| C2S `gUndo` | Geri topla (R-34). (protocol.json) | yok (plan istemcide kurulup tek seferde gönderiliyordu) | eksik | ws → `TableRuntime.Move` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:308` |
| C2S `gDiscard` | Taş at / bitir. (protocol.json) | `commit` planının atışı (`Packages/com.galaxy101.contracts/Runtime/Match.cs:11`) | farklı | ws → `TableRuntime.Move` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:308` |
| C2S `gReady` | "Sonraki el" düğmesi; tüm insanlar hazırsa 15 sn beklenmez. (protocol.json) | yok (15 sn hep beklenir) | eksik | ws → `TableRuntime.ReadyUp` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:350` |
| C2S `gRackOrder` | Istaka dizilimi (yalnız doğrulama; dizilim istemcidedir). (protocol.json) | yok | eksik | ws → `TableRuntime.RackOrder` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:364` |
| C2S `chatSend` | Sohbet: serbest yazı (≤120) ya da hazır mesaj/emoji anahtarı. (protocol.json) | ws `match.chat` (`Packages/com.galaxy101.contracts/Runtime/Realtime.cs:25`): 80 karakter, hazır cümle sırası 0–5 (`Games/MatchSession.cs:288`) | farklı | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:511` |
| C2S `giftSend` | Hediye ısmarla. "all" = fiyat × alıcı sayısı. (protocol.json) | ws `match.gift` (`Packages/com.galaxy101.contracts/Runtime/Realtime.cs:23`), masaya = 3 × fiyat (`MatchSession.cs:305`) | farklı | ws → `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:532` |
| S2C `ack` | Her C2S için tam bir kez. (protocol.json) | `ack {id, ok, code}` yalnız `id` varsa (`Packages/com.galaxy101.contracts/Runtime/Realtime.cs:57`) | farklı | ws `ack` `Server/src/Galaxy101.Server/Realtime/Realtime.cs:177` |
| S2C `session` | Giriş başarılı. (protocol.json) | HTTP `TokenResponse` (giriş HTTP'de) | farklı | ws `session` `Server/src/Galaxy101.Server/V4/Hub.cs:264` |
| S2C `snapshot` | Tam durum. Kopma sonrası dönülecek ekran. (protocol.json) | `match.start` + `match.state` (`Packages/com.galaxy101.contracts/Runtime/Match.cs:68`, `:188`) yalnız maç | farklı | ws `snapshot` `Server/src/Galaxy101.Server/V4/Hub.cs:284` |
| S2C `profile` | Profil değişti. (protocol.json) | HTTP `GET /v1/profile` | farklı | HTTP `GET /v1/profile` yanıtı |
| S2C `balance` | Her bakiye hareketi. İstemci sayaç animasyonunu bundan oynatır. (protocol.json) | ws `wallet` (`Packages/com.galaxy101.contracts/Runtime/Realtime.cs:34`): tutar, neden yok | farklı | ws `balance` `Server/src/Galaxy101.Server/V4/Hub.cs:207` |
| S2C `xp` | XP kazanımı: her el sonunda (reason:"hand") ve maç sonunda sıra bonusu (reason:"match"). (protocol.json) | maç sonunda `match.end` yerleşim XP'si (`Packages/com.galaxy101.contracts/Runtime/Match.cs:220`) | farklı | ws `xp` `Server/src/Galaxy101.Server/V4/Hub.Match.cs:85` |
| S2C `daily` | Günlük ödül + görevler. Gün değişimi Europe/Istanbul. Bir gün atlanırsa seri 1. güne döner; 7. günden sonra baştan. (protocol.json) | HTTP `GET /v1/daily` | farklı | HTTP `GET /v1/daily` yanıtı |
| S2C `presence` | Çevrimiçi/oyunda değişimi. (protocol.json) | ws `presence {accountId, presence}` (`Packages/com.galaxy101.contracts/Runtime/Realtime.cs:30`) | farklı | ws `presence` `Packages/com.galaxy101.contracts/Runtime/Realtime.cs:30` |
| S2C `friends` | Liste. (protocol.json) | HTTP `GET /v1/friends` | farklı | HTTP `GET /v1/friends` yanıtı |
| S2C `friendRequestIn` | Yeni istek. (protocol.json) | ws `friend.request` (`Packages/com.galaxy101.contracts/Runtime/Realtime.cs:31`) | farklı | ws `friend.request` `Packages/com.galaxy101.contracts/Runtime/Realtime.cs:31` |
| S2C `friendRequestResult` | İstek sonucu. (protocol.json) | ws `friend.accepted` (`Packages/com.galaxy101.contracts/Runtime/Realtime.cs:32`) | farklı | ws `friend.accepted` `Packages/com.galaxy101.contracts/Runtime/Realtime.cs:32` |
| S2C `friendRemoved` | Arkadaşlık bitti. (protocol.json) | yok | eksik | — |
| S2C `leaderboard` | Liderlik. (protocol.json) | HTTP | farklı | HTTP `GET /v1/leaderboard/{scope}` yanıtı |
| S2C `lobby` | Tam liste. (protocol.json) | ws `lobby.tables` (`Packages/com.galaxy101.contracts/Runtime/Lobby.cs:21`) | farklı | ws `lobby` `Server/src/Galaxy101.Server/V4/Hub.cs:29` |
| S2C `lobbyTable` | Masa eklendi/değişti. (protocol.json) | tam liste yeniden (`Packages/com.galaxy101.contracts/Runtime/Lobby.cs:21`) | farklı | ws `lobbyTable` `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:150` |
| S2C `lobbyTableRemoved` | Masa kapandı/başladı listeden düştü. (protocol.json) | tam liste yeniden (`Packages/com.galaxy101.contracts/Runtime/Lobby.cs:21`) | farklı | ws `lobbyTableRemoved` `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:150` |
| S2C `tableState` | Koltuk/durum değişti. Table.katlama, Table.katlamaPartner (eşli+katlamalı değilse null) lobi kartı ve masa bilgisinde gösterilir. (protocol.json) | ws `table.state` (`Packages/com.galaxy101.contracts/Runtime/Lobby.cs:22`) | farklı | ws `tableState` `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:144` |
| S2C `tableClosed` | Masa kapandı → Masa kapandı diyaloğu. (protocol.json) | `table.state {closed, reason}` (`Packages/com.galaxy101.contracts/Runtime/Lobby.cs:135`) | farklı | ws `tableClosed` `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:389` |
| S2C `kicked` | Sahip seni çıkardı. (protocol.json) | `table.state {reason:kicked}` (`Packages/com.galaxy101.contracts/Runtime/Lobby.cs:135`) | farklı | ws `kicked` `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:433` |
| S2C `countdown` | Bekleme odası 3-2-1. (protocol.json) | `table.state.countdown` saniye (`Packages/com.galaxy101.contracts/Runtime/Lobby.cs:142`) | farklı | ws `countdown` `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:463` |
| S2C `seatConnection` | Kopma/dönüş. autoplay:true = üst üste 2 zaman aşımından sonra koltuğu sunucu oynuyor (koltukta "Otomatik" rozeti). (protocol.json) | `match.state.seats[].status` (`Packages/com.galaxy101.contracts/Runtime/Match.cs:188`) | farklı | ws `seatConnection` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:276` |
| S2C `leftTable` | "Masadan kalktın" tostu → 1300 ms sonra lobi. (protocol.json) | `match.leave` ack | farklı | ws `leftTable` `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:356` |
| S2C `inviteIn` | Gelen davet. (protocol.json) | ws `invite {from, roomCode, stake}` (`Packages/com.galaxy101.contracts/Runtime/Realtime.cs:33`) | farklı | ws `inviteIn` `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:48` |
| S2C `inviteExpired` | Davet süresi doldu/geri çekildi. (protocol.json) | yok | eksik | ws `inviteExpired` `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:46` |
| S2C `inviteResult` | Davet sonucu. (protocol.json) | yok | eksik | ws `inviteResult` `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:45` |
| S2C `rankedSearch` | Arama durumu (koltuklar tek tek dolar). (protocol.json) | ws `queue.status` (`Packages/com.galaxy101.contracts/Runtime/Realtime.cs:35`) | farklı | ws `rankedSearch` `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:57` |
| S2C `rankedFound` | Maç bulundu, 10 sn kabul. (protocol.json) | yok | eksik | ws `rankedFound` `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:110` |
| S2C `rankedAccepted` | Bir oyuncu kabul etti. (protocol.json) | yok | eksik | ws `rankedAccepted` `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:163` |
| S2C `rankedCancelled` | Arama/kabul bitti. (protocol.json) | yok | eksik | ws `rankedCancelled` `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:140` |
| S2C `rankedReady` | "Masa hazırlanıyor". (protocol.json) | yok | eksik | ws `rankedReady` `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:196` |
| S2C `rankChange` | Maç sonu LP. leagueUp → lig atlama ekranı, tierUp → kademe atlama, down → düşme tostu. (protocol.json) | `match.end` yerleşim `lpDelta` (`Packages/com.galaxy101.contracts/Runtime/Match.cs:215`) | farklı | ws `rankChange` `Server/src/Galaxy101.Server/V4/Hub.Match.cs:157` |
| S2C `matchStart` | Maç başlıyor; 3-2-1 sonra dağıtım. (protocol.json) | ws `match.start` (`Packages/com.galaxy101.contracts/Runtime/Realtime.cs:37`) | farklı | ws `matchStart` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:101` |
| S2C `dealStart` | Tören başladı: balyalar diziliyor. (protocol.json) | yok (tören istemcide, sunucu `Game:DealSeconds` 22 sn bekler) | eksik | ws `dealStart` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:116` |
| S2C `dealStep` | Tören adımı. indicator yalnız indicator adımında açıklanır. (protocol.json) | yok | eksik | ws `dealStep` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:124` |
| S2C `dealDie` | Zar atıldı; değer bu anda açıklanır. (protocol.json) | yok | eksik | ws `dealDie` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:148` |
| S2C `handDealt` | Taşlar ıstakaya; yalnız kendi ıstakan (izleyiciye null). (protocol.json) | `match.state.view.hand` (`Packages/com.galaxy101.contracts/Runtime/Match.cs:188`) | farklı | ws `handDealt` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:195` |
| S2C `turn` | Sıra başladı; tur sayacı deadline'a göre. (protocol.json) | `match.state.turnDeadline` (`Packages/com.galaxy101.contracts/Runtime/Match.cs:194`) | farklı | ws `turn` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:220` |
| S2C `legal` | Düğme etkinlikleri. (protocol.json) | yok (istemci motoru hesaplar) | eksik | ws `legal` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:227` |
| S2C `game` | Kural motoru olayı: drew, sideReturned, openShort, opened, laid, added, okeySwapped, undone, discarded, finished, penalty, auto, draw, seatLeft, handEnd, matchEnd, turn. Alanlar logic/rules.js ev.push satırları. (protocol.json) | `match.state.lastEvent` (`Packages/com.galaxy101.contracts/Runtime/Match.cs:193`) | farklı | ws `game` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:472` |
| S2C `handWait` | El sonu: "Sonraki el · 15". (protocol.json) | ws `match.handEnd.nextHandAt` (`Packages/com.galaxy101.contracts/Runtime/Match.cs:206`) | farklı | ws `handWait` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:345` |
| S2C `readyState` | Hazır olan koltuklar. (protocol.json) | yok | eksik | ws `readyState` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:358` |
| S2C `matchResult` | Maç sonu sıralama ve ödeme (R-59, R-91). (protocol.json) | ws `match.end` (`Packages/com.galaxy101.contracts/Runtime/Match.cs:220`) | farklı | ws `matchResult` `Server/src/Galaxy101.Server/V4/Hub.Match.cs:127` |
| S2C `chat` | Sohbet satırı. quick = strings.csv anahtarı. (protocol.json) | ws `match.chat` (`Packages/com.galaxy101.contracts/Runtime/Realtime.cs:25`) | farklı | ws `chat` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:261` |
| S2C `gift` | Hediye uçuşu. (protocol.json) | ws `match.gift` (`Packages/com.galaxy101.contracts/Runtime/Realtime.cs:23`) | farklı | ws `gift` `Server/src/Galaxy101.Server/V4/Hub.cs:34` |
| S2C `tableView` |  (protocol.json) | ws `match.state` (`Packages/com.galaxy101.contracts/Runtime/Match.cs:188`) | farklı | ws `tableView` `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:481` |

## 2 · Hata kodları (94)

| Kimlik | Pakette ne diyor (kaynak) | Projede ne vardı | Durum | v4 sonrası (dosya:satır) |
|---|---|---|---|---|
| `E_BAD_MESSAGE` | ui silent · İstemci hatası; kayda yaz (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:414` |
| `E_UNKNOWN_MESSAGE` | ui silent · Sürüm uyuşmazlığı; kayda yaz (protocol.json errors) | `not_found` | farklı | — |
| `E_AUTH_REQUIRED` | ui screen · Giriş seçimine dön (protocol.json errors) | `401` | farklı | — |
| `E_SESSION_EXPIRED` | ui screen · — (protocol.json errors) | `invalid_token` | farklı | `Server/src/Galaxy101.Server/V4/Hub.cs:261` |
| `E_EMAIL_INVALID` | ui inline · alan altı kırmızı (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/Auth/Passwords.cs:54` |
| `E_PASS_SHORT` | ui inline · — (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/Auth/PasswordReset.cs:121` |
| `E_NAME_INVALID` | ui inline · — (protocol.json errors) | `validation / inappropriate` | farklı | `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:25` |
| `E_EMAIL_EXISTS` | ui inline · — (protocol.json errors) | `email_taken` | farklı | `Server/src/Galaxy101.Server/Auth/Passwords.cs:60` |
| `E_NAME_TAKEN` | ui inline · — (protocol.json errors) | yok (genel `validation`) | eksik | — |
| `E_ACCOUNT_UNKNOWN` | ui inline · — (protocol.json errors) | `account_not_found` | farklı | `Server/src/Galaxy101.Server/Auth/Passwords.cs:74` |
| `E_PASS_WRONG` | ui inline · "N deneme hakkın kaldı" (detail.left) (protocol.json errors) | `wrong_password + remainingAttempts` | farklı | `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:48` |
| `E_ACCOUNT_LOCKED` | ui inline · "5 dakika sonra tekrar dene" (detail.until), "Şifremi unuttum" bağlantısı vurgulu (protocol.json errors) | `account_locked + retryAfter` | farklı | `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:49` |
| `E_CONFIRM_MISMATCH` | ui inline · Sil düğmesi pasif kalır (protocol.json errors) | yok (genel `validation`) | eksik | — |
| `E_IN_GAME` | ui toast · — (protocol.json errors) | yok (genel `validation`) | eksik | — |
| `E_STATUS_LONG` | ui inline · — (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/Profile/ProfileEndpoints.cs:30` |
| `E_FRAME_LOCKED` | ui toast · Galaksi çerçevesi seviye 30 (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/Profile/ProfileEndpoints.cs:30` |
| `E_SKU_UNKNOWN` | ui toast · — (protocol.json errors) | `not_found` | farklı | `Server/src/Galaxy101.Server/Economy/ShopEndpoints.cs:34` |
| `E_ALREADY_OWNED` | ui toast · — (protocol.json errors) | `already_owned` | farklı | `Server/src/Galaxy101.Server/Economy/ShopEndpoints.cs:36` |
| `E_NO_GEMS` | ui toast · eksik miktar detail.missing (protocol.json errors) | `insufficient_funds` | farklı | `Server/src/Galaxy101.Server/Economy/ShopEndpoints.cs:41` |
| `E_NOT_OWNED` | ui toast · — (protocol.json errors) | yok (genel `validation`) | eksik | — |
| `E_DAILY_CLAIMED` | ui silent · düğme zaten pasif (protocol.json errors) | `already_claimed` | farklı | `Server/src/Galaxy101.Server/Economy/Daily.cs:135` |
| `E_QUEST_UNKNOWN` | ui silent · — (protocol.json errors) | `not_found` | farklı | `Server/src/Galaxy101.Server/Economy/Daily.cs:134` |
| `E_QUEST_INCOMPLETE` | ui silent · — (protocol.json errors) | `not_complete` | farklı | `Server/src/Galaxy101.Server/Economy/Daily.cs:136` |
| `E_QUEST_CLAIMED` | ui silent · — (protocol.json errors) | `already_claimed` | farklı | `Server/src/Galaxy101.Server/Economy/Daily.cs:135` |
| `E_USER_UNKNOWN` | ui inline · "bulunamadı" (protocol.json errors) | `not_found` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:28` |
| `E_ALREADY_FRIENDS` | ui inline · "zaten arkadaşsınız" (protocol.json errors) | `already_friends` | farklı | `Server/src/Galaxy101.Server/Social/FriendsEndpoints.cs:52` |
| `E_REQUEST_PENDING` | ui inline · — (protocol.json errors) | yok (genel `validation`) | eksik | — |
| `E_REQUEST_UNKNOWN` | ui silent · — (protocol.json errors) | `not_found` | farklı | `Server/src/Galaxy101.Server/Social/FriendsEndpoints.cs:87` |
| `E_NOT_FRIENDS` | ui silent · — (protocol.json errors) | `not_found` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:29` |
| `E_ALREADY_SEATED` | ui toast · — (protocol.json errors) | `conflict` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:173` |
| `E_TABLE_NAME` | ui inline · Oluştur pasif (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:175` |
| `E_PIN_FORMAT` | ui inline · — (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:176` |
| `E_TABLE_OPTIONS` | ui silent · — (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:180` |
| `E_NO_CHIPS` | ui toast · "Yetersiz bakiye · X çip eksik" (detail.missing) (protocol.json errors) | `insufficient_funds` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:182` |
| `E_TABLE_CLOSED` | ui dialog · — (protocol.json errors) | `table_closed` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:229` |
| `E_PIN_WRONG` | ui shake · PIN kutuları sallanır + hata (protocol.json errors) | `wrong_pin` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:231` |
| `E_TABLE_FULL` | ui dialog · — (protocol.json errors) | `table_full` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:233` |
| `E_TABLE_PLAYING` | ui toast · dereceli / botlara karşı masaya katılım (protocol.json errors) | `table_playing` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:269` |
| `E_CODE_FORMAT` | ui inline · — (protocol.json errors) | `table_closed` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:254` |
| `E_CODE_UNKNOWN` | ui inline · — (protocol.json errors) | `table_closed` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:256` |
| `E_SPECTATE_OFF` | ui toast · — (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:314` |
| `E_NOT_SEATED` | ui silent · — (protocol.json errors) | `not_found` | farklı | `Server/src/Galaxy101.Server/V4/Hub.cs:312` |
| `E_NOT_OWNER` | ui silent · — (protocol.json errors) | `not_host` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:406` |
| `E_STATUS` | ui silent · — (protocol.json errors) | `validation / rule.wrongPhase` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:407` · motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:281` |
| `E_SEAT_TAKEN` | ui toast · — (protocol.json errors) | `table_full` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:415` |
| `E_SEAT_EMPTY` | ui silent · — (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:426` |
| `E_TABLE_NOT_FULL` | ui silent · Başlat pasif (protocol.json errors) | `not_ready` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:444` |
| `E_USER_OFFLINE` | ui silent · satır düğmesi pasif (protocol.json errors) | yok (genel `validation`) | eksik | `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:30` |
| `E_USER_BUSY` | ui silent · — (protocol.json errors) | yok (genel `validation`) | eksik | `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:32` |
| `E_INVITE_EXPIRED` | ui toast · — (protocol.json errors) | yok (genel `validation`) | eksik | `Server/src/Galaxy101.Server/V4/Hub.Invites.cs:54` |
| `E_ALREADY_QUEUED` | ui silent · — (protocol.json errors) | `conflict` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:62` |
| `E_MODE` | ui silent · — (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:64` |
| `E_PARTY_SIZE` | ui silent · HEMEN OYNA pasif (protocol.json errors) | yok (genel `validation`) | eksik | `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:69` |
| `E_PARTY_RANGE` | ui toast · ±1 lig dışı (protocol.json errors) | yok (genel `validation`) | eksik | `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:74` |
| `E_MATCH_EXPIRED` | ui toast · — (protocol.json errors) | yok (genel `validation`) | eksik | `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:152` |
| `E_NOT_QUEUED` | ui silent · — (protocol.json errors) | `(yok sayılır)` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Ranked.cs:170` |
| `E_SPECTATOR` | ui silent · — (protocol.json errors) | yok (genel `validation`) | eksik | `Server/src/Galaxy101.Server/V4/Hub.cs:313` |
| `E_DEALING` | ui silent · — (protocol.json errors) | yok (genel `validation`) | eksik | `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:312` |
| `E_NOT_DEALER` | ui silent · — (protocol.json errors) | yok (genel `validation`) | eksik | `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:134` |
| `E_RACK_MISMATCH` | ui resync · resync gönder (protocol.json errors) | yok (genel `validation`) | eksik | `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:366` |
| `E_CHAT_OFF` | ui silent · sohbet paneli gizli (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:514` |
| `E_CHAT_QUICK_ONLY` | ui silent · yazı alanı gizli (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:515` |
| `E_CHAT_LENGTH` | ui inline · — (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:516` |
| `E_CHAT_QUICK_UNKNOWN` | ui silent · — (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:517` |
| `E_GIFTS_OFF` | ui silent · hediye düğmesi gizli (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:535` |
| `E_GIFT_UNKNOWN` | ui silent · — (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:538` |
| `E_GIFT_TARGET` | ui silent · — (protocol.json errors) | `validation` | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:543` |
| `E_NOT_YOUR_TURN` | ui silent · taş ıstakaya döner (protocol.json errors) | `rule.wrongPlayer` | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:500` |
| `E_PHASE` | ui silent · taş ıstakaya döner (protocol.json errors) | `rule.wrongPhase` | farklı | `Packages/com.galaxy101.table/Runtime/TableRuntime.cs:133` · motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:522` |
| `E_HAND_OVER` | ui silent · — (protocol.json errors) | `rule.wrongPhase` | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:497` |
| `E_TILE_NOT_IN_RACK` | ui resync · — (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:585` |
| `E_SIDE_EMPTY` | ui silent · — (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:529` |
| `E_SIDE_RETURNED` | ui toast · geri bırakılan taş tekrar alınamaz (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:527` |
| `E_SIDE_DISCARD` | ui toast · soldan alınan taş atılamaz (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:759` |
| `E_SIDE_MUST_OPEN` | ui toast · önce aç ya da geri bırak (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:646` |
| `E_RETURN_INVALID` | ui silent · — (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:552` |
| `E_DECK_EMPTY` | ui silent · — (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:536` |
| `E_MELD_INVALID` | ui toast · geçersiz per kırmızı çerçeve (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:572` |
| `E_MELD_MIX` | ui toast · seri/grup ile çift karışamaz (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:594` |
| `E_MELD_NOT_FOUND` | ui resync · — (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:700` |
| `E_ALREADY_OPENED` | ui silent · — (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:643` |
| `E_NOT_OPENED` | ui toast · önce elini açmalısın (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:678` |
| `E_OPEN_MODE` | ui toast · çift açan seri/grup açamaz (seri açan çift açabilir) (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:681` |
| `E_LAST_TILE` | ui toast · ıstakada atılacak taş kalmalı (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:595` |
| `E_ISLE_INVALID` | ui silent · taş ıstakaya döner (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:703` |
| `E_ISLE_DISABLED` | ui toast · bu masada işleme kapalı (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:695` |
| `E_SWAP_INVALID` | ui toast · okey kırmızı çerçeve, taş döner (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:727` |
| `E_NO_UNDO` | ui silent · — (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:744` |
| `E_LEAVE_INVALID` | ui silent · — (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | `Server/src/Galaxy101.Server/V4/Hub.Tables.cs:334` · motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:818` |
| `E_ACTION` | ui silent · — (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:493` |
| `E_SPEC_DUP` | ui silent · yalnız test (protocol.json errors) | yok | pakette yalnız test | — |
| `E_RESET_CODE` | ui inline · kod yanlış (protocol.json errors) | `code_invalid` | farklı | `Server/src/Galaxy101.Server/Auth/PasswordReset.cs:122` |
| `E_OAUTH_FAILED` | ui toast · — (protocol.json errors) | `invalid_token` | farklı | `Server/src/Galaxy101.Server/Auth/AuthEndpoints.cs:61` |
| `E_SEAT_NOT_BOT` | ui silent · yalnız motor (protocol.json errors) | `rule.<ad>` (eski motorun hata kümesi; birebir değil) | farklı | motor `Packages/com.okey101.core/Runtime/V4/G101Rules.cs:807` |

## 3 · Süreler (`protocol/timings.md`)

| Kimlik | Pakette ne diyor (kaynak) | Projede ne vardı (dosya:satır) | Durum | v4 sonrası |
|---|---|---|---|---|
| `preDealCountdown` | 3000 ms, `matchStart` → ilk `dealStart` (timings §1) | yok: maç başlar başlamaz dağıtılırdı (`Games/MatchSession.cs:117`) | eksik | `TT:10`, `TR` `Start` → `matchStart{countdownMs:3000}` (SRV-06) |
| `seatedToCountdown` | 900 ms, sahibi bot/demo olan masada 4. koltuk dolunca | dolu masa 5 sn'de kendiliğinden (`Games/Tables.cs:22`) | farklı | `TT:12`, `HT:450`. Gerçek sunucuda sahip hep oturan insan olduğu için tetiklenmez; sahip Başlat'a basar. Eski 5 sn yalnız eski masalarda (pakette yok). |
| `waitRoomCountdown` | 3000 ms (3 × 1000), `tableStart` → `matchStart` | sahip Başlat 3 sn (`Games/Tables.cs:22`), olay `table.state.countdown` | farklı | `TT:14-15`, `HT:458`: `countdown{n:3,2,1}` saniyede bir (SRV-06) |
| `turnMs` | masa seçeneği 15/20/30/45 sn (vars. 30) | `Game:TurnSeconds` 30, masa seçeneği (`Infrastructure/Options.cs:52`) | var | `TT:35`, `TR` `StartTurn` `turn{turnMs, deadline}` |
| uyarı (`warn`) | yalnız istemci, vars. 5 sn | istemci ayarı | var | sunucu bilmez |
| `handEndWait` | 15000 ms; herkes `gReady` → hemen (D-25) | `Game:NextHandSeconds` 15, gReady yok (`Options.cs:53`) | farklı | `TT:37`, `TR:339` `HandOver`, `TR:350` `ReadyUp` |
| `inviteTtl` | 30000 ms | davet süresiz (`Games/GamesEndpoints.cs:21`) | eksik | `TT:38`, `HI` `InviteSendAsync` (SRV-08) |
| `rankedAccept` | 10000 ms | kabul adımı yok | eksik | `TT:39`, `HR:115` `AcceptTimeout` (SRV-10) |
| `rankedWiden` | 30000 ms, ±1 → ±2 lig | 30 sn, 5 sn'de bir tarama (`Games/Lobbies.cs:35`, `:49`) | var | `TT:40`, `HR:89` `TryMatch` + kuyruğa girişte zamanlayıcı |
| `rankedLoading` | 1800 ms, `rankedReady` → `matchStart` | yok | eksik | `TT:41`, `HR:179` (SRV-09) |
| `leaveToLobby` | 1300 ms, `leftTable.toLobbyIn` | yok | eksik | `TT:42`, `HT:327` (SRV-07) |
| `connect` | 850 ms, yalnız prototip (DEMO-12) | — | pakette yalnız demo | uygulanmadı (gerçek ağ) |
| `autoplayAfterTimeouts` | 2, yalnız bağlantısı kopuk (Q-10, D-35) | `Game:TimeoutsBeforeAuto` 2, bağlı oyuncuya da (`Options.cs:57`, `Games/MatchSession.cs:371`) | farklı | `TT:44`, `TR:266` `Timeout`: yalnız `!Connected` (SRV-16). Eski yol aynı kaldı. |
| `reconnectMatch` | sınırsız | maç bitene dek (`Games/MatchSession.cs:182`) | var | `Hub.HelloAsync` |
| `reconnectWaitRoom` | 0 ms | odadan hemen çıkar, iade yok (bahis maç başında alınıyordu) | farklı | `TT:46`, `HT:361` (SRV-17) |
| `clientRetries` × `clientRetryMs` | 5 × 3000 ms | istemci | — | `TT:47-48` (istemci kullanır; ISTEMCI_NOTU) |
| `loginLock` | 300 000 ms | `Game:PasswordLockSeconds` 300 (`Options.cs:44`) | var | değişmedi (SRV-14, SRV-19) |
| gün değişimi | 00:00 Europe/Istanbul | `GameCalendar` (`Infrastructure/Api.cs`) | var | SRV-22 |
| hafta değişimi | pazartesi 00:00 İstanbul | `GameCalendar.WeeklyBoard` | var | SRV-21 |
| tören: balyalar | `dealStart` 0, 2940 ms | yok: sunucu `Game:DealSeconds` 22 sn beklerdi (`Options.cs:55`, `MatchSession.cs:477`) | eksik | `TT:18` (SRV-02: tablo milisaniyesi milisaniyesine) |
| tören: zar adımı | `dealStep:dice` 2940, bot 700 / insan ≤ 15 000 (D-23) | yok | eksik | `TT:19-20`, `TR` `RollDice` (SRV-02b) |
| tören: 1. zar | `dealDie{0}` 3640, 1570 ms | yok | eksik | `TT:21` |
| tören: zarlar arası | 650 ms (insan ≤ 15 000) | yok | eksik | `TT:23` |
| tören: 2. zar | `dealDie{1}` 5860, 1990 ms | yok | eksik | `TT:22` |
| tören: zar sonrası | 1000 ms | yok | eksik | `TT:24` |
| tören: gösterge | `dealStep:indicator` 8850, 2300 ms | yok | eksik | `TT:25` |
| tören: fazla taş | `dealStep:extra` 11 150, 1300 ms | yok | eksik | `TT:26` |
| tören: dağıtım | `dealStep:distribute` 12 450, 5100 ms | yok | eksik | `TT:27` |
| tören: orta | `dealStep:center` 17 550, 2000 ms | yok | eksik | `TT:28` |
| tören: ıstakaya dizilme | `handDealt` 19 550, 2136 ms | yok | eksik | `TT:29` |
| tören: ilk tur | `turn` 21 686 (D-24) | 22 000 (`Options.cs:55`) | farklı | `TT:32` `DealTotalBotDealer`; eski yol `Game:DealMilliseconds` 21 686 |
| bot: çek | 600 ms (bots.js TIMING, D-20) | `MatchSession.AutoPlayDelay` 1 sn, tek hamlede tur (`MatchSession.cs:25`) | farklı | `G101Bots.Timing.Draw` (Hat A), `TR:241` `BotStep` |
| bot: aç/yere aç/işle/okey al | 430 ms her biri | yok (tur bütün) | eksik | `G101Bots.Timing.Meld` |
| bot: at (per sonrası / doğrudan) | 650 / 730 ms | yok | eksik | `G101Bots.DelayFor` |
| bot: sohbet atış sonrası | %16, `bot.1…6` | yok | eksik | `TR` `BotStep` → `G101Bots.ChatAfterDiscard` |
| bot: oyuncu yazınca | %60, 1400 ms | yok | eksik | `TR:563` `BotChat.Reply` |
| bot: hediyeye teşekkür | 1900 ms (roket 6300) | yok | eksik | `TR:578` `BotChat.Thanks` |

## 4 · Kurallar ve kararlar (sunucu tarafı)

| Kimlik | Pakette ne diyor (kaynak) | Projede ne vardı (dosya:satır) | Durum | v4 sonrası |
|---|---|---|---|---|
| R-59 | N el; eşitlikte sıra paylaşılır, ödül/LP/haftalık/XP bonusu kapladığı sıraların ortalaması (RULES §6) | paylaşım vardı, sıfıra doğru yuvarlama (`Games/Settlement.cs:55-56`) | farklı | `TS:73` `Shared` (JS Math.round), `TS:34` `Compute`; eski yol `Games/Settlement.cs:59-61` |
| R-60 | kalkan bahsini kaybeder, ödül/XP bonusu yok, yerine bot; tekli son, eşlide takım son değil (RULES §7) | `Games/MatchSession.cs:252` `LeaveAsync`, `Settlement.cs:53` | var | `TR:378` `Leave`, `HT:327` (bot kalkanın adıyla, D-31) (SRV-07, SRV-25) |
| R-61 | işleme kapalı → işle/okey al yok | masa seçeneği `processing` | var | motor `opts.isle` ← `rules.isle` (`HM` `StartMatchAsync`) |
| R-62 | ceza kapalı → R-40…R-44 yazılmaz | `penalties` + ayrı `okeyPenalty` | farklı | motor `opts.penalties` ← `rules.penalties`; ayrı okey cezası seçeneği pakette yok |
| R-63 | izleyici seçeneği; sohbet açık/hızlı/kapalı | `spectators`, `chat on/quick/off` (`Games/MatchSession.cs:283`) | farklı | `chat: open/quick/off` `HT:511` (SRV-13) |
| R-64 | katlamalı eşik, eşlide `katlamaPartner` (vars. açık) | `doubling`, `partnerRaises` (`MatchSession.cs:93`) | farklı | `katlama`/`katlamaPartner` masa kartı ve motor (SRV-23) |
| R-65 | oynanan masada botun/kalkanın koltuğu devralınır, bahis o an, toplamla devam; dereceli/botlara karşı yok | `MatchSession.cs:216` `TakeSeatAsync` | var | `HT:266` `TakeOverAsync`, `TR:403` (SRV-18); havuza eklenir (D-34) |
| R-66 | kopma kalkma değil; 2 zaman aşımı → sunucu oynar; bekleme odasında hemen çıkar + iade | maçta var (bağlı oyuncuya da); bekleme odasında iade yok | farklı | `TR:266`, `TR:424`, `HT:361` (SRV-16, SRV-17) |
| R-70 / R-71 | süre dolunca otomatik oyun (motor `timeout`) | `BotPlanner` ile tam tur (`MatchSession.cs:395`) | farklı | motor `GameAction.Timeout` (`TR:266`) |
| R-90 | bahis oturunca düşülür; maç öncesi çıkana iade | maç başında alınır (`Games/MatchStore.cs:21`) | farklı | `HT:191` `SeatAsync`, `HT:217` `RefundAsync` (SRV-04, SRV-17) |
| R-91 | havuz = ödenen bahisler, kesinti 0, beraberlikte eşit bölüşüm, eşlide masadaki ortaklara | pot = insan sayısı × bahis, `RakePercent` 0, bot payı aynı sıradaki insana (`Settlement.cs:32-54`) | farklı | `TS:34-46` (devralmada havuz büyür, kick iadesi havuzdan düşer) (SRV-18, SRV-25) |
| R-92 | seviye n → n+1 = 375·n | `Profile/Progression.cs` | var | `xp.xpNext` (SRV-24) |
| R-93 | el: bitiren +340, diğerleri +60; sıra bonusu 500/200/80/0 (paylaşılır); kalkan bonus almaz | maç sonunda toplu; bonus paylaşılmıyordu (`MatchSession.cs:551`) | farklı | el XP'si anında yazılır + `xp{reason:hand}` (`HM:45`), bonus `xp{reason:match}` (`HM:93`) (SRV-24) |
| R-94 | dereceli: bahis yok, katlamasız, 5 el, 30 sn; LP spec/27 | bahis yok, `Game:HandsPerMatch` 4 el (`Options.cs:51`) | farklı | `TableEconomy.RankedHands` 5 / 30 sn, `HR:179` (SRV-09) |
| R-95 | haftalık 30/15/5/1 (özel ve botlara karşı hariç), pazartesi İstanbul; sezon LP | özel hariç (`Settlement.cs`) | var | `HM:105` (botlara karşı ve özel hariç) (SRV-21) |
| R-96 | misafir 10.000, hesap 25.000, herkese 20 elmas | `Options.cs:40-45` | var | değişmedi (SRV-20) |
| R-97 | günlük seri İstanbul, kaçırılırsa 1. gün, 7'den sonra baştan | `Economy/Daily.cs:56` | var | değişmedi (SRV-12, SRV-22) |
| R-98 | 3 yanlış → 5 dk kilit; sıfırlama kilidi kaldırır | `Auth/Passwords.cs:84`, `Auth/PasswordReset.cs:126` | var | hata gövdesine `E_*` + `detail` (SRV-14, SRV-19) |
| D-11 | maç başlamadan bekleme odasından çıkana bahis iadesi | bahis maç başında alındığı için iade gereksizdi | farklı | `HT:217` (SRV-04) |
| D-12 | eşitlikte havuz eşit bölünür; eşlide takım payı masadaki insanlara | `Settlement.cs:54` | var | `TS:41-46` |
| D-20 | bot zamanlaması 600/430/650/730; sohbet %16/%60 | yok | eksik | Hat A `G101Bots.Timing/Chat`, `TR:241`, `TR:563` |
| D-22 | sohbet seçenekleri etkisi, izleyici | kısmen | farklı | `HT:511` (kapalı: `E_CHAT_OFF`; hızlı: `E_CHAT_QUICK_ONLY`; izleyici `E_SPECTATE_OFF`) |
| D-23 | insan dağıtıcı her zar için ≤ 15 sn; atmazsa sunucu atar | yok | eksik | `TT:20`, `TR` `RollDice` (SRV-02b) |
| D-24 | tören sunucuda adım adım, 21 686 ms | 22 sn bekleme (`Options.cs:55`) | farklı | `TR` `StartHand`/`Roll`/`DealRest` (SRV-02) |
| D-25 | herkes hazırsa 15 sn beklenmez | yok | eksik | `TR:350` `ReadyUp` |
| D-26 | hediye "herkese" = fiyat × alıcı sayısı | 3 × fiyat (`MatchSession.cs:305`) | farklı | `HT:546`; eski yol değişmedi (pakette yok) |
| D-27 | paket bonusu miktara eklenir | bonus yalnız gösterim (`Economy/ShopEndpoints.cs:88`) | farklı | `Economy/Catalog.cs:37` `WithBonus`, `ShopEndpoints.cs:89` (StoreTests 550 → 605) |
| D-28 | (geçersiz → R-98) | — | — | — |
| D-29 | dereceli duo 0 ve 2. koltuk | duo yok | eksik | `HR:89` sıra [0,2,1,3] |
| D-30 | lige çıkışta 3 maç koruma, her maç 1 azalır, korumada LP sınırda kalır | `Profile/Ranked.cs:17`, `:51-56` | var | aynı `Ranked.Apply`; `rankChange.protectedLeft` |
| D-31 | kalkanın yerine geçen bot onun adıyla; ödeme/LP yok | sunucu oynar, ad aynı | var | `HT:327` |
| D-32 | sohbet ≤ 120 karakter | 80 (`MatchSession.cs:289`) | farklı | `TableEconomy.ChatMax` 120, iki yolda |
| D-33 | (geçersiz → R-66) | — | — | — |
| D-34 | devralmada havuz = ödenen bahisler | pot = bahis × insan (`Settlement.cs`) | farklı | `HT:266` `BetsPaid += bet` (SRV-18: havuz 2000) |
| D-35 | sunucu yalnız kopuk oyuncunun koltuğunu oynar | bağlı oyuncu da 2 zaman aşımında otomatiğe geçerdi (`MatchSession.cs:371`) | farklı | `TR:266` (SRV-16). Eski yol değişmedi. |
| D-36 | dönüş (hello) ya da hamle sunucu oynamasını bitirir; sıra ondaysa süre baştan | `SyncAsync`/`SubmitAsync` (`MatchSession.cs:169-192`) | var | `TR:424` `Reconnect`, `TR` `Move` (+ yeni `turn`) |
| D-37 | haftalık puanı yalnız koltuktaki insan alır; devralan sonuna göre | kalkan almaz (`Settlement.cs:53`) | var | `TS:34` (yalnız `person` koltuklar) |
| D-38 | şifremi unuttum her zaman başarılı | `Auth/PasswordReset.cs:93` | var | değişmedi |
| D-40 | paylaşılan sıra ortalaması en yakın tama | sıfıra doğru (`Settlement.cs:55-56`) | farklı | `TS:73`; eski yol `Settlement.cs:59-61` (SettlementTests 22 → 23) |
| D-41 | eşlide kazanan takım 1-2, kaybeden 3-4: LP +20/−15, haftalık 23/3, XP 350/40 | takım sırası 1/2, XP 500/200 (`MatchSession.cs:551`) | farklı | `TS:49-68`; eski yol `SeatOutcome.XpBonus` |
| D-42 | el XP'si bitirene 340, ortak 60; botlara karşı ve derecelide de | çevrimiçi maçlarda | farklı | `HM:45` her modda (botlara karşı dahil) |

## Pakette yok

Sunucuda olup pakette karşılığı olmayan, **silinmeyen** özellikler (ayrıntı `docs/v4/PROTOKOL_ESLEME.md` sonu):

| Özellik | Yer | Not |
|---|---|---|
| Hızlı eşleşme kuyruğu `queue.join` (solo / pairs / doubling / ranked) | `Games/Lobbies.cs:33` | eski motorla oynar |
| `Game:HandsPerMatch` 4 | `Infrastructure/Options.cs` | yalnız hızlı eşleşme |
| `Game:Stakes` (solo/pairs/doubling/ranked/private) | `Infrastructure/Options.cs` | yalnız hızlı eşleşme |
| `Game:SoloPayout`, `Game:RakePercent` | `Games/Settlement.cs` | eski yol |
| Kodlu oda `room.*` | `Games/Lobbies.cs` | eski protokol |
| Eski lobi masaları `table.*` ve tur-bütün `match.*` | `Games/Tables.cs`, `Games/MatchSession.cs` | eski istemci ve eski kayıtlar |
| Dolu eski masanın 5 sn'de kendiliğinden sayması | `Games/Tables.cs:22` | paket: sahibi insan olan masada yalnız Başlat |
| Bekleme odasında sahiplik devri | `Games/Tables.cs:349` | v4: sahip çıkarsa masa kapanır (paket; kullanıcının önceki kararıyla çelişki — EK-211) |
| `session.replaced` | `Realtime/Realtime.cs` | tek cihaz |
| Küfür maskesi, engelleme, şikayet, moderatör | `Social/` | v4 sohbetinde de |
| İzleyici sınırı 20, sohbet/emoji hız sınırı | `Games/MatchSession.cs` | yalnız eski yol |
| Sezon geçişi, sezon ödülü, Galaksi ligi | `Profile/Ranked.cs` | |
| Gerçek para mağazası (makbuz doğrulaması), hesap bağlama, Apple girişi, token yenileme | `Economy/`, `Auth/` | paket DEMO |
| Profil istatistik/rozet/geçmiş, bulut ayarları, KVKK onayı, paylaşım bağlantısı | `Profile/`, `Social/` | |
| Eski motor maçlarının yeniden başlatmada geri kurulması | `Games/MatchManager.cs` | v4 masaları geri kurulmaz, bahis iade (EK-210) |

# Faz 2 · Masa · rapor

Dal `v4/table` (worktree `C:/GD/Okey101-v4/table`, taban `26f6697` = main `d2ec303` + v4/server + v4/data + v4/assets3d; sonra `main` `6565e6a` birleştirildi). Unity açılmadı: Unity tarafı `tools/v4/unity_compile.py` ile derlendi, saf C# `dotnet test` ile sınandı. Kararlar K-01, K-04, K-06, K-09 (`docs/v4/KARARLAR.md`). Eksikler `EKSIKLER.md` (EK-501…512), Unity farkları `UNITY_FARKLARI.md` (UF-501…511).

## 1 · Ne yapıldı

| Adım | Sonuç | Yer |
|---|---|---|
| 1 · İstemci modeli | `TableClient`: ıstakam (motor kimlikleri), rakip taş sayıları, ıskartalar, perler (`id/owner/kind/tiles/placedBy`), gösterge/okey, deste, dağıtan/zarlar/tören adımı, tur `{seat, phase, n, sideTake, sideMust, deadline}`, koltuk başına açılış değeri/biçimi ve ceza, toplamlar, el no, geçmiş, koltuk bayrakları, bağlantı/otomatik. protocol.json S2C olaylarını uygular: `matchStart`, `dealStart`, `dealStep`, `dealDie`, `handDealt`, `turn`, `legal`, `game{drew, sideReturned, openShort, opened, laid, added, okeySwapped, undone, discarded, finished, penalty, auto, handStart, turn, seatLeft, seatTaken, handEnd, matchEnd}`, `handWait`, `readyState`, `matchResult`, `seatConnection`, `tableView`, `snapshot`. R-34 anlık görüntüsü (geri topla / geri bırak) istemcide tutulur. `View(now)` sunucunun `GameView(seat)` ağacını yeniden kurar. Kurallar istemcide yazılmadı: `Legal`, `OpenNeed` (katlamalı R-64), `IsIslek`, `AddSides`, `SwapFits`, `AutoDiscardTile`, `AnalyzeMeld` hep `G101Rules` (koltuğun izdüşümü `Project()` üzerinde). `TileIds` D-01 ↔ int tek eşleme. `V4Wire` JSON ↔ sözleşme tipleri. | `Assets/Galaxy101/Table/Scripts/Client/` (asmdef `Galaxy101.Table.Client`, motor referanssız) |
| 2 · Yerel oturum | `LocalTableHost : ITableHost` (mantıksal saat, zamanlayıcılar kendi anlarında, mesajlar runtime çağrısından sonra modele), botlara karşı uzlaşma (R-94 çip yok, D-42 el/sıra XP), `ResumeFrom` (hazır el, EK-501). `LocalTableSession` saati `GameClock` (D-60) ileri adımlarıyla sürer; donunca masa durur. `TableScenarios`: client-tests.json elleri (HAND_DEAL/OPEN/BOMB, AYSE_MELDS, setupPenOpenTest, setupIsleTest) motor elleri; R43/R44 rig. `RackMelds`: ıstaka parçalarının seri/grup/çift önerisi (AnalyzeMeld). | `Client/LocalTableHost.cs`, `Game/TableSession.cs`, `Client/TableScenarios.cs`, `Client/RackMelds.cs` |
| 3 · TableGame v4 | Eski motor akışı (`LocalSession`, `BotPlanner`, `TurnDraft`, `CommitTurnCommand`, int `TileId` görünümleri) çıktı. Masa yalnız modeli okur, her eylem bir adım: çek/soldan al (`gDraw`), geri bırak (`gReturnSide`), aç/yere aç (`gOpen`/`gLay`, ıstaka parçaları), çift aç, işle (`gAdd`; İşle düğmesi sırayla hepsi), okey al (`gSwap`, yalnız `SwapFits`), geri topla (`gUndo`), at/bitir (`gDiscard`), Elden bit (R-53: yalnız eşik tutunca; aç + son taş), zar (`gRollDice`), sonraki el (`gReady`). Sunum katmanı aynı (TableScene, TableBoard, RackGrid/RackInput, TileView/TileFactory, TableFx, TableGifts, TableHud); `TableCeremony` sunucu adımlarını bekleyerek oynar (dealStart → dice → dealDie ×2 → indicator → extra → distribute(plan) → center → handDealt). Eksik açılış prototipteki gibi 1,5 sn tahtada, ceza sonra (UF-505). Son el: sonuç kartı → masa kapanır → lobide tost (REMOVED T45/T46). Sohbet/hediye/emoji maçtan geçer (botların cevabı ve teşekkürü runtime'ın, D-20). Ses: ses hattının `TableSounds` eşlemesi (`docs/v4/audio/RAPOR.md` §5). | `Game/TableGame.cs`, `Game/TableCeremony.cs`, `Game/RackInput.cs`, `Game/TileCodes.cs` |
| 4 · Ağ oturumu | `V4TableSession`: soketteki masa mesajları modele, her hamle kendi C2S mesajı ve ack (`code` = motor hatası), saat farkı `at`'tan. Kopunca "Bağlantı koptu · Deneme n/5", 3 sn arayla bilet → soket → `hello` → `session` + `snapshot`; 5. denemeden sonra "Lobiye dön" (HUD kartı `table.t.netTitle`, `table.netAttempt`, `table.netBarEl`, `table.askExitNet`, `table.retryNet`). `GalaxyOnline` `matchStart`'ı izler (`V4TableSession.Watch`), menü masayı açar (`MainMenuController.OnMatchStarted(V4TableSession)`); masanın kendi denemesi sürerken genel yeniden bağlanma bekler (`SuspendReconnect`). `snapshot.screen != table` → masa kapanır. | `Scripts/Online/V4TableSession.cs`, `GalaxyOnline.cs`, `MainMenuController.cs` |
| 5 · Kaldırılanlar | T33 okey rozeti ve T48 uzun basma ipucu (`ShowOkeyTip`, `okey-badge`, `okey-tip`) silindi; T44 puansız el yok (çiftle bitişi motor puanlar); T45/T46 maç sonu ekranı açılmaz (son kart → lobi tostu, dereceli → Dereceli sonuç ekranı); T40/T41 yönlendiricide `removed`. | `TableHud.cs`, `TableGame.cs` |
| 6 · Yönlendirici | `G101StateRouter.Table.cs`: states.json yolları adım adım (§4); `G101StateRouter.Steps()` içinde tek satır. | `Scripts/Diagnostics/` |
| 7 · HUD | openVals düğmeleri (Geri bırak, Yere çift aç, İşle), yeniden bağlanma kartı, yükleme ekranının satırı/mesajı/ipucu (paket metinleri `strings_v4`, `TableStrings` okuyor), D-62 adları: eylem satırı, gösterge, koltuk/ben plakaları, deste, bekleme kartı, sonuç başlığı. Kalanlar EK-511. | `Table/Scripts/Hud/` |

## 2 · Commit'ler (`v4/table`)

| Commit | İçerik |
|---|---|
| `717f164` | İstemci modeli, `TileIds`, `V4Wire`, `LocalTableHost` (ilk), NUnit eşlik testleri; `unity_compile.py` csproj'u olmayan asmdef'ler için proje üretir ve eksik proje başvurularını ekler |
| `841d4a5` | Yerel host, hazır test akışları, R43/R44, `RackMelds`; senaryo testleri |
| `91587fd` | `main` (6565e6a) birleştirme |
| `1994d8d` | TableGame v4, adım tabanlı tören, ağ oturumu ve yeniden bağlanma, menü giriş noktaları; eski motor testleri yerine v4 testleri |
| `e93b1cc` | Geri bırak / Yere çift aç / İşle, yeniden bağlanma kartı, ses |
| `50241d6` | Durum yönlendirici G01–G34, T01–T51; yükleme ekranı; `strings_v4` |
| `d080e5b` | T33/T48 kaldırıldı; D-62 adları |
| son commit | Dağıtım sonrası bot çayı (DEMO), bu rapor, EKSIKLER, UNITY_FARKLARI |

Kaldırılan eski testler (eski motor akışını sınıyorlardı; yerlerine v4 karşılıkları): `TableGameRuntimeTests`, `TableV3Tests`, `DealCeremonyTests`, `OnlineTableTests` → `tools/v4/nunit-table` + `Tests/PlayMode/TableV4RuntimeTests.cs`; `RackGridTests`'in açılış ölçeri testleri `RackMelds`'i prototipin `segVal`'ine karşı sınar. `MatchWire.cs`, eski `NetworkTableSession` ve eski `TableScenarios` silindi.

## 3 · Testler ve komutlar (worktree kökünden)

```
dotnet test tools/v4/nunit-table/TableClientTests.csproj
  → Başarılı: 36, Toplam: 36
```
- `TableClientTests` (18): 12 tam maç (tekli/eşli, katlamalı, katlamalı yalnız rakip, cezasız/işlemesiz, 6 seed) — kişi koltuğu karışık politikayla oynar (botun hamlesi, soldan alma, geri bırak, geri topla, yanlış hamle, zaman aşımı, zar, hazır); sunucunun her adımından sonra (her zamanlayıcı, her hamle) kişinin ve izleyicinin modeli yalnız olaylardan (`tableView` uygulanmadan) runtime'ın `GameView(seat)`'iyle kanonik JSON olarak aynı; `legal` olayı modelin hesabıyla aynı. Ayrıca: bütün motor olaylarının kapsandığı, iki kişi + kalkma/koltuk devralma, kopma → otomatik oyun → `snapshot` ile dönüş, JSON tel gidiş-dönüşü (`V4Wire`), güvenilen görünüm kipi, `TileIds` eşlemesi. Bir mutasyon denemesi (okeySwapped'da okeyi ıstakaya koymamak) testleri kırdı.
- `TableScenarioTests` (18): 7 akış yasal el (invariants), referans sayıları (88/101, 131, 89 ve Elden bit yok, 66), açılış/geri topla, eksik açılış +101, okey alma (Y4 evet, K5 hayır) + İşle 5 taş + geri topla, işlek atış +101, Ayşe'nin işleyip bitirmesi (−101 / +202), soldan alma + geri bırakma, R43 (+90 P_OPENED_WITH) ve R44 (+101 P_OKEY_TAKEN) gerçek runtime ve botlarla, botlara karşı 2 elli maç → `matchResult` (havuz 0) + XP, tören adım sırası ve 21 686 ms.

```
python tools/v4/unity_compile.py --repo C:/GD/Okey101-v4/table --csproj-from "C:/GD/Okey 101" --scratch <geçici> --project Galaxy101.PlayMode.Tests
  → Galaxy101.PlayMode.Tests: OK (0 errors)
  (aynı: Galaxy101.Editor.Tests, Galaxy101.Table.Tests, Assembly-CSharp-Editor → OK)
```
Unity PlayMode/EditMode testleri koşturulmadı (EK-508).

## 4 · Durumlar

Yönlendirici yolu: `acct` → menü (Doğukan Y., 10.000 çip); `scenario` R43/R44 → ilk elin rig'i (seed 7); `table {flow, bots, noWait}` → Test masası (Ayşe K. #FF6B9A Sv 31, Burak T. #4FC3F7 Sv 18, Zeynep A. #9CCC65 Sv 42; bahis 5.000, 5 el, 30 sn; çip 5.000) ya da Botlara karşı (Bot Ayla/Cem/Defne); masa açılınca 400 ms sonra `go(flow)` ve prototipin zamanlı ekleri (open 1,1 sn openHand; gift 0,3/0,6 sn; chat 0,3/1,4 sn; exit 0,4 sn; net 0,5 sn; settings 0,3 sn; play 1,6 sn çay; gerçek dağıtımdan 2,6 sn sonra çay); `waitDeal` törenin görsel adımı; `masa …`, `set Masa {…}`, `set App` (T51), `wait`, `waitMy`, `waitPen`. Seed 42 (senaryolar 7). Hiçbir kare Unity'de çekilmedi — son sütun yazılan yolu ve beklenen farkı verir.

| Kimlik | Başlık | Akış | Yol adımları | Durum · fark |
|---|---|---|---|---|
| G01 | Masa yükleniyor | — · noWait | — | yazıldı, Unity'de çekilmedi · UF-506 (yükleme menünün üstünde) |
| G02 | Bot balyaları diziyor | deal | — | yazıldı, Unity'de çekilmedi · UF-501 (seed 42 dağıtımı, sunucu takvimi) |
| G03 | Sen diziyorsun | deal | — | yazıldı, Unity'de çekilmedi · UF-501 (seed 42 dağıtımı, sunucu takvimi) |
| G04 | Zar at düğmesi | deal | — | yazıldı, Unity'de çekilmedi · UF-501 (seed 42 dağıtımı, sunucu takvimi) |
| G05 | Zar yuvarlanıyor | deal | waitDeal dice; masa rollDice | yazıldı, Unity'de çekilmedi · UF-501 (seed 42 dağıtımı, sunucu takvimi) |
| G06 | 1. zar · Tekrar at | deal | waitDeal dice; masa rollDice; wait 2200 | yazıldı, Unity'de çekilmedi · UF-501 (seed 42 dağıtımı, sunucu takvimi) |
| G07 | Gösterge açılıyor | deal | waitDeal dice; masa rollDice; wait 2200; masa rollDice; waitDeal ind | yazıldı, Unity'de çekilmedi · UF-501 (seed 42 dağıtımı, sunucu takvimi) |
| G08 | Gösterge → okey kartı, fazla taş | deal | waitDeal dice; masa rollDice; wait 2200; masa rollDice; waitDeal extra | yazıldı, Unity'de çekilmedi · UF-501 (seed 42 dağıtımı, sunucu takvimi) |
| G09 | Balyalar dağıtılıyor | deal | waitDeal dice; masa rollDice; wait 2200; masa rollDice; waitDeal dist | yazıldı, Unity'de çekilmedi · UF-501 (seed 42 dağıtımı, sunucu takvimi) |
| G10 | Kalan balyalar ortaya | deal | waitDeal dice; masa rollDice; wait 2200; masa rollDice; waitDeal center | yazıldı, Unity'de çekilmedi · UF-501 (seed 42 dağıtımı, sunucu takvimi) |
| G11 | Istakalar doluyor | deal | waitDeal dice; masa rollDice; wait 2200; masa rollDice; waitDeal fill | yazıldı, Unity'de çekilmedi · UF-501 (seed 42 dağıtımı, sunucu takvimi) |
| G12 | Oyun başladı (22 taş) | deal | waitDeal dice; masa rollDice; wait 2200; masa rollDice; waitDeal None | yazıldı, Unity'de çekilmedi · UF-501 (seed 42 dağıtımı, sunucu takvimi) |
| G13 | El açıldı: perler ortada | open | — | yazıldı, Unity'de çekilmedi · UF-503 |
| G14 | İşleme testi (herkes açık) | isle | — | yazıldı, Unity'de çekilmedi · UF-503 |
| G15 | Altın işleme göstergeleri | isle | — | yazıldı, Unity'de çekilmedi · UF-503 |
| G16 | Yeşil okey alma göstergesi (Y4 seçili) | isle | wait 1300; masa scSel ["Y4"] | yazıldı, Unity'de çekilmedi · UF-503 |
| G17 | Okey alındı (Y4 ↔ okey) | isle | wait 1300; masa scSwap ["Y4", 2, 0] | yazıldı, Unity'de çekilmedi · UF-503 |
| G18 | İşleme yapıldı + Geri topla | isle | wait 1500; masa isle | yazıldı, Unity'de çekilmedi · UF-503 |
| G19 | Geri toplandı | isle | wait 1500; masa isle; wait 900; masa undoTurn | yazıldı, Unity'de çekilmedi · UF-503 |
| G20 | Hatalı açma (turuncu Aç) | penOpen | — | yazıldı, Unity'de çekilmedi · UF-503 |
| G21 | Hatalı per masada | penOpen | wait 1500; masa openHand | yazıldı, Unity'de çekilmedi · UF-505 (badOpen görüntüsü 1,5 sn) |
| G22 | Ceza tostu | penOpen | wait 1500; masa openHand | yazıldı, Unity'de çekilmedi · UF-505 (badOpen görüntüsü 1,5 sn) |
| G23 | İşlek taş testi | penIslek | — | yazıldı, Unity'de çekilmedi · UF-503 |
| G24 | İşlek taş cezası (B4 atıldı) | penIslek | wait 1500; masa scDiscardCode ["B4"] | yazıldı, Unity'de çekilmedi · UF-503 |
| G25 | Elden bit düğmesi | bomb | — | yazıldı, Unity'de çekilmedi · UF-502 (R-53: Elden bit yok, 89/101) |
| G26 | Taş fırlatılıyor | bomb | wait 1500; masa bomb | yazıldı, Unity'de çekilmedi · UF-502 (R-53: Elden bit yok, 89/101) |
| G27 | Nükleer patlama | bomb | wait 1500; masa bomb | yazıldı, Unity'de çekilmedi · UF-502 (R-53: Elden bit yok, 89/101) |
| G28 | ELDEN BİTTİN | bomb | wait 1500; masa bomb | yazıldı, Unity'de çekilmedi · UF-502 (R-53: Elden bit yok, 89/101) |
| G29 | El sonu (kazandın) | bomb | wait 1500; masa bomb | yazıldı, Unity'de çekilmedi · UF-502 (R-53: Elden bit yok, 89/101) |
| G30 | Sıra sende | play | — | yazıldı, Unity'de çekilmedi · UF-503 |
| G31 | Soldan alındı + Geri bırak | play | wait 2000; masa drawFrom ["side"] | yazıldı, Unity'de çekilmedi · UF-503 |
| G32 | Masa ayarları | settings | — | yazıldı, Unity'de çekilmedi · UF-503 |
| G33 | Masadan kalk onayı | exit | — | yazıldı, Unity'de çekilmedi · UF-503 |
| G34 | El sonu (Ayşe kazandı) | end | — | yazıldı, Unity'de çekilmedi · UF-504 (bitiş ≈ 1,5 sn), UF-503 |
| T01 | Bekleme: 2/4 oyuncu | seat | — | yazıldı, Unity'de çekilmedi · bekleme odası (yerel) |
| T02 | Masa doldu, geri sayım | seat | — | yazıldı, Unity'de çekilmedi · bekleme odası → 3-2-1 |
| T03 | Dağıtım: girdap, karma | deal | — | yazıldı, Unity'de çekilmedi · — |
| T04 | Dağıtım bitti, İlk hamle senin | play | — | yazıldı, Unity'de çekilmedi · UF-503 |
| T05 | Çekme aşaması, Ayşe'nin perleri | play | — | yazıldı, Unity'de çekilmedi · UF-503 |
| T06 | Çay ısmarlandı bildirimi | play | — | yazıldı, Unity'de çekilmedi · UF-503 |
| T07 | Oyuncu kartı (Zeynep), hızlı ikram | play | wait 1500; set Masa {"card": 3} | yazıldı, Unity'de çekilmedi · UF-503 |
| T08 | Hediye paneli · İkram | gift | — | yazıldı, Unity'de çekilmedi · UF-503 |
| T09 | Hediye paneli · Hediye (Pasta) | gift | wait 700; set Masa {"giftTab": 1, "giftSel": "cake"} | yazıldı, Unity'de çekilmedi · UF-503 |
| T10 | Hediye paneli · Emoji | gift | wait 700; set Masa {"giftTab": 2, "giftSel": null} | yazıldı, Unity'de çekilmedi · UF-503 |
| T11 | Roket uçuşu | play | wait 1500; masa botGift [3, "rocket", 2, ""] | yazıldı, Unity'de çekilmedi · UF-503 |
| T12 | Roket indi + teşekkür balonu | play | wait 1500; masa botGift [3, "rocket", 2, ""] | yazıldı, Unity'de çekilmedi · UF-503 |
| T13 | Sohbet paneli | chat | — | yazıldı, Unity'de çekilmedi · UF-503 |
| T14 | El açıldı · Yere aç / İşle | open | — | yazıldı, Unity'de çekilmedi · UF-503 |
| T15 | Ayarlar · Görünüm | settings | — | yazıldı, Unity'de çekilmedi · UF-503 |
| T16 | Masa ayarları · Oyun | settings | wait 500; set Masa {"setTab": 0} | yazıldı, Unity'de çekilmedi · UF-503 |
| T17 | Masa ayarları · Ses | settings | wait 500; set Masa {"setTab": 1} | yazıldı, Unity'de çekilmedi · UF-503 |
| T18 | Masa ayarları · Bildirimler | settings | wait 500; set Masa {"setTab": 3} | yazıldı, Unity'de çekilmedi · UF-503 |
| T19 | Tema: Ceviz salon + Klasik ceviz ıstaka | play | wait 600; masa setSet [{"tableSkin": "wood", "rackSkin": "walnut"}, true] | yazıldı, Unity'de çekilmedi · UF-503 |
| T20 | Tema: Hologram + Neon cam ıstaka | play | wait 600; masa setSet [{"tableSkin": "holo", "rackSkin": "neon"}, true] | yazıldı, Unity'de çekilmedi · UF-503 |
| T21 | Tema: Andromeda masa + ıstaka | play | wait 600; masa setSet [{"tableSkin": "andromeda", "rackSkin": "andromeda"}, true] | yazıldı, Unity'de çekilmedi · UF-503 |
| T22 | Kuşbakışı görünüm (Andromeda) | play | wait 600; masa setSet [{"tableSkin": "andromeda", "rackSkin": "andromeda", "view": "top"}, true] | yazıldı, Unity'de çekilmedi · UF-503 |
| T23 | Bitiş anı: Ayşe bitirdi | end | — | yazıldı, Unity'de çekilmedi · UF-504 (bitiş ≈ 1,5 sn), UF-503 |
| T24 | El sonu tablosu | end | — | yazıldı, Unity'de çekilmedi · UF-504 (bitiş ≈ 1,5 sn), UF-503 |
| T25 | Masadan kalk onayı | exit | — | yazıldı, Unity'de çekilmedi · UF-503 |
| T26 | Bağlantı koptu | net | — | yazıldı, Unity'de çekilmedi · UF-511 |
| T27 | EN — oyun sırasında | play | — | yazıldı, Unity'de çekilmedi · UF-503 |
| T28 | Botlara karşı: başlık, ANTRENMAN, el sayacı | play · bots | — | yazıldı, Unity'de çekilmedi · UF-503 |
| T30 | Botlara karşı · masadan kalk onayı | exit · bots | — | yazıldı, Unity'de çekilmedi · UF-503 |
| T33 | Okey rozeti masada | kaldırıldı (REMOVED.md) | — | yönlendirici atlar |
| T34 | Ceza: attığın K9 ile bot açtı (+90, R-43) | — · bots | scenario R43; waitMy; masa scDiscard ["K9b"]; waitPen | yazıldı, Unity'de çekilmedi · gerçek tören + R43/R44 rig; UF-510 |
| T35 | Eksik açılış cezası (+101) | penOpen | wait 1500; masa openHand | yazıldı, Unity'de çekilmedi · UF-505 (badOpen görüntüsü 1,5 sn) |
| T36 | Ceza: masadaki okeyin alındı (+101, R-44) | — · bots | scenario R44; waitMy; masa scOpen [[["R10a", "R11a", "Y5a", "R13a"], ["K13a", "B13a", "Y13a"], ["K10a", "K11a", "K12a"]]]; wait 900; masa scDiscard ["R6a"]; waitPen | yazıldı, Unity'de çekilmedi · gerçek tören + R43/R44 rig; UF-510 |
| T38 | Yerden alındı + Geri bırak | play | wait 2000; masa drawFrom ["side"] | yazıldı, Unity'de çekilmedi · UF-503 |
| T39 | Soldan alınan taş halkaya geri sürükleniyor | play | wait 2000; masa drawFrom ["side"]; wait 700; masa scDrag ["$side", "ring", 0.9] | yazıldı, Unity'de çekilmedi · UF-508, UF-503 |
| T40 | Mesaj kataloğu | kaldırıldı (REMOVED.md) | — | yönlendirici atlar |
| T41 | Mesaj kuyruğu | kaldırıldı (REMOVED.md) | — | yönlendirici atlar |
| T42 | El sonu · botlara karşı | end · bots | — | yazıldı, Unity'de çekilmedi · UF-504 (bitiş ≈ 1,5 sn), UF-503 |
| T43 | El sonu · çevrimiçi masa | end | — | yazıldı, Unity'de çekilmedi · UF-504 (bitiş ≈ 1,5 sn), UF-503 |
| T44 | El sonu puansız (çiftle bitiş) | kaldırıldı (REMOVED.md) | — | yönlendirici atlar |
| T45 | Maç sonu · kazandın | kaldırıldı (REMOVED.md) | — | yönlendirici atlar |
| T46 | Maç sonu · 2. sıra | kaldırıldı (REMOVED.md) | — | yönlendirici atlar |
| T47 | Okeyi al: Y4 sürükleniyor, uygun hedefler parlıyor | isle | wait 1300; masa scDrag ["Y4", "target", 0.55] | yazıldı, Unity'de çekilmedi · UF-508 |
| T48 | Okey alma ipucu | kaldırıldı (REMOVED.md) | — | yönlendirici atlar |
| T49 | Okeyi al: yanlış taşla deneme (K5) | isle | wait 1300; masa scSwap ["K5", 2, 0, true] | yazıldı, Unity'de çekilmedi · UF-503 |
| T50 | Menü → masa yükleme ekranı | — · noWait | — | yazıldı, Unity'de çekilmedi · UF-506 (yükleme menünün üstünde) |
| T51 | Masa açılamadı hatası | — · noWait | wait 300; set App {"gameOn": false} | yazıldı, Unity'de çekilmedi · UF-506 (yükleme menünün üstünde) |


## 5 · Orkestratörün Unity'de doğrulayacakları

1. Derleme: yeni asmdef'ler `Galaxy101.Table.Client` (Assets/Galaxy101/Table/Scripts/Client, motor referanssız) ve `Galaxy101.Table.Tests` (Editor); `Galaxy101.Runtime` artık `Galaxy101.Table.Runtime` ve `Galaxy101.Table.Client`'a başvurur.
2. Test Runner: `Galaxy101.Table.Tests` (36, NUnit ile aynı), `TableV4RuntimeTests` (5), `TableHudRuntimeTests`, `RackGridTests`, `LobbyTests.TheDeveloperMenuListsTheSpec25Scenarios`.
3. Botlara karşı: menü → "Botlara karşı" → 3-2-1 → tören (bot dağıtıcıda ~21,7 sn: balyalar, zar, gösterge, fazla taş, dağıtım, orta, ıstakalar) → ilk tur; çek/at, aç (eşik altı turuncu → taşlar 1,5 sn tahtada, sonra +101), işle, okey al, geri topla, soldan al + geri bırak, zaman aşımı ("Süren doldu"), el sonu kartı + "Sonraki el", 5. elden sonra lobiye dönüş ve tost; ses olayları.
4. Durumlar: `-g101State=G01,…,G34,T01,…,T51 -g101Seed=42` (kaldırılanlar atlanır) ve `compare_states.py`; önce G02–G12 (tören), G13, G16–G19 (okey/işle/geri topla), G20–G22 + T35 (eksik açılış), G24, G25–G29 (Elden bit yok), T23/T24/G34 (bitiş + el sonu), T26 (yeniden bağlanma kartı), T34/T36 (gerçek tören + R43/R44; waitMy ~25 sn), T47/T49.
5. Çevrimiçi: v4 sunucuyla `vsBotsStart` ya da bekleme odası `tableStart` → `matchStart` masayı açmalı (lobi hattı v4'e geçince); soket koparılınca kart, 3 sn arayla 5 deneme, dönüşte `snapshot` ile masa animasyonsuz kurulur.
6. Birleştirmede çakışabilecek yerler: `MainMenuController` (OnMatchStarted, OnRankedMatched'in masa satırları, PendingToast, LoadingKick), `GalaxyOnline` (Watch, SuspendReconnect).

## 6 · Kalan farklar

- HUD eşliği (adım 7) kısmi: tören kartı, bitiş bandı, sonuç satırları, hediye/sohbet/ayar pencereleri ve oyuncu kartı eski adlarla; ölçüler referansla karşılaştırılmadı (EK-511).
- Test akışlarında deste sayısı (UF-503), bitiş zamanlaması (UF-504), yükleme ekranının altı (UF-506).
- Sunucu tarafı: `TableRuntime` hazır elden başlatma (EK-501), devralma/dönüşte yeni `deadline` yayılmıyor (EK-502), hazır mesaj anahtarları (EK-503).
- Lobi/dereceli hatlarının v4 `matchStart`'a geçişi (EK-509).

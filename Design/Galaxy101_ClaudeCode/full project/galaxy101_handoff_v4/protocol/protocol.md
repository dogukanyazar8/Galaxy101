# protocol.md · Galaxy 101 istemci–sunucu protokolü · v0.5.0

Makine okunur kaynak: `protocol.json` (bu belge ondan üretildi). Prova sunucusu: `logic/mockServer.js`. Süreler: `timings.md`.

## Taşıma
Tek WebSocket. Her mesaj JSON nesnesi, alan "t" mesaj adı. C2S mesajlarında "rid" (istemci sıra no) zorunlu; sunucu her C2S için tam bir "ack" döner. S2C olaylarında "at" = sunucu saati (ms). Prova: logic/mockServer.js → createServer({clock,seed}).connect(onMessage).send(msg).

## Gizli bilgi kuralı
- Rakip ıstakaları hiçbir mesajda yer almaz; yalnız taş sayıları (counts). İstisna: el sonunda handEnd.racks (açık gösterim).
- Deste sırası (H.deck) asla gönderilmez; yalnız deck = kalan sayı.
- Zar değerleri dealDie olayı ile atış anında gelir; gösterge ve okey dealStep:indicator ile gelir. handStart motor olayı istemciye dice/indicator/okey boşaltılarak iletilir.
- Desteden çekilen taş (game.drew src=deck) yalnız çekene tile ile gider; diğerlerine tile=null. Yerden (soldan) alınan taş herkese açıktır.
- Eksik açılış (game.openShort) taşları yalnız deneyene gider.
- İzleyici (seat=-1) kendi ıstakası olmadan, oyuncuların gördüğü açık bilgiyi alır.
- Her oyuncuya kendi görünümü: snapshot.game = gameView(table, seat).

## Kopma ve yeniden bağlanma
1. Maç sırasında kopan oyuncu "kalktı" sayılmaz. Sunucu koltuğu tutar, seatConnection{connected:false} yayınlar.
2. Sırası gelince normal tur süresi işler; süre dolunca R-70 otomatik oyun. Üst üste 2 zaman aşımından sonra koltuğu sunucu oynar (seatConnection{autoplay:true}); bot mantığı oyuncu adına oynar.
3. Oyuncu maç bitene kadar dönebilir: hello{token} → session + snapshot{screen:"table"}; sunucunun oynaması o anda biter, sıra ondaysa tur süresi baştan başlar.
4. Bekleme odasında (S13) bağlantı koparsa oyuncu odadan hemen çıkarılır, bahsi iade edilir (leftTable{reason:"disconnect"}). Sahibiyse masa kapanır.
5. İstemci yeniden bağlanma ekranı: "Bağlantı koptu · Deneme n/5", denemeler 3 sn arayla; 5. deneme de başarısızsa "Lobiye dön" düğmesi.
6. Dönünce snapshot.screen istemcinin döneceği ekranı belirler; snapshot.game ile masa animasyonsuz kurulur, sonra canlı olaylar akar.

| snapshot.screen | Dönülen ekran |
|---|---|
| `menu` | Ana menü (1a) |
| `waitroom` | Bekleme odası (S13) |
| `table` | Oyun masası (dağıtım sürüyorsa game.deal.step ve elapsed ile tören kaldığı adımdan sürer) |
| `ranked.search` | Dereceli arama |
| `ranked.found` | Maç bulundu (kabul sayacı) |
| `ranked.loading` | Masa hazırlanıyor |

## İstemci niyetleri (C2S)

Hepsi `{t, rid, ...alanlar}`; yanıt `ack`. `ENGINE` = kural motoru hata kodlarının tamamı (RULES.md).

| Ad | Grup | Alanlar | Dönüş | Hatalar | Açıklama |
|---|---|---|---|---|---|
| `hello` | oturum | token: string | token: string | E_SESSION_EXPIRED | Yeniden bağlanma. Başarıda session + snapshot. |
| `register` | oturum | email: string<br>pass: string<br>name: string | token: string | E_EMAIL_INVALID E_PASS_SHORT E_NAME_INVALID E_EMAIL_EXISTS E_NAME_TAKEN | S04 Kayıt ol. 25.000 çip + 20 elmas. |
| `login` | oturum | email: string<br>pass: string | token: string | E_ACCOUNT_UNKNOWN E_PASS_WRONG E_ACCOUNT_LOCKED | S03 Giriş yap. E_PASS_WRONG.detail.left = kalan hak; 3. yanlışta 5 dk kilit, E_ACCOUNT_LOCKED.detail.until. |
| `guest` | oturum | name: string?<br>color: string? | token: string | E_NAME_INVALID | S05 Misafir olarak oyna. 10.000 çip + 20 elmas. |
| `passwordResetRequest` | oturum | email: string | — | — | Şifremi unuttum: kod e-postayla gönderilir. Hesabın var olup olmadığı yanıtta belli edilmez. |
| `passwordReset` | oturum | email: string<br>code: string<br>pass: string | — | E_RESET_CODE E_PASS_SHORT | Yeni şifre; başarıda giriş kilidi kalkar (Q-14). |
| `oauth` | oturum | provider: "google"<br>email: string<br>name: string?<br>idToken: string | token: string | E_OAUTH_FAILED | S02 Google ile devam et. Yeni hesap 25.000 çip + 20 elmas. |
| `logout` | oturum | — | — | — | Çıkış yap. |
| `accountDelete` | oturum | confirm: string | — | E_CONFIRM_MISMATCH E_IN_GAME | Hesabı sil (SİL / DELETE). |
| `resync` | oturum | — | — | — | Tam durum anlık görüntüsü ister → snapshot. |
| `ping` | oturum | — | serverTime: int | — | Saat eşitleme. |
| `profileUpdate` | profil | name: string?<br>status: string?<br>color: string?<br>frame: int? | — | E_NAME_INVALID E_STATUS_LONG E_FRAME_LOCKED | Profili düzenle. |
| `shopBuy` | ekonomi | sku: string | — | E_SKU_UNKNOWN E_ALREADY_OWNED E_NO_GEMS | Mağaza satın alma. SKU listesi mockServer ECON.shop. |
| `skinSet` | ekonomi | skin: string | — | E_NOT_OWNED | Taş görünümü seç. |
| `dailyGet` | günlük | — | — | — | → daily. |
| `dailyClaim` | günlük | — | amount: int | E_DAILY_CLAIMED | Günlük ödülü al. |
| `questClaim` | günlük | questId: string | — | E_QUEST_UNKNOWN E_QUEST_INCOMPLETE E_QUEST_CLAIMED | Görev ödülünü al. |
| `friendsGet` | arkadaş | — | — | — | → friends. |
| `friendFind` | arkadaş | query: string | result: "friend"|"player"|"none"<br>user: User? | — | Arkadaş ekle araması (kod AB1234 → AB-1234, sonra ad içerir). |
| `friendRequest` | arkadaş | userId: string | — | E_USER_UNKNOWN E_ALREADY_FRIENDS E_REQUEST_PENDING | İstek gönder. |
| `friendRespond` | arkadaş | userId: string<br>accept: bool | — | E_REQUEST_UNKNOWN | İsteği kabul/ret. |
| `friendRemove` | arkadaş | userId: string | — | E_NOT_FRIENDS | Arkadaşlıktan çıkar. |
| `leaderboardGet` | liderlik | board: "weekly"|"season"|"friends" | — | — | → leaderboard. weekly: 30/15/5/1 puan (özel oda ve botlara karşı hariç), pazartesi 00:00 İstanbul sıfırlanır · season: dereceli LP · friends: arkadaşlar arasında sezon LP. |
| `lobbySubscribe` | lobi | — | — | — | Masa listesi + canlı güncellemeler (lobbyTable, lobbyTableRemoved). |
| `lobbyUnsubscribe` | lobi | — | — | — | Canlı güncellemeyi bırak. |
| `tableCreate` | lobi | name: string<br>mode: "solo"|"pairs"<br>private: bool<br>pin: string(4)?<br>hands: 3|5|7<br>bet: 1000|5000|10000|50000|100000<br>katlama: bool<br>turnSec: 15|20|30|45<br>rules: {spectate:bool,chat:"open"|"quick"|"off",gifts:bool,isle:bool,penalties:bool}<br>katlamaPartner: bool? (yalnız eşli + katlamalı; varsayılan true) | table: Table | E_ALREADY_SEATED E_TABLE_NAME E_PIN_FORMAT E_TABLE_OPTIONS E_NO_CHIPS | S10 Masa aç. Bahis oturunca düşülür (R-90). Katlamalı (R-64): yalnız açılış eşiği yükselir. |
| `tableJoin` | lobi | tableId: string<br>pin: string?<br>seat: int? | table: Table<br>seat: int | E_TABLE_CLOSED E_ALREADY_SEATED E_PIN_WRONG E_TABLE_FULL E_NO_CHIPS E_TABLE_PLAYING | Otur. Şifreli masada pin zorunlu (S11). Masa oynanıyorsa "Oyuna katıl" (R-65): botun ya da kalkan oyuncunun koltuğu devralınır, bahis o an ödenir, koltuğun toplamıyla devam edilir; ardından snapshot{screen:"table"}. |
| `tableJoinCode` | lobi | code: string<br>pin: string? | table: Table<br>seat: int | E_CODE_FORMAT E_CODE_UNKNOWN E_PIN_WRONG E_TABLE_FULL E_NO_CHIPS E_ALREADY_SEATED | S12 Kodla katıl (GX-XXXX). |
| `tableSpectate` | lobi | tableId: string | — | E_TABLE_CLOSED E_SPECTATE_OFF E_ALREADY_SEATED | İzle → snapshot{spectator:true}. |
| `tableLeave` | lobi | — | — | E_NOT_SEATED | Bekleme odasında: bahis iade. Maçta: R-60 (bahis kaybı, son sıra, yerine bot) → leftTable. |
| `seatAddBot` | bekleme | seat: int | — | E_NOT_SEATED E_NOT_OWNER E_STATUS E_SEAT_TAKEN | Sahip: boş koltuğa bot. |
| `seatKick` | bekleme | seat: int | — | E_NOT_SEATED E_NOT_OWNER E_STATUS E_SEAT_EMPTY | Sahip: oyuncuyu/botu çıkar (bahis iade). |
| `tableStart` | bekleme | — | — | E_NOT_SEATED E_NOT_OWNER E_STATUS E_TABLE_NOT_FULL | Sahip: Başlat → countdown 3-2-1. |
| `inviteSend` | davet | userId: string<br>seat: int?<br>recent: bool? | inviteId: string<br>expiresAt: int | E_NOT_SEATED E_STATUS E_USER_UNKNOWN E_NOT_FRIENDS E_USER_OFFLINE E_USER_BUSY E_TABLE_FULL | S14/S15 Davet. 30 sn geçerli. |
| `inviteRespond` | davet | inviteId: string<br>accept: bool | — | E_INVITE_EXPIRED E_TABLE_FULL E_NO_CHIPS E_TABLE_CLOSED | Gelen daveti yanıtla. |
| `inviteCancel` | davet | inviteId: string | — | E_INVITE_EXPIRED | Gönderilen daveti geri çek. |
| `vsBotsStart` | botlar | mode: "solo"|"pairs"<br>hands: int? | table: Table | E_ALREADY_SEATED | Botlara karşı: 3 bot, çip yok, doğrudan matchStart. |
| `rankedQueue` | dereceli | mode: "solo"|"duo"|"squad"<br>party: string[] | — | E_ALREADY_SEATED E_ALREADY_QUEUED E_MODE E_PARTY_SIZE E_PARTY_RANGE | Arama başlat. Duo: party 1 kişi, Squad: 3 kişi. |
| `rankedAccept` | dereceli | matchId: string<br>accept: bool | — | E_MATCH_EXPIRED | Maç bulundu: kabul / reddet (10 sn). |
| `rankedCancel` | dereceli | — | — | E_NOT_QUEUED | Aramayı iptal et. |
| `gRollDice` | masa | — | — | E_NOT_SEATED E_SPECTATOR E_PHASE E_NOT_DEALER | Dağıtan insan oyuncu zarı atar (her zar ayrı). 15 sn içinde atmazsa sunucu atar. |
| `gDraw` | masa | src: "deck"|"side" | — | ENGINE | Desteden çek / soldan al. |
| `gReturnSide` | masa | — | — | ENGINE | Soldan alınan taşı geri bırak (R-32). |
| `gOpen` | masa | melds: [{kind:"run"|"group"|"pair",tiles:string[]}] | — | ENGINE | Elini aç (101 / 5 çift). |
| `gLay` | masa | melds: [{kind,tiles}] | — | ENGINE | Açmış oyuncu ek per yere açar. |
| `gAdd` | masa | tile: string<br>meld: string<br>side: "L"|"R"|null | — | ENGINE | İşle. |
| `gSwap` | masa | tile: string<br>meld: string<br>index: int | — | ENGINE | Masadaki okeyi al (R-26). |
| `gUndo` | masa | — | — | ENGINE | Geri topla (R-34). |
| `gDiscard` | masa | tile: string | — | ENGINE | Taş at / bitir. |
| `gReady` | masa | — | — | E_NOT_SEATED E_SPECTATOR E_PHASE | "Sonraki el" düğmesi; tüm insanlar hazırsa 15 sn beklenmez. |
| `gRackOrder` | masa | tiles: (string|null)[] | — | E_RACK_MISMATCH | Istaka dizilimi (yalnız doğrulama; dizilim istemcidedir). |
| `chatSend` | sohbet | text: string?<br>quick: string? | — | E_NOT_SEATED E_CHAT_OFF E_CHAT_QUICK_ONLY E_CHAT_LENGTH E_CHAT_QUICK_UNKNOWN | Sohbet: serbest yazı (≤120) ya da hazır mesaj/emoji anahtarı. |
| `giftSend` | hediye | gift: string<br>to: int|"all" | — | E_NOT_SEATED E_GIFTS_OFF E_GIFT_UNKNOWN E_GIFT_TARGET E_NO_CHIPS | Hediye ısmarla. "all" = fiyat × alıcı sayısı. |

## Sunucu olayları (S2C)

| Ad | Grup | Kime | Alanlar | Açıklama |
|---|---|---|---|---|
| `ack` | sistem | gönderen | rid: int<br>ok: bool<br>error: string?<br>detail: object? | Her C2S için tam bir kez. |
| `session` | oturum | kullanıcı | me: Me<br>serverVersion: string<br>serverTime: int | Giriş başarılı. |
| `snapshot` | oturum | kullanıcı | me: Me<br>screen: "menu"|"waitroom"|"table"|"ranked.search"|"ranked.found"|"ranked.loading"<br>table: Table?<br>game: GameView?<br>ranked: Queue?<br>spectator: bool? | Tam durum. Kopma sonrası dönülecek ekran. |
| `profile` | profil | kullanıcı | me: Me | Profil değişti. |
| `balance` | ekonomi | kullanıcı | chips: int<br>gems: int<br>dChips: int<br>dGems: int<br>reason: "bet"|"betRefund"|"win"|"daily"|"quest"|"purchase"|"shop"|"gift"<br>ref: string? | Her bakiye hareketi. İstemci sayaç animasyonunu bundan oynatır. |
| `xp` | ekonomi | kullanıcı | gained: int<br>xp: int<br>xpNext: int<br>level: int<br>levelUp: int<br>reason: string | XP kazanımı: her el sonunda (reason:"hand") ve maç sonunda sıra bonusu (reason:"match"). |
| `daily` | günlük | kullanıcı | daily: {day:int,claimedToday:bool,amounts:int[7],msToReset:int}<br>quests: [{id,cur,goal,reward,gem,claimed}] | Günlük ödül + görevler. Gün değişimi Europe/Istanbul. Bir gün atlanırsa seri 1. güne döner; 7. günden sonra baştan. |
| `presence` | arkadaş | arkadaşlar | user: User | Çevrimiçi/oyunda değişimi. |
| `friends` | arkadaş | kullanıcı | friends: User[]<br>requests: User[] | Liste. |
| `friendRequestIn` | arkadaş | alıcı | from: User | Yeni istek. |
| `friendRequestResult` | arkadaş | iki taraf | user: User<br>accepted: bool | İstek sonucu. |
| `friendRemoved` | arkadaş | iki taraf | userId: string | Arkadaşlık bitti. |
| `leaderboard` | liderlik | kullanıcı | board: "weekly"|"season"|"friends"<br>rows: [{pos,user,rankIdx,lp,pts}]<br>mine: row?<br>resetsAt: int? | Liderlik. |
| `lobby` | lobi | abone | tables: Table[] | Tam liste. |
| `lobbyTable` | lobi | abone | table: Table | Masa eklendi/değişti. |
| `lobbyTableRemoved` | lobi | abone | id: string | Masa kapandı/başladı listeden düştü. |
| `tableState` | masa | masadakiler | table: Table | Koltuk/durum değişti. Table.katlama, Table.katlamaPartner (eşli+katlamalı değilse null) lobi kartı ve masa bilgisinde gösterilir. |
| `tableClosed` | masa | masadakiler | id: string<br>reason: "ownerLeft" | Masa kapandı → Masa kapandı diyaloğu. |
| `kicked` | bekleme | çıkarılan | tableId: string | Sahip seni çıkardı. |
| `countdown` | bekleme | masadakiler | n: int<br>startsIn: int<br>cancelled: bool? | Bekleme odası 3-2-1. |
| `seatConnection` | masa | masadakiler | seat: int<br>connected: bool<br>autoplay: bool? | Kopma/dönüş. autoplay:true = üst üste 2 zaman aşımından sonra koltuğu sunucu oynuyor (koltukta "Otomatik" rozeti). |
| `leftTable` | masa | kalkan | reason: "leave"|"disconnect"<br>toLobbyIn: int | "Masadan kalktın" tostu → 1300 ms sonra lobi. |
| `inviteIn` | davet | alıcı | inviteId: string<br>from: User<br>table: Table<br>expiresAt: int | Gelen davet. |
| `inviteExpired` | davet | alıcı | inviteId: string | Davet süresi doldu/geri çekildi. |
| `inviteResult` | davet | gönderen | inviteId: string<br>userId: string<br>result: "accepted"|"declined"|"expired"|"failed"<br>seat: int?<br>error: string? | Davet sonucu. |
| `rankedSearch` | dereceli | kullanıcı | mode: string<br>since: int<br>range: 1|2<br>filled: int<br>found: null | Arama durumu (koltuklar tek tek dolar). |
| `rankedFound` | dereceli | kullanıcı | found: {matchId,players:[{user,bot}],expiresAt,accepted:bool[4]} | Maç bulundu, 10 sn kabul. |
| `rankedAccepted` | dereceli | kullanıcı | index: int<br>accepted: bool[4] | Bir oyuncu kabul etti. |
| `rankedCancelled` | dereceli | kullanıcı | reason: "timeout"|"declined"|"cancel" | Arama/kabul bitti. |
| `rankedReady` | dereceli | masadakiler | table: Table<br>loadingMs: int | "Masa hazırlanıyor". |
| `rankChange` | dereceli | kullanıcı | before: {idx,lp}<br>after: {idx,lp}<br>delta: int<br>place: int<br>tierUp: bool<br>leagueUp: bool<br>down: bool<br>protectedLeft: int | Maç sonu LP. leagueUp → lig atlama ekranı, tierUp → kademe atlama, down → düşme tostu. |
| `matchStart` | masa | masadakiler | table: Table<br>countdownMs: int | Maç başlıyor; 3-2-1 sonra dağıtım. |
| `dealStart` | dağıtım | masadakiler | handNo: int<br>dealer: int<br>stacks: 15<br>perStack: 7<br>buildMs: int<br>humanDealer: bool | Tören başladı: balyalar diziliyor. |
| `dealStep` | dağıtım | masadakiler | step: "dice"|"indicator"|"extra"|"distribute"|"center"<br>dealer: int?<br>rollWaitMs: int?<br>stack: int?<br>level: int?<br>rolledLevel: int?<br>fakeSkip: bool?<br>indicator: string?<br>okey: {c,n}?<br>plan: [{seat,stacks:int[]}]?<br>stacks: int[]?<br>deck: int? | Tören adımı. indicator yalnız indicator adımında açıklanır. |
| `dealDie` | dağıtım | masadakiler | index: 0|1<br>value: 1..6<br>rollMs: int | Zar atıldı; değer bu anda açıklanır. |
| `handDealt` | dağıtım | her oyuncuya ayrı | handNo: int<br>rack: string[]|null<br>counts: int[4]<br>deck: int<br>indicator: string<br>okey: {c,n}<br>starter: int | Taşlar ıstakaya; yalnız kendi ıstakan (izleyiciye null). |
| `turn` | masa | masadakiler | seat: int<br>phase: "draw"|"play"<br>n: int<br>turnMs: int<br>deadline: int | Sıra başladı; tur sayacı deadline'a göre. |
| `legal` | masa | sıradaki oyuncu | legal: {draw,side,discard,open,undo,returnSide,add} | Düğme etkinlikleri. |
| `game` | masa | masadakiler (görünüm süzgeçli) | ev: EngineEvent | Kural motoru olayı: drew, sideReturned, openShort, opened, laid, added, okeySwapped, undone, discarded, finished, penalty, auto, draw, seatLeft, handEnd, matchEnd, turn. Alanlar logic/rules.js ev.push satırları. |
| `handWait` | masa | masadakiler | nextHandIn: int<br>nextAt: int<br>handNo: int<br>hands: int | El sonu: "Sonraki el · 15". |
| `readyState` | masa | masadakiler | ready: int[] | Hazır olan koltuklar. |
| `matchResult` | masa | masadakiler | ranking: Unit[]<br>totals: int[4]<br>history: array<br>pool: int (ödenen bahislerin toplamı)<br>rake: 0<br>payouts: int[4]<br>xpBonus: int[4]<br>lp: (int|null)[4]<br>weekly: int[4] | Maç sonu sıralama, ödeme, XP bonusu, LP, haftalık puan (R-59, R-91, R-93, R-95). |
| `chat` | sohbet | masadakiler | seat: int<br>user: string?<br>text: string?<br>quick: string?<br>bot: bool?<br>spectator: bool? | Sohbet satırı. quick = strings.csv anahtarı. |
| `gift` | hediye | masadakiler | from: int<br>to: int[]<br>gift: string<br>cost: int | Hediye uçuşu. |

## Hata kodları ve arayüz karşılığı

ui: `silent` görünmez (düğme zaten pasif / taş geri döner) · `inline` alan altı metin · `toast` üst tost 2,6 sn · `dialog` diyalog · `shake` sallanma + metin · `screen` ekrana dönüş · `resync` istemci resync gönderir. Metin anahtarı `err.<KOD>` (strings.csv, faz 4). Durum kimlikleri faz 3'te states.json'da kesinleşir; şimdilik bilinen kimlik ya da ekran adı.

| Kod | ui | Durum | Not |
|---|---|---|---|
| `E_BAD_MESSAGE` | silent | — | İstemci hatası; kayda yaz |
| `E_UNKNOWN_MESSAGE` | silent | — | Sürüm uyuşmazlığı; kayda yaz |
| `E_AUTH_REQUIRED` | screen | S02 | Giriş seçimine dön |
| `E_SESSION_EXPIRED` | screen | S02 |  |
| `E_EMAIL_INVALID` | inline | S04 | alan altı kırmızı |
| `E_PASS_SHORT` | inline | S04 |  |
| `E_NAME_INVALID` | inline | S04|S05|ProfilDüzenle |  |
| `E_EMAIL_EXISTS` | inline | S04 |  |
| `E_NAME_TAKEN` | inline | S04 |  |
| `E_ACCOUNT_UNKNOWN` | inline | S03 |  |
| `E_PASS_WRONG` | inline | S03 | "N deneme hakkın kaldı" (detail.left) |
| `E_ACCOUNT_LOCKED` | inline | S03 | "5 dakika sonra tekrar dene" (detail.until), "Şifremi unuttum" bağlantısı vurgulu |
| `E_CONFIRM_MISMATCH` | inline | Ayarlar.Hesap.Sil | Sil düğmesi pasif kalır |
| `E_IN_GAME` | toast | Ayarlar.Hesap |  |
| `E_STATUS_LONG` | inline | ProfilDüzenle |  |
| `E_FRAME_LOCKED` | toast | ProfilDüzenle | Galaksi çerçevesi seviye 30 |
| `E_SKU_UNKNOWN` | toast | Mağaza |  |
| `E_ALREADY_OWNED` | toast | Mağaza.Görünüm |  |
| `E_NO_GEMS` | toast | Mağaza.Görünüm | eksik miktar detail.missing |
| `E_NOT_OWNED` | toast | Mağaza.Görünüm |  |
| `E_DAILY_CLAIMED` | silent | GünlükÖdül | düğme zaten pasif |
| `E_QUEST_UNKNOWN` | silent | GünlükÖdül |  |
| `E_QUEST_INCOMPLETE` | silent | GünlükÖdül |  |
| `E_QUEST_CLAIMED` | silent | GünlükÖdül |  |
| `E_USER_UNKNOWN` | inline | ArkadaşEkle | "bulunamadı" |
| `E_ALREADY_FRIENDS` | inline | ArkadaşEkle | "zaten arkadaşsınız" |
| `E_REQUEST_PENDING` | inline | ArkadaşEkle |  |
| `E_REQUEST_UNKNOWN` | silent | Arkadaşlar.İstekler |  |
| `E_NOT_FRIENDS` | silent | Arkadaşlar |  |
| `E_ALREADY_SEATED` | toast | S09 |  |
| `E_TABLE_NAME` | inline | S10 | Oluştur pasif |
| `E_PIN_FORMAT` | inline | S10 |  |
| `E_TABLE_OPTIONS` | silent | S10 |  |
| `E_NO_CHIPS` | toast | S09|S10|Masa.Hediye | "Yetersiz bakiye · X çip eksik" (detail.missing) |
| `E_TABLE_CLOSED` | dialog | S09.MasaKapandı |  |
| `E_PIN_WRONG` | shake | S11 | PIN kutuları sallanır + hata |
| `E_TABLE_FULL` | dialog | S09.MasaDoldu |  |
| `E_TABLE_PLAYING` | toast | S09 | dereceli / botlara karşı masaya katılım |
| `E_CODE_FORMAT` | inline | S12 |  |
| `E_CODE_UNKNOWN` | inline | S12 |  |
| `E_SPECTATE_OFF` | toast | S09 |  |
| `E_NOT_SEATED` | silent | — |  |
| `E_NOT_OWNER` | silent | S13 |  |
| `E_STATUS` | silent | — |  |
| `E_SEAT_TAKEN` | toast | S13 |  |
| `E_SEAT_EMPTY` | silent | S13 |  |
| `E_TABLE_NOT_FULL` | silent | S13 | Başlat pasif |
| `E_USER_OFFLINE` | silent | S14 | satır düğmesi pasif |
| `E_USER_BUSY` | silent | S14 |  |
| `E_INVITE_EXPIRED` | toast | S14|DavetKartı |  |
| `E_ALREADY_QUEUED` | silent | Dereceli |  |
| `E_MODE` | silent | Dereceli |  |
| `E_PARTY_SIZE` | silent | Dereceli | HEMEN OYNA pasif |
| `E_PARTY_RANGE` | toast | Dereceli.Davet | ±1 lig dışı |
| `E_MATCH_EXPIRED` | toast | Dereceli.MaçBulundu |  |
| `E_NOT_QUEUED` | silent | Dereceli |  |
| `E_SPECTATOR` | silent | Masa |  |
| `E_DEALING` | silent | G02–G12 |  |
| `E_NOT_DEALER` | silent | G05 |  |
| `E_RACK_MISMATCH` | resync | — | resync gönder |
| `E_CHAT_OFF` | silent | T06 | sohbet paneli gizli |
| `E_CHAT_QUICK_ONLY` | silent | T06 | yazı alanı gizli |
| `E_CHAT_LENGTH` | inline | T06 |  |
| `E_CHAT_QUICK_UNKNOWN` | silent | T06 |  |
| `E_GIFTS_OFF` | silent | T07 | hediye düğmesi gizli |
| `E_GIFT_UNKNOWN` | silent | T07 |  |
| `E_GIFT_TARGET` | silent | T07 |  |
| `E_NOT_YOUR_TURN` | silent | Masa | taş ıstakaya döner |
| `E_PHASE` | silent | Masa | taş ıstakaya döner |
| `E_HAND_OVER` | silent | Masa |  |
| `E_TILE_NOT_IN_RACK` | resync | — |  |
| `E_SIDE_EMPTY` | silent | Masa |  |
| `E_SIDE_RETURNED` | toast | T38 | geri bırakılan taş tekrar alınamaz |
| `E_SIDE_DISCARD` | toast | T38 | soldan alınan taş atılamaz |
| `E_SIDE_MUST_OPEN` | toast | T39 | önce aç ya da geri bırak |
| `E_RETURN_INVALID` | silent | T39 |  |
| `E_DECK_EMPTY` | silent | Masa |  |
| `E_MELD_INVALID` | toast | T40 | geçersiz per kırmızı çerçeve |
| `E_MELD_MIX` | toast | T40 | seri/grup ile çift karışamaz |
| `E_MELD_NOT_FOUND` | resync | — |  |
| `E_ALREADY_OPENED` | silent | Masa |  |
| `E_NOT_OPENED` | toast | T41 | önce elini açmalısın |
| `E_OPEN_MODE` | toast | T41 | çift açan seri/grup açamaz (seri açan çift açabilir) |
| `E_LAST_TILE` | toast | T40 | ıstakada atılacak taş kalmalı |
| `E_ISLE_INVALID` | silent | Masa | taş ıstakaya döner |
| `E_ISLE_DISABLED` | toast | Masa | bu masada işleme kapalı |
| `E_SWAP_INVALID` | toast | T49 | okey kırmızı çerçeve, taş döner |
| `E_NO_UNDO` | silent | Masa |  |
| `E_LEAVE_INVALID` | silent | Masa |  |
| `E_ACTION` | silent | — |  |
| `E_SPEC_DUP` | silent | — | yalnız test |
| `E_RESET_CODE` | inline | S03.ŞifremiUnuttum | kod yanlış |
| `E_OAUTH_FAILED` | toast | S02 |  |
| `E_SEAT_NOT_BOT` | silent | — | yalnız motor |


### tableView (S2C) · v0.5.0 faz 2b
Her `commit` sonrasında (oyun olaylarından hemen sonra), her tur başında ve `handDealt` ile birlikte sunucu her insan oyuncuya **kendi** `GameView` görünümünü gönderir (`snapshot.game` ile aynı biçim; `turn.sideMust` eklendi). Gizli bilgi: `rack` yalnız alıcının ıstakası, rakipler yalnız `counts`. İstemci bir commit'e ait `game` olaylarını biriktirir, `tableView` gelince birlikte canlandırır.

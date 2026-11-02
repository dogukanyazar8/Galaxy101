# DEMO_LIST.md · yalnız prototipe ait sahte veri ve kısayollar · v0.5.0

Gerçek sunucuya taşınmaz. Kodda her biri `// DEMO:` ile işaretli.

| Kimlik | Ne | Yer | Gerçek sunucuda |
|---|---|---|---|
| DEMO-01 | Şifreli masa deneme PIN'i **1234** ("Akşam keyfi") | mockServer `DEMO_PIN`, seedLobby | masa sahibinin belirlediği PIN |
| DEMO-02 | Kodla katıl deneme kodu **GX-7Q2M** ("Özel oda") | mockServer `DEMO_CODE` | sunucu üretir |
| DEMO-03 | Sahte oyuncu dizini (Ayşe Y., Mehmet K., Selin A., Burak T., Elif C., Zeynep O., Can K., Deniz Y.) ve kodları | mockServer `DEMO_PLAYERS` | gerçek hesaplar |
| DEMO-04 | Varsayılan prototip oyuncusu "Doğukan Y." (125.400 çip, 320 elmas, Sv 32, Altın II 72 LP) — yalnız tasarım görüntüleri için | mockServer `DEMO_ME`, alanları boş `guest` çağrısı | gerçek başlangıç R-96 (10.000 / 25.000 çip, 20 elmas) |
| DEMO-05 | Lobide hazır 3 masa | mockServer `seedLobby` | gerçek masalar |
| DEMO-06 | Bot adları (Kemal B., Nur S., …) | mockServer `botSeat` | sunucu bot adı havuzu |
| DEMO-07 | Sahte oyuncular arkadaşlık isteğini 1,5 sn sonra kabul eder | `friendRequest` | gerçek yanıt |
| DEMO-08 | Sahte arkadaşlar daveti 1,2–2 sn içinde kabul eder | `inviteSend` | gerçek yanıt |
| DEMO-09 | Dereceli kuyrukta rakipler 1,2 sn arayla "bulunur" (bot), botlar 0,8 + 0,4·i sn'de kabul eder | `rankedQueue`, `rankedFound` | gerçek eşleştirme kuyruğu |
| DEMO-10 | Ödeme yok: mağaza paketi anında verilir | `shopBuy` | mağaza sağlayıcısı doğrulaması |
| DEMO-11 | Oturum anahtarı = kullanıcı kimliği (imzasız) | `hello` | imzalı belirteç |
| DEMO-12 | Sahte ağ gecikmeleri: bağlanma 850 ms, giriş 900 ms, kayıt 1100 ms, misafir 700 ms | `TIMINGS.connect`, spec/18 | gerçek ağ |
| DEMO-13 | Liderlik tablosundaki diğer oyuncular sahte dizinden | `DEMO_PLAYERS` | gerçek hesaplar (formül R-95 gerçek) |
| DEMO-14 | Şifreler düz metin tutulur | `register` | karma + tuz |
| DEMO-15 | Şifre sıfırlama kodu her zaman 123456, e-posta gönderilmez | `passwordReset` | e-postayla tek kullanımlık kod |
| DEMO-16 | Google girişi kimlik belirtecini doğrulamaz | `oauth` | Google kimlik doğrulaması |

Arayüzdeki hazır eller (`HAND_DEAL`, `HAND_OPEN`, `HAND_BOMB`, `AYSE_MELDS`) ve motorsuz test akışları `logic/scenarios/client-tests.json`'da belgelendi; gerçek oyunda kullanılmaz.

| Kimlik | Ne | Yer | Gerçek sunucuda |
|---|---|---|---|
| DEMO-17 | Sabit ilk el (`rig`): `createServer({ rig })` | mockServer `startHand`, `logic/scenarios/R43.json`, `R44.json` | yok; yalnız test/ekran görüntüsü |
| DEMO-18 | `window.G101Scenario` → masa sunucusu ilk eli sabitler; `scOpen` / `scDiscard` motor kimliğiyle oynar | `G101-Masa` `srvStart`, `g101-state.js` | yok |


## Oyun sayfası · prototip ↔ gerçek sunucu farkı
Gerçek maç (`Galaxy 101 - Gerçek Ana Oyun` → lobi → `G101-Masa`): masa kendi `mockServer` örneğine bağlanır (`guest` + `vsBotsStart`). Dağıtım (karma, dağıtan, zar, gösterge), bot hamleleri, bot sohbeti, tur süresi/zaman aşımı, el puanı ve sonraki el sunucudan gelir; istemci olayları canlandırır. Rakip taşları istemcide yüzü kapalı yer tutucudur, yalnız açılınca kimlik kazanır.

| Yer | Prototipte | Gerçek oyunda |
|---|---|---|
| G101-Masa `srvStart` | Her masa kendi `mockServer` örneğini açar; lobideki masa, oyuncu adları ve bahis masaya özellik (props) olarak geçer | Tek sunucu bağlantısı: menü → lobi → masa aynı oturum (`protocol.md` · hello, lobbySubscribe, tableJoin) |
| G101-Masa `startTurn` | İstemci, sunucu süresi bitmeden 0,4 sn önce otomatik oynar; geç kalırsa sunucu oynar (`syncMine`) | Kalıcı (tasarım gereği) |
| G101-Masa `startDeal` / `rollDice` / `nextHand` | `Math.random` yalnız motorsuz test yolunda (ayarlar → test senaryoları) | Kalıcı prototip aracı; hazır eller `logic/scenarios/client-tests.json`. Sunucu senaryoları seed'li (`R43`, `R44`) |
| G101-Masa `setupPlay` | Test akışları motorsuz; rakip 34 puan | Kalıcı prototip aracı; gerçek maçta puan motordan (R-52) |
| G101-Masa | Bot ikramı (2. koltuk çay) istemci zamanlayıcısıyla | Sunucu `gift` olayı (`protocol.json` giftSend → gift) |
| G101-Masa | Sonuç kartı ödül ve XP'yi istemcide R-91/R-93 ile aynı formülle hesaplar (havuz = 4 × bahis, +340/+60) | Değerler `handEnd` / `matchResult` olayından okunur |

## Durum yönlendirici (faz 3) · yalnız prototip/test
| Yer | Kısayol |
|---|---|
| `g101-state.js` | `?state=ID` bileşenlerin iç durumunu doğrudan kurar (`window.__g101.<Ad>.setState`, yöntem çağrıları). Gerçek oyunda bu durumlar sunucu olaylarıyla oluşur. |
| `g101-state.js` | Hesap yoksa `guest:state` adlı misafir hesabı açılır (10.000 çip). |
| `g101-state.js` | Masa durumları sabit "Test masası" (Ayşe K., Burak T., Zeynep A.) ile açılır; bahis cüzdandan düşer. |
| `G101-Masa` `go(key)` | Test akışları (open, isle, penOpen, penIslek, bomb, end, gift, chat, exit, net, settings) hazır eller ve motorsuz yol kullanır. |

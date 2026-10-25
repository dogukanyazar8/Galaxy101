# Galaxy 101 — Sunucu

ASP.NET Core (.NET 10) + PostgreSQL. Menü verisi (hesap, profil, ayarlar, ekonomi, günlük ödül, mağaza, arkadaşlar, liderlik) ve sunucu otoriteli çevrimiçi masa. Railway için tek örnek (single instance) olarak tasarlandı.

## Yapı

| Proje | İçerik |
|---|---|
| `src/Galaxy101.Server` | Minimal API uç noktaları, WebSocket, maç aktörleri, EF Core şeması ve migration'lar |
| `src/Galaxy101.Rules` | Kural motoru (`Packages/com.okey101.core/Runtime`) ve `BotPlanner.cs`, Unity kaynaklarından **kopyasız** derlenir |
| `src/Galaxy101.Table` | v4 maç akışı (`Packages/com.galaxy101.table/Runtime`), Unity kaynağından kopyasız, C# 9 |
| `../Shared/Galaxy101.Contracts` | netstandard2.1 DTO'lar; Unity istemcisi aynısını kullanacak |
| `tests/Galaxy101.Server.Tests` | xUnit; gerçek PostgreSQL 17 konteyneri (Testcontainers) |

Klasörler özellik bazlı: `Auth`, `Profile`, `Economy`, `Social`, `Realtime`, `Games`, `Data`, `Infrastructure`.

## İlkeler

- **Sunucu otoritedir.** İstemci bakiye, ödül, skor, sıra yazmaz; yalnız istek gönderir.
- **Bakiye tek yerden değişir:** `Ledger` — negatife düşmeyen koşullu `UPDATE` + değişmez işlem defteri satırı. `(hesap, idempotency anahtarı)` benzersiz; tekrar eden istek bir kez uygulanır.
- **Maç = tek iş parçacıklı aktör** (`MatchSession`). Kilit yok, hamleler tek sırayla. Hamle önce veritabanına yazılır, sonra geçerli olur. Yeniden başlatmada tohumlanmış dağıtım + hamle günlüğü yeniden oynatılır; aynı masa döner.
- **Gizli el:** her koltuğa yalnız kendi `PlayerViewProjector` görünümü gider.
- **Zaman aşımı / ayrılan / kopan koltuk:** çevrimdışı masadaki aynı `BotPlanner` oynar; plan geçersizse garantili yasal yedek hamle.
- Dış servis (Google Play, App Store, Google/Apple kimliği) yapılandırılmamışsa ilgili uç nokta `503 store_unavailable` / `401 invalid_token` döner; sessiz başarı yok.

## Yerel çalıştırma

```bash
docker run -d --name galaxy-pg -e POSTGRES_PASSWORD=dev -p 5432:5432 postgres:17-alpine
```

```bash
cd Server && dotnet run --project src/Galaxy101.Server --ConnectionStrings:Game "Host=localhost;Database=postgres;Username=postgres;Password=dev" --Auth:SigningKey "<base64, en az 32 bayt>"
```

Testler (Docker Desktop açık olmalı):

```bash
cd Server && dotnet test
```

Migration ekleme: `dotnet tool restore` sonra `dotnet ef migrations add <Ad> -p src/Galaxy101.Server -o Data/Migrations`. Uygulama açılışta bekleyen migration'ları uygular (`Database:MigrateOnStartup`).

## Railway

1. Projeye PostgreSQL ekle; servis değişkenlerinde `DATABASE_URL` referansı otomatik gelir.
2. Bu depoyu servis olarak bağla. `railway.json` Dockerfile'ı (`Server/Dockerfile`, bağlam depo kökü) ve `/health` kontrolünü tanımlar. Replika sayısı **1** kalmalı (maçlar bellekte, kalıcılık veritabanında).
3. Ortam değişkenleri:

| Değişken | Zorunlu | Not |
|---|---|---|
| `Auth__SigningKey` | evet | `openssl rand -base64 48` |
| `Auth__GoogleClientIds__0..n` | Google girişi için | Android/iOS/masaüstü OAuth istemci kimlikleri |
| `Auth__AppleClientIds__0` | Apple girişi için | Bundle / Services ID |
| `Store__GooglePackageName`, `Store__GoogleServiceAccountJson` | Play satın alımı için | Hizmet hesabına Play Console'da finans izni |
| `Store__AppleBundleId`, `Store__AppleRootCertificate` | App Store satın alımı için | Apple Root CA - G3, base64 DER |
| `Game__PublicBaseUrl`, `Game__PrivacyPolicyUrl`, `Game__PrivacyPolicyVersion` | önerilir | Paylaşım bağlantısı ve KVKK metni |

Tüm oyun ayarları (`Game__*`) `Infrastructure/Options.cs` içinde varsayılanlarıyla.

## HTTP API (özet)

Kimlik: `Authorization: Bearer <access>`. Hatalar `{ "code": "...", "message": "..." }`; kodlar `Contracts.ErrorCodes`.

| Uç nokta | Açıklama |
|---|---|
| `POST /v1/auth/guest` · `/external` · `/refresh` · `/link` · `/logout` | Misafir, Google/Apple girişi, token yenileme (dönüşümlü, tekrar kullanımda aile iptal), hesap bağlama |
| `POST /v1/realtime/ticket` | 30 sn'lik tek kullanımlık WebSocket bileti |
| `GET/PUT /v1/profile`, `GET /v1/profile/stats|badges|history`, `POST /v1/profile/privacy` | Profil, istatistik, rozet, geçmiş, KVKK onayı |
| `GET/PUT /v1/settings` (revision), `PUT /v1/settings/planet` | Bulut ayarları; çözünürlük cihazda kalır |
| `DELETE /v1/account` | Hesap silme (mağaza zorunluluğu) |
| `GET /v1/wallet`, `GET /v1/shop/catalog`, `POST /v1/shop/purchase`, `POST /v1/shop/store-purchase` | Cüzdan, katalog, gem ile alım, makbuzlu gerçek para alımı |
| `GET /v1/daily`, `POST /v1/daily/claim`, `POST /v1/daily/quests/{id}/claim` | Günlük seri ve görevler (sunucu günü, Europe/Istanbul) |
| `GET /v1/friends`, `POST /v1/friends/requests`, `.../{id}/accept|decline`, `DELETE /v1/friends/{id}`, `POST /v1/friends/{id}/invite` | Arkadaşlar, istek, özel odaya davet |
| `GET /v1/players/search?q=`, `GET /v1/share`, `GET /p/{kod}` | Arama (kod / ad öneki), paylaşım bağlantısı, açılış sayfası |
| `GET /v1/leaderboard/{weekly|season|friends}` | Liderlik |
| `GET /v1/matches/current` | Süren maç (masaya dön kartı) |
| `GET /health`, `GET /v1/info` | Sağlık, sürüm/sunucu saati |

## WebSocket (`/v1/ws?ticket=...`)

Çerçeve: `{ "type": "...", "id": "opsiyonel", "data": { ... } }`. `id` gönderilirse `ack { id, ok, code }` döner. Tipler `Contracts.MessageTypes`.

- İstemci → sunucu: `ping`, `queue.join {mode, stake}`, `queue.leave`, `room.create {stake, password}`, `room.join {code, password}`, `room.leave`, `match.command`, `match.sync`, `match.leave`, `match.emote`, `match.gift`.
- Sunucu → istemci: `presence`, `friend.request`, `friend.accepted`, `invite`, `wallet`, `queue.status`, `room.state`, `match.start`, `match.state` (koltuğa özel görünüm + son hamle + süre), `match.handEnd`, `match.end`, `session.replaced`.
- Hamle: `match.command { matchId, expectedVersion, kind: draw|return|shortOpening|commit, source, opening, plan }`. Kural ihlali `rule.<ErrorCode>` (ör. `rule.discardMustBeUsed`).
- Yeniden bağlanınca: yeni bilet → `/v1/ws` → `GET /v1/matches/current` → `match.sync`.

## Ürün kararları (yapılandırılabilir; 24 Ekim 2026 onaylandı)

Dereceli LP ve lig eşikleri dereceli mod açılırken yeniden ele alınacak; Botlara karşı sonra eklenecek. Maç geçmişi ve hesap hareketleri süresiz saklanır. Bağlantı kopması, hesap kilidi ve günlük seri satırları 25 Ekim 2026'da onaylandı.

| Konu | Varsayılan | Ayar |
|---|---|---|
| Maç uzunluğu | 4 el (her oyuncu bir kez başlar) | `Game:HandsPerMatch` |
| Hamle süresi / sonraki el | 30 sn / 15 sn | `Game:TurnSeconds`, `Game:NextHandSeconds` |
| Dağıtım töreni (eski yol bekler) | 21 686 ms | `Game:DealMilliseconds` |
| Otomatik oynatmaya geçiş | Art arda 2 zaman aşımı | `Game:TimeoutsBeforeAuto` |
| Pot | Tekli: kazanan alır; Eşli: kazanan takım böler; komisyon %0 | `Game:SoloPayout`, `Game:RakePercent` |
| Masadan kalkma | Bahis potta kalır, son sıra, ödül/XP yok; koltuğu sunucu oynar. Eşli: kalkanın yerine bot geçer, oyun devam eder; takım bu yüzden son sayılmaz, takım kazanırsa ödülün tamamını masada kalan ortak alır; kalkan bireysel olarak son sıradadır (25 Ekim 2026) | — |
| Eşitlik | Eşitlik bozucu yok: berabere kalanlar sırayı paylaşır (1, 1, 3), ödül/LP/haftalık puanı bölüşür (25 Ekim 2026 onaylandı) | `Games/Settlement.cs` |
| Bağlantı kopması | Maçta kopan oyuncu kalkmış sayılmaz: süresi normal işler, art arda 2 zaman aşımından sonra koltuğu sunucu oynar, maç bitene kadar dönebilir. Bekleme odasında kopan oyuncu odadan hemen çıkar | `Game:TimeoutsBeforeAuto` |
| Hesap kilidi | 3 yanlış şifre → 5 dk kilit; şifre sıfırlama kilidi kaldırır | `Game:PasswordAttempts`, `Game:PasswordLockSeconds` |
| Günlük seri | Kaçırılan gün seriyi 1. güne döndürür; 7. günden sonra baştan; gün Europe/Istanbul | `Economy/Daily.cs` |
| Dereceli | Bahissiz; aşağıdaki "Dereceli" bölümü | `Profile/Ranked.cs`, `Game:SeasonStart`, `Game:SeasonWeeks` |
| Haftalık puan | 30 / 15 / 5 / 1 (özel oda hariç) | `Game:WeeklyPoints` |
| XP | El kazanan 340, diğerleri 60; maç sonu sıra bonusu 500 / 200 / 80 / 0 (kalkan almaz) | `Game:XpHandWin`, `Game:XpHandPlayed`, `Game:XpPlace` |
| Başlangıç bakiyesi | Misafir 10.000 çip, e-posta/Google 25.000 çip; herkese 20 gem | `Game:StartingChips`, `Game:RegisteredStartingChips`, `Game:StartingGems` |
| Bahis seviyeleri | Tekli/Eşli 1K–100K, Katlamalı 10K–250K, Özel 0–100K | `Game:Stakes` |
| Ligler | Bronz, Gümüş, Altın, Elmas, Usta × III/II/I (kademe 100 LP) + Galaksi | `Profile/Ranked.cs` |
| Seviye | n → n+1 için 375·n XP (istemci `table_rules.json` aynı) | `Profile/Progression.cs` |
| İkram | Katalog fiyatı; masaya ikram = 3 kişilik ücret | `Economy/catalog.json` |

## Bilinen sınırlar

- **Tek örnek.** Maçlar, kuyruk, odalar, çevrimiçi durum bellekte. Yatay ölçek gerekirse maçları bir koordinatöre (ör. Redis + yapışkan yönlendirme) taşımak gerekir; şimdilik gereksiz.
- Dereceli eşleştirme LP aralığına bakmaz (aynı kuyrukta ilk gelen).
- KVKK aydınlatma metninin kendisi hukuki içerik; sunucu yalnız kabul edilen sürümü kaydeder.
- Gerçek Google/Apple kimlik ve mağaza bilgileri olmadan bu yollar yalnız birim testleriyle (kendi ürettiğimiz sertifika zinciri, sahte doğrulayıcı) doğrulandı.


## v4 tasarım paketi (protokol 0.4.0, 25 Ekim 2026)
- **Yeni istemci protokolü** aynı soketten: `hello`, `tableCreate` … `g*` adım hamleleri, `chatSend`, `giftSend`, `rankedQueue` (ad ve alanlar `pkg/protocol/protocol.json`; ack `{id, ok, code, detail, at, …}`). Hesap/ekonomi/sosyal mesajları HTTP uçlarında; hata gövdesinde paket kodu `error`. Tam eşleme `docs/v4/PROTOKOL_ESLEME.md`, istemci notu `docs/v4/hat-b/ISTEMCI_NOTU.md`.
- `src/Galaxy101.Server/V4/` — `Hub` (tek döngü, mantıksal saat) + `Store`; maç akışı `Packages/com.galaxy101.table` (`src/Galaxy101.Table` derler), kural motoru `Okey101.Core.V4`.
- Bahis oturunca düşer, maç öncesi çıkana iade; havuz = ödenen bahisler; beraberlik ve eşli sıralar paylaşılır (en yakın tama). Dağıtım töreni sunucuda 21 686 ms (bot dağıtıcı).
- Eski `table.*` / `match.*` / `queue.*` / `room.*` mesajları eski istemci ve eski kayıtlar için duruyor. v4 masaları yeniden başlatmada geri kurulmaz; tuttukları bahis açılışta iade edilir.

## v7 (25 Ekim 2026) — Katlamalı ve eşlide kalkma
- **Katlamalı** (`docs/rules/101-v1.md`): bir elde sonradan açan, kendinden önce açanı geçmek zorunda — seri 101 → önceki + 1, çift 5 → önceki + 1 (üst sınır yok); seri ve çift eşikleri bağımsız, el bitince sıfırlanır. Katlamalı kuyruğu (`mode: doubling`) ve `table.create {doubling: true}` masaları bu kuralla oynar. Eşli masada `partnerRaises` (varsayılan açık; "Ortağın açılışı eşiği yükseltsin") kapalıysa yalnız rakip takımın açılışları eşiği yükseltir.
- Tel: `match.state` → `rules.doubling`, `rules.partnerRaises`, her oyuncuda `openingValue` (açılış toplamı / çift sayısı); `TableSummary.partnerRaises`. İstemci aynı motorla eşiği gösterir (Aç kadranı "/ N", "Çift aç · n/N") ve eşik altında eksik açılış cezasına gider.
- **Eşlide kalkma:** takım artık son sayılmaz; kalkanın koltuğunu sunucu oynar, takım toplamıyla sıralanır. Takım kazanırsa takımın ödülünün tamamını masada kalan ortak alır; kalkan bahsini kaybeder, son sıradadır (eşlide 2.); haftalık puan, sıra bonusu ve kalktıktan sonraki ellerin XP'sini almaz. Tekli masada kalkan yine son sırada.

## v6 (24 Ekim 2026) — Dereceli
- **Ligler:** Bronz, Gümüş, Altın, Elmas, Usta; her biri III → II → I, kademe başına 100 LP (`b3` 0 … `m1` 1400). **Galaksi** (`gx`): 1500 LP ve üstü olup sezon tablosunda ilk 100'de olanlar; sırasıyla gösterilir.
- **LP:** Bronz–Altın 1. +30, 2. +10, 3. −10, 4. −20; Elmas ve üstü +25 / +5 / −12 / −22 (her oyuncu kendi liginin tablosuyla). LP 0'ın altına inmez. Yeni bir lige çıkınca 3 maç koruma: kayıp o ligin altına düşürmez.
- **Eşleştirme:** `queue.join {mode: ranked, stake: 0}`; dörtlü en fazla 1 lig farkla kurulur, en eski bekleyen 30 sn'yi geçince 2 lig. 5 sn'de bir yeniden bakılır.
- **Sezon:** `Game:SeasonStart` (Pazartesi, ev saat dilimi) itibarıyla `Game:SeasonWeeks` (8) haftalık sezonlar; ondan önce 1. sezon. Yeni sezonda oyuncunun ilk erişiminde (profil, kuyruk, maç sonu): geçen sezonun en iyi ligine göre ödül (Bronz 2.000 · Gümüş 5.000 · Altın 10.000 · Elmas 25.000 · Usta 50.000 · Galaksi 100.000 çip, bir kez), sonuç `season_results`'a yazılır, LP kademe başından 4 kademe aşağıya iner (Elmas II → Altın III), koruma biter.
- **Profil:** `progress` → `league`, `nextLeague`, `divisionLp` (0–99), `season`, `seasonEndsAt`, `seasonRank`, `bestLeague`, `protected`.
- Kilit sırası her yerde: günlük durum → istatistik → cüzdan.

## v5 (24 Ekim 2026) — İzle ve Oyuna katıl
- **İzle:** `table.watch {tableId | code, pin}` (masa izleyiciye açıksa; şifreli masada PIN). İzleyici `match.start {spectator: true}` alır ve maçı 0. koltuktan görür; **hiçbir elin taşı gönderilmez** (el ve özel çekiş boş), olaylar, sohbet ve emojiler gelir. Masa açılınca `match.sync`, çıkınca `match.leave`; soket kopunca izleme biter. Maç başına en çok 20 izleyici; izleyici hamle yapamaz.
- **Oyuna katıl:** oynanan masada `table.join {tableId, seat?}` sunucunun oynadığı bir koltuğu (botun ya da kalkan oyuncunun) devralır: bahis o an ödenir, koltuğun toplam puanıyla devam edilir, sıra ondaysa tam tur süresi başlar. Kalkan oyuncunun kaybı (son sıra, −bahis) o anda kaydedilir. Diğer koltuklara `match.seats` gider; lobi listesinde `openSeats`.
- Lobi, katılan ya da izleyen oyuncuya `table.state {closed, matchId, reason: joined | watch}` gönderir; istemci masayı maç başlamış gibi açar.
- **Ödeme düzeltmesi:** pot yalnız bahis yatıranlardan oluşur (botlar yatırmaz, almaz); botun payı aynı sıradaki insana (eşli ortak, eşitlik) geçer, yoksa dağıtılmaz. Önceden pot hep bahis × 4 sayılıyordu: botlu masada yatırılmamış çip ödeniyordu.
- **Kayıt sağlamlığı:** el ve hamle yazımı tekrarlanabilir (cevabı kaybolan yazma yeniden denenince anahtar hatasına takılmaz); `match_participants` satır anahtarı `id` (bir koltukta kalkan ve devralan iki satır).

## v4 (24 Ekim 2026) — hesap kurtarma ve oyuncu güvenliği
- **Şifremi unuttum:** `POST /v1/auth/password/forgot {email}` hesabın e-postasına 6 haneli kod gönderir (15 dk, 5 deneme, dakikada bir yeni kod; bilinmeyen e-postaya da aynı 204 cevabı). `POST /v1/auth/password/reset {email, code, password}` şifreyi değiştirir, kilidi kaldırır, hesabın bütün oturumlarını kapatır. Kodlar bellekte (tek örnek). E-posta: `Email:Host`, `Email:Port` (587), `Email:EnableSsl`, `Email:User`, `Email:Password`, `Email:From`. Yapılandırılmamışsa `503 email_unavailable`; Development ortamında kod loga yazılır.
- **Küfür filtresi:** masa sohbeti maskelenir (`***`); oyuncu adı ve durum mesajı `400 inappropriate` ile reddedilir; lobi masa adı maskelenir. Türkçe/İngilizce, harf benzeri rakam/simge, tekrarlanan ve aralıklı harfler yakalanır; kelime bütün olarak karşılaştırılır ("götürmek" temiz).
- **Engelle:** `POST/DELETE /v1/players/{id}/block`, `GET /v1/players/blocks`. Engellenenin masa sohbeti ve emojileri engelleyene gitmez (maç sırasında da anında); arkadaşlık biter; arkadaşlık isteği ve davet geçmez.
- **Şikayet:** `POST /v1/players/{id}/report {reason: chat|name|cheating|other, details?, matchId?}` — aynı oyuncu için günde bir, kişi başı günde 10.
- **Moderatör:** `X-Admin-Key` başlığı (`Admin:Key`, ≥ 32 karakter; boşsa uçlar 404): `GET /v1/admin/reports?open=`, `POST /v1/admin/reports/{id}/resolve`, `POST /v1/admin/accounts/{id}/ban {minutes, reason}` (oturumlar hemen düşer, maç terk edilir; giriş ve token yenileme `403 banned` + kalan saniye; refresh token harcanmaz, yasak bitince misafir de geri döner), `POST /v1/admin/accounts/{id}/unban`.
- **Arama dizini:** `pg_trgm` + `accounts.name` GIN trigram dizini (ad aramasında tablo taraması yok).

## v3 (23 Ekim 2026)
- **Kimlik:** `POST /v1/auth/register` (ad, e-posta, şifre ≥ 6, şartlar), `POST /v1/auth/login` (bilinmeyen e-posta 404, yanlış şifre 401 + kalan deneme, 3 yanlışta 423 kilit). Misafir girişi takma ad + avatar rengi alır. Başlangıç bakiyesi: misafir 10.000, e-posta/Google 25.000.
- **Klasik lobi (WebSocket):** `lobby.subscribe` → `lobby.tables` (canlı liste; özel masalar listelenmez); `table.create {options}` (Tekli/Eşli, ad, açık/özel, 4 haneli PIN, 3/5/7 el, 1K–100K bahis, katlamalı, 15/20/30/45 sn, izleyici, sohbet açık/hızlı/kapalı, hediye, işleme, ceza), `table.join {tableId|code GX-XXXX, pin, seat}`, `table.leave`, `table.addBot {seat}`, `table.kick {seat}`, `table.start` → `table.state` (bekleme odası, geri sayım: dolunca 5 sn, sahip Başlat 3 sn). Maç masanın koltuk düzeni, el sayısı, tur süresi, sohbet/hediye kurallarıyla başlar; bot koltukları bahis yatırmaz ve ödeme almaz. Liste oynanan masayı "Oynanıyor · N. el" gösterir, maç kapanınca düşer.
- **Davet:** `POST /v1/friends/{id}/invite {roomCode}` masa kodlarını da kabul eder; arkadaş ya da son 20 maçta birlikte oynanan oyuncu davet edilebilir. `GET /v1/players/recent` (Son oynadıkların).
- İzle ve Oyuna katıl v5'te eklendi. İşleme ve iki +101 cezası masanın seçeneğiyle motorda uygulanır (`RulesDto`).

# Galaxy 101 — Sunucu

ASP.NET Core (.NET 10) + PostgreSQL. Menü verisi (hesap, profil, ayarlar, ekonomi, günlük ödül, mağaza, arkadaşlar, liderlik) ve sunucu otoriteli çevrimiçi masa. Railway için tek örnek (single instance) olarak tasarlandı.

## Yapı

| Proje | İçerik |
|---|---|
| `src/Galaxy101.Server` | Minimal API uç noktaları, WebSocket, maç aktörleri, EF Core şeması ve migration'lar |
| `src/Galaxy101.Rules` | Kural motoru (`Packages/com.okey101.core/Runtime`) ve `BotPlanner.cs`, Unity kaynaklarından **kopyasız** derlenir |
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

## Varsayılan ürün kararları (yapılandırılabilir, onay bekliyor)

| Konu | Varsayılan | Ayar |
|---|---|---|
| Maç uzunluğu | 4 el (her oyuncu bir kez başlar) | `Game:HandsPerMatch` |
| Hamle süresi / sonraki el | 30 sn / 15 sn | `Game:TurnSeconds`, `Game:NextHandSeconds` |
| Otomatik oynatmaya geçiş | Art arda 2 zaman aşımı | `Game:TimeoutsBeforeAuto` |
| Pot | Tekli: kazanan alır; Eşli: kazanan takım böler; komisyon %0 | `Game:SoloPayout`, `Game:RakePercent` |
| Masadan kalkma | Bahis potta kalır, son sıra, ödül/XP yok; koltuğu sunucu oynar | — |
| Dereceli | Bahissiz; LP +24 / +8 / −8 / −16, 0'ın altına inmez | `Game:RankedLp` |
| Haftalık puan | 30 / 15 / 5 / 1 (özel oda hariç) | `Game:WeeklyPoints` |
| XP | El kazanan 340, diğerleri 60 (prototip değerleri) | `Game:XpHandWin`, `Game:XpHandPlayed` |
| Başlangıç bakiyesi | 10.000 çip, 20 gem | `Game:StartingChips`, `Game:StartingGems` |
| Bahis seviyeleri | Tekli/Eşli 1K–100K, Katlamalı 10K–250K, Özel 0–100K | `Game:Stakes` |
| Ligler | b2 0, b1 150, s2 300, s1 450, g2 600, g1 800, d2 1000, d1 1300 LP | `Profile/Progression.cs` |
| Seviye | n → n+1 için 375·n XP | `Profile/Progression.cs` |
| İkram | Katalog fiyatı; masaya ikram = 3 kişilik ücret | `Economy/catalog.json` |

## Bilinen sınırlar

- **Tek örnek.** Maçlar, kuyruk, odalar, çevrimiçi durum bellekte. Yatay ölçek gerekirse maçları bir koordinatöre (ör. Redis + yapışkan yönlendirme) taşımak gerekir; şimdilik gereksiz.
- **Katlamalı** kural farkı `docs/rules/101-v1.md`'de tanımlı değil; mod şimdilik yalnız bahis seviyesiyle ayrışır (çekirdek motorun çarpanları her modda aynı).
- Dereceli eşleştirme LP aralığına bakmaz (aynı kuyrukta ilk gelen).
- Ad araması `ILIKE` ile tablo taraması yapar; oyuncu sayısı büyürse `pg_trgm` dizini eklenmeli.
- KVKK aydınlatma metninin kendisi hukuki içerik; sunucu yalnız kabul edilen sürümü kaydeder.
- Gerçek Google/Apple kimlik ve mağaza bilgileri olmadan bu yollar yalnız birim testleriyle (kendi ürettiğimiz sertifika zinciri, sahte doğrulayıcı) doğrulandı.


## v3 (23 Ekim 2026)
- **Kimlik:** `POST /v1/auth/register` (ad, e-posta, şifre ≥ 6, şartlar), `POST /v1/auth/login` (bilinmeyen e-posta 404, yanlış şifre 401 + kalan deneme, 3 yanlışta 423 kilit). Misafir girişi takma ad + avatar rengi alır. Başlangıç bakiyesi: misafir 10.000, e-posta/Google 25.000.
- **Klasik lobi (WebSocket):** `lobby.subscribe` → `lobby.tables` (canlı liste; özel masalar listelenmez); `table.create {options}` (Tekli/Eşli, ad, açık/özel, 4 haneli PIN, 3/5/7 el, 1K–100K bahis, katlamalı, 15/20/30/45 sn, izleyici, sohbet açık/hızlı/kapalı, hediye, işleme, ceza), `table.join {tableId|code GX-XXXX, pin, seat}`, `table.leave`, `table.addBot {seat}`, `table.kick {seat}`, `table.start` → `table.state` (bekleme odası, geri sayım: dolunca 5 sn, sahip Başlat 3 sn). Maç masanın koltuk düzeni, el sayısı, tur süresi, sohbet/hediye kurallarıyla başlar; bot koltukları bahis yatırmaz ve ödeme almaz. Liste oynanan masayı "Oynanıyor · N. el" gösterir, maç kapanınca düşer.
- **Davet:** `POST /v1/friends/{id}/invite {roomCode}` masa kodlarını da kabul eder; arkadaş ya da son 20 maçta birlikte oynanan oyuncu davet edilebilir. `GET /v1/players/recent` (Son oynadıkların).
- Açık: izleyici modu (İzle) ve oynanan masaya katılma (Oyuna katıl) henüz yok; işleme/ceza seçenekleri kaydedilir, motor henüz uygulamaz.

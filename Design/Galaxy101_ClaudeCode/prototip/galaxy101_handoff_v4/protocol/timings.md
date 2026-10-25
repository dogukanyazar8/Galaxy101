# timings.md · Galaxy 101 zamanlama sözleşmesi · v0.4.0

Bütün sunucu süreleri `logic/mockServer.js` → `TIMINGS` (T) ile birebir aynı. Bot süreleri `logic/bots.js` → `TIMING`. İstemci animasyon süreleri prototipten (`G101-Masa.dc.html`, `table3d.js`) ölçüldü. Sunucu animasyonu beklemez; olayları kendi saatine göre gönderir. İstemci, sunucu takvimine sığacak şekilde canlandırır.

Saat: sunucu olaylarında `at` (sunucu ms). İstemci `ping` ile farkı ölçer, `deadline` / `nextAt` / `expiresAt` değerlerini yerel saate çevirir. Sayaçlar her zaman bu mutlak değerlerden hesaplanır, yerel geri sayımdan değil.

## 1 · Sunucu süreleri

| Kimlik | Süre | Ne zaman başlar | Bitince | Kaynak |
|---|---|---|---|---|
| `preDealCountdown` | 3000 ms | `matchStart` | ilk elin `dealStart` | prototip `countdown()` |
| `seatedToCountdown` | 900 ms | sahibi bot/demo olan masada 4. koltuk dolunca | bekleme odası geri sayımı | `join()` |
| `waitRoomCountdown` | 3000 ms (3 × 1000) | `tableStart` ya da otomatik | `matchStart` | `guestCountdown()` |
| `turnMs` | masa seçeneği 15 / 20 / 30 / 45 sn (vars. 30) | her `turn` | R-70 otomatik oyun (`game.auto`) | `startTurn()` |
| uyarı | `warn` ayarı (vars. 5 sn kala) | yalnız istemci | sayaç kırmızı + titreşim | istemci ayarı, sunucu bilmez |
| `handEndWait` | 15000 ms | `handEnd` | sonraki `dealStart`; tüm insanlar `gReady` gönderirse hemen | "Sonraki el · 15" |
| `inviteTtl` | 30000 ms | `inviteSend` | `inviteExpired` / `inviteResult:expired` | S14 metni |
| `rankedAccept` | 10000 ms | `rankedFound` | herkes kabul ettiyse `rankedReady`, yoksa aramaya dönüş | spec/27 |
| `rankedWiden` | 30000 ms | `rankedQueue` | eşleşme aralığı ±1 → ±2 lig | spec/27 |
| `rankedLoading` | 1800 ms | `rankedReady` | `matchStart` | prototip |
| `leaveToLobby` | 1300 ms | `leftTable` | istemci lobiye geçer | spec/20 |
| `connect` | 850 ms | yalnız prototip | — | DEMO |
| `autoplayAfterTimeouts` | 2 zaman aşımı | bağlantısı kopuk oyuncunun üst üste zaman aşımları | koltuğu sunucu oynar (`seatConnection.autoplay`) | Q-10 |
| `reconnectMatch` | sınırsız | maç sırasında kopma | oyuncu maç bitene dek dönebilir; kalktı sayılmaz | Q-10 |
| `reconnectWaitRoom` | 0 ms | bekleme odasında kopma | oyuncu hemen çıkarılır, bahis iade | Q-10 |
| `clientRetries` × `clientRetryMs` | 5 × 3000 ms | istemci bağlantıyı kaybedince | "Deneme n/5"; sonra "Lobiye dön" | Q-10, istemci |
| `loginLock` | 300 000 ms | 3. yanlış şifre | kilit kalkar; "Şifremi unuttum" ile şifre değişince hemen kalkar | Q-14 |
| gün değişimi | 00:00 Europe/Istanbul (UTC+3) | — | günlük ödül, görevler | Q-16 |
| hafta değişimi | pazartesi 00:00 Europe/Istanbul | — | haftalık liderlik sıfırlanır | Q-11 |

### Dağıtım töreni (G02–G12) — sunucu takvimi

Bot dağıtıcı ile toplam **21 686 ms** (`dealStart` → ilk `turn`). Önceki pakette sunucu 22 sn varsaymıştı; doğru değer budur. İnsan dağıtıcıda her zar için en fazla 15 000 ms bekleme eklenir.

| Adım | Olay | Başlangıç (bot dağıtıcı) | Süre | Hesap |
|---|---|---|---|---|
| Balyalar | `dealStart` | 0 | 2940 | 450 + 106 × 15 + 900 |
| Zar adımı | `dealStep:dice` | 2940 | 700 (insan: ≤ 15 000) | bot gecikmesi |
| 1. zar | `dealDie{index:0}` | 3640 | 1570 | 3D zar 1450 + 120 |
| zarlar arası | — | 5210 | 650 (insan: `gRollDice` ya da ≤ 15 000) | |
| 2. zar | `dealDie{index:1}` | 5860 | 1990 | 3D zar 420 hazırlık + 1450 + 120 |
| zar sonrası | — | 7850 | 1000 | |
| Gösterge | `dealStep:indicator` | 8850 | 2300 | |
| Fazla taş | `dealStep:extra` | 11 150 | 1300 | |
| Dağıtım | `dealStep:distribute` | 12 450 | 5100 | 250 + 4 × 620 + 4 × 480 + 450 |
| Orta | `dealStep:center` | 17 550 | 2000 | |
| Istakaya dizilme | `handDealt` (her oyuncuya kendi ıstakası) | 19 550 | 2136 | 80 + 22 × 48 + 1000 |
| İlk tur | `turn` | 21 686 | — | |

İnsan dağıtıcı: `dealStep:dice.rollWaitMs = 15000`; oyuncu `gRollDice` gönderirse zar hemen atılır. Bekleme dolarsa sunucu atar. İkinci zar için de aynı kural (DECISIONS D-23).

### Bot süreleri (sunucu)

| Eylem | Gecikme | Not |
|---|---|---|
| çek (deste / sol) | 600 ms | turun başından |
| aç / yere aç / işle / okey al | 430 ms | her biri ayrı |
| at (per sonrası) | 650 ms | |
| at (doğrudan) | 730 ms | |
| sohbet (atış sonrası) | %16 olasılık, hemen | `bot.1…bot.6` |
| oyuncu yazınca yanıt | %60 olasılık, 1400 ms | |
| hediyeye teşekkür | 1900 ms (roket 6300 ms) | |

## 2 · İstemci animasyonları ve çakışma kuralı

Kural adları: **sıra** = biten animasyondan sonra oynat · **yeniden hedefle** = yeni hedefe o anki konumdan yeni tween başlat (beklemeden) · **atla** = son kareye sıçra · **paralel** = birlikte oynar.

Genel ilke: oyun durumu her zaman son gelen olaydır. Animasyon durumun önünde ya da arkasında kalamaz; en geç sonraki olayın canlandırması başlarken tamamlanmış sayılır. `snapshot` gelirse her şey **atla**.

| Animasyon | Tetikleyici | İstemci süresi | Eğri | Yeni olay gelirse |
|---|---|---|---|---|
| 3-2-1 sayacı (`countPop`) | `matchStart`, `countdown` | 1000 ms / rakam | `countPop` | atla (rakam sunucudan) |
| Balya dizilişi | `dealStart` | 106 taş × 15 ms kademe, taş başına 580–860 ms (`deal` tween) | bezier yay | `dealStep:dice` gelirse kalan taşlar atla |
| Zar yuvarlanma | `dealDie` | 1450 ms (+420 ms hazırlık 2. zarda) | sıçrama 3 kademe | sıra (iki zar üst üste gelirse 2. zar 1. bitince) |
| Gösterge çevirme | `dealStep:indicator` | 1150 ms flip + balya düşmesi 420 + 45 ms × seviye | flip | `extra` gelirse atla |
| Fazla taş | `dealStep:extra` | 650 ms block | block | atla |
| Balyaları dağıtma | `dealStep:distribute` | 8 adım, 620 / 480 ms ara, balya içi 110 ms | block | `center` gelirse atla |
| Orta deste | `dealStep:center` | 600 ms + 850 ms (260 ms gecikme) | block | `handDealt` gelirse atla |
| Istakaya dizilme | `handDealt` | 80 + x × 48 ms kademe, taş başına deal tween | deal | `turn` gelirse atla |
| Taş çekme | `game.drew` | 420–850 ms (`0.42 + mesafe × 0.38`, en çok 0.85 s) | yay (arc ≤ 0.24) | yeniden hedefle |
| Istaka içi kaydırma | yerel sürükleme | 180–340 ms | doğrusal kayma | yeniden hedefle |
| Taş atma | `game.discarded` | 420–850 ms travel + `disc` efekti | yay | yeniden hedefle |
| Yere açma / işleme | `game.opened`, `game.laid`, `game.added` | 300–550 ms (`lay`) | düz | yeniden hedefle |
| Okey alma | `game.okeySwapped` | travel 420–850 ms × 2 (çapraz) | yay | yeniden hedefle |
| Geri topla / geri bırak | `game.undone`, `game.sideReturned` | travel 420–850 ms (T39: 300 ms) | yay | yeniden hedefle |
| Bitiş (okeyle bitiş "nuke") | `game.finished` | 1750 ms + `nukeFlash` 1300 ms | apex yay | sıra: `handEnd` paneli bitişten sonra açılır |
| Ceza rozeti | `game.penalty` | `popIn` 500 ms | `popIn` | paralel |
| Tur sayacı halkası | `turn` | `turnMs` doğrusal | linear | yeni `turn` gelince sıfırla |
| El sonu paneli | `game.handEnd` | `modalIn` 400 ms; `barFill` 15 s | modalIn | sıra (bitiş animasyonundan sonra) |
| Maç sonu | `matchResult` | `modalIn` 400 ms | | sıra (el sonu panelinden sonra) |
| LP çubuğu | `rankChange` | `barFill` 3 s | | sıra; lig/kademe atlama ekranı çubuktan sonra |
| Sohbet balonu | `chat` | `bubble` 2800 ms | | paralel; aynı koltukta yenisi eskisini değiştirir |
| Hediye uçuşu | `gift` | uçuş 1600 ms (roket 6300 ms dahil) | | paralel, sıra yok |
| Tost | hata `ack` | `toastIn` 300 ms, görünür 2600 ms | | yenisi eskisini değiştirir |
| Bakiye sayacı | `balance` | 600 ms sayma | | yeniden hedefle (son değere) |

Animasyon parametrelerinin keyframe düzeyinde tam dökümü: `anim/css.json` (arayüz), `anim/tween3d.json` (3D masa), `anim/easings.json` (eğriler).

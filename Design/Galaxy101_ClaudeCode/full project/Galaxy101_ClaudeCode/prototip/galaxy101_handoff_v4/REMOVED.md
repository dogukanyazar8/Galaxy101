# REMOVED.md · kaldırılan kurallar ve kısayollar · v0.5.0

Anılmamak kaldırılmak değildir; kaldırılan her şey burada.

## Kural motoru (faz 1)

| Kaldırılan | Eski yer | Yerine |
|---|---|---|
| Görülmemiş taş havuzu `poolTake` — çekilen, botların attığı ve işlediği taşlar o an havuzdan rastgele üretiliyordu | `G101-Masa.dc.html` · spec/22 | Gerçek deste ve gerçek ıstakalar (R-01, R-05) |
| `_gone` listesi (yığın 4'ü aşınca alttakiler "düşüyordu") | `G101-Masa.dc.html` | Motor tüm yığını tutar; arayüz yalnız üstteki 4 taşı çizer |
| Açmış rakibin el puanı sabit 34 | `endHand` | Gerçek kalan taş toplamı (R-52) |
| Sabit XP +340 / +60 | `endHand`, spec/17 §6 | Q-02 açık |
| "Botlar %60 işlek taş atar" (`_botIslek`) | `oppDiscard` | Bot atışı R-71 + bots.js |
| Botun %25 olasılıkla soldan alması | `oppDraw` | `bots.wantSide` |
| Kazananın bahis × 4 alması | `endHand` | Havuzun %95'i (R-91) |
| Sadece elden bitişte ×2 (spec/22) ve puan formülünde çarpanın iki kez uygulanması | spec/22 | Çarpımsal M (R-50…R-56) |
| spec/22'deki "yalnız 2 ceza" | spec/22 | spec/17'deki 5 ceza geri geldi (R-40…R-44) |
| Okey ataması için "eşitse ilk uygun renk" belirsizliği | spec/17 §2 | D-06 |
| Gösterge sahte okeyken sahte okeyin gösterge kalabilmesi (iki sahte üst üste) | `dealReveal` | D-03 |
| Elden bitişte 101 aranmaması | `bombInfo` | D-07 / Q-04 |
| Math.random (dağıtan, karıştırma, zar, bot kararları) | her yer | seed'li RNG (D-02) |
| Sarma 12-13-1 (seride 1'in 13'ten sonra gelmesi), okeyin 13'ün ardına konması, 11-12-13'e 1 işleme | rules.js, bots.js, `G101-Masa` meldInfo / extendSide / jokerFits | R-12 (sarma yok) |
| Tahta yerleşiminde 1'in 13. sütuna konması | `G101-Masa` layoutBoard (meldInfo end=14) | 1 her zaman 1. sütun |
| Elden bitişte "en az 15 taş" şartı (prototip bombInfo `all.length < 15`) | `G101-Masa` bombInfo | R-53 eşikli elden bitiş |
| Taş bittiğinde elin puanlanmaması (yalnız cezalar) | rules.js endHand | R-55 |
| Eşlide kalkan oyuncunun takımının son sayılması | rules.js ranking | R-60 |
| %5 kesinti | mockServer ECON.rake | R-91 kesinti yok |
| Seviye eğrisi 1000 + 200·n | mockServer | R-92 375·n |
| tests/build_vectors.js (vektör üreticisi; JSON dosyalarıyla çelişiyordu) | tests/ | tests/rules/*.json tek kaynak |

## Sunucu sınırı (faz 2)

| Kaldırılan | Eski yer | Yerine |
|---|---|---|
| Arayüzün durumu doğrudan değiştirmesi (setState ile bakiye, koltuk, el, puan) | tüm DC'ler | C2S niyeti → `logic/mockServer.js` → S2C olayı (protocol.json) |
| "Sunucu yok, mock" notu ve tahmine bırakılmış sunucu davranışı | spec/14, spec/20 | protocol.md, timings.md |
| Dağıtım töreninin istemcide zamanlanması (`this.at(...)` zinciri) ve istemcide zar üretimi | `G101-Masa` startDeal…dealFinish | Sunucu takvimi: dealStart / dealStep / dealDie / handDealt |
| İstemcide tur zamanlayıcısı ve `autoPlay` | `startTurn`, `autoPlay` | Sunucu `turn.deadline` + R-70 |
| Bekleme odası `_wsim` sahte oyuncu giriş/çıkış simülasyonu | `G101-Lobi` | Lobi masaları sunucuda (DEMO tohum), canlı güncellemeler lobbyTable |
| Eski spec'teki 22 sn dağıtım varsayımı | Claude Code sunucusu | 21 686 ms (D-24) |

İstemcinin bu sınıra bağlanması (DC'lerin mockServer üzerinden çalışması) faz 3'te prototip yeniden düzenlenirken yapılır.


## Durum kataloğu (faz 3)

| Kaldırılan durum | Neden |
|---|---|
| T33 · Okey rozeti masada | masada okey temsil rozeti yok; okeyin değeri perdeki konumundan çıkar (R-13) |
| T40 · Mesaj kataloğu | Mesaj kataloğu bir referans sayfası: Referans - Masa Ek Durumlar |
| T41 · Mesaj kuyruğu | Mesaj kuyruğu referans sayfasında; oyunda tek satırlık sistem mesajı + tost |
| T44 · El sonu puansız (çiftle bitiş) | Çiftle bitiş artık puanlanır (×2, R-50…R-56); "Bu el puanlanmadı" yok |
| T45 · Maç sonu · kazandın | Ayrı maç sonu ekranı yok; maç sonu lobide tost (dereceli: S16 sonuç) |
| T46 · Maç sonu · 2. sıra | Ayrı maç sonu ekranı yok (T45 ile aynı) |
| T48 · Okey alma ipucu | Okeye uzun basma ipucu yok |

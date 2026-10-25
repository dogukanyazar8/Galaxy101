# v4 · mimari kararlar

Kaynak görev: `Design/PROMPT_v4_OTONOM_GELISTIRME.md` §0.5. Paket: `Design/Galaxy101_ClaudeCode/prototip/galaxy101_handoff_v4/` (aşağıda `pkg/`). Her karar gerekçesiyle; uygulanınca "Durum" güncellenir.

| Kimlik | Karar | Durum |
|---|---|---|
| K-01 | Kural motoru: `rules.js` C#'a satır satır taşınır (yeni adım motoru), eski tur-bütün motor yalnız eski kayıtlar için kalır | karar · Hat A |
| K-02 | RNG: mulberry32 + `ShuffleVersion` sürümü | karar · Hat A |
| K-03 | Botlar: `bots.js` C#'a taşınır; `BotPlanner` yalnız eski motorda | karar · Hat A |
| K-04 | Protokol: mevcut taşıma korunur, `protocol.json` mesajları adım adım eşlenir | karar · Hat B |
| K-05 | 26 sunucu akışı xUnit entegrasyon testi | karar · Hat B |
| K-06 | Unity durum yönlendirici + ortak saat (D-60…D-62) | karar · Faz 2 |
| K-07 | Referans kareleri: duruma sabitlenmiş tema/dil URL'den ezilmez | uygulandı (Faz 0.3) |
| K-08 | Karşılaştırmada arka plan tuvali (yıldız, gezegen) maskelenir | karar · Faz 2 |

## K-01 · Kural motoru: `rules.js` → C# adım motoru

**Karar.** `Packages/com.okey101.core` paketinde `logic/rules.js` birebir (satır satır, aynı veri biçimi ve aynı olaylarla) C#'a taşınır: `Okey101.Core.V4` ad alanı, string taş kimlikleri (D-01), maç durumu (`newMatch` → `startHand` → `apply(action)` → olaylar), `legal`, `ranking`, `invariants`, `fromSpec`, `rigFromHands`, `autoDiscardTile`, `openNeed`. 106 vektör NUnit'te, vektör JSON'larını paketten okuyan genel bir koşucuyla (runner.js'nin C# karşılığı: yol erişimi, kısmi eşleşme, olay sırası) koşar. Mevcut `GameEngine` / `TurnEvaluator` / `TableEvaluator` silinmez: yalnız `xorshift32-fy-v1` ile kaydedilmiş maçların ve replay'lerin açılması için kalır (K-02).

**Neden uyarlama değil.** Varsayılan "mevcut motor korunur, vektörler adaptörle koşar" idi. Adaptörün ifade edemeyeceği model farkları, motorun yeniden yazılmasına eşdeğer:
1. *Tur modeli.* Mevcut motor bir turu tek komutla alır (`CommitTurnCommand(TurnPlan{actions, discard})`, `Packages/com.okey101.core/Runtime/Model/Commands.cs:68`); ara durum yoktur. Paket ve protokol adım adımdır: `gDraw`, `gOpen`, `gLay`, `gAdd`, `gSwap`, `gUndo`, `gReturnSide`, `gDiscard` (`pkg/protocol/protocol.json` c2s); her adım sunucuda doğrulanır ve diğer oyunculara `game.opened`/`laid`/`added`/… olayı gider. Vektörler her adımdan sonra ara durumu ve olayı denetler (örn. OPEN-01: açılıştan sonra `hand.opened.0 = 105`, sonra ikinci `open` → `E_ALREADY_OPENED`).
2. *Tur içi durum.* Geri topla anlık görüntüsü (R-34), bekleyen cezaların tur sonunda yazılması (D-18), geri bırakın turdaki bütün işlemleri geri alması (D-10), eksik açılışın ayrı komut değil başarısız bir `open` olması (R-23: +101, taşlar ıstakaya döner, aynı turda yeniden deneme) mevcut modelde yok ya da farklı.
3. *Maç düzeyi.* Vektörler maç durumunu da denetler (`totals`, `seats[k].left/bot`, `takeSeat`, `leave`, paylaşılan sıra `ranking`, `status`); mevcut motorda maç düzeyi uygulama/sunucu katmanında (`LocalSession`, `MatchSession`, `Settlement`).
4. *Taş kimliği.* D-01 string kimlik ve kanonik sıra; mevcut `TileId` sayısal.
Adaptörün vektöre özel kod yazmadan bunları kapatması, adaptörün içinde ikinci bir motor yazmak demek. Görev metni bu durumda satır satır taşımayı öngörüyor (§0.5).

**Sonuç.** Sunucu (Hat B) oyunu yeni motorla oynatır; istemci (Faz 2) adım protokolüne geçer. Eski motorun testleri yeşil kalır (değiştirilmez).

## K-02 · RNG ve dağıtım

mulberry32 (`uint`, `rngInt(n) = next() % n`), sıra D-02: dağıtan → Fisher–Yates (`j = rng % (i+1)`) → iki zar; balya/gösterge/fazla taş/deste R-02…R-05, D-03, D-04 birebir. Yeni kayıtların `ShuffleVersion` değeri `mulberry32-v4`, `RuleVersion` değeri `101-v4`. `xorshift32-fy-v1` / `101-v1` kayıtları eski motorla açılır; ikisi de tanınmayan sürümü reddeder (mevcut davranış: `DiagnosticsJson.cs:187`).

## K-03 · Botlar

`logic/bots.js` (D-20 zamanlama, D-21 politika, R-80, `bestMelds`, `wantSide`, `nextAction`, `simulate`) C#'a taşınır; SIM-01…03 vektörleri koşar. Sunucu botları, kalkanın yerine oynayan bot (R-60, D-31) ve "koltuğu sunucu oynar" (R-66, D-35, D-36) bunu kullanır. Otomatik oyun R-70 / R-71 motorun `timeout` eylemidir. Mevcut `BotPlanner` yalnız eski motorla oynanan kayıtlar için kalır; paketin tarif ettiği bir rolü yok.

## K-04 · Protokol

Mevcut taşıma korunur: HTTP uçları + tek WebSocket, zarf `{type, id, data}` (`Server/src/Galaxy101.Server/Realtime/Connection.cs:8`). Eşleme kuralı: protocol.json `t` → `type`, `rid` → `id`, alanlar → `data`, sunucu `ack` → mevcut yanıt çerçevesi, S2C `at` → `data.at`. Hesap/ekonomi/sosyal mesajları mevcut HTTP uçlarına eşlenebilir (aynı alanlar ve hata kodları). Oyun mesajları (`g*`) WebSocket'te adım adım. Her mesaj, alan ve hata kodu `docs/v4/PROTOKOL_ESLEME.md`'de; eksikler eklenir. Gizli bilgi kuralı (`protocol.json` `hiddenInfo`) sunucuda uygulanır ve testle doğrulanır.

## K-05 · Sunucu akışları

`tests/server.tests.js`'deki 26 akış (SRV-SC1, SRV-01…25) gerçek sunucuya karşı xUnit entegrasyon testi olur (`Server/tests/Galaxy101.Server.Tests/V4/`), sanal saat için `FakeTimeProvider`. DEMO kısayolları (DEMO-01…18) gerçek sunucuya girmez; akış bir DEMO değerine dayanıyorsa (örn. SRV-08 demo arkadaş kabulü) test aynı akışı gerçek iki istemciyle kurar ve farkı PROTOKOL_ESLEME'de yazar.

## K-06 · Unity durum yönlendirici

`g101-state.js` adımlarının karşılığı yalnız `UNITY_EDITOR || DEVELOPMENT_BUILD` derlemesinde: `-g101State=ID -g101Seed=42 -g101Theme=saturn -g101Lang=tr -g101Ft=<ms>`. Ortak saat `GameClock` (D-60) ve `System.Random(seed)` yerine paketle aynı mulberry32 akışı; `ft` anlamı D-61 (yola varınca ft ms sonra kanonik 100000+ft anında donma). UI Toolkit öğe adları `data-g101-id` ile aynı (D-62).

## K-07 · Referans kareleri (Faz 0.3)

Görevdeki URL şablonu `theme=saturn&lang=tr` içerir; ancak yönlendirici URL parametresini durumun kendi değerinin önüne koyar (`g101-state.js` `run()`: `if (st.theme && !patch.planet)`). 02–05 (tema) ve 90–97, T27 (EN) durumları bu yüzden URL'de o parametre olmadan açılır, kendi tema/dilleriyle çekilir. Diğer temalar ve EN varyantı ayrı klasörlerde (`artifacts/v4/ref/themes/<tema>/`, `artifacts/v4/ref/en/`). Betik: `tools/ref_capture.py`. Tarayıcı: yüklü Chrome (GPU açık); yazılım GL (SwiftShader) masa durumlarında yönlendiricinin 30 sn sınırını aşıyordu.

## K-08 · Karşılaştırma eşikleri

Referansın kendisi iki açılışta aynı kareyi vermiyor (EKSIKLER EK-001: yıldız/gezegen tuvali). Bu yüzden piksel farkı arka plan tuvali maskelenerek ölçülür; yerleşim dökümü (kutu ±3 px, renk, font, harf aralığı, köşe, gölge) birincil ölçüttür. Eşikler `docs/v4/DURUMLAR.md` başında.

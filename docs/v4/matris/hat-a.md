# Hat A · kural matrisi (RULES.md R-01…R-98, DECISIONS D-01…D-21, D-39…D-43)

Kaynak paket: `Design/Galaxy101_ClaudeCode/prototip/galaxy101_handoff_v4/` (aşağıda `pkg/`). Her satır: paketin dediği → eski motorda (v4 öncesi, `753287e`) ne vardı → durum → v4 motorundaki karşılığı ve testi.

Kısaltmalar:
- **Eski motor** yolları `Packages/com.okey101.core/Runtime/…` altında (`Rules/TableEvaluator.cs`, `Turns/TurnEvaluator.cs`, `Turns/GameEngine.cs`, `Turns/DealCeremony.cs`, `Rules/PenaltyEvaluator.cs`, `Rules/Penalties.cs`, `Rules/MeldValidator.cs`, `Scoring/ScoreCalculator.cs`, `Model/Tile.cs`, `Model/RuleOptions.cs`, `Random/SeededRandom.cs`). Sunucu yolları `Server/src/Galaxy101.Server/Games/…`.
- **v4**: `G101Rules.cs` = `Packages/com.okey101.core/Runtime/V4/G101Rules.cs`, `G101Bots.cs` = `…/V4/G101Bots.cs`, `V4Replay.cs` = `Assets/Okey101/Application/V4Replay.cs`.
- **Testler**: büyük harfli kimlikler (`DEAL-01` …) paket vektörleridir; NUnit'te aynı adla koşar (`Packages/com.okey101.core/Tests/Editor/V4/V4VectorTests.cs`). Pakette iki kez kullanılan kimliklerin ikincisi `MELD-19~2`, `MELD-20~2`, `FIN-09~2` adıyla koşar (EK-101). `Parity` = `V4ParityTests` (72 bot maçı, rules.js ile olay akışı ve son durum özeti aynı). `V4EngineTests`, `V4ReplayTests` ayrıca.
- **Durum**: *var* = eski motor paketle aynı sonucu veriyordu; *farklı* = eski motor başka davranıyordu; *eksik* = eski motorda yoktu; *pakette yok* = kural paketçe geçersiz sayılmış ya da motor kuralı değil. Satır numaraları `753287e` içindir (eski motor değişmedi).

## Kurallar (RULES.md)

| Kimlik | Pakette ne diyor (kaynak) | Projede ne vardı (dosya:satır, eski motor) | Durum | v4 sonrası (dosya, test) |
|---|---|---|---|---|
| R-01 | 106 taş, her kimlik oyunda tam bir kez (RULES §1) | Sayısal `TileId` 0–105 (`Model/Tile.cs:8`, `:57-60`); sahiplik denetimi `Turns/StateInvariant.cs` | var (kimlik biçimi farklı, D-01) | `G101Rules.AllTiles`, `Invariants` · COPY-01, COPY-02, SCN-03, her vektörde `invariants` |
| R-02 | Dağıtan ilk elde RNG 0–3, sonra `(D+1)%4`; Fisher–Yates `j = rng % (i+1)` | Önce karıştırma, sonra dağıtan (`Turns/DealCeremony.cs:35`, `:38`); xorshift32 + ret örnekleme (`Random/SeededRandom.cs:7-29`); sonraki elin dağıtanı oturumdan gelir (`Turns/RoundFactory.cs:11`) | farklı | `DealHand` (`G101Rules.cs:154`) · DEAL-01, TURN-02, `V4EngineTests.TheDealOrderIsDealerThenShuffleThenDice`, `TheNextHandDealerIsTheNextSeatAndNeedsNoRng` |
| R-03 | 15 balya × 7 + fazla taş; gösterge balyası `a−1`, seviye `b−1`; fazla taş `(a−1+1)%15` | `DealCeremony.cs:40-60` aynı | var | `DealHand` · DEAL-02, DEAL-04 |
| R-04 | Sahte okey gösterge olamaz: üstteki, yoksa alttaki; okey = renk, `n%13+1` | `DealCeremony.cs:46-55`; `Model/Tile.cs:61-65` | var | `DealHand` (`G101Rules.cs:174`), `OkeyFromIndicator` · DEAL-02, DEAL-03 |
| R-05 | `S(k)` dağıtımı; deste = gösterge balyasının kalanı + büyük indeksli + küçük indeksli balya; `deck[0]` ilk çekilen | Balya sırası aynı ama taşlar ıstakaya alttan üste eklenir (`DealCeremony.cs:62-69`); deste `S(13)` üstten, `S(14)`, sonra gösterge balyası (`:71-74`) | farklı (deste sırası, D-04) | `DealHand` (`G101Rules.cs:192`) · DEAL-01, DEAL-04 |
| R-06 | 22 taşlı oyuncu ilk turda çekmeden oynar ve atar | `Turns/RoundFactory.cs:22` (`PlanningTurn`, `IsInitialTurn`) | var | `NewTurn(starter,'play',1)` (`G101Rules.cs:203`) · DEAL-05 |
| R-10 | Seri: aynı renk, ardışık, 3–13; değer toplam | `Rules/MeldValidator.cs:15`, `:46-61` | var | `AnalyzeMeld` · MELD-01, MELD-02, MELD-10, MELD-18 |
| R-11 | Grup: aynı sayı, farklı renk, 3–4; değer sayı × adet | `MeldValidator.cs:16`, `:63-74` | var | `AnalyzeMeld` · MELD-04…07 |
| R-12 | Sarma yok (12-13-1, okey 13'ün ardına yok); 1 her zaman 1 | `IsRun` max−min+1 sarmaya izin vermez (`MeldValidator.cs:46-61`) | var | `AnalyzeMeld` (`G101Rules.cs:110`) · MELD-08, MELD-09, MELD-11, MELD-19~2, MELD-20~2, ISLE-07 |
| R-13 | Okey her taşın yerine geçer, değeri konumdan; grupta eksik renk R,B,Y,K sırasıyla; sahte okey sıradan taş | Okeyin temsil ettiği yüzü istemci bildirir (`PlacedTile.RepresentedFace`, `MeldValidator.cs:27-30`); sahte okey = okey yüzü (`Model/Tile.cs:69-75`) | farklı (temsil istemcinin seçimi; D-06 yok) | `AnalyzeMeld` repr · MELD-02, MELD-03, MELD-05, MELD-11, MELD-12, MELD-19, ISLE-04, PEN-04 |
| R-14 | Perde en az bir gerçek (okey olmayan) taş | Denetim yok: yalnız okeylerden oluşan per, yüzler tutarlıysa geçer (`MeldValidator.cs:22-43`) | eksik | `AnalyzeMeld` (`G101Rules.cs:105`) · MELD-17 |
| R-15 | Çift: aynı renk-sayı; okey her taşla, iki okey / iki sahte okey | `MeldValidator.cs:38-40` (bildirilen yüzler eşit) | var | `AnalyzeMeld` pair · MELD-13…16 |
| R-16 | Tür istemciden, sunucu doğrular; sıralama ve botlar en yüksek toplamlı kümeyi seçer | Tür `MeldProposal.Kind` istemciden; bot ayrı arama (`Assets/Okey101/Bots/BotPlanner.cs:114`) | var | `GameAction.Melds[].Kind`, `G101Bots.BestMelds` · MELD-20 |
| R-20 | Seri açma ≥ eşik (101; katlamalıda R-64); açılış puanı sabit | `Rules/TableEvaluator.cs:20-37`, `:139-171` (değer sabit `:167-168`) | var | `DoOpen`, `OpenNeed` (`G101Rules.cs:622`, `:637`) · OPEN-01, KAT-02 |
| R-21 | Çift açma ≥ 5 (katlamalıda R-64) | `TableEvaluator.cs:29`, `:159` | var | `DoOpen` · OPEN-03, KAT-04 |
| R-22 | Bir açılışta seri ve çift karışmaz (`E_MELD_MIX`); geçersiz per reddedilir, ceza yok | `TableEvaluator.cs:150-153` (`InvalidGroup`) | var (hata kodu farklı) | `CheckMelds` · OPEN-05, OPEN-06 |
| R-23 | Eksik açılış kabul edilir: +101, taşlar ıstakada, açılmamış sayılır, aynı turda yeniden denenebilir | Eşik altı açış reddedilir (`TableEvaluator.cs:160`); ceza ayrı `ShortOpeningCommand` ile +101 (`Turns/GameEngine.cs:102-114`) | farklı (biçim: tek `open` hamlesi + `openShort` olayı) | `DoOpen` (`G101Rules.cs:649-656`) · OPEN-02, OPEN-04, KAT-01, FIN-11, FIN-13 |
| R-24 | Seri açan sonradan seri/grup ve çift yere açar (açtığı turda da); çift açan yalnız çift (`E_OPEN_MODE`) | Seri açan çifti ancak masada çift açmış biri varsa koyar (`TableEvaluator.cs:178`, `:191-198`); çift açan seri koyamaz (`:176-177`) | farklı | `DoLay` (`G101Rules.cs:672`) · OPEN-03, OPEN-09, OPEN-10, OPEN-11 |
| R-25 | İşle: açmış oyuncu, çektikten sonra, herhangi bir seri/gruba (çift hariç); okey serinin iki ucuna, uç istemciden | `Extend` (`TableEvaluator.cs:200-240`); çift açana tur başına 2 taş sınırı (`:90`, `:275-281`) | farklı (çift açan sınırı, D-08) | `DoAdd`, `AddSides` (`G101Rules.cs:689`) · ISLE-01…09, FIN-06 |
| R-26 | Okey alma: okeyin temsil ettiği gerçek taşla değiştir, okey ıstakaya; koyan başkasıysa ona +101 | `Swap` (`TableEvaluator.cs:242-273`); çiftteki okey de alınabiliyordu (`:257`) | farklı (pakette çiftten okey alınmaz) | `DoSwap`, `SwapFits` (`G101Rules.cs:712`) · OKEY-01…03, SCN-02 |
| R-30 | Tur: çek (deste ya da `(k+3)%4`'ün son attığı), oyna, at | `GameEngine.cs:46-81` (önceki koltuk `:65`) | var | `DoDraw` (`G101Rules.cs:517`) · TURN-01, SIDE-05 |
| R-31 | Açmamış oyuncu soldan aldığı taşla aynı turda açmak zorunda (`E_SIDE_MUST_OPEN`); açmış oyuncu kullanmak zorunda değil | Soldan alınan taş her oyuncu için kullanılmalıydı (`Turns/TurnEvaluator.cs:26-29`); açmamışsa ilk açılışta (`:30-31`) | farklı (açmış oyuncu da zorundaydı) | `DoDraw` sideMust, `DoOpen`/`DoDiscard` (`G101Rules.cs:646`, `:760`) · SIDE-01, SIDE-03, SIDE-04 |
| R-32 | Soldan alınan taş aynı turda atılamaz; geri bırakınca turdaki her işlem geri alınır | `TurnEvaluator.cs:28`; `ReturnDiscard` (`GameEngine.cs:83-99`), tur atomik olduğundan geri alınacak işlem yok | var | `DoReturnSide` (`G101Rules.cs:547`), `E_SIDE_DISCARD` · SIDE-01…03, AUTO-02 |
| R-33 | Geri bırakılan taş aynı turda yeniden alınamaz | `GameEngine.cs:50-51` (`PreviousDiscardReturned`) | var | `E_SIDE_RETURNED` · SIDE-02 |
| R-34 | Geri topla: ilk değiştirici işlemden önceki durum; bekleyen cezalar silinir; eksik açılış cezası kalır | Motor turu tek komutla alır (`Model/Commands.cs:68`); geri alma istemci taslağında (`Assets/Okey101/Application/TurnDraft.cs`) | farklı (yapısal: motor adım adım değil) | `Snap`/`Restore`/`DoUndo` (`G101Rules.cs:740`) · OPEN-08, OKEY-03, SIDE-04 |
| R-35 | Açma/yere açma/işlemeden sonra ıstakada ≥1 taş (`E_LAST_TILE`) | `TurnEvaluator.cs:18` (`LastTileRequired`) | var | `CheckMelds` (`G101Rules.cs:595`), `DoAdd` · OPEN-07, ISLE-08 |
| R-36 | Bitiş: açmış oyuncunun son taşını atması; aynı turda işleyip bitirmek serbest | `TurnEvaluator.cs:35-37`; `GameEngine.cs:124-129` | var | `DoDiscard` → `EndHand` · FIN-06, FIN-09 |
| R-37 | Bitiş taşı masanın ortasına, yığına girmez; işlek/okey cezası doğurmaz | Ceza yok (`Rules/PenaltyEvaluator.cs:16`) ama taş atanın yığınına eklenir (`TurnEvaluator.cs:66-67`) | farklı (bitiş taşı yığına giriyordu) | `finishTile` (`DoDiscard`) · FIN-02 |
| R-40 | Eksik açılış +101, açana, anında | `ShortOpeningCommand` +101 (`GameEngine.cs:102-114`, `Rules/Penalties.cs:9`) | var (biçim R-23) | `P_OPEN_SHORT` · OPEN-02, OPEN-04 |
| R-41 | İşlek taş: masadaki bir seri/gruba eklenebilen ya da bir okeyin yerine geçebilen taş, +101 atana | Yalnız pere eklenebilme denetlenir, okeyin yerine geçme yok; atan açmamışsa ya da çift açıp 2 taş sınırına ulaştıysa ceza yok (`PenaltyEvaluator.cs:21-32`) | farklı | `IsIslek`, `DoDiscard` (`G101Rules.cs:773`) · PEN-01, PEN-02 |
| R-42 | Okey atma +101 (bitiş hariç); okey atışında işlek yazılmaz; sahte okey okey değil | `PenaltyEvaluator.cs:16`, `:107` | var | `DoDiscard` (`G101Rules.cs:772`) · PEN-03, PEN-04, FIN-02 |
| R-43 | Açtıran taş: soldan alınan taşla açan açmamış oyuncu → atana taşın perdeki değeri × 10, açanın turu bitince | `TurnEvaluator.cs:53-65`; okeyse 0 (`Penalties.cs:13-14`) | farklı (okey değeri, D-16) | `DoOpen` bekleyen `P_OPENED_WITH`, `CommitPending` · SIDE-01, SCN-01 |
| R-44 | Masadaki okeyi alınan: okeyi masaya koyana +101 (alan kendisi değilse), alanın turu bitince | +101 perin sahibine yazılır (`TableEvaluator.cs:263-271`), okeyi işleyerek koyan başkasıysa da | farklı (kime: perin sahibi ↔ okeyi koyan `placedBy`) | `DoSwap` (`G101Rules.cs:734`) · OKEY-01, OKEY-03, SCN-02 |
| R-50 | El puanı = temel × M + ceza; M = elden × okey × çift (≤ ×8); bitiren −101 | `Scoring/ScoreCalculator.cs:27-45` | var | `EndHand` (`G101Rules.cs:825`) · FIN-01…05, FIN-10, FIN-12 |
| R-51 | Açmamış rakip `202 × M + ceza` | `ScoreCalculator.cs:36` | var | `EndHand` · FIN-01, FIN-02 |
| R-52 | Açmış rakip `kalan × M × (çiftse 2) + ceza`; okey 101, sahte okey okeyin sayısı | `ScoreCalculator.cs:39-40`; `GameEngine.cs:171-177` | var | `HandValueOf` · FIN-01, FIN-09~2 |
| R-53 | Elden bitiş: hiç açmamış oyuncu aynı turda açıp son taşı atar; eşik aranır (katlamalıda yükselmiş); ×2 | `FinishesFromHand`: en az 15 taş, eşik aranmaz (`TableEvaluator.cs:13`, `:158-160`; `docs/rules/101-v1.md:168`) | farklı | `DoOpen` eşiği (`G101Rules.cs:649`), `EndHand` elden (`:833`) · FIN-03, FIN-05, FIN-10…13 |
| R-54 | Okeyle bitiş ×2; çift açmış bitiren ×2 | `ScoreCalculator.cs:30-31`; ayrıca herkes çift açtıysa el puansız iptal (`GameEngine.cs:132-134`, `ScoreCalculator.cs:24-25`) | farklı (`AllPairs` iptali pakette yok, `REMOVED.md` T44) | `EndHand` (`G101Rules.cs:834-835`) · FIN-02, FIN-04, FIN-05, FIN-12 |
| R-55 | Taş bitti: deste boşken atış yapılır, el bitmezse biter; seri N, çift 2N, açmamış 202, ceza eklenir; −101 ve çarpan yok | `StockExhausted` (`GameEngine.cs:135-136`), aynı formül M = 1 (`ScoreCalculator.cs:27-45`) | var | `EndHand(-1)` (`G101Rules.cs:775`) · FIN-07, FIN-09~2 |
| R-56 | Çift açmış, bitirmemiş oyuncunun puanı ayrıca ×2 | `ScoreCalculator.cs:40` | var | `pairX` · FIN-04, FIN-05, FIN-10 |
| R-57 | Eşli: ortaklar 0–2, 1–3; bitirenin ortağı 0 (+ kendi cezası); takım = iki ortağın toplamı | Çekirdekte eşli yok, ortak normal puanlanır (`ScoreCalculator.cs:12-46`); sunucu takımı toplar (`MatchSession.cs:672`) | farklı (ortağa 0 yok) | `EndHand` (`G101Rules.cs:840`), `teamScore` · FIN-08, MATCH-03 |
| R-58 | Cezalar çarpılmaz | `ScoreCalculator.cs:43-44` | var | `EndHand` · FIN-03 |
| R-59 | Maç N el; düşük toplam kazanır; eşitlik aynı sırayı paylaşır (1,1,3,4); ödül/LP/haftalık/XP paylaşımı D-40; eşlide takım toplamı | Sıra ve paylaşım sunucuda (`Settlement.cs:37-53`) | var (sunucu) | `G101Rules.Ranking` · MATCH-01…03 · ödül/LP/XP paylaşımı **Hat B** |
| R-60 | Kalkan bahsini kaybeder, yerine bot; tekli masada son (önce kalkan daha geride); eşlide takım son sayılmaz | `Settlement.cs:13`, `:26`, `:53` | var (sunucu) | `DoLeave`, `Ranking` · LEAVE-01, LEAVE-02 · bahis/ödül/XP **Hat B** |
| R-61 | İşleme kapalı: işle ve okey alma yapılamaz (`E_ISLE_DISABLED`), işlek cezası yok | `RuleOptions.Processing` (`Model/RuleOptions.cs:15`); kapalıyken yalnız `Extend` reddedilir, okey alma açık kalır (`TableEvaluator.cs:133-134`); işlek yok (`PenaltyEvaluator.cs:21`) | farklı (okey alma kapanmıyordu) | `DoAdd`, `DoSwap` · ISLE-10, PEN-06 |
| R-62 | Ceza kuralları kapalı: R-40…R-44'ün hiçbiri yazılmaz; eksik açılış yine geri alınır | Yalnız okey atma ve işlek cezası ayrı ayrı kapanır (`RuleOptions.cs:13-14`); eksik açılış, açtıran taş, okey alma kapanmaz | farklı | `AddPenalty` (`G101Rules.cs:370`) · PEN-05, PEN-06 |
| R-63 | İzleyici ve sohbet seçenekleri | — | pakette yok (motor kuralı değil) | **Hat B** / Faz 2 |
| R-64 | Katlamalı: seri önceki + 1, çift önceki + 1, bağımsız, el sonunda sıfır; eşlide "ortağın açılışı yükseltsin" (vars. açık) | `TableEvaluator.cs:20-37` (`OpeningBar`), `RuleOptions.Doubling`/`PartnerOpeningsRaise` | var (elden bitişte eşik yoktu, R-53) | `OpenNeed` (`G101Rules.cs:622`) · KAT-01…11, FIN-13, SIM-03 |
| R-65 | Devralma: koltuk yeni oyuncuya, koltuğun toplamıyla devam, "kalktı" kalkar | Sunucu koltuk durumu (`MatchSession.cs:227`) | var (sunucu) | `DoTakeSeat` (`G101Rules.cs:804`) · TAKE-01 · bahis **Hat B** |
| R-66 | Bağlantı kopması kalkma değil; 2 zaman aşımından sonra sunucu oynar | `MatchSession.cs:371`, `Infrastructure/Options.cs:57` | var (sunucu) | **Hat B** (bot hamlesi `G101Bots.NextAction` hazır) |
| R-70 | Süre dolunca: çekme aşamasındaysa çek; soldan alınıp açılamamış taşı geri bırak, desteden çek; R-71 taşını at; yapılmış açılış/işleme korunur | Sunucu botu turun tamamını yeniden planlar (`MatchSession.cs:395-420`), `AutoPlayDelay` 1 sn (`:25`) | farklı | `DoTimeout` (`G101Rules.cs:790`) · AUTO-01, AUTO-02, DEAL-05 · zamanlayıcı **Hat B** |
| R-71 | Otomatik atılacak taş: soldan alınan değil; okey/işlek olmayan; en az bağlantılı; eşitse sayısı büyük; eşitse kimliği küçük | Yok (atılacak taşı `BotPlanner` seçer) | eksik | `AutoDiscardTile` (`G101Rules.cs:438`) · AUTO-01, AUTO-03 |
| R-80 | Botlar aynı motoru kullanır, yalnız açık bilgi ve kendi ıstakası | `BotPlanner` `PlayerView` ile (`BotPlanner.cs:28`, `:51`) ama arama tabanlı başka bir politika | farklı (politika, D-21) | `G101Bots` · SIM-01, SIM-02, SIM-03, COPY-02, SCN-01, SCN-02, Parity (72 maç) |
| R-90 | Bahis oturunca düşülür; bekleme odasından çıkana iade | — | — | **Hat B** |
| R-91 | Havuz = ödenen bahisler; kesinti yok; eşitlikte eşit bölüşüm; eşlide masada kalan insan ortaklar | — | — | **Hat B** |
| R-92 | Seviye n → n+1: 375·n XP | — | — | **Hat B** |
| R-93 | XP: bitiren +340, diğerleri +60; sıra bonusu 500/200/80/0 | — | — | **Hat B** |
| R-94 | Dereceli / botlara karşı ekonomi | — | — | **Hat B** |
| R-95 | Liderlik: haftalık 30/15/5/1, sezon LP | — | — | **Hat B** |
| R-96 | Başlangıç bakiyesi | — | — | **Hat B** |
| R-97 | Günlük ödül | — | — | **Hat B** |
| R-98 | Giriş kilidi | — | — | **Hat B** |

## Kararlar (DECISIONS.md)

| Kimlik | Pakette ne diyor (kaynak) | Projede ne vardı (dosya:satır, eski motor) | Durum | v4 sonrası (dosya, test) |
|---|---|---|---|---|
| D-01 | Taş kimlikleri string `R5a`/`Ja`; kanonik sıra R,B,Y,K; 1–13; a,b; Ja,Jb | Sayısal `TileId` (`Model/Tile.cs:8`) | farklı | `AllTiles` (`G101Rules.cs:55-66`) · tüm vektörler |
| D-02 | mulberry32 (uint), `rngInt(n) = next() % n`; dağıtan → karıştırma → zarlar aynı akıştan | xorshift32 + ret örnekleme; karıştırma → dağıtan → zar (`SeededRandom.cs:7-29`, `DealCeremony.cs:35-39`) | farklı | `RngNext`/`RngInt` (`G101Rules.cs:40-51`) · DEAL-01, `V4EngineTests.Mulberry32MatchesRulesJs`, `RngIntIsNextModuloN`, `TheDealOrderIsDealerThenShuffleThenDice`; kayıt sürümü `mulberry32-v4` (`V4ReplayTests`) |
| D-03 | Gösterge seviyesinde ve üstünde iki sahte okey → alttaki ilk taş | `DealCeremony.cs:48-55` | var | `DealHand` · DEAL-03 |
| D-04 | `deck[0]` = gösterge balyasının en alt taşı, sonra büyük indeksli, sonra küçük | `S(13)` üstten, `S(14)`, gösterge balyası (`DealCeremony.cs:71-74`) | farklı | `DealHand` · DEAL-01, DEAL-04 |
| D-05 | Geçersiz → sarma yok (R-12) | — | pakette yok (geçersiz) | R-12 |
| D-06 | Gruptaki okey eksik renklerden R,B,Y,K sırasıyla ilkini temsil eder | Temsil edilen yüzü istemci seçer (`MeldValidator.cs:27-30`) | farklı | `AnalyzeMeld` grup repr · MELD-05, MELD-12, SCN-01 (değer) |
| D-07 | Geçersiz → elden bitişte eşik (R-53) | — | pakette yok (geçersiz) | R-53 |
| D-08 | Çift açan masadaki seri/gruplara işler; yalnız çift olarak yere açar | Tur başına 2 taş sınırı (`TableEvaluator.cs:90`, `:275-281`; `docs/rules/101-v1.md:117`) | farklı | `DoAdd`, `DoLay` · OPEN-09, ISLE-* |
| D-09 | Okey alma, okeyi aynı turda kullanma zorunluluğu getirmez | Zorunluluk yok (`TableEvaluator.cs:242-273`) | var | `DoSwap` · OKEY-01, OKEY-02 |
| D-10 | Geri bırak, turdaki tüm işlemleri geri alır | Tur atomik: geri bırakmada uygulanmış işlem yok (`GameEngine.cs:83-99`) | var (yapısal) | `DoReturnSide` → `Restore` · SIDE-04 |
| D-11 | Bekleme odasından çıkana bahis iadesi | — | — | **Hat B** |
| D-12 | Eşitlikte havuz eşit bölünür | — | — | **Hat B** |
| D-13 | Geçersiz → R-59 | — | pakette yok (geçersiz) | R-59 |
| D-14 | Geçersiz → R-55 | — | pakette yok (geçersiz) | R-55 |
| D-15 | Okey atma cezası alan atışta ayrıca işlek cezası yazılmaz | Okey önce döner (`PenaltyEvaluator.cs:20`) | var | `DoDiscard` (`G101Rules.cs:772-773`) · PEN-04 |
| D-16 | Açtıran taş yalnız açmamış oyuncunun soldan aldığıyla; değer perdeki değer (okeyse temsil ettiği sayı) | Okeyse 0 (`Penalties.cs:13-14`) | farklı | `TileValueInMeld` · SIDE-01, SCN-01 |
| D-17 | Kendi koyduğu okeyi geri alırsa ceza yok; eşlide ortağın okeyi alınırsa yazılır | `TableEvaluator.cs:271` (alan ≠ perin sahibi) | var (kime yazıldığı farklı, R-44) | `DoSwap` (`G101Rules.cs:734`) · OKEY-01…03 |
| D-18 | Bekleyen cezalar tur sonunda yazılır; geri topla siler | Tur atomik; cezalar commit'te (`TurnEvaluator.cs:53-65`, `TableEvaluator.cs:271`) | var (yapısal) | `Pending`, `CommitPending` · OKEY-03, SIDE-01 |
| D-19 | Zaman aşımında yapılmış geçerli açılış/işleme korunur; yalnız soldan alınıp açılamamış taş geri bırakılır | Sunucu bota turu baştan oynatır (`MatchSession.cs:414-420`) | farklı | `DoTimeout` · AUTO-02 |
| D-20 | Bot zamanlaması: çek 600, per/işle 430, atış 650 (per sonrası) / 730 ms; sohbet %16 / %60 | Sunucuda sabit 1 sn (`MatchSession.cs:25`), bot sohbeti yok | farklı | `G101Bots.Timing`, `Chat`, `DelayFor`, `ChatAfterDiscard` · sunucuya bağlama **Hat B** |
| D-21 | Bot açabildiği ilk turda açar; seriyi çifte tercih eder, yalnız çiftle elden bitebiliyorsa çift; ortağın okeyini almaz; okeyi yalnız bitişe bir taş kala işler | Arama tabanlı `BotPlanner` (`BotPlanner.cs:114`) | farklı | `NextAction`, `OpeningPlan` · SIM-01…03, SCN-01, SCN-02, Parity |
| D-39 | "En az 15 taş" kaldırıldı, elden bitişte eşik aranır | `TableEvaluator.cs:13` | farklı | = R-53 · FIN-10…13 |
| D-40 | Paylaşılan sıra: konum değerlerinin ortalaması (LP, haftalık, XP); havuz eşit | `Settlement.cs:37-53` | — | **Hat B** (motor: `Ranking` paylaşılan sıra, MATCH-02) |
| D-41 | Eşlide kazanan takım 1.–2., kaybeden 3.–4. konum | `Settlement.cs:51` | — | **Hat B** (motor: `Ranking` takım birimi, MATCH-03, LEAVE-02) |
| D-42 | El XP'si yalnız bitirene +340; eşlide ortağı +60 | — | — | **Hat B** |
| D-43 | Katlamalıda yükselmiş eşiğin altı eksik açılış (+101) | Eşik altı açış eksik açılış cezası (`docs/rules/101-v1.md:177`) | var | `DoOpen` + `OpenNeed` · KAT-01 |

## Özet

- 62 kural (R-01…R-98'den RULES.md'de olanlar) + 26 karar satırı.
- Eski motorla **farklı** ya da **eksik** olup v4 motorunda paket davranışına geçenler: R-02, R-05, R-13, R-14, R-23, R-24, R-25, R-26, R-31, R-34, R-37, R-41, R-43, R-44, R-53, R-54, R-57, R-61, R-62, R-70, R-71, R-80; D-01, D-02, D-04, D-06, D-08, D-16, D-19, D-20, D-21, D-39.
- Eski motor değiştirilmedi; `101-v1` kayıtları onunla açılmaya devam eder (K-02, `V4ReplayTests.ALegacyRecordStillOpensWithTheOldEngine`).

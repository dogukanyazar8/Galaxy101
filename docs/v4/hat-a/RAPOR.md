# Hat A · Kural motoru ve botlar · rapor

Dal `v4/rules` (worktree `C:/GD/Okey101-v4/rules`), taban `753287e`. Kaynak: v4 paketi `Design/Galaxy101_ClaudeCode/prototip/galaxy101_handoff_v4/` (`pkg/`). Kararlar K-01…K-03 (`docs/v4/KARARLAR.md`) uygulandı. Unity açılmadı; doğrulama SDK NUnit projesiyle.

## Sonuç

| Ölçüt | Sonuç |
|---|---|
| Paket vektörleri (NUnit, vektör başına adlı test) | **106/106** (`TestCategory=V4Vectors`) |
| SIM vektörleri (C# botlarıyla tam maç) | **4/4**: SIM-01 (seed 42 toplamları `[199,1021,898,385]`), SIM-02 (seed 99 `[141,65,147,596]`), SIM-03 (katlamalı eşli), COPY-02 |
| rules.js ↔ C# eşlik (72 bot maçı, olay akışı + son durum SHA-256) | **72/72** (`TestCategory=V4Parity`) |
| Mevcut çekirdek/bot/uygulama testleri | önce **583/583**, sonra **583/583** (değişen test yok) |
| Kayıt sürümü testleri (K-02) | **15/15** (`V4ReplayTests`) |
| Motor/RNG ve koşucu testleri | **15/15** (`V4EngineTests` + `V4VectorTests` yardımcıları) |
| Toplam SDK NUnit | **791/791** |
| Paketin kendi Node koşusu | 106/106 vektör, 26/26 akış (değişmedi) |

## Yapılanlar

### K-01 · Kural motoru (`Packages/com.okey101.core/Runtime/V4/`, ad alanı `Okey101.Core.V4`)
- `G101Rules.cs`: `logic/rules.js` v0.4.0'ın satır satır karşılığı. Aynı sıra ve aynı adlar: `newMatch`, `apply` (saf: girdiyi değiştirmez, yeni durum + olaylar; hata olursa girdi durumu ve boş olay listesi), adımlar `startHand`/`nextHand`/`draw`/`returnSide`/`open`/`lay`/`add`/`swap`/`undo`/`discard`/`timeout`/`leave`/`takeSeat`, `fromSpec`, `rigFromHands`, `legal`, `ranking`, `invariants`, `openNeed`, `autoDiscardTile`, `analyzeMeld`, `okeyFromIndicator`, `isIslek`, `addSides`, `swapFits`, taş yardımcıları (`isTile`, `isFake`, `colorOf`, `numOf`, `codeOf`, `isOkey`, `realOf`, `handValueOf`), `leftOf`/`nextOf`/`partnerOf`, sabitler (`Const`). Hata kodları ve olay nesneleri (`{e:'opened', seat, mode, value, melds}` …) birebir.
- `V4Model.cs`: durum şekilleri (`MatchState`, `HandState`, `TurnState`, `TurnSnapshot`, `TableMeld`, `PenaltyEntry`, `DealRecord`, `HandResult`, `RankUnit`, …). Her biri `Clone()` (rules.js `clone`) ve `ToTree()` (rules.js'deki özellik adlarıyla düz ağaç: `hand.racks.0`, `hand.openMode.0`, `seats.1.left` …) taşır. `V4Tree.Canonical`: anahtarları sıralı, JSON kütüphanesiz kanonik JSON (özet için).
- `V4Actions.cs`: `GameAction` (rules.js eylem nesnesi + fabrika yöntemleri), `MeldSpec`, `DealRig`, `ScenarioDeal`, `HandSpec`, `SpecException` (`E_SPEC_*`), `GameEvent` (ad + sıralı alanlar, ağaç değerleri; sunucu olduğu gibi JSON'a çevirebilir), `ApplyResult`, `LegalMoves`, `MeldAnalysis`.

### K-02 · RNG ve kayıt sürümleri
- mulberry32 `uint` aritmetiğiyle (`G101Rules.RngNext/RngInt`); sıra D-02 (dağıtan → Fisher–Yates → iki zar). rules.js'den alınan altın değerlerle sınandı.
- `G101Rules.RuleVersion = "101-v4"`, `ShuffleVersion = "mulberry32-v4"`.
- `Assets/Okey101/Application/V4Replay.cs`: `V4ReplayRecord` (seçenekler, seed, kabul edilen her eylem, son durumun SHA-256'sı), `V4Replay.Restore/Hash`, kapalı DataContract JSON şekli (bilinmeyen eylem/taş, bozuk zar/dizilim reddedilir), `RecordVersions.Classify` → `Legacy` / `V4` / `Unknown`.
- `ReplayCodec`: `WriteV4`, `ReadAny` (sürüm alanlarına bakıp doğru motora yönlendirir; başka her birleşim reddedilir). `Read`/`Decode`/`Write` eski motor için aynen kaldı; v4 dosyasını açmazlar. `DiagnosticsJson`: ortak `DecodeAs<T>` ve `VersionDto` (davranış değişmedi). `ScenarioLoader.Import` iki türü de kabul eder, `LoadV4` eklendi.

### K-03 · Botlar (`Runtime/V4/G101Bots.cs`)
- `logic/bots.js` karşılığı: `Timing` (D-20), `Chat` (D-20 olasılık ve gecikmeleri), `BestMelds`, `BestPairs`, `OpeningPlan`, `WantSide`, `NextAction`, `DelayFor`, `ChatAfterDiscard`, `Simulate`. Yalnız kendi ıstakası + açık bilgi (R-80); oyun kararları deterministik, sohbet dışarıdan verilen `RngState` ile.
- Çekirdek Runtime altında olduğu için sunucunun `Server/src/Galaxy101.Rules/Galaxy101.Rules.csproj` projesi (`Runtime/**/*.cs`) bunları değişiklik olmadan derler (aynı ayarlarla, uyarılar hata olarak, geçici bir kopyada denendi).

### Testler
- `tools/v4/nunit/CoreV4Tests.csproj` (+ `RepoRootSetUp.cs`): `Runtime/**`, `Tests/**`, `Assets/Okey101/{Bots,Application}/**`, `Assets/Okey101/Tests/EditMode/**` (yalnız `SimulationTests.cs` hariç, EK-108); `LangVersion 9.0`, `UNITY_EDITOR;OKEY_DIAGNOSTICS`, NUnit 3.14 + adaptör 4.6 + Test SDK 17.14.1 + Newtonsoft 13.0.3; çıktı `%TEMP%/okey101-v4-rules-nunit/`.
- `Packages/com.okey101.core/Tests/Editor/V4/` (yeni asmdef `Okey101.Core.V4.Tests`, Newtonsoft `precompiledReferences` ile):
  - `VectorRunner.cs`: `tests/runner.js` karşılığı (yol erişimi `a.b.0`, `#len`, `#set`, `invariants`; kısmi eşleşme, tür zorlaması yok, `null` ≠ eksik; sıralı olay eşleşmesi; `noEvents`; `call`, `sim`, `botTurn`). Vektöre özel kod yok.
  - `V4VectorTests.cs`: 106 adlı vektör testi; kopyaların paketle bayt bayt aynı olduğu testi (`Design` yoksa `Assert.Ignore`); koşucunun yanlış beklentiyi yakaladığı test; `apply` saflık testi.
  - `V4ParityTests.cs` + `v4-parity.json` (üretici `tools/v4/nunit/gen_parity.js`): 6 seçenek kümesi × 12 seed bot maçı; olay akışı ve son durum rules.js ile aynı özet.
  - `V4EngineTests.cs`: mulberry32 altın değerleri, D-02 sırası, sonraki elin dağıtanı, kanonik JSON, `legal`, aralık dışı `swap` indeksi.
- `Packages/com.okey101.core/Tests/Editor/V4Vectors/`: paketin 13 dosyası (index + 12 takım), `.meta` ile.
- `Assets/Okey101/Tests/EditMode/V4ReplayTests.cs`: v4 kaydı gidiş-dönüş, aynı seed → aynı bayt, eski kayıt eski motorla açılır, bilinmeyen/karışık sürümler (iki tür için) reddedilir, kurcalanmış kayıt reddedilir, kurulu senaryo (R43) içe alınır ve `LoadV4` ile açılır.

## Commit'ler (`v4/rules`)

| Commit | İçerik |
|---|---|
| `ebabed5` | rules.js ve bots.js'nin C# portu, SDK NUnit projesi |
| `78ab227` | C# vektör koşucusu, 106 vektör, rules.js eşlik testi |
| `e8afaed` | Kayıt sürümleri `mulberry32-v4`/`101-v4`, eski motorla yan yana; motor ve kayıt testleri |
| (bu rapor) | `docs/v4/matris/hat-a.md`, `docs/v4/hat-a/RAPOR.md`, `docs/v4/hat-a/EKSIKLER.md` |

## Komutlar ve çıktılar (worktree kökünden)

```
node tools/v4/run_package_tests.js
  → vectors 106/106 · server flows 26/26

dotnet test tools/v4/nunit/CoreV4Tests.csproj          # değişiklikten önce (753287e + yalnız proje)
  → Başarılı! Başarısız: 0, Başarılı: 583, Toplam: 583

dotnet test tools/v4/nunit/CoreV4Tests.csproj          # şimdi
  → Başarılı! Başarısız: 0, Başarılı: 791, Toplam: 791
dotnet test tools/v4/nunit/CoreV4Tests.csproj --no-build --filter "TestCategory=V4Vectors"
  → Başarılı: 106, Toplam: 106
dotnet test tools/v4/nunit/CoreV4Tests.csproj --no-build --filter "TestCategory=V4Parity"
  → Başarılı: 72, Toplam: 72
dotnet test tools/v4/nunit/CoreV4Tests.csproj --no-build --filter "Name=SIM-01|Name=SIM-02|Name=SIM-03|Name=COPY-02"
  → Başarılı: 4, Toplam: 4
dotnet test tools/v4/nunit/CoreV4Tests.csproj --no-build --filter "FullyQualifiedName!~Okey101.Core.V4&FullyQualifiedName!~V4ReplayTests"
  → Başarılı: 583, Toplam: 583   (mevcut testler)
dotnet test tools/v4/nunit/CoreV4Tests.csproj --no-build --filter "FullyQualifiedName~V4ReplayTests"
  → Başarılı: 15, Toplam: 15

node tools/v4/nunit/gen_parity.js > Packages/com.okey101.core/Tests/Editor/V4/v4-parity.json   # paket değişirse
```

İlk koşuda 6 eski test kırmızıydı (`ScenarioFixtureTests`): test sunucusu çıktı klasöründe başlıyor, senaryolar göreli yolla okunuyor. `RepoRootSetUp.cs` çalışma klasörünü depo köküne alır (Unity'nin yaptığı gibi); sonra 583/583. Hiçbir eski test değiştirilmedi.

Ek derleme denetimleri (geçici klasörde, `Server/` dokunulmadan): sunucunun `Galaxy101.Rules` ayarlarıyla (`net10.0`, `LangVersion latest`, uyarı = hata) ve `netstandard2.1` + `LangVersion 9.0` + uyarı = hata ile `Runtime/**` derlendi: ikisi de başarılı.

## Alınan kararlar

1. **Ağaç izdüşümü açık yazıldı** (`ToTree()`), yansıma yok: C# alanları PascalCase kalır, ağaç rules.js adlarını taşır. Aynı ağaç sunucunun tel biçimi olabilir; eşlik testi ağacın rules.js `JSON.stringify` çıktısıyla anahtar ve değer olarak aynı olduğunu gösteriyor.
2. **Olaylar `GameEvent`** (ad + sıralı alanlar, ağaç değerleri), türlü sınıflar değil: rules.js olay nesneleri birebir korunur, sunucu tele olduğu gibi koyar; `Int/Str/Bool` yardımcıları var.
3. **Ad: `G101Rules` / `G101Bots`** (rules.js'nin küresel adları). `Rules` adı `Okey101.Core.Rules` ad alanıyla çakışırdı.
4. **Pakette tekrarlanan üç vektör kimliği** (EK-101): ikinci kopya `ID~2` adıyla koşar; 106 test adı tekil.
5. **Yeni test asmdef'i** (`Okey101.Core.V4.Tests`): Newtonsoft için mevcut `Okey101.Core.Tests` asmdef'ini değiştirmek yerine (görevin verdiği `overrideReferences` deseni, `Galaxy101.Service.Tests` gibi).
6. **rules.js'nin `TypeError` verdiği iki bozuk girdi** hata koduyla reddedilir (EK-103). Başka her yerde davranış birebir; JS'nin kararlı `Array.sort`'una dayanan yerlerde kararlı sıralama (`StableSort`), taş sayısı tablosu yalnız hız için.
7. **Test kökü**: vstest keşfi çıktı klasöründe çalıştığı için `VectorRunner.RepoRoot()` sırayla `OKEY101_REPO_ROOT`, derlemedeki `RepoRoot` metaverisi (SDK projesi yazar) ve çalışma klasöründen yukarı arama kullanır; Unity'de çalışma klasörü zaten proje kökü.
8. **Kayıt biçimi (K-02)**: v4 kaydı başlangıç durumu değil eylem listesi taşır (yeni maç + aynı seed + aynı eylemler = aynı maç); özet `V4Tree.Canonical(state.ToTree())` SHA-256. `rig`'li `startHand` yalnız senaryo kayıtlarında (DEMO-17); dizilim 106 taşın permütasyonu, zarlar 1–6, dağıtan 0–3 değilse reddedilir.
9. **Eski motor dokunulmadı** (`GameEngine`, `TurnEvaluator`, `TableEvaluator`, `BotPlanner` …); eski testler değişmedi.

### Kullanıcı kararlarıyla (memory `product-decisions.md`) karşılaştırma
- *Elden bitiş*: kullanıcı kararı (2026-10-25) eşiğin aranmasını istiyor ve "katlamalıda yükselmiş eşik" notu yorum olarak düşülmüştü; paket R-53 bunu açıkça yazıyor → v4 motoru yükselmiş eşiği uygular (FIN-13). Çelişki yok; eski motorun `FinishesFromHand` (≥15 taş, eşiksiz) davranışı yalnız eski kayıtlarda kalır.
- *Havuz*: notta "pot = stake × 4"; paket R-91/D-34 "ödenen bahislerin toplamı" (devralmada eklenir) → paket geçerli, **Hat B**.
- *Eşlide kalkan*: notta "kalkan son sıra (eşlide 2.)"; paket R-60/D-41 takımı son saymaz, sıra takım birimidir → motor `ranking` paketle aynı (LEAVE-02); ödül/sıra bonusu dağıtımı **Hat B**.

## Hat B için notlar (sunucu bu API'yi kullanacak)
- Maç: `var s = G101Rules.NewMatch(opts, seed); var r = G101Rules.Apply(s, GameAction.StartHand());` sonra her C2S niyeti `GameAction.*` → `Apply`; `r.Ok`/`r.Error`/`r.Detail` (`{error, tile?, index?, need?}`), `r.Events` (`GameEvent.ToTree()` tele hazır), `r.State`.
- Zaman aşımı: `GameAction.Timeout(seat)` (R-70/R-71, D-19). Bot / "koltuğu sunucu oynar": `G101Bots.NextAction(state, seat)` → uygula → `G101Bots.DelayFor(prev, next)` bekle → tekrar sor (D-20); reddedilirse `Timeout` (bots.js `simulate` gibi). Sohbet: `G101Bots.ChatAfterDiscard(rng)`, `Chat.ReplyChance/ReplyDelay/ThanksDelay*`.
- Kontroller: `G101Rules.Legal(state, seat)`, eşik gösterimi `G101Rules.OpenNeed(state, seat, "run"|"pair")`.
- Gizli bilgi süzümü motorda yok (EK-106): `ToTree()` bütün ıstakaları içerir.
- Kayıt: `V4ReplayRecord.Capture(opts, seed, actions, final)` + `ReplayCodec.WriteV4/ReadAny`; maç sonu sıra `state.Ranking` (`RankUnit`).

## Açık konular
- EK-101…EK-108 (`docs/v4/hat-a/EKSIKLER.md`); en önemlileri: Unity'de doğrulanmadı (EK-107), gizli bilgi süzümü sunucuda yazılacak (EK-106), paketteki tekrarlı vektör kimlikleri (EK-101).
- İstemci (`LocalSession`, `BotRunner`, sunum) ve sunucu hâlâ eski motoru oynatıyor; v4 motoruna geçiş Hat B (sunucu) ve Faz 2 (istemci) işi. Kural matrisi `docs/v4/matris/hat-a.md`, eski motorla v4 arasındaki 32 farkı (22 kural, 10 karar) satır satır gösteriyor.

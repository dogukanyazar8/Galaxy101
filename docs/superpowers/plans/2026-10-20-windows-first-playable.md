# Okey 101 Windows First Playable Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Execution method requires the user's selection; this plan recommends inline execution with executing-plans.

**Goal:** Windows x64 üzerinde bir insan ve üç yerel botla 101-v1 kurallarına uygun, tamamlanabilen ve test edilebilen tek el üretmek.

**Architecture:** UnityEngine bağımsız C# çekirdek bütün hamleleri doğrular; Unity sunumu ve botlar oyuncuya özel görünümler kullanır. Çekiş kesinleşir; masa işlemleri ve atış atomik tur planıyla onaylanır. Aynı kaynak kümesi ileride online sunucuda kullanılabilir; bu sürüm ağ altyapısı kurmaz.

**Tech Stack:** Unity 6000.6.6f1, URP, uGUI, Input System, .NET Standard 2.1 uyumlu C#, Unity Test Framework/NUnit; Windows x64/Direct3D 11.

**Spec:** [Onaylı tasarım](../specs/2026-10-20-windows-first-playable-design.md), [onaylı kurallar](../../rules/101-v1.md). İkisini de okumadan uygulamaya başlanmaz.

## Global Constraints

- Başlangıç motoru: kurulu Unity 6000.6.6f1; sürüm değişimi gerekirse önce somut derleme/çalışma engeli gösterilir.
- Görüntüleme: URP, tek sabit kamera, Windows x64 ve başlangıçta Direct3D 11.
- Arayüz: uGUI; tek çalışma zamanı UI sistemi. Girdi Unity Input System üzerinden eylemlere çevrilir.
- Çekirdek: UnityEngine bağımlılığı olmayan C#; Unity .NET Standard 2.1 API yüzeyine uygun.
- İlk test altyapısı: Unity Test Framework; kural testleri EditMode, etkileşim testleri PlayMode.
- İlk sürümde ağ SDK'sı, veritabanı, hesap sistemi, ücretli eklenti veya yeni tween bağımlılığı yoktur.
- Dört oyuncu, eşsiz oyun. İlk sürümde bir insan ve üç yerel bot.
- Sabit 101; başkasının açış toplamı eşiği değiştirmez.
- Yerel oyunda tur süresi sınırı yok; duraklatma vardır.
- Açılış ve puanlama dahil bütün kural değerleri 101-v1'den gelir; uygulama kolaylığı için değiştirilmez.
- Assets, Packages ve ProjectSettings henüz yoktur. Burada listelenen ürün dosyaları gelecekte oluşturulacak dosyalardır.
- .NET SDK 10.0.102'nin kurulu olması Unity'nin .NET 10 API'lerini kullanmasına izin vermez.
- Bu plan Git init/commit/push veya bağımlılık kurulumu çalıştırmaz. Uygulamada yetkilendirilmiş Git işlemleri varsa görev sonunda yalnız ilgili dosyalar kaydedilir; yoksa görev doğrulama kanıtıyla teslim edilir.

## Review Focus

1. Aynı yüzlü farklı fiziksel taşlar ve dışarıdan değiştirilen listeler: kimlikler korunmalı, eski durum değişmemeli. T01/T04/T06.
2. Yeni el açıldıktan sonra gelen bot kararı veya çift tık: önceki el/tur komutu uygulanmamalı. T06/T09/T12.
3. Yandan alınan taşın okey değişiminde kullanılması ve okeyin elde tutulması: doğru fiziksel taşın kullanımı sayılmalı; yaklaşık arama geçerli yolu reddetmemeli. T05/T08.
4. Duraklatma, odak kaybı, animasyon azaltma ve sahne kapanması: görünüm/otorite ayrışmamalı, bot işi sızmamalı. T09/T11/T12.
5. Bozuk, sürümü bilinmeyen veya aşırı büyük senaryo/tekrar dosyası: yükleme güvenle reddedilmeli, mevcut el korunmalı. T10.

---

## Çalışma ve doğrulama sözleşmesi

Her görevde sıra: testleri ekle, dar kontrolün beklenen nedenle başarısızlığını gözle, minimum uygulamayı yap, aynı kontrolün geçtiğini gözle, değişikliği incele. Önce proje/test altyapısının kurulması gereken T01 bu altyapıyı davranış kodundan önce kurar. Grafik görevlerinde yapay görsel birim testleri yerine belirtilen PlayMode davranışı ve gerçek görüntü kontrolü kullanılır.

Test ad alanları: `Okey101.Core.Tests`, `Okey101.Application.Tests`, `Okey101.PlayMode.Tests`. Görevler ilgili test sınıfı filtresiyle çalıştırılır. Tüm testleri her görev sonunda yeniden çalıştırma; aşama kapısında veya yeni bir risk belirdiğinde genişlet.

T01'in test betiği ve T07'nin geliştirme build betiği için komut sözleşmesi:

```powershell
& .\scripts\Test-Unity.ps1 -Platform EditMode -Filter 'Okey101.Core.Tests.DeckTests'
& .\scripts\Test-Unity.ps1 -Platform PlayMode -Filter 'Okey101.PlayMode.Tests.TableSmokeTests'
& .\scripts\Build-Windows.ps1 -Configuration Development
```

`Test-Unity.ps1`: parametreler `Platform` = EditMode/PlayMode, isteğe bağlı `Filter` ve `Category`. Unity tam yolu sabittir; proje yolu betiğin konumundan çözülür. EditMode için `-batchmode -nographics -runTests`, PlayMode için grafik bağlamıyla `-batchmode -runTests`; ikisinde `-projectPath`, `-testPlatform`, `-testResults`, `-logFile` kullanılır. Test çağrısına `-quit` eklenmez. Süreç beklenir; çıkış kodu, yeni oluşmuş NUnit XML, `failed=0`, `total>0` ve hedef grupta beklenmedik atlama olmadığı doğrulanır. Sonuç yoksa veya filtre hiç test seçmediyse başarı yazılmaz. Editor proje kilidi varsa süreç öldürülmez; engel bildirilir.

Çıktı: `artifacts/test-results/<benzersiz-calisma-kimligi>/results.xml`, `editor.log`. Önceki rapor yeni çalışmanın sonucu sayılamaz. Betikler argüman dizileri kullanır; shell komut dizgesi birleştirmez. Arka plan yardımcıları açılırsa pencere gizlenir. Varsayılan test çağrısı `Simulation` kategorisini hariç tutar; yalnız T10/A4 açıkça çağırır.

Test tablosundaki her satır ayrı NUnit testi veya adı belirtilmiş parametreli testtir. İfadeler beklenen assertion'ı belirtir; henüz çalıştırılmış test değildir. Her görev sonunda ilgili `artifacts` yolu ve gözlenen sonuç kaydedilir.

## Dosya sınırları ve ortak arayüzler

Kısaltmalar yalnız bu belge içindeki yol gösterimidir: **C** = `Packages/com.okey101.core/Runtime`, **CT** = `Packages/com.okey101.core/Tests/Editor`, **A** = `Assets/Okey101`. Files satırları bu köklere göre tam proje yollarını tanımlar. Bütün dosyalar ve Unity varlıkları kendi `.meta` dosyalarıyla taşınır.

| Alan | Dosyalar / sorumluluk |
|---|---|
| Model | C/Model/Tile.cs, Meld.cs, RoundState.cs, PlayerView.cs, Commands.cs, Results.cs: değiştirilemez veri ve temel değer doğrulamaları |
| Rastgelelik/dağıtım | C/Random/SeededRandom.cs; C/Turns/RoundFactory.cs |
| Kurallar | C/Rules/MeldValidator.cs, MeldCandidateGenerator.cs, TableEvaluator.cs, PenaltyEvaluator.cs, DiscardTurnFinder.cs |
| Tur | C/Turns/TurnEvaluator.cs, GameEngine.cs, StateInvariant.cs |
| Puan | C/Scoring/ScoreCalculator.cs |
| Görünüm projeksiyonu | C/Model/PlayerViewProjector.cs: aynı saf projeksiyon çekirdek ve oturum tarafından kullanılır; Core, Application'a referans vermez |
| Oturum | A/Application/LocalSession.cs, TurnDraft.cs, ReplayCodec.cs, ScenarioLoader.cs |
| Bot | A/Bots/BotPlanner.cs, BotRunner.cs |
| Sunum | A/Presentation/Input, Table, UI, Audio; yalnız görünüm ve giriş |
| Üretim | A/Editor/ProjectSetup.cs, BuildWindows.cs; scripts/Test-Unity.ps1, Build-Windows.ps1 |

T01 veri tiplerini üretir; aşağıdaki alan/adlar görevler arasında değiştirilmez. `IReadOnlyList` tek başına değiştirilemezlik garantisi değildir: bütün giriş koleksiyonları kopyalanır, dışarıya değiştirilebilir dizi referansı verilmez.

- `TileColor { Red, Yellow, Blue, Black }`; `TileId(int value)` yalnız 0–105; `TileFace(TileColor color, int number)` yalnız 1–13.
- `TileCatalog.Create(): IReadOnlyList<Tile>`; `Tile` = Id + nullable PrintedFace. Numara kimliği `2 * (13 * colorIndex + number - 1) + copyIndex`; copyIndex 0/1. Sahte okeyler 104/105.
- `TileCatalog.IsRealJoker(TileId id, TileFace indicator): bool`; `GetNonJokerFace(TileId id, TileFace indicator): TileFace` gerçek okey için çağrı hatasını reddeder, sahteyi göstergenin üstüne çevirir.
- `OpeningKind { None, Series, Pairs }`, `MeldKind { Run, Set, Pair }`, `RoundPhase { AwaitingDraw, PlanningTurn, Finished, Cancelled }`, `DrawSource { Stock, PreviousDiscard }`, `FinishReason { DiscardWin, StockExhausted, AllPairs }`.
- `PlacedTile(TileId id, TileFace representedFace)`; `MeldProposal(MeldKind kind, IReadOnlyList<PlacedTile> tiles)`; `Meld(int id, int owner, MeldProposal group)`. Bağlantılar taş kimliğine bağlıdır; masadaki okeyin karşılığı grup genişletirken değiştirilemez.
- `PlayerState`: Hand, Opening, Penalties. `DrawReceipt`: Source, Tile, WasLastStock. `RoundState`: RoundId, Version, Seed, StartingPlayer, ActivePlayer, TurnNumber, Phase, Indicator, Stock, dört Players, Table, dört Discards, nullable PendingDraw, OpeningAtTurnStart, SeriesTilesUsed, IsInitialTurn, dört LastAcceptedSequences, nullable Result. İndeksler/oyuncular 0–3; koltuk sırası oyun yönündedir.
- `CommandMeta(Guid roundId, int actor, long sequence, long expectedVersion)`; kimlik RoundId/Actor/Sequence bileşimidir. Sequence pozitif ve oyuncu başına artar; son kabul edilen dört değer tutulur. Böylece sınırsız bir komut kimliği sözlüğü gerekmez.
- `DrawCommand(CommandMeta meta, DrawSource source)` ve `CommitTurnCommand(CommandMeta meta, TurnPlan plan)` ortak `GameCommand` tabanından türer.
- `TurnPlan(IReadOnlyList<TableAction> actions, TileId discard, bool acceptPenalty)`.
- `TableAction` alt tipleri: `OpenGroups(OpeningKind opening, IReadOnlyList<MeldProposal> groups)`, `AddGroup(MeldProposal group)`, `ExtendMeld(int meldId, bool prepend, IReadOnlyList<PlacedTile> tiles)`, `SwapJoker(int meldId, TileId jokerId, TileId replacementId)`. Çift grubu ExtendMeld ile büyütülemez. Yeni grup kimliklerini motor artan sıra ile verir.
- `RuleResult<T>`: Success, Value, Error. `ErrorCode` üyeleri: None, InvalidState, InvalidTile, InvalidGroup, InvalidOpening, OpeningRequired, OpeningTypeLocked, ProcessingLimit, JokerBindingLocked, InvalidReplacement, PreviousDiscardUnavailable, DiscardMustBeUsed, LastTileRequired, PenaltyConfirmationRequired, WrongRound, WrongPlayer, WrongPhase, StaleVersion, DuplicateCommand, NoLegalUse, Cancelled.
- `FinishInfo`: Reason, nullable Winner, WinnerOpening, WinnerOpenedThisTurn, DiscardedRealJoker. `ScoreBreakdown`: HasScore, N, J, P, B, M, nullable Total. `RoundResult`: FinishInfo + dört ScoreBreakdown; iptalde HasScore=false/Total=null, sıfır puan yazılmış gibi gösterilmez.
- `CommandReceipt`: Accepted, Error, Version, IReadOnlyList<VisibleEvent>; ham RoundState içermez. `VisibleEvent`: Kind, Actor, optional TileId/MeldId; gizli çekiş taşı yalnız o oyuncunun makbuzunda görünür.
- `PlayerView`: RoundId, Version, Actor, ActivePlayer, Phase, OwnHand, OwnOpening, OwnPenalties, OwnLastSequence, Indicator, StockCount, kamuya açık oyuncu taş adetleri/açılışları/cezaları, Table, Discards, kendi PendingDraw bilgisi, SeriesTilesUsed, nullable Result. Rakip elleri veya kapalı deste sırası yoktur.

`RoundState` yalnız çekirdek/oturum içinde kullanılır. Bot API'si RoundState kabul edemez. Çekirdek ve uygulama hata nedenini ErrorCode ile sunuma verir; kullanıcıya stack trace gösterilmez.

## T01 — Tekrarlanabilir proje, taş modeli ve dağıtım

**Files:** Create `scripts/Initialize-Unity.ps1`, `scripts/Test-Unity.ps1`, `.gitignore`; resmi şablondan `Assets`, `Packages/manifest.json`, `Packages/packages-lock.json`, `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/ProjectSettings.asset`; `Packages/com.okey101.core/package.json`, `Runtime/Okey101.Core.asmdef`, `Tests/Editor/Okey101.Core.Tests.asmdef`, `Tests/Shared/Okey101.TestSupport.asmdef`, `Tests/Shared/RoundTestBuilder.cs`; C/Model altında yukarıdaki altı dosya, C/Random/SeededRandom.cs, C/Turns/RoundFactory.cs, C/Turns/StateInvariant.cs. Test: CT/DeckTests.cs, ModelIsolationTests.cs. Build-Windows geliştirme betiği T07'de, Release desteği T13'te eklenir.

**Interfaces:** `RoundFactory.Create(uint seed, Guid roundId, int? startingPlayer): RoundState`; `SeededRandom.NextUInt(): uint`, `NextInt(int exclusiveMax): int`; `StateInvariant.Validate(RoundState state): RuleResult<bool>`. Model sözleşmesi yukarıdadır.

- [x] **1. Test altyapısını oluştur.** Kurulu `com.unity.template.urp-blank-17.2.6.tgz` arşivinin ProjectData içeriğinden yalnız Assets/Packages/ProjectSettings'i aktar. Hedeflerden biri zaten varsa dur; mevcut docs'u silme veya üzerine yazma. Yeni geçici açma klasörü kullan; arşiv/hedef yollarının amaçlanan dizinde kaldığını doğrula. Lisans/build desteği engelini kaydet; otomatik hesap bağlantısı veya kurulum yapma. Core asmdef `noEngineReferences=true`; Core.Tests yalnız Editor/TestAssemblies; embedded paket manifestte testable olsun. TestSupport, Editor API'si kullanmayan TestAssemblies işaretli yardımcı assembly'dir; hem EditMode hem PlayMode testleri referans verebilir, normal ürün build'ine girmez.

Bootstrap komutu: `& .\scripts\Initialize-Unity.ps1`; beklenen çıktı doğru ProjectVersion.txt ve üç ürün kökü, mevcut docs dosyalarında değişiklik yok. `.gitignore`: Library, Temp, Obj, Logs, UserSettings, .vs ve artifacts çıktıları hariç tutulur; kaynaklar ve .meta dosyaları hariç tutulmaz.
- [x] **2. Paketleri çöz ve sabitle.** Yerel şablonda gözlenen URP 17.6.0, Input System 1.20.0, uGUI 2.6.0 değerlerini koru. Unity Package Manager'ın editörle uyumlu kararlı Test Framework sürümünü çöz, tam sürümü manifest/lock'a yaz. Kural kodundan önce en küçük NUnit discovery kontrolünü çalıştır; geçmesi kuralların testi değildir.
- [x] **3. Davranış testlerini ekle ve dar komutla başarısızlığını gözle.**

| Test | Girdi ve assertion |
|---|---|
| DeckTests.CatalogHas106UniqueTiles | `Assert.That(ids.Distinct().Count(), Is.EqualTo(106));` sayı yüzleri ikişer, sahte sayısı 2 |
| DeckTests.DealConservesAllTiles | seed 1, başlangıç 0: `hands=[22,21,21,21]`, `Stock.Count=20`, gösterge 1; birleşim 106 tekil kimlik |
| DeckTests.Indicator13WrapsJokerTo1 | gösterge Red/13; iki Red/1 gerçek okey, iki sahte normal Red/1 |
| DeckTests.RandomHasStableGoldenVector | xorshift32, seed 1: `Assert.That(first3, Is.EqualTo(new uint[]{270369,67634689,2647435461}));` |
| DeckTests.SameSeedAndStarterRepeatDeal | aynı seed/RoundId/başlangıçta gösterge, eller ve deste sırası birebir eşit; NextInt(1)=0, NextInt(0) ve negatif sınır reddedilir |
| ModelIsolationTests.InputAndReturnedCollectionsCannotMutateState | kaynak listeyi ve elde edilen kopyayı değiştir: `Assert.That(originalStateHash, Is.EqualTo(hashAfter));` |
| DeckTests.InvalidIdentityOrDuplicatePlacementIsRejected | −1/106 kimliği ve iki bölgede aynı kimlik: `Assert.That(valid, Is.False);` |

Run: `& .\scripts\Test-Unity.ps1 -Platform EditMode -Filter 'Okey101.Core.Tests.DeckTests;Okey101.Core.Tests.ModelIsolationTests'`. Beklenen ilk sonuç: ilgili eksik davranış assertion'ı veya henüz tanımlanmamış model/API; altyapı hatasını davranış başarısızlığı sayma.

- [x] **4. Modeli ve dağıtımı uygula.** xorshift32 algoritma kimliği `xorshift32-fy-v1`; seed 0 için başlangıç state `0x6D2B79F5`; işaretlenmemiş 32 bit taşma açıkça kullanılır. NextInt reddetmeli örneklemeyle modulo yanlılığını kaldırır. Numara taşı göstergeyi seç/ayır, kalan 105 taşı Fisher–Yates ile karıştır, başlangıç oyuncusunu seç veya doğrula, sırayla 21'er ve başlangıca bir ek taş dağıt. Başlangıç Phase=PlanningTurn, PendingDraw=null. Diğer tur başlangıçları AwaitingDraw. Bu PRNG yerel tekrar içindir, güvenli online karıştırma değildir.
- [x] **5. Aynı dar kontrolleri geçir.** Core assembly'nin UnityEngine referansı olmadığını ayrıca test et. Proje başlangıç import logunu ve çözülen paket sürümlerini kaydet. T01 sonucu henüz oynanabilir el değildir.

## T02 — Geçerli perler, çiftler ve okey karşılıkları

**Files:** Create C/Rules/MeldValidator.cs, MeldCandidateGenerator.cs; CT/MeldValidatorTests.cs, MeldCandidateTests.cs. Consumes T01 Tile/Meld/RuleResult.

**Interfaces:** `MeldValidator.Validate(MeldProposal group, TileFace indicator): RuleResult<int>`; sonuç açılış sayı toplamıdır. `MeldCandidateGenerator.Generate(IReadOnlyList<TileId> hand, TileFace indicator, MeldKind kind): IReadOnlyList<MeldProposal>`; varsayılanlar toplam azalan, eşitlikte renk Red/Yellow/Blue/Black ve kimlik sırasıyla döner. İki gerçek okeyden çiftte de eşit karşılıkları olan geçerli bir yorum üretilir.

- [x] **1. Testleri ekle.**

| Test | Girdi ve assertion |
|---|---|
| MeldValidatorTests.RunBoundaries | aynı renk 1-2-3 ve 11-12-13: `Success=true`; 12-13-1, 13-1-2, iki taş: `Success=false` |
| MeldValidatorTests.SetRequiresDistinctColors | Red/7, Blue/7, Black/7 toplam `21`; iki Red/7 içeren veya beş taşlık grup reddedilir |
| MeldValidatorTests.PairAndTwoJokers | aynı yüz iki fiziksel kopya ve iki gerçek okey çifti kabul; farklı sayı/renk normal çift reddedilir |
| MeldValidatorTests.TwoJokersUseDeclaredFaces | Red/5 + joker[Red/6] + joker[Red/7]: `Assert.That(total, Is.EqualTo(18));` |
| MeldCandidateTests.AmbiguousRunDefaultsToHighestScore | Red/5 + iki okey: önerilen 5-6-7 toplam `18`; 3-4-5 toplam 12 alternatif olarak bulunur |
| MeldValidatorTests.FakeJokerIsNotWildcard | normal karşılığı uymayan sahte okey: `Assert.That(result.Success, Is.False);` |

- [x] **2. Fail:** EditMode Filter `Okey101.Core.Tests.MeldValidatorTests;Okey101.Core.Tests.MeldCandidateTests`.
- [x] **3. Uygula:** normal taşın temsil değerini doğrula; yalnız gerçek okey serbest karşılık alabilir. Kimlik tekrarını reddet. Adayları fiziksel taş alt kümeleri ve iki joker yerleşimleri üzerinden üret; sadece toplam yüz sayısını kullanma.
- [x] **4. Pass:** aynı filtre; tam 1–13 per ve iki farklı renkli normal taşla yanlış çift dahil negatif durumları doğrula. Kanıtı kaydet.

## T03 — Puan kırılımı ve bitiş çarpanları

**Files:** Create C/Scoring/ScoreCalculator.cs; CT/ScoreCalculatorTests.cs. Consumes T01 ScoreBreakdown/FinishInfo/OpeningKind.

**Interface:** `ScoreCalculator.Calculate(OpeningKind opening, bool isWinner, int normalSum, int realJokerCount, int penalties, FinishInfo finish): ScoreBreakdown`.

- [x] **1. Testleri ekle.**

| Test | Assertion |
|---|---|
| ScoreCalculatorTests.KnownExamples | seri N12/J1/P0/M1 = `113`; çift aynı değerler = `226`; kazanan seri açmış ve yalnız gerçek okeyle bitmişse kaybeden çift = `452` |
| ScoreCalculatorTests.AllEightFinishCombinations | üç boolean bitiş özelliğinin 8 birleşiminde M sırasıyla çarpım; üçü true kazanan P0: `Total=-808` |
| ScoreCalculatorTests.PenaltyOrder | çift N12/J1/P101/M2: `Assert.That(Total, Is.EqualTo(654));` yani (226+101)×2 |
| ScoreCalculatorTests.UnopenedDoesNotAddHeldJoker | None/J2/P101/M2: `Assert.That(Total, Is.EqualTo(606));` |
| ScoreCalculatorTests.StockEndHasNoWinnerBonus | StockExhausted: M=1, None/P0=202; kazanan olamaz |
| ScoreCalculatorTests.CancellationDoesNotWriteZeroScore | AllPairs: `Assert.That(HasScore, Is.False); Assert.That(Total, Is.Null);` |

- [x] **2. Fail:** EditMode Filter `Okey101.Core.Tests.ScoreCalculatorTests`.
- [x] **3. Uygula:** B, P ve M'yi ayrı alanlarda koru. Negatif N/P veya 0–2 dışı J'yi programlama girdisi hatası olarak reddet; beklenen oyun hatalarını exception ile yönetme. Sahte okey normal değerine N'yi hesaplayan çağıran tarafından katılır.
- [x] **4. Pass:** aynı filtre; kazananın birikmiş P cezasının da M'ye girdiğini doğrula. Bitiş dışı sonuçta bitiş bayrakları kabul edilmez.

Kanıt ve uygulama sözleşmesi: [T03 test raporu](../../testing/t03-scoring.md). Dar kontrol 43/43, tam EditMode 97/97 geçti.

## T04 — Açılış, işleme ve okey değiştirme

**Files:** Create C/Rules/TableEvaluator.cs; CT/OpeningTests.cs, ProcessingTests.cs, JokerSwapTests.cs. Consumes T01 RoundState/TableAction ve T02 validator.

**Interface:** `TableEvaluator.Evaluate(RoundState drawnState, IReadOnlyList<TableAction> actions): RuleResult<RoundState>`. Sonuç yeni aday durumdur; girdi değişmez. Henüz atış/puan/sıra değişimi yapmaz.

- [x] **1. Testleri ekle.** Test fixture'ları RoundTestBuilder ile bütün 106 kimliği tek konuma yerleştirir; örnek eldeki gerekli taşlar, kalanlar deste/diğer eller ile tamamlanır.

| Test | Girdi ve assertion |
|---|---|
| OpeningTests.Series100Fails101Succeeds | yeni grup toplamı 100: InvalidOpening; 101: Opening=Series |
| OpeningTests.FourPairsFailFiveSucceed | dört çift reddedilir; beş çiftte Opening=Pairs; toplam aranmaz |
| OpeningTests.ProcessedTilesNeverCountTowardOpening | yeni gruplar 100, işlemede 1: `Assert.That(Error, Is.EqualTo(InvalidOpening));` |
| OpeningTests.OpeningKindCannotChange | Series sonrası OpenGroups(Pairs) ve tersi: OpeningTypeLocked |
| ProcessingTests.OpenThenProcessInSamePlan | geçerli OpenGroups ardından ExtendMeld kabul; ters sırada OpeningRequired |
| ProcessingTests.PairAreaRequiresSomeoneOpenedPairs | seri oyuncunun AddGroup(Pair) hamlesi çift açan yoksa reddedilir; varsa kabul, Opening=Series kalır |
| ProcessingTests.PairPlayerBudgetIsPerTurn | iki ayrı seri grubuna 1+1 kabul; üçüncü taş ProcessingLimit; yeni çift bütçeyi artırmaz |
| JokerSwapTests.BindingAndIdentityArePreserved | doğru eşdeğer taş yerleşir, aynı fiziksel joker ele döner; kaynak durum hash'i değişmez |
| JokerSwapTests.SwapConsumesOneBudgetAndCanKeepJoker | Pairs oyuncu seri grubunda swap: SeriesTilesUsed=1; ele gelen joker elde kalabilir |
| JokerSwapTests.FakeReplacementAndPairSwap | yalnız karşılığı eşleşen sahte değişim geçerli; çiftte swap bütçeyi artırmaz |
| JokerSwapTests.WrongReplacementIsAtomic | yanlış renk/sayı, jokerle joker değişimi veya başka oyuncunun taşını kullanma: başarısız; tüm bölgeler öncekiyle aynı |

- [x] **2. Fail:** EditMode Filter `Okey101.Core.Tests.OpeningTests;Okey101.Core.Tests.ProcessingTests;Okey101.Core.Tests.JokerSwapTests`.
- [x] **3. Uygula:** eylemleri sırayla aday kopyaya uygula; işlem bütçesini taş adediyle say. Yeni grup kimliği artar. Eski okey karşılığını koru; masadaki normal taşları taşıyan, grubu bölen/birleştiren hiçbir aksiyon API'si ekleme.
- [x] **4. Pass:** aynı filtre; hata sonrası her fiziksel taş için konum ve karşılık eşitliğini kontrol et. Seri açanın gruba uygun işlemesinde sayısal bütçe sınırı uygulama.

Kanıt ve çağıran sözleşmesi: [T04 test raporu](../../testing/t04-table-actions.md). Dar kontrol 64/64, tam EditMode 161/161 geçti.

## T05 — Tam tur doğrulaması ve kesin yandan alma araması

**Files:** Create C/Model/PlayerViewProjector.cs; C/Rules/PenaltyEvaluator.cs, DiscardTurnFinder.cs; C/Turns/TurnEvaluator.cs; CT/TurnEvaluatorTests.cs, DiscardTurnFinderTests.cs, PenaltyTests.cs. Consumes T02–T04; T03 skor hesabını T06 sonuçlandırması kullanır.

**Interfaces:** `PlayerViewProjector.ForPlayer(RoundState state, int actor): PlayerView`; `TurnEvaluator.Evaluate(RoundState drawnState, TurnPlan plan): RuleResult<RoundState>`; `PenaltyEvaluator.GetDiscardPenalty(RoundState afterActions, TileId discard, bool isWinningDiscard): int`; `DiscardTurnFinder.Find(PlayerView beforeDraw, CancellationToken token): RuleResult<TurnPlan>`. Find, geçerli yandan çekiş sonrası kullanılacak bir tanık plan döndürür; başarı yerine NoLegalUse veya Cancelled ayrımı korunur. TurnEvaluator yalnız aday masa/eldeki taşlar/atık/P'yi değiştirir; Version, komut sequence'i, sonraki oyuncu ve nihai Result T06'nın sorumluluğudur. PendingDraw, OpeningAtTurnStart ve son kapalı taş bilgisi sonuçlandırmaya kadar korunur.

- [x] **1. Testleri ekle.**

| Test | Girdi ve assertion |
|---|---|
| TurnEvaluatorTests.DiscardIsMandatory | tüm taşları masaya koyan plan LastTileRequired; bir son taş atılan plan geçerli |
| TurnEvaluatorTests.PreviousDiscardUsesExactPhysicalTile | alınan kopya elde kalıp aynı yüzlü diğeri masadaysa DiscardMustBeUsed |
| DiscardTurnFinderTests.UnopenedMustUseDiscardInOpening | taş yalnız işlenebiliyor fakat 101/5 çift oluşmuyorsa NoLegalUse |
| DiscardTurnFinderTests.SwapWitnessMayKeepRecoveredJoker | yandan eşdeğer normal taş, swap'a girer; çıkan okey elde, başka taş atılır: `Assert.That(result.Success, Is.True);` |
| DiscardTurnFinderTests.FindsMultiActionPathWithoutHeuristicCutoff | önce geçerli açılış sonra gerekli swap/işleme yolu; tanık plan tam doğrulamada başarılı |
| DiscardTurnFinderTests.CancelIsNotProofOfIllegality | iptal edilmiş arama: `Assert.That(Error, Is.EqualTo(Cancelled));` NoLegalUse değil |
| PenaltyTests.SameDiscardHasOnlyOnePenalty | gerçek okey aynı zamanda işlek olsa da ceza `101`, 202 değil |
| PenaltyTests.BudgetAndOpeningAffectPenalty | açmamış veya çift bütçesi dolmuş işlek atış 0; açık ve hakkı varsa 101 |
| PenaltyTests.FinishAndExcludedActions | bitiş atışı 0; yalnız çift eklenebilen/okey değiştirilebilen normal taş için işlek cezası 0 |
| TurnEvaluatorTests.PenaltyNeedsExplicitAcceptance | cezalı atış AcceptPenalty=false: PenaltyConfirmationRequired; true: P yalnız 101 artar |

- [x] **2. Fail:** EditMode Filter `Okey101.Core.Tests.TurnEvaluatorTests;Okey101.Core.Tests.DiscardTurnFinderTests;Okey101.Core.Tests.PenaltyTests`.
- [x] **3. Uygula:** atılacak fiziksel taşın elde olduğunu doğrula; önce masa eylemleri, sonra atış ve R10, sonra bitiş/ceza bilgisi. Yandan alma araması yalnız PlayerView'daki kendi el/kamu durumu üzerinden hipotetik çekiş kurar. Tüm geçerli grup alt kümelerini, karşılıkları, izinli ekleme/swap sıralarını ara; ziyaret anahtarı el kimlikleri, gruplar/karşılıklar, açılış ve bütçeyi içerir. Son taşı bırakmayan dalları ele. Stratejik puan sıralaması kullanılabilir, doğruluk için aday sayısı sınırı kullanılamaz; döngüler ziyaret kümesiyle kesilir.
- [x] **4. Pass:** aynı filtre; tanık planı bağımsız TurnEvaluator ile tekrar doğrula. Aynı aramayı bot strateji bütçesiyle sınırlama. Kalan 106 taşın nerede olduğu aramanın sonucu üzerinde etkili olmamalı.

Kanıt ve arama sınırları: [T05 test raporu](../../testing/t05-turn-validation.md). T05'in 60 testi dahil tam EditMode 221/221 geçti.

## T06 — Komut motoru, sıra ve sonuçlandırma

**Files:** Create C/Turns/GameEngine.cs; extend C/Turns/StateInvariant.cs; CT/GameEngineTests.cs, RoundEndTests.cs. Consumes T01–T05.

**Interface:** `GameEngine.Apply(RoundState state, GameCommand command, CancellationToken token): RuleResult<RoundState>`. Başarıda Version tam 1 artar; geçersiz komutta otorite değişmez. Çekirdek senkron/saf C#'tır; ağır aramayı uygulama kuyruğu görüntü döngüsü dışında çalıştırır. Yandan çekiş kontrolü için T05'teki saf PlayerViewProjector kullanılır; Application bağımlılığı oluşturulmaz.

- [x] **1. Testleri ekle.**

| Test | Girdi ve assertion |
|---|---|
| GameEngineTests.Initial22TurnCannotDrawAgain | ilk oyuncu Draw: WrongPhase; elindeki bir taşı atınca sonraki oyuncu AwaitingDraw |
| GameEngineTests.NormalTurnAndDirection | çekiş sonrası 22, atış sonrası 21; NextPlayer=(p+1)%4; çift çekiş reddedilir |
| GameEngineTests.StaleDuplicateWrongActorAndWrongRound | sırasıyla StaleVersion/DuplicateCommand/WrongPlayer/WrongRound; durum hash'i değişmez |
| GameEngineTests.DiscardDrawRequiresWitness | T05 NoLegalUse ise çekiş yok; Cancelled ise çekiş kesinleşmez |
| GameEngineTests.FailAfterSeveralActionsRollsBackAll | son eylemi geçersiz plan: masa/el/bütçe/P/version aynı; önceden kesinleşmiş çekiş korunur |
| RoundEndTests.LastStockDrawCanStillWin | son çekiş turunda geçerli bitiş: Reason=DiscardWin ve doğru M |
| RoundEndTests.LastStockNoFinishScoresWithoutWinner | Reason=StockExhausted, Winner=null, M=1; sonraki tur yok |
| RoundEndTests.FakeJokerScoresAssignedNumber | gösterge Red/7 iken eldeki sahte okey N'ye 8 ekler; gerçek okey yalnız J'ye 1 ekler |
| RoundEndTests.FourPairsCancelAfterCompletedTurn | dört Pairs, bitiş yok: Cancelled, tüm Total=null; yeniden dağıtımda aynı başlangıç |
| RoundEndTests.WinningDiscardHasPriority | bitiş ve deste sonu aynı anda: DiscardWin; bitiş atışına 101 ceza yok |
| GameEngineTests.ConservationAfterEveryAcceptedCommand | `Assert.That(uniqueLocations, Is.EqualTo(106));` önceki durum nesnesi değişmez |

- [x] **2. Fail:** EditMode Filter `Okey101.Core.Tests.GameEngineTests;Okey101.Core.Tests.RoundEndTests`.
- [x] **3. Uygula:** RoundId/Actor/Sequence/ExpectedVersion/Phase kontrolünü yan etkiden önce yap. Çekişi kesinleştir; PendingDraw ve LastStock bilgisini sakla. CommitTurn sonrası bitiş, dört çift iptali, deste sonu, sonraki oyuncu sırasıyla çöz. Kazanma yokken N/J değerlerini fiziksel ellerden üret; T03'e gönder. Komut başına yalnız ilgili oyuncunun kabul edilmiş sequence değerini yükselt. Terminal durumda yeni oyun komutu kabul etme.
- [x] **4. Pass:** aynı filtre; T01–T06 çekirdek testlerini aşama öncesi bir kez birlikte çalıştır. Bu geçiş görsel veya Windows build kanıtı değildir.

Kanıt ve otorite sözleşmesi: [T06 test raporu](../../testing/t06-command-engine.md). Dar kontrol 51/51, tam EditMode 272/272 geçti.

## T07 — Yerel oturum ve etkileşimli temel masa — A0

**Files:** Create A/Application/Okey101.Application.asmdef, LocalSession.cs, TurnDraft.cs; A/Presentation/Okey101.Presentation.asmdef, GameBootstrap.cs, Input/TableInput.cs, Table/TablePresenter.cs, Table/TileView.cs, Table/RackLayout.cs, UI/TurnToolbar.cs; A/Editor/ProjectSetup.cs, BuildWindows.cs; `scripts/Build-Windows.ps1`; A/Scenes/Bootstrap.unity, Table.unity; A/Prefabs/Tiles/Tile.prefab, Table/Table.prefab, Table/Rack.prefab; A/Tests/EditMode/Okey101.Application.Tests.asmdef, PlayerViewTests.cs; A/Tests/PlayMode/Okey101.PlayMode.Tests.asmdef, TableSmokeTests.cs.

**Interfaces:** `LocalSession.Start(uint seed, int? starter): void`, `GetView(int actor): PlayerView`, `SubmitAsync(GameCommand command, CancellationToken token): Task<CommandReceipt>`, `SetPaused(bool paused): void`, `IsPaused: bool`, `Dispose(): void`; T05'in PlayerViewProjector arayüzünü tüketir. `TurnDraft.Reset(PlayerView view): void`, `SetPlan(TurnPlan plan): void`, `Build(): TurnPlan`; `TablePresenter.Render(PlayerView view): void`; `ProjectSetup.CreateOrUpdate(): void` yalnız bu görevdeki sahipli varlıkları oluşturur/günceller. `GameBootstrap` oturum, sunum ve yaşam döngüsünün composition root'udur. `Okey101.Editor.BuildWindows.Development(): void`, `Build-Windows.ps1 -Configuration Development` A0 paketini üretir; başarısız BuildReport sıfır olmayan sonuç verir. Windows64/Mono/DX11 seçimi T13 ile aynıdır.

- [x] **1. Testleri ekle.** `PlayerViewTests.OpponentFacesAndStockOrderAreAbsent`: görünüm şemasında rakip Hand/deste dizisi yok; `OwnHand.Count=21/22`. `PlayerViewTests.PublicDrawEventHidesTileFromOthers`: diğer oyuncunun stock çekişinde TileId=null. `TableSmokeTests.DrawDiscardAndReorder`: ıstaka dizmesi otorite Version'ını değiştirmez; çekiş ve atış toplam iki sürüm ilerletir. `TableSmokeTests.RejectedCommandKeepsAllTilesVisible`: görünümde kayıp/çift taş yok. Assertion'lar `Is.Null`, `Is.EqualTo(expectedVersion)` ve kimlik kümelerinin eşitliğidir.
- [x] **2. Fail:** ilgili EditMode PlayerViewTests ve PlayMode TableSmokeTests filtreleri.
- [x] **3. Uygula:** Core assembly referansını açıkça ekle. Oturum tek seri komut kuyruğuyla otoriteyi değiştirir; ağır arama ve bot işi için görüntü döngüsünü bloke etmez. Snapshot projeksiyonu kuyruğun tutarlı durumundan kopyalanır. Unity nesneleri yalnız ana iş parçacığında kullanılır. Duraklatılmış oturumda SubmitAsync WrongPhase ile reddedilir; yeni bir oyun hamlesi kesinleşmez. Geliştirme sırasında oyuncu perspektifi/test kontrolü seçilebilir; normal görünüm insan=0'dır. Bu kontrol bot yerine sunulmaz. Test assembly'leri Core ve TestSupport'a referans verir; PlayMode assembly'si Editor-only Core.Tests'e referans vermez.
- [x] **4. Görsel temel oluştur:** tek ortografik kamera, ilkel masa/ıstaka/taşlar; 24 ıstaka yuvası; Input System UI modülü. Taş al ve at, tıkla-seç/tıkla-hedef, yalnız yerleşimi değiştiren Diz çalışır. Dört oyuncu sıra bilgisi ve deste sayısı görünür.
- [x] **5. Pass ve A0 kabulü:** filtreleri geçir; `& .\scripts\Build-Windows.ps1 -Configuration Development` ile ilk Windows paketini üret, çalıştır. 1280×720 ve 1920×1080'de eldeki 22 taşın görünür olduğunu gözle. CPU/GPU/RAM, çözünürlük ve kalite bilgisini `artifacts/acceptance/a0-machine.md` dosyasına yaz. A0 deneyimi kuralların tüm UI'sini veya botları içerdiği iddiası taşımaz.

T07 iki çözünürlük native gözlemi, T09'un güncel Release sahnesinde kapatıldı; eski A0 paketi için geriye dönük görsel kabul iddiası yok. Ayrıntı: [T09 raporu](../../testing/t09-bots.md). T08 hazır ellerle kapsamlı native kontrolü ayrı ve açık.

## T08 — Açılış/işleme önizlemesi ve tam insan kontrolü — A1

**Files:** Create A/Presentation/UI/OpeningPanel.cs, JokerBindingPicker.cs, PenaltyDialog.cs, Table/MeldAreaView.cs; A/Tests/PlayMode/TurnInteractionTests.cs; Modify A/Application/TurnDraft.cs, A/Presentation/Input/TableInput.cs, UI/TurnToolbar.cs, Table/TablePresenter.cs, A/Scenes/Table.unity.

**Interfaces:** `TurnDraft.PreviewAsync(CancellationToken token): Task<TurnPreview>`; `TurnPreview` (A/Application/TurnDraft.cs) = Valid, Error, OpeningTotal, PairCount, BindingChoices, ProjectedOwnHand, ProjectedTable, SeriesTilesUsed, DiscardPenalty. Başka oyuncunun gizli eli bulunmaz. `OpeningPanel.Show(TurnPreview preview): void`; `JokerBindingPicker.Choose(TileId joker, TileFace face): void`; `PenaltyDialog.ConfirmAsync(int penalty, CancellationToken token): Task<bool>`.

- [x] **1. PlayMode testlerini ekle.** `Opening101AndFivePairsShowExactTotals`: 100'de onay kapalı, 101/beş çiftte açık; `CanProcessAfterDraftOpening`: aynı tur ikinci eylem geçerli; `DiscardWitnessSwapLeavesJokerInRack`: yandan normal taş swap'a girince çıkan joker elde kalır; `ChangingJokerChoiceRecomputesPreview`: toplam yeni seçime eşit; `RejectThirdProcessingAndUndoDraft`: üçüncü taş reddedilir, geri almada çekilmiş taş korunur; `PenaltyCancelDoesNotCommit`: iptalde Version/P değişmez; `CommitRequiresDiscard`: taş seçilmeden onay kapalı.
- [x] **2. Fail:** PlayMode Filter `Okey101.PlayMode.Tests.TurnInteractionTests`.
- [x] **3. Uygula:** bütün girişler Core/oturum API'sine gider; UI içinde ikinci kural hesabı yazma. Önizleme sıralı taslağın kopyasıdır, kalıcı otoriteyi değiştirmez. Yandan alma uygunluğu araması sürerken tekrar çekiş engellenir; kullanıcı aramayı iptal edip çekiş kesinleşmeden kapalı kaynağı seçebilir. İptal sonucu NoLegalUse gibi gösterilmez. Yeni okey karşılıkları yalnız onaydan önce değiştirilebilir.
- [ ] **4. Pass ve A1 kabulü:** filtre ve R01–R17 kapsam tablosunu kontrol et. Hazır seri/çift/değişim/bitiş durumlarıyla insan kontrolünü elle dene; puan sonucunu T03 örnekleriyle karşılaştır. R01 bot ve R17 bot adımlatma kabulünün A2'de kapanacağını raporda açık tut; ilk oynanabilir sürüm tamamlandı deme.

## T09 — Üç kurallı, gizli bilgiye erişmeyen bot — A2

**Files:** Create A/Bots/Okey101.Bots.asmdef, BotPlanner.cs, BotRunner.cs; A/Tests/EditMode/BotPlannerTests.cs; A/Tests/PlayMode/BotLifecycleTests.cs. Modify A/Presentation/GameBootstrap.cs, A/Presentation/Okey101.Presentation.asmdef, A/Tests/EditMode/Okey101.Application.Tests.asmdef, A/Tests/PlayMode/Okey101.PlayMode.Tests.asmdef, A/Scenes/Bootstrap.unity. Bots, Core/Application'a referans verir; ters yönde Application → Bots referansı eklenmez.

**Interfaces:** `BotPlanner.ChooseDraw(PlayerView view, CancellationToken token): DrawDecision`, `PlanTurn(PlayerView view, CancellationToken token): TurnPlan`; `DrawDecision` aynı dosyada = Source + nullable Witness. `BotRunner.RunOneTurnAsync(int actor, CancellationToken token): Task`; yalnız LocalSession'ın o oyuncu görünümü ve komut makbuzunu kullanır.

- [x] **1. Testleri ekle.**

| Test | Assertion |
|---|---|
| BotPlannerTests.SeriesAndPairOpeningFixtures | ayrı el fixture'larında önerilen plan geçerli; Opening sırasıyla Series/Pairs |
| BotPlannerTests.FinishesWithDiscardWhenPossible | bitiş fixture'ında `Assert.That(final.Winner, Is.EqualTo(actor));` |
| BotPlannerTests.UsesWitnessAndRespectsTwoTileBudget | yandan seçerse tanık planın fiziksel taşı kullanması; Pairs işlemesi en fazla 2 |
| BotPlannerTests.HiddenStateCannotAffectDecision | aynı PlayerView, farklı gizli eller/deste: `Assert.That(planA, Is.EqualTo(planB));` kanonik plan karşılaştırması |
| BotPlannerTests.SameObservationProducesSameDecision | tekrar üretilebilir aynı plan; rastgele zaman bütçesi yok |
| BotLifecycleTests.NewRoundRejectsOldBotResult | eski görev tamamlandığında yeni RoundId/Version/eller aynı kalır |
| BotLifecycleTests.PauseAndResumeHasExactlyOneAction | durakta komut yok; devam sonrası bir tur, iki kez çekiş yok |

- [x] **2. Fail:** EditMode BotPlannerTests ve PlayMode BotLifecycleTests.
- [x] **3. Uygula:** bitiren tur önceliği, sonra elde kalan ceza değerini azaltma; yandan almayı kapalıdan çekişe göre açıkça faydalıysa veya bitiriyorsa tercih et. Strateji sınırı başlangıçta 4096 arama düğümüdür, saat süresi değildir; bu sınır T05 kesin aramasına uygulanmaz. Eşitlik: daha az kalan taş, daha düşük ceza, kanonik eylem/TileId sırası. Rastgele atma stratejisi kullanma. Kapalı çekişte güvenli atış geri dönüşü, yandan çekişte önceden doğrulanmış tanık plan korunur.
- [x] **4. Yaşam döngüsünü bağla:** GameBootstrap botları ve oturumu birleştirir; yeni el/menü/duraklatma cancellation token'ını iptal eder. Devamda taze görünümle yeniden hesapla. Her komutta yeni RoundId/Version kontrolü vardır. Eski görev sonucunu yeni elde yeniden etiketleyip gönderme. Gerekli Bots referansları yalnız sunum/composition root ve test assembly'lerine eklenir.
- [x] **5. Pass:** filtreler; insan+üç botla normal tamamlanan elin elle gözlemi. Her bot yetenek fixture'ını ayrıca geçir; yalnız destenin bitmesi botların yeterliliği sayılmaz.

Kanıt: [T09 raporu](../../testing/t09-bots.md). EditMode290/290, PlayMode30/30; Development ve Release build başarılı. Native720p insan+üç bot normal bitişi Version27/stock9, Bot1 kazanır; 1080p22taş, sıralama/taşıma/duraklat-devam gözlendi. Menü henüz T12 kapsamında; mevcut sahne dispose/yeni oturum/duraklatma sınırları test edildi. T10 başlamadı.

## T10 — Senaryolar, tekrar kaydı ve 1.000 dağıtım kabulü

**Files:** Create A/Application/ReplayCodec.cs, ScenarioLoader.cs; A/Presentation/UI/DebugPanel.cs; A/Editor/ScenarioExporter.cs; A/Tests/EditMode/ReplayTests.cs, ScenarioLoaderTests.cs, SimulationTests.cs; A/Tests/Fixtures/Scenarios/series-opening.json, pair-opening.json, joker-swap.json, finish-x8.json, stock-end.json, four-pairs.json. Modify LocalSession.cs ve Table.unity. Geliştirme dışı build'de test senaryoları ve panel paketlenmez.

**Interfaces:** `ReplayCodec.Write(ReplayRecord record, string path): void`, `Read(string path): RuleResult<ReplayRecord>`; `ScenarioLoader.Load(string path): RuleResult<RoundState>`. ReplayRecord aynı dosyada = SchemaVersion=1, RuleVersion="101-v1", ShuffleVersion="xorshift32-fy-v1", Seed, InitialState (tam dağıtım/deste/başlangıç/gösterge), kabul edilmiş GameCommand listesi, ExpectedFinalHash. `DebugPanel.StepBotAsync(): Task`, `LoadScenario(string path): void`. ScenarioExporter, T01 builder mantığını ürün koduna bağımlı yapmadan aynı invariant'larla dosya üretir.

- [x] **1. Testleri ekle.** `ReplayTests.RestoresExactFinalStateAndScores`: bütün kimlik/karşılık/puanlar ve final hash eşit; `ReplayTests.AnimationSettingsDoNotAffectReplay`: animasyon açık/azaltılmış aynı kanonik sonuç; `ScenarioLoaderTests.RejectsMalformedUnknownVersionOversizeAndDuplicates`: bozuk JSON, bilinmeyen sürüm, 20 MiB üstü dosya, 107 taş/çift kimlik reddedilir ve aktif el hash'i değişmez; `ScenarioLoaderTests.RejectsRootedTraversalAndExternalTargets`: uygulama kayıt kökü dışı dosya yazımı reddedilir; `SimulationTests.Seeds0To999TerminateLegally`: her başlangıçtan botlu terminal sonuç veya iptal, her komut sonrası 106 kimlik, bütün skorlar R12–R16 ile uyumlu.
- [x] **2. Fail:** önce ReplayTests/ScenarioLoaderTests EditMode; simülasyonu ayrı çalıştır.
- [x] **3. Uygula:** polimorfik CLR tip adı deserialization kullanma; sabit komut/action discriminant allowlist'i ve açık JSON DTO'ları kullan. Kayıt sırası sürümlüdür; kültüre bağlı sayı/sıra yok. Kayıt kökü `Application.persistentDataPath/Diagnostics` altında doğrulanır; dosya adını uygulama üretir. Okumada boyut/sürüm ve StateInvariant önce doğrulanır, ancak sonra aktif oturum değiştirilir. Dışa aktarma kullanıcı eylemiyle yapılır, kendiliğinden sunucuya gönderilmez. Yazma hatası görünür ve güvenli biçimde bildirilir; mevcut el değişmez.
- [x] **4. Pass:** dar filtreler. Simülasyon komutu: `& .\scripts\Test-Unity.ps1 -Platform EditMode -Filter 'Okey101.Application.Tests.SimulationTests' -Category Simulation`. 0–999 tohumları, terminal türleri ve bot açılış/bitiş sayıları raporlanır. Dağıtım başına 2000 kabul edilmiş komut denetim sınırıdır; aşılırsa test hata verir, oyunu zorla bitirip başarılı saymaz. İptal otomatik yeniden dağıtım döngüsü simülasyonda yeni bir test vakası sayılmaz.
- [x] **5. A2 kabulünü kapat:** altı fixture test panelinden yüklenir, bot tek tur adımlanır, kayıt tekrar oynatılır. 1.000 dağıtım sonucu hedefli kural testlerinin yerine geçmez.

Kanıt: [T10 raporu](../../testing/t10-replay-and-scenarios.md). Son EditMode308/308, PlayMode32/32, ayrı Simulation1000/1000;320normal679desteSonu1AllPairs,39312komut. Native geliştirme panelinde altı fixture/adım/kayıt/tekrar; ×8 −808 ve iptalde puan yokluğu gözlendi. Son Development/Release0hata0uyarı, DLL/asset paket kapısı6/0fixture. T08'in bütün insan kontrolü native kabulü ayrı açık; T11–T13 başlamadı. A2'nin bu görevdeki bot/senaryo/tekrar kısmı kapandı, menü akışı T12'de.

## T11 — Masa varlıkları, hareket ve okunabilirlik — A3

**Files:** Create A/Art/Models/Tile.fbx, Rack.fbx, Table.fbx; A/Art/Materials/Felt.mat, Wood.mat, Tile.mat; A/Art/Textures/TileFaces.png; A/Art/Audio/draw.wav, place.wav, discard.wav; A/Art/Sources/okey-table.blend; A/Art/asset-sources.md; A/Presentation/Table/TileMotion.cs, UI/TableViewport.cs, Audio/TableAudio.cs; A/Tests/PlayMode/TablePresentationTests.cs. Modify A/Prefabs/Tiles/Tile.prefab, A/Prefabs/Table/Rack.prefab, A/Prefabs/Table/Table.prefab, A/Presentation/Table/TablePresenter.cs, A/Scenes/Table.unity. Önce kurulu Blender'ı kontrol et; yoksa eksik aracı bildir, üretimi yapılmamış FBX/Blender varlığını varmış gibi gösterme. Yasal kaynak kaydı olmayan dış varlık kullanma.

**Interfaces:** `TileMotion.PlayAsync(IReadOnlyList<VisibleEvent> events, bool reducedMotion, CancellationToken token): Task`; `TableViewport.FocusGroup(int meldId): void`, `ResetFocus(): void`; `TableAudio.Play(VisibleEvent visibleEvent): void`.

- [x] **1. PlayMode testleri:** `RepeatedClickDuringMotionCommitsOnce`: bir fiziksel taş/tek komut; `ReducedMotionHasSameFinalPlacement`: normal/azaltılmış hareket sonunda aynı TileId hedefleri; `PauseDoesNotAdvanceOrLoseAnimation`: duraklatmada konum sabit, devamda doğru son; `SceneUnloadCancelsWithoutTouchingDestroyedObjects`: hata logu ve sahipsiz iş yok; `ManyGroupsRemainSelectable`: 60+ açık taşlı kurulu durumda yakın görünümde doğru grup kimliği seçilir.
- [x] **2. Fail:** PlayMode Filter `Okey101.PlayMode.Tests.TablePresentationTests`.
- [x] **3. Üret ve bağla:** onaylı keçe/ahşap/kırık beyaz yönü. Bir ortak taş geometrisi ve paylaşılan atlas; gereksiz taş başına materyal yok. FBX ölçek/eksen kontrolü, basit çarpışma yüzeyi; kontrolsüz fizik simülasyonu yok. 150–220 ms hareket başlangıcı, completion/cancellation ile giriş kilidi; kural mantığını animasyon callback'ine taşıma. Masa merkezinin kaydırma/yakın görünümü kamerayı döndürmez.
- [ ] **4. Pass ve görsel kontrol:** filtre; 1280×720/1920×1080/2560×1440/2560×1080 ekran görüntüleri `artifacts/acceptance/a3/` altında. 720p'de insan taşı sayı yüksekliği en az 24 px; 22 taş görünür; Türkçe karakterler ve renk simgeleri okunur. Ses düzeyi/tekrar spam'i elle kontrol edilir. Görsel dosyaların varlığı görsel kabul yerine geçmez.
- [x] **5. İlk profil:** A0'da kaydedilen bilgisayarda paketlenmiş geliştirme sürümünde CPU/GPU kare sürelerini al. Hedef 1080p/60 FPS; yakalanmayan hedefi geçti diye raporlama. Son kararlılık ölçümü T13'tedir.

T11 uygulama/otomatik kontroller hazır; [T11 raporu](../../testing/t11-table-presentation.md). EditMode308/308, PlayMode43/43 (T11'de11test), iki paket0hata/0derlemeuyarısı, Development/Release fixture6/0 ve probe paket kapısı doğrulandı. Dört gerçek paket görüntüsü incelendi; 720p sayı enaz36.43px. Son kısa600kare ölçümü59.97FPS, karep9516.670ms (öncekiaday16.715ms); T13 uzun ölçüm hedefi geçmiş sayılmadı. 13 taşlık perde başlık örtüşmesi RED/GREEN ile düzeltildi. Madde4'ün görüntü/test kısmı geçti; ses seviyesi/tekrar rahatsızlığının insan onayı ve son native güvenlik penceresi sonrası kontrol bekliyor. T12 başlamadı.

## T12 — Menü, ayarlar, sonuç ve güvenli yaşam döngüsü

**Files:** Create A/Application/LocalSettings.cs; A/Presentation/UI/MainMenu.cs, PauseMenu.cs, ResultPanel.cs, LeaveRoundDialog.cs; A/Prefabs/UI/MainMenu.prefab, PauseMenu.prefab, ResultPanel.prefab; A/Tests/PlayMode/GameFlowTests.cs. Modify Bootstrap.unity, Table.unity, LocalSession.cs, BotRunner.cs.

**Interfaces:** `LocalSettings.Load(): LocalSettings`, `Save(): void`; alanlar MasterVolume 0–1, Fullscreen bool, ReducedMotion bool. `ResultPanel.Show(RoundResult result): void`; `LeaveRoundDialog.ConfirmAsync(CancellationToken token): Task<bool>`.

- [x] **1. PlayMode testleri:** `MenuStartsOneHumanThreeBots`: dört oyuncu ve tek oturum; `PauseStopsBotAndResumeDoesNotDoubleSubmit`: tek tur ilerleme; `FocusLossPausesLocalSession`: odak kaybında görüntü ve bot işi durur, otomatik komut gelmez; `CancelLeavePreservesExactRound`: RoundId/Version/eller aynı; `ConfirmLeaveCancelsOldWork`: eski bot/sahne görevleri yeni masaya dokunmaz; `ReplayNextStarterAndCancelledSameStarter`: normal yeni elde sıra bir ilerler, AllPairs yeniden dağıtımında başlangıç değişmez; `ResultShowsBreakdownAndCancellation`: N/J/P/B/M/Total görünür, iptalde sıfır puan yerine puan yazılmadığı gösterilir.
- [x] **2. Fail:** PlayMode Filter `Okey101.PlayMode.Tests.GameFlowTests`.
- [x] **3. Uygula:** Yerel Oyna/Ayarlar/Çıkış, duraklat/devam, sonuç ve yeniden oyna. Ayarlar yerel basit depoda tutulur; oturum kaydet/devam et menüsü ekleme. Bozuk ayarda güvenli varsayılan yükle, hatayı iç loga yaz; kullanıcı dosyası yüzünden oyunu başlatamaz hale getirme. Onay iptalinde mevcut oturumu dispose etme. Tüm iptal token'larını sahne/oturum sahibi temizler.
- [ ] **4. Pass:** filtre; masa aç/kapat ve yeni oyun döngüsünü 10 kez elle dene. Aynı anda iki oturum/bot döngüsü, yetim animasyon veya çoğalan ses üreticisi olmadığını Profiler/Hierarchy ile kontrol et.

## T13 — Windows paketi ve nihai kabul — A4

**Files:** Create docs/testing/windows-acceptance.md, docs/testing/rule-test-matrix.md; Modify A/Editor/ProjectSetup.cs, A/Editor/BuildWindows.cs, scripts/Build-Windows.ps1. Outputs: `artifacts/builds/windows-development/Okey101.exe`, `artifacts/builds/windows-release/Okey101.exe` ve yan veri dosyaları; `artifacts/acceptance/a4-report.md`.

**Interfaces:** `Okey101.Editor.BuildWindows.Development(): void`, `Release(): void`; ikisi BuildPipeline sonucunu kontrol eder, başarısızsa sıfır olmayan çıkış verir. Betik `-Configuration Development|Release` kabul eder; Unity `-batchmode -quit -projectPath ... -executeMethod ... -logFile ... -force-d3d11` ile çağrılır. Çalışan Editor kilidi varsa durur, süreç öldürmez. Build hedefi StandaloneWindows64, grafik API'si Direct3D11, başlangıç backend'i Mono'dur; Mono build desteği yoksa gerçek engeli bildir, sessizce farklı backend seçme.

- [x] **1. Nihai test matrisini kapat.** Aşağıdaki R01–R17 tablosundaki dosya/test isimlerini üretilmiş testlerle karşılaştır; eksik davranış varsa ilgili görevde test ve uygulamayı tamamla. Yalnız belge satırının bulunması kapsama kanıtı sayılmaz. T13 kaynak + gerçek XML sınıfları: docs/testing/rule-test-matrix.md, artifacts/acceptance/t13-test-groups.json.
- [ ] **2. Aşama testlerini çalıştır.** Tüm EditMode (Simulation hariç), tüm PlayMode; T10'daki 1.000 dağıtımı son mantık değişikliğinden sonra bir kez çalıştır. XML'de beklenen grupların gerçekten çalıştığını doğrula. Hata varsa önce dar filtreyle teşhis et; aynı değişmemiş tam suite'i tekrar tekrar çalıştırma.
- [ ] **3. İki Windows yapısını üret.** Development'ta test paneli vardır; Release'te panel/senaryolar/özel tam deste kayıtları ürün içeriği olarak bulunmaz. Başarılı BuildReport, mevcut tarih/sürüm/kural sürümü ve gerçek çıktı yolu raporlanır. Derleme succeeded logu tek başına oynanış kabulü değildir.
- [ ] **4. Paketlenmiş oyunu elle dene.** Yerel Oyna; normal tam el; seri ve çift açılışı; değişim; cezalı atış iptal/onay; özel bitiş fixture'ları Development'ta; sonuç ve yeniden oyna; duraklat/odak değişimi; menüye dönüş. Kullanıcının açabileceği exe ve gereken yan dosyalar birlikte teslim edilir.
- [ ] **5. Kararlılık ve performans.** Aynı A0 bilgisayarında 1080p/aynı kalite; ısınma sonrası 10 dakika kare zamanı kaydı, p95 hedef 16,7 ms; toplam 30 dakika kararlılık, bellek ve duraklat/yeni el döngüsü. VSync kaynaklı bekleme ile CPU/GPU çalışma zamanını ayrı raporla. Sorunlu frame'i ayrıca incele; ortalama FPS ile takılmayı gizleme.
- [ ] **6. Son teslim kaydı.** a4-report içinde geçen/başarısız/çalıştırılmayan/ortamdan engellenen kontrolleri ayır. Windows testini Android/iOS veya online kabulü sayma. İlk oynanabilir tamamlandı iddiası ancak R01–R17, üç bot, gerçek Windows paketi ve elle tam el kontrolü kapandıysa yapılır.

## Kural ve aşama eşlemesi

| Kural | Sahip görev | Kanıt dosyası/senaryo |
|---|---|---|
| R01 kapsam | T07/T09/T12 | TableSmokeTests, BotPlannerTests, GameFlowTests |
| R02 dağıtım/gösterge | T01 | DeckTests |
| R03 tur | T06 | GameEngineTests |
| R04 gruplar | T02 | MeldValidatorTests, MeldCandidateTests |
| R05 açılış | T04/T08 | OpeningTests, TurnInteractionTests |
| R06 işleme izinleri | T04 | ProcessingTests |
| R07 bütçe | T04/T05 | ProcessingTests, PenaltyTests |
| R08 okey karşılığı | T02/T04/T08 | MeldCandidateTests, JokerSwapTests, TurnInteractionTests |
| R09 değişim | T04/T05 | JokerSwapTests, DiscardTurnFinderTests |
| R10 yandan alma | T05/T06/T08 | DiscardTurnFinderTests, TurnEvaluatorTests, TurnInteractionTests |
| R11 son taş | T05/T06 | TurnEvaluatorTests, RoundEndTests |
| R12 temel puan | T03/T06 | ScoreCalculatorTests, RoundEndTests |
| R13 çarpanlar | T03/T06 | ScoreCalculatorTests.AllEightFinishCombinations |
| R14 cezalar | T03/T05/T08 | ScoreCalculatorTests.PenaltyOrder, PenaltyTests, TurnInteractionTests |
| R15 deste sonu | T06 | RoundEndTests |
| R16 dört çift | T06/T12 | RoundEndTests, GameFlowTests |
| R17 yardım/bot/test paneli | T07–T12 | PlayerViewTests, BotPlannerTests, ReplayTests, GameFlowTests |

| Aşama | Kapanış |
|---|---|
| A0 | T01–T07: çekme/atma/dizme çalışan temel masa |
| A1 | T08: bütün kural eylemleri insan tarafından denenebilir; bot/test paneli kabulü A2'ye bağlı |
| A2 | T09–T10: üç bot, hazır senaryolar, tekrar ve 1.000 dağıtım |
| A3 | T11–T12: masa hissi, okunabilirlik, ekran akışı |
| A4 | T13: gerçek Windows paketi ve ayrı kabul raporu |

İlk interaktif masadan önce çekirdek görevlerinin yapılması, kuralları sahneye gömmemek içindir. T01 resmi şablonun statik sahnesi erken açılabilir; oynanış A0 kanıtı değildir. Takvim tahmini ilk import, lisans/build desteği ve ilk kural görevleri görülmeden verilmez.

## Yürütme ve gözden geçirme

Öneri: aynı sohbet içinde sıralı uygulama, sonunda bağımsız kod incelemesi. T01–T08 ortak durum/komut sözleşmelerine sıkı bağlıdır; erken bağımsız yazım gereksiz arayüz uyuşmazlığı yaratır. Her görev dar test/çıktı incelemesiyle kapatılır; aşamalar kullanıcı tarafından oynanır. Görev başına ayrı ajanlı çalışma istenirse kullanıcı seçimiyle etkinleştirilir; bu belge kendiliğinden ajan başlatmaz veya model seçmez.

Bu belgenin onayı ürün kodunun yazıldığı veya testlerin geçtiği anlamına gelmez. Plan incelemesi ve yürütme yöntemi seçiminin ardından T01'den başlanır. Önceden onaylanmış kurallar yeniden sorulmaz.

## Kaynak ve plan doğrulama notu

- Yerel resmi URP şablonu arşivinin manifesti okunarak URP 17.6.0/Input System 1.20.0/uGUI 2.6.0 doğrulandı; proje kurulmadı.
- [Unity Test Framework komut satırı](https://docs.unity3d.com/Packages/com.unity.test-framework@1.4/manual/reference-command-line.html): platform, filtre ve NUnit sonuç dosyası davranışı. Projede çözülen paketle ilk discovery çalışmasında yeniden doğrulanır.
- [Unity 6.6 Editor komutları](https://docs.unity.com/en-us/engine/6000.6/manual/unity-editor/command-line-arguments/editor): batch ve executeMethod çağrıları.
- Bu aşamada yalnız planın kapsamı, bağlantıları, görev bağımlılıkları ve API adları incelendi. Listelenen oyun testleri, build ve görsel kabul henüz çalıştırılmadı.

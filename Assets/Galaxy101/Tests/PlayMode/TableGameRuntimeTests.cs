using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Galaxy101.Table;
using NUnit.Framework;
using Okey101.Core.Model;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>Live table: engine-driven deal, turns, bots, my draw/discard and the rack input's ray mapping.</summary>
    public sealed class TableGameRuntimeTests
    {
        private GameObject gameOwner, sceneOwner, hudOwner;
        private string statePath;
        private float savedTimeScale;
        private Mouse mouse;
        private InputSettings.EditorInputBehaviorInPlayMode? savedBehavior;
        private InputSettings.BackgroundBehavior? savedBackground;

        private TableGame Build(int human, uint seed, bool withHud)
        {
            savedTimeScale = Time.timeScale;
            // TableGame first: TableScene.Awake sees it and skips the static demo layout.
            gameOwner = new GameObject("TableGame"); var game = gameOwner.AddComponent<TableGame>(); game.Configure(human, seed);
            sceneOwner = new GameObject("GalaxyTable"); sceneOwner.AddComponent<TableScene>();
            if (withHud)
            {
                statePath = Path.Combine(Path.GetTempPath(), "galaxy-table-game-" + Guid.NewGuid().ToString("N") + ".json");
                WriteLanguage(statePath);
                hudOwner = new GameObject("TableHud"); hudOwner.SetActive(false);
                hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
                var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
                gameOwner.AddComponent<RackInput>();
            }
            return game;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = savedTimeScale;
            if (mouse != null) { InputSystem.RemoveDevice(mouse); mouse = null; }
            if (savedBackground.HasValue) { InputSystem.settings.backgroundBehavior = savedBackground.Value; savedBackground = null; }
            if (savedBehavior.HasValue) { InputSystem.settings.editorInputBehaviorInPlayMode = savedBehavior.Value; savedBehavior = null; }
            foreach (var go in new[] { hudOwner, gameOwner, sceneOwner }) if (go != null) UnityEngine.Object.Destroy(go);
            yield return null;
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        /// <summary>GALAXY_TABLE_LANG=en runs the table tests with an English profile (text overflow pass).</summary>
        private static void WriteLanguage(string path)
        {
            string lang = Environment.GetEnvironmentVariable("GALAXY_TABLE_LANG");
            if (string.IsNullOrEmpty(lang)) return;
            File.WriteAllText(path, "{\"version\":1,\"Settings\":{\"lang\":\"" + lang + "\"}}");
        }

        /// <summary>
        /// Text overflow check (TR and EN): every visible label stays inside the 1280×720 canvas and inside its in-flow
        /// parent's width; absolutely placed badges may overhang their parent but not the canvas.
        /// </summary>
        private static void AssertNoOverflow(VisualElement canvas, string where)
        {
            var bounds = canvas.worldBound;
            var problems = new List<string>();
            canvas.Query<Label>().ForEach(label =>
            {
                if (string.IsNullOrEmpty(label.text) || label.resolvedStyle.display == DisplayStyle.None || label.resolvedStyle.visibility == Visibility.Hidden || label.resolvedStyle.opacity <= 0.01f) return;
                ScrollView scroll = null;
                for (var e = label.parent; e != null && e != canvas; e = e.parent) { if (e.resolvedStyle.display == DisplayStyle.None || e.name == "table-anchors") return; if (e is ScrollView sv && scroll == null) scroll = sv; if (e.parent != null && e.parent.name == "table-social" && e.resolvedStyle.translate != Vector3.zero) return; if (e.name == "table-gifts" && e.resolvedStyle.translate.x != 0) return; }
                var r = label.worldBound;
                // Inside a scroll list only the horizontal extent counts: rows below the fold are clipped, not overflowing.
                var box = scroll != null ? scroll.worldBound : bounds;
                if (r.xMin < box.xMin - 1 || r.xMax > box.xMax + 1 || scroll == null && (r.yMin < bounds.yMin - 1 || r.yMax > bounds.yMax + 1)) problems.Add("outside " + (scroll != null ? "its list" : "canvas") + ": '" + label.text + "' " + r);
                var parent = label.parent;
                if (parent != null && label.resolvedStyle.position != Position.Absolute && label.layout.width > parent.contentRect.width + 1.5f && parent.resolvedStyle.width > 0)
                    problems.Add("wider than its parent '" + parent.name + "': '" + label.text + "' " + label.layout.width.ToString("F0") + " > " + parent.contentRect.width.ToString("F0"));
            });
            Assert.That(problems, Is.Empty, where + " text overflow: " + string.Join(" | ", problems));
        }

        private static IEnumerator Until(Func<bool> condition, float realSeconds, string what)
        {
            float end = Time.realtimeSinceStartup + realSeconds;
            while (!condition()) { if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what); yield return null; }
        }

        private static void AssertBoardMatchesEngine(TableGame game)
        {
            var view = game.Session.GetView(TableGame.Me);
            Assert.That(game.Rack.Tiles.OrderBy(x => x), Is.EqualTo(view.OwnHand.Select(t => t.Value).OrderBy(x => x)), "my rack holds exactly my hand");
            foreach (var id in game.Rack.Tiles) Assert.That(game.Board.ViewOf(id), Is.Not.Null, "rack tile " + id + " has a view");
            for (int k = 1; k < 4; k++) Assert.That(game.Board.HiddenCount(k), Is.EqualTo(view.Players[k].HandCount), "seat " + k + " hidden tiles");
            foreach (var pile in view.Discards) foreach (var id in pile) Assert.That(game.Board.ViewOf(id.Value), Is.Not.Null, "discard " + id + " visible");
            foreach (var meld in view.Table) foreach (var t in meld.Group.Tiles) Assert.That(game.Board.ViewOf(t.Id.Value), Is.Not.Null, "meld tile visible");
        }

        [UnityTest]
        [Timeout(420000)]
        public IEnumerator BotsPlayAWholeHandAndTheBoardTracksEveryTile()
        {
            var game = Build(-1, 4242, true);
            Time.timeScale = 6;
            yield return Until(() => game.Current == TableGame.Phase.Opponent, 30, "the deal to finish");
            int checks = 0; long lastChecked = -1; float deadline = Time.realtimeSinceStartup + 380;
            while (game.LastResult == null)
            {
                var view = game.Session.GetView(TableGame.Me);
                // Check each engine version once the board has reconciled it (commands complete asynchronously).
                if (view.Result == null && game.SyncedVersion == view.Version && view.Version != lastChecked && game.Current == TableGame.Phase.Opponent)
                { lastChecked = view.Version; AssertBoardMatchesEngine(game); checks++; }
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("The hand did not finish in time; turns=" + game.TurnsPlayed);
                yield return null;
            }
            Assert.That(game.TurnsPlayed, Is.GreaterThan(4));
            Assert.That(checks, Is.GreaterThan(4));

            // Finish banner, then the hand result card (T24) with every seat, then the next hand.
            var hud = hudOwner.GetComponent<TableHud>();
            yield return Until(() => game.ResultOpen, 30, "the hand result card");
            yield return null;
            Assert.That(hud.Canvas.Q<VisualElement>("table-result-card"), Is.Not.Null);
            for (int k = 0; k < 4; k++) Assert.That(hud.Canvas.Q<VisualElement>("result-row-" + k), Is.Not.Null, "result row " + k);
            Assert.That(hud.Canvas.Q<Label>("result-next-label").text, Does.StartWith("Sonraki el").Or.StartWith("Next hand"));
            string capture = Environment.GetEnvironmentVariable("GALAXY_TABLE_RESULT_CAPTURE");
            if (!string.IsNullOrEmpty(capture)) { Time.timeScale = 1; yield return Capture(hudOwner.GetComponent<UIDocument>(), capture); Time.timeScale = 6; }
            Debug.Log("GALAXY101_TABLE_SOAK turns=" + game.TurnsPlayed + " reason=" + game.LastResult.FinishInfo.Reason + " checks=" + checks);
            game.NextHand();
            yield return Until(() => game.HandNumber == 2 && !game.ResultOpen, 30, "the next hand");
            Assert.That(game.Current, Is.EqualTo(TableGame.Phase.Deal).Or.EqualTo(TableGame.Phase.Opponent).Or.EqualTo(TableGame.Phase.Draw).Or.EqualTo(TableGame.Phase.Discard));
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator IDiscardDrawAndDiscardAgainWhileBotsPlay()
        {
            var game = Build(TableGame.Me, 777, true);
            Time.timeScale = 1;
            yield return Until(() => game.Current == TableGame.Phase.Seating, 10, "the waiting table");
            Assert.That(game.Seated, Is.EqualTo(new[] { true, false, false, false }));
            var waitingHud = hudOwner.GetComponent<TableHud>();
            yield return null;
            Assert.That(waitingHud.Canvas.Q<VisualElement>("table-waiting"), Is.Not.Null);
            Assert.That(waitingHud.Canvas.Q<VisualElement>("invite-1"), Is.Not.Null);
            yield return Until(() => game.Seated[2], 5, "Ayşe to join at 1.3 s");
            string seatCapture = Environment.GetEnvironmentVariable("GALAXY_TABLE_SEAT_CAPTURE");
            if (!string.IsNullOrEmpty(seatCapture)) yield return Capture(hudOwner.GetComponent<UIDocument>(), seatCapture);
            game.Invite(1);
            Assert.That(game.Seated[1], Is.True, "Davet et fills the seat at once");
            yield return Until(() => game.Countdown > 0, 10, "the 3-2-1 countdown");
            Time.timeScale = 3;
            yield return Until(() => game.CanDiscard, 40, "my first discard turn (I start with 22 tiles)");
            Assert.That(game.Rack.Count, Is.EqualTo(22));
            yield return Until(() => game.SyncedVersion == game.Session.GetView(TableGame.Me).Version, 5, "the board to reconcile");
            AssertBoardMatchesEngine(game);

            // The rack input maps a screen point through the HUD canvas onto the very tile drawn there.
            yield return null;
            var input = gameOwner.GetComponent<RackInput>();
            var hud = hudOwner.GetComponent<TableHud>();
            int probe = game.Rack.Tiles.First();
            var cam = Camera.allCameras.First(c => c.targetTexture != null && c.CompareTag("MainCamera"));
            var vp = cam.WorldToViewportPoint(game.Board.ViewOf(probe).Target.position);
            var canvasPoint = new Vector2(vp.x * TableHud.Width, (1 - vp.y) * TableHud.Height);
            var panelPoint = hud.Canvas.LocalToWorld(canvasPoint);
            float scale = hud.Canvas.panel.visualTree.worldBound.width / Screen.width;
            var screen = new Vector2(panelPoint.x / scale, Screen.height - panelPoint.y / scale);
            Assert.That(input.TryRay(screen, out var ray), Is.True);
            Physics.SyncTransforms();
            Assert.That(Physics.RaycastAll(ray, 10).Select(h => h.collider.GetComponent<TileView>()).Any(v => v != null && v.Id == probe), Is.True, "ray hits the probed tile");

            int first = game.Rack.Tiles.Last();
            game.Discard(first);
            yield return Until(() => !game.Rack.Tiles.Contains(first) && game.Current == TableGame.Phase.Opponent, 10, "my discard to be accepted");
            Assert.That(game.Session.GetView(TableGame.Me).Discards[0].Last().Value, Is.EqualTo(first));

            yield return Until(() => game.CanDraw, 90, "my draw turn after three bots");
            Assert.That(game.TurnsPlayed, Is.GreaterThanOrEqualTo(4), "me and three bots played");
            AssertNoOverflow(hudOwner.GetComponent<TableHud>().Canvas, "T28 header");
            game.RequestExit(); yield return null; yield return new WaitForSecondsRealtime(0.4f);
            AssertNoOverflow(hudOwner.GetComponent<TableHud>().Canvas, "T30 leave card");
            game.CancelExit();
            yield return Until(() => game.SyncedVersion == game.Session.GetView(TableGame.Me).Version, 5, "the board to reconcile");
            AssertBoardMatchesEngine(game);
            string capture = Environment.GetEnvironmentVariable("GALAXY_TABLE_GAME_CAPTURE");
            if (!string.IsNullOrEmpty(capture)) yield return Capture(hudOwner.GetComponent<UIDocument>(), capture);

            var task = game.Draw(DrawSource.Stock, null);
            yield return Until(() => task.IsCompleted, 10, "the draw");
            Assert.That(task.Result, Is.True);
            Assert.That(game.Rack.Count, Is.EqualTo(22));
            Assert.That(game.LastDrawn, Is.GreaterThanOrEqualTo(0));
            game.Discard(game.LastDrawn);
            yield return Until(() => game.Current == TableGame.Phase.Opponent || hudOwner.GetComponent<TableHud>().ActiveToast != null, 10, "the second discard");
            if (game.Current != TableGame.Phase.Opponent)
            {
                // Rule corner (e.g. a laid-off-able joker): any other tile is fine.
                game.Discard(game.Rack.Tiles.First(id => id != game.LastDrawn));
                yield return Until(() => game.Current == TableGame.Phase.Opponent, 10, "the fallback discard");
            }
            Assert.That(game.Rack.Count, Is.EqualTo(21));
        }

        /// <summary>World point → screen pixels (bottom-left), the inverse of RackInput.TryRay.</summary>
        private static Vector2 ScreenOf(TableHud hud, Vector3 world)
        {
            var cam = Camera.allCameras.First(c => c.targetTexture != null && c.CompareTag("MainCamera"));
            var vp = cam.WorldToViewportPoint(world);
            var panelPoint = hud.Canvas.LocalToWorld(new Vector2(vp.x * TableHud.Width, (1 - vp.y) * TableHud.Height));
            float scale = hud.Canvas.panel.visualTree.worldBound.width / Screen.width;
            return new Vector2(panelPoint.x / scale, Screen.height - panelPoint.y / scale);
        }

        private IEnumerator Pointer(Vector2 position, bool pressed)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = (ushort)(pressed ? 1 : 0) });
            yield return null; yield return null;
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator PointerDragsOnTheRackAndDoubleClickDiscards()
        {
            var game = Build(TableGame.Me, 9001, true);
            // Batch editors have no focused Game view; let device input reach play mode regardless.
            savedBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            savedBackground = InputSystem.settings.backgroundBehavior; InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>();
            yield return Until(() => game.CanDiscard && game.SyncedVersion == game.Session.GetView(TableGame.Me).Version, 40, "my first turn");
            yield return new WaitForSeconds(1.5f); // tiles settle after the deal
            var hud = hudOwner.GetComponent<TableHud>(); var table = sceneOwner.GetComponent<TableScene>();
            Assert.That(game.Rack.TryFind(game.Rack[0, 2], out var from), Is.True);
            int id = game.Rack[0, 2]; Assert.That(game.Rack[0, 14], Is.EqualTo(RackGrid.Empty));

            // Drag: press on the tile, move in steps (> 5 px), release over the empty top-right slot.
            Vector2 a = ScreenOf(hud, game.Board.ViewOf(id).Target.position), b = ScreenOf(hud, table.SlotPose(0, 0, 14).position);
            yield return Pointer(a, false); yield return Pointer(a, true);
            for (int i = 1; i <= 8; i++) yield return Pointer(Vector2.Lerp(a, b, i / 8f), true);
            yield return Pointer(b, false);
            Assert.That(game.Rack[0, 14], Is.EqualTo(id), "the dragged tile lands in the empty slot");
            Assert.That(game.Rack[0, 2], Is.EqualTo(RackGrid.Empty));
            yield return new WaitForSeconds(0.6f);

            // Double click a tile within 380 ms: discard.
            int victim = game.Rack[1, 5];
            Vector2 c = ScreenOf(hud, game.Board.ViewOf(victim).Target.position);
            yield return Pointer(c, false); yield return Pointer(c, true); yield return Pointer(c, false); yield return Pointer(c, true); yield return Pointer(c, false);
            yield return Until(() => !game.Rack.Tiles.Contains(victim), 10, "the double-click discard");
            Assert.That(game.Session.GetView(TableGame.Me).Discards[0].Last().Value, Is.EqualTo(victim));
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator ShortOpeningPenaltyAndPileTileCanBePutBack()
        {
            var game = Build(TableGame.Me, 5151, true);
            game.Configure(TableGame.Me, 5151, 30, false);
            var hud = hudOwner.GetComponent<TableHud>();
            Time.timeScale = 3;
            yield return Until(() => game.CanDiscard && game.SyncedVersion == game.Session.GetView(TableGame.Me).Version, 40, "my first turn");

            // T35: Aç below 101 is a short opening: +101, the tiles stay, a Penalty toast and my badge.
            Assert.That(HandEvaluation.Of(game.Rack, game.Indicator).Sum, Is.LessThan(101), "a freshly dealt rack is unsorted");
            int tiles = game.Rack.Count;
            hud.Press("open");
            yield return Until(() => game.Session.GetView(TableGame.Me).OwnPenalties == 101, 10, "the short opening penalty");
            yield return null;
            Assert.That(game.Rack.Count, Is.EqualTo(tiles));
            Assert.That(hud.ActiveToast, Does.StartWith("Eksik açılış").Or.StartWith("Short opening"));
            yield return new WaitForSecondsRealtime(0.4f);
            AssertNoOverflow(hud.Canvas, "T35 short opening");
            Assert.That(hud.Canvas.Q<VisualElement>("penalty-badge-0"), Is.Not.Null);
            Assert.That(game.PenaltyReasons(TableGame.Me).Single().Amount, Is.EqualTo(101));
            string penaltyCapture = Environment.GetEnvironmentVariable("GALAXY_TABLE_PENALTY_CAPTURE");
            if (!string.IsNullOrEmpty(penaltyCapture)) { Time.timeScale = 0.2f; yield return Capture(hudOwner.GetComponent<UIDocument>(), penaltyCapture); Time.timeScale = 3; }

            game.Discard(game.Rack.Tiles.First());
            yield return Until(() => game.CanDraw, 120, "my draw turn");
            var view = game.Session.GetView(TableGame.Me);
            if (view.Discards[3].Count == 0) Assert.Inconclusive("Burak's pile is empty this time.");

            // T38: the pile tile can always be taken; it is marked and cannot be thrown at the next player.
            int pile = view.Discards[3].Last().Value;
            var draw = game.Draw(DrawSource.PreviousDiscard, null);
            yield return Until(() => draw.IsCompleted, 10, "the pile draw");
            Assert.That(draw.Result, Is.True);
            Assert.That(game.PickedTile, Is.EqualTo(pile));
            yield return null;
            Assert.That(hud.Canvas.Q<VisualElement>("picked-tag"), Is.Not.Null);
            Assert.That(hud.Canvas.Q<VisualElement>("putback-ring"), Is.Not.Null);
            AssertNoOverflow(hud.Canvas, "T38 pile tile");
            string pileCapture = Environment.GetEnvironmentVariable("GALAXY_TABLE_PILE_CAPTURE");
            if (!string.IsNullOrEmpty(pileCapture)) { Time.timeScale = 0.2f; yield return Capture(hudOwner.GetComponent<UIDocument>(), pileCapture); Time.timeScale = 3; }
            game.Discard(pile);
            yield return null;
            Assert.That(hud.ActiveToast, Does.StartWith("Yerden aldığın taşı atamazsın").Or.StartWith("You can't discard"));
            yield return new WaitForSecondsRealtime(0.4f); // toastIn settles
            AssertNoOverflow(hud.Canvas, "T38 error");
            Assert.That(game.Rack.Tiles.Contains(pile), Is.True);

            // T39: put it back, then only the deck is left.
            game.PutBack();
            yield return Until(() => game.CanDraw, 10, "the put back");
            Assert.That(game.Board.ViewOf(pile).Duration, Is.EqualTo(0.3f).Within(1e-4f), "T39: back on the pile in 300 ms");
            Assert.That(game.CanDrawSide, Is.False);
            Assert.That(game.Session.GetView(TableGame.Me).Discards[3].Last().Value, Is.EqualTo(pile));
            Assert.That(game.Rack.Tiles.Contains(pile), Is.False);
            var stock = game.Draw(DrawSource.Stock, null);
            yield return Until(() => stock.IsCompleted, 10, "the deck draw");
            Assert.That(stock.Result, Is.True);
        }

        /// <summary>
        /// A hand in progress: I have opened, Zeynep (seat 1) has Blue 5 · okey · Blue 6… laid with the okey standing for
        /// Blue 7, and Blue 7 is on my rack. Indicator Black 13, so the okey is Black 1.
        /// </summary>
        private static RoundState OkeyOnTheTable(out int blueSeven, out int okey, out int wrong)
        {
            int Id(TileColor c, int n, int copy = 0) => 2 * (13 * (int)c + n - 1) + copy;
            var all = Enumerable.Range(0, 106).ToList();
            int indicator = Id(TileColor.Black, 13);
            okey = Id(TileColor.Black, 1); blueSeven = Id(TileColor.Blue, 7); wrong = Id(TileColor.Red, 7);
            var meldTiles = new[] { Id(TileColor.Blue, 5), Id(TileColor.Blue, 6), okey };
            var meld = new Meld(0, 1, new MeldProposal(MeldKind.Run, new[]
            {
                new PlacedTile(new TileId(meldTiles[0]), new TileFace(TileColor.Blue, 5)),
                new PlacedTile(new TileId(meldTiles[1]), new TileFace(TileColor.Blue, 6)),
                new PlacedTile(new TileId(okey), new TileFace(TileColor.Blue, 7)),
            }));
            var taken = new HashSet<int>(meldTiles) { indicator, blueSeven, wrong };
            var free = new Queue<int>(all.Where(id => !taken.Contains(id)));
            var mine = new List<int> { blueSeven, wrong }; while (mine.Count < 22) mine.Add(free.Dequeue());
            var hands = new List<int>[] { mine, new List<int>(), new List<int>(), new List<int>() };
            for (int k = 1; k < 4; k++) while (hands[k].Count < (k == 1 ? 18 : 21)) hands[k].Add(free.Dequeue());
            var stock = free.Select(id => new TileId(id)).ToArray();
            var openings = new[] { OpeningKind.Series, OpeningKind.Series, OpeningKind.None, OpeningKind.None };
            var players = Enumerable.Range(0, 4).Select(k => new PlayerState(hands[k].Select(id => new TileId(id)).ToArray(), openings[k], 0)).ToArray();
            var discards = Enumerable.Range(0, 4).Select(_ => (IReadOnlyList<TileId>)Array.Empty<TileId>()).ToArray();
            return new RoundState(Guid.NewGuid(), 12, 7, 1, 0, 8, RoundPhase.PlanningTurn, new TileId(indicator), stock, players,
                new[] { meld }, discards, new DrawReceipt(DrawSource.Stock, new TileId(mine[21]), false), OpeningKind.Series, 0, false, new long[] { 3, 3, 3, 3 });
        }

        /// <summary>Me to draw, not opened: Red 11-13, Blue 10-11, Yellow 5-9 on hand; Burak (seat 3) threw Blue 9.</summary>
        private static RoundState FedOpening(out int blueNine, out int[] groups, out int[] fillers)
        {
            int Id(TileColor c, int n, int copy = 0) => 2 * (13 * (int)c + n - 1) + copy;
            int indicator = Id(TileColor.Black, 13);
            blueNine = Id(TileColor.Blue, 9);
            groups = new[] { Id(TileColor.Red, 11), Id(TileColor.Red, 12), Id(TileColor.Red, 13), Id(TileColor.Blue, 10), Id(TileColor.Blue, 11),
                Id(TileColor.Yellow, 5), Id(TileColor.Yellow, 6), Id(TileColor.Yellow, 7), Id(TileColor.Yellow, 8), Id(TileColor.Yellow, 9) };
            var taken = new HashSet<int>(groups) { indicator, blueNine, Id(TileColor.Black, 1), Id(TileColor.Black, 1, 1) };
            var free = new Queue<int>(Enumerable.Range(0, 106).Where(id => !taken.Contains(id)));
            fillers = Enumerable.Range(0, 11).Select(_ => free.Dequeue()).ToArray();
            var hands = new List<int>[] { groups.Concat(fillers).ToList(), new List<int>(), new List<int>(), new List<int>() };
            free = new Queue<int>(free.Concat(new[] { Id(TileColor.Black, 1), Id(TileColor.Black, 1, 1) }));
            for (int k = 1; k < 4; k++) while (hands[k].Count < 21) hands[k].Add(free.Dequeue());
            var players = Enumerable.Range(0, 4).Select(k => new PlayerState(hands[k].Select(id => new TileId(id)).ToArray(), OpeningKind.None, 0)).ToArray();
            var discards = new IReadOnlyList<TileId>[] { Array.Empty<TileId>(), Array.Empty<TileId>(), Array.Empty<TileId>(), new[] { new TileId(blueNine) } };
            return new RoundState(Guid.NewGuid(), 10, 9, 1, 0, 4, RoundPhase.AwaitingDraw, new TileId(indicator), free.Select(id => new TileId(id)).ToArray(), players,
                Array.Empty<Meld>(), discards, null, OpeningKind.None, 0, false, new long[] { 3, 3, 3, 3 });
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator OpeningWithThePreviousDiscardFeedsItsOwnerAndTheTileGlows()
        {
            var state = FedOpening(out int blueNine, out int[] groups, out int[] fillers);
            var game = Build(TableGame.Me, 88, true);
            game.Configure(TableGame.Me, 88, 60, false, state);
            Time.timeScale = 3;
            yield return Until(() => game.CanDraw && game.SyncedVersion == game.Session.GetView(TableGame.Me).Version, 40, "my draw turn in the prepared hand");
            var draw = game.Draw(DrawSource.PreviousDiscard, null);
            yield return Until(() => draw.IsCompleted, 10, "taking Blue 9");
            // Arrange the rack: Red 11-13 · Blue 9-11 · Yellow 5-9 on top, the rest below.
            var plan = new Dictionary<int, RackSlot>();
            int[] top = { groups[0], groups[1], groups[2], -1, blueNine, groups[3], groups[4], -1, groups[5], groups[6], groups[7], groups[8], groups[9] };
            for (int i = 0; i < top.Length; i++) if (top[i] >= 0) plan[top[i]] = new RackSlot(0, i);
            for (int i = 0; i < fillers.Length; i++) plan[fillers[i]] = new RackSlot(1, i);
            game.ApplyRackPlan(plan);
            Assert.That(HandEvaluation.Of(game.Rack, game.Indicator).Sum, Is.GreaterThanOrEqualTo(101));
            game.OpenRuns();
            Assert.That(game.Rack.Tiles.Contains(blueNine), Is.False, "Blue 9 went into the opening");
            game.Discard(fillers[0]);
            yield return Until(() => game.Current == TableGame.Phase.Opponent, 10, "the committed opening");
            Assert.That(game.Session.GetView(TableGame.Me).Players[3].Penalties, Is.EqualTo(90), "Burak fed a 9: +90");
            Assert.That(game.HudState().GlowTile, Is.EqualTo(blueNine), "T34: the feeding tile glows");
            yield return null;
            Assert.That(hudOwner.GetComponent<TableHud>().Canvas.Q<VisualElement>("glow-tile-" + blueNine), Is.Not.Null);
            yield return new WaitForSecondsRealtime(1.4f);
            Assert.That(game.HudState().GlowTile, Is.EqualTo(-1), "for 1.2 s");
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator OkeyOnTheTableShowsWhatItStandsForAndCanBeTaken()
        {
            var state = OkeyOnTheTable(out int blueSeven, out int okey, out int wrong);
            var game = Build(TableGame.Me, 77, true);
            game.Configure(TableGame.Me, 77, 60, false, state);
            var hud = hudOwner.GetComponent<TableHud>();
            Time.timeScale = 3;
            yield return Until(() => game.CanDiscard && game.SyncedVersion == game.Session.GetView(TableGame.Me).Version, 40, "my turn in the prepared hand");

            // T33: the okey carries a "7" badge in Blue.
            var mark = game.HudState().Okeys.Single();
            Assert.That(mark.Id, Is.EqualTo(okey)); Assert.That(mark.Number, Is.EqualTo("7")); Assert.That(mark.Color, Is.EqualTo("#1F5FBF"));
            yield return null;
            Assert.That(hud.Canvas.Q<VisualElement>("okey-badge-" + okey), Is.Not.Null);

            // T47: dragging Blue 7 lights the okey up; T49: another tile is refused.
            game.SetDragTile(blueSeven);
            var dragging = game.HudState();
            Assert.That(dragging.Okeys.Single().Target, Is.True);
            Assert.That(dragging.DragBanner, Is.Not.Null);
            game.ShowOkeyTip(okey, false); yield return null;
            AssertNoOverflow(hud.Canvas, "T33/T47 okey");
            string okeyCapture = Environment.GetEnvironmentVariable("GALAXY_TABLE_OKEY_CAPTURE");
            if (!string.IsNullOrEmpty(okeyCapture)) { game.ShowOkeyTip(okey, false); yield return Capture(hudOwner.GetComponent<UIDocument>(), okeyCapture); }
            game.SetDragTile(-1);
            game.TakeOkey(wrong, okey);
            yield return null;
            Assert.That(hud.ActiveToast, Does.Contain("Mavi 7").Or.Contain("Blue 7"));
            Assert.That(game.Rack.Tiles.Contains(okey), Is.False);

            var okeyView = game.Board.ViewOf(okey);
            Assert.That(okeyView.transform.Find("tas_okey")?.gameObject.activeSelf, Is.True, "the okey on the table wears the gold mark");
            game.TakeOkey(blueSeven, okey);
            Assert.That(game.Rack.Tiles.Contains(okey), Is.True, "the okey comes to my rack");
            Assert.That(okeyView.Duration, Is.EqualTo(0.45f).Within(1e-4f), "T47: 450 ms flight to the rack");
            Assert.That(okeyView.transform.Find("tas_okey").gameObject.activeSelf, Is.True, "and keeps it on my rack");
            string rackOkeyCapture = Environment.GetEnvironmentVariable("GALAXY_TABLE_RACKOKEY_CAPTURE");
            if (!string.IsNullOrEmpty(rackOkeyCapture)) yield return Capture(hudOwner.GetComponent<UIDocument>(), rackOkeyCapture);
            Assert.That(game.Rack.Tiles.Contains(blueSeven), Is.False);
            int discard = game.Rack.Tiles.First(id => id != okey);
            game.Discard(discard);
            yield return Until(() => game.Current == TableGame.Phase.Opponent, 10, "the committed turn");
            Assert.That(game.Session.GetView(TableGame.Me).Players[1].Penalties, Is.EqualTo(101), "Zeynep laid the okey: +101");
            Assert.That(game.PenaltyReasons(1).Single().Amount, Is.EqualTo(101));
        }

        [UnityTest]
        [Timeout(600000)]
        public IEnumerator FiveHandMatchEndsWithStandingsXpAndPlayAgain()
        {
            var game = Build(TableGame.Me, 2468, true);
            game.Configure(TableGame.Me, 2468, 1.5f, false); // my turns time out: the table plays them for me
            var hud = hudOwner.GetComponent<TableHud>();
            int startLevel = hud.Model.Level; long startXp = hud.Model.Xp;
            Time.timeScale = 6;
            string resultCapture = Environment.GetEnvironmentVariable("GALAXY_TABLE_RESULT2_CAPTURE");
            for (int hand = 1; hand <= TableRules.Current.MatchHands; hand++)
            {
                yield return Until(() => game.ResultOpen && game.MatchHand == hand, 240, "hand " + hand + " result");
                yield return null;
                Assert.That(hud.Canvas.Q<VisualElement>("table-result-card"), Is.Not.Null);
                Assert.That(game.HudState().Result.Rows.Count, Is.EqualTo(4));
                yield return new WaitForSecondsRealtime(0.6f); // rows slide in
                AssertNoOverflow(hud.Canvas, "T42 hand " + hand + " result");
                if (hand == 1 && !string.IsNullOrEmpty(resultCapture)) { Time.timeScale = 1; yield return Capture(hudOwner.GetComponent<UIDocument>(), resultCapture); Time.timeScale = 6; }
                game.NextHand();
            }
            // T45–T46: the match end waits for the player.
            yield return Until(() => game.MatchOpen, 30, "the match end card");
            yield return null;
            Assert.That(hud.Canvas.Q<VisualElement>("table-match-card"), Is.Not.Null);
            yield return new WaitForSecondsRealtime(0.6f);
            AssertNoOverflow(hud.Canvas, "T45/T46 match end");
            var match = game.HudState().Match;
            Assert.That(match.Standings.Count, Is.EqualTo(4));
            Assert.That(match.Standings.Select(x => x.Total), Is.Ordered, "lowest total first");
            Assert.That(match.ByHand.All(row => row.Length == TableRules.Current.MatchHands), Is.True);
            Assert.That(match.Xp, Is.GreaterThanOrEqualTo(5 * TableRules.Current.XpHandOther));
            long gained = (hud.Model.Level - startLevel) * 0L + (hud.Model.Xp - startXp);
            Assert.That(hud.Model.Level > startLevel || hud.Model.Xp > startXp, Is.True, "XP is written to the profile");
            string matchCapture = Environment.GetEnvironmentVariable("GALAXY_TABLE_MATCH_CAPTURE");
            if (!string.IsNullOrEmpty(matchCapture)) { Time.timeScale = 1; yield return Capture(hudOwner.GetComponent<UIDocument>(), matchCapture); Time.timeScale = 6; }
            hud.Press("again");
            yield return Until(() => !game.MatchOpen && game.MatchHand == 0, 10, "a new match");
            Assert.That(game.Totals.All(t => t == 0), Is.True);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator LoadingScreenShowsProgressAndTheOpenFailedCard()
        {
            savedTimeScale = Time.timeScale;
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-loading-" + Guid.NewGuid().ToString("N") + ".json");
            WriteLanguage(statePath);
            hudOwner = new GameObject("Menu"); hudOwner.SetActive(false);
            var document = hudOwner.AddComponent<UIDocument>(); document.panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var menu = hudOwner.AddComponent<MainMenuController>(); hudOwner.SetActive(true); menu.Initialize(statePath);
            yield return null; yield return null;

            // T50
            var loading = new TableLoadingOverlay(menu.View, hudOwner.transform);
            loading.Show(5); loading.SetProgress(.64f);
            yield return null;
            Assert.That(menu.View.Root.Q<VisualElement>("table-loading"), Is.Not.Null);
            Assert.That(menu.View.Root.Q<Label>("table-loading-percent").text, Is.EqualTo("64%"));
            Assert.That(menu.View.Root.Q<VisualElement>("table-loading-tip"), Is.Not.Null);
            AssertNoOverflow(menu.View.Root.Q<VisualElement>("galaxy-canvas"), "T50 loading");
            string capture = Environment.GetEnvironmentVariable("GALAXY_TABLE_LOADING_CAPTURE");
            if (!string.IsNullOrEmpty(capture)) yield return Capture(document, capture);

            // T51
            bool retried = false, lobby = false;
            loading.ShowError(() => lobby = true, () => retried = true);
            yield return null;
            Assert.That(menu.View.Root.Q<VisualElement>("table-loading-error-card"), Is.Not.Null);
            AssertNoOverflow(menu.View.Root.Q<VisualElement>("galaxy-canvas"), "T51 open failed");
            string errorCapture = Environment.GetEnvironmentVariable("GALAXY_TABLE_LOADING_ERROR_CAPTURE");
            if (!string.IsNullOrEmpty(errorCapture)) yield return Capture(document, errorCapture);
            using (var click = NavigationSubmitEvent.GetPooled()) { var retry = menu.View.Root.Q<Button>("table-loading-retry"); click.target = retry; retry.SendEvent(click); }
            yield return null;
            Assert.That(retried, Is.True); Assert.That(lobby, Is.False);
            Assert.That(loading.ErrorVisible, Is.False);
            loading.Hide();
            Assert.That(menu.View.Root.Q<VisualElement>("table-loading"), Is.Null);
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator EffectsFollowTheDealTurnsAndTheFinish()
        {
            var game = Build(TableGame.Me, 6060, true);
            game.Configure(TableGame.Me, 6060, 30, false);
            string dir = Environment.GetEnvironmentVariable("GALAXY_TABLE_FX_CAPTURE_DIR");
            var document = hudOwner.GetComponent<UIDocument>();
            yield return Until(() => game.Current == TableGame.Phase.Deal, 20, "the deal");
            yield return null;
            // Shuffle beat (spec/10 §2): rings and particles, the deck spinning.
            Assert.That(game.Fx.LiveWaves, Is.GreaterThan(0)); Assert.That(game.Fx.LivePoints, Is.GreaterThan(40));
            Assert.That(game.Fx.DeckSpinning, Is.True, "the deck spins for 0.9 s");
            if (!string.IsNullOrEmpty(dir)) { Time.timeScale = 0.15f; yield return Capture(document, Path.Combine(dir, "fx_deal.png")); Time.timeScale = 1; }

            Time.timeScale = 3;
            yield return Until(() => game.CanDiscard && game.SyncedVersion == game.Session.GetView(TableGame.Me).Version, 40, "my first turn");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(game.Fx.DiscardRingAlpha, Is.GreaterThan(0.3f), "the discard ring pulses while I discard");
            Assert.That(game.Rack.Tiles.All(id => game.Board.ViewOf(id).Bar != null), Is.True, "rack tiles carry bars");
            game.Discard(game.Rack.Tiles.First());
            yield return Until(() => game.CanDraw, 120, "my draw turn");
            Time.timeScale = 1;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(game.Fx.DeckGlowAlpha, Is.GreaterThan(0.2f), "the deck glows while I draw");
            var draw = game.Draw(DrawSource.Stock, null);
            yield return Until(() => draw.IsCompleted, 10, "the draw");
            yield return new WaitForSecondsRealtime(1.0f);
            var drawn = game.Board.ViewOf(game.LastDrawn);
            Assert.That(drawn.NewUntil, Is.GreaterThan(Time.time), "a tile landing on my rack gets the blinking new bar");
            Assert.That(drawn.Bar.activeSelf, Is.True);
            if (!string.IsNullOrEmpty(dir)) { Time.timeScale = 0.25f; yield return Capture(document, Path.Combine(dir, "fx_new_bar.png")); Time.timeScale = 1; }
            game.SortRack(true); yield return null;
            Assert.That(game.Rack.Tiles.Any(id => game.Board.ViewOf(id).Valid) || HandEvaluation.Of(game.Rack, game.Indicator).ValidTiles.Count == 0, Is.True, "valid sets get green bars");
            if (!string.IsNullOrEmpty(dir)) yield return Capture(document, Path.Combine(dir, "fx_new_tile.png"));

            // Finish celebration (spec/10 §7): spot light and confetti bursts.
            var table = sceneOwner.GetComponent<TableScene>();
            game.Fx.Celebrate(table.Corner(2), game.AvatarOnScreen(2));
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(game.Fx.SpotIntensity, Is.GreaterThan(5)); Assert.That(game.Fx.LivePoints, Is.GreaterThan(30));
            if (!string.IsNullOrEmpty(dir))
            {
                yield return new WaitForSecondsRealtime(3.5f);
                Time.timeScale = 0.3f; game.Fx.Celebrate(table.Corner(2), game.AvatarOnScreen(2));
                yield return Capture(document, Path.Combine(dir, "fx_celebrate.png")); Time.timeScale = 1;
            }
        }

        private static void Click(VisualElement root, string name)
        {
            var e = root.Q<VisualElement>(name); Assert.That(e, Is.Not.Null, "element " + name);
            using (var evt = ClickEvent.GetPooled()) { evt.target = e; e.SendEvent(evt); }
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator SettingsWindowAppliesEveryChangeAtOnce()
        {
            var game = Build(TableGame.Me, 7070, true);
            game.Configure(TableGame.Me, 7070, 60, false);
            string dir = Environment.GetEnvironmentVariable("GALAXY_TABLE_SET_CAPTURE_DIR");
            var hud = hudOwner.GetComponent<TableHud>(); var document = hudOwner.GetComponent<UIDocument>(); var table = sceneOwner.GetComponent<TableScene>();
            Time.timeScale = 3;
            yield return Until(() => game.CanDiscard && game.SyncedVersion == game.Session.GetView(TableGame.Me).Version, 40, "my first turn");
            Time.timeScale = 1;

            // T16: Oyun tab with the design defaults.
            hud.Press("settings"); yield return null;
            Assert.That(hud.Window, Is.EqualTo("settings"));
            var root = hud.Canvas;
            Assert.That(root.Q<VisualElement>("table-settings"), Is.Not.Null);
            Assert.That(hud.Settings.AutoSort && hud.Settings.Highlight && hud.Settings.DoubleClick, Is.True);
            Assert.That(hud.Settings.WarnSeconds, Is.EqualTo(5));
            Assert.That(root.Q<VisualElement>("setting-warn-5").ClassListContains("selected"), Is.True);
            if (!string.IsNullOrEmpty(dir)) yield return Capture(document, Path.Combine(dir, "set_game.png"));
            yield return null; AssertNoOverflow(root, "settings · game");

            Click(root, "setting-autoSort"); Click(root, "setting-dbl"); yield return null;
            Assert.That(hud.Settings.AutoSort, Is.False); Assert.That(game.DoubleClickDiscard, Is.False, "double-click discard follows the switch");
            Assert.That(root.Q<VisualElement>("setting-autoSort").ClassListContains("on"), Is.False);
            Click(root, "setting-warn-0"); yield return null;
            Assert.That(hud.Settings.WarnSeconds, Is.EqualTo(0));
            Click(root, "setting-highlight"); yield return null;
            Assert.That(game.Rack.Tiles.Any(id => game.Board.ViewOf(id).Valid), Is.False, "no green bars once highlighting is off");

            // Language: the whole table switches, the window stays open.
            string startLang = hud.Settings.Text("lang");
            Click(root, "setting-lang-en"); yield return null;
            Assert.That(hud.Settings.Text("lang"), Is.EqualTo("en"));
            Assert.That(root.Query<Label>().ToList().Any(l => l.text == "Table settings"), Is.True, "the window speaks English");
            Assert.That(root.Q<Label>("deck-count").parent.Query<Label>().ToList().Any(l => l.text == "Deck"), Is.True, "the top bar speaks English");
            yield return null; AssertNoOverflow(root, "settings · game (en)");
            Click(root, "setting-lang-" + startLang); yield return null;

            // T17: Ses — sliders keep the value, toggles flip.
            hud.Press("settings-tab:1"); yield return null;
            Assert.That(root.Q<VisualElement>("setting-music"), Is.InstanceOf<GalaxySlider>());
            hud.Settings.Set("music", 30); Assert.That(hud.Settings.Int("music"), Is.EqualTo(30));
            Assert.That(hud.Settings.Int("master"), Is.EqualTo(80));
            if (!string.IsNullOrEmpty(dir)) yield return Capture(document, Path.Combine(dir, "set_sound.png"));
            yield return null; AssertNoOverflow(root, "settings · sound");

            // T18: Bildirimler — "Sıra ipuçları" hides the turn hints.
            hud.Press("settings-tab:3"); yield return null;
            Click(root, "setting-turnHint"); yield return null;
            Assert.That(game.HudState().TurnHint, Is.False);
            if (!string.IsNullOrEmpty(dir)) yield return Capture(document, Path.Combine(dir, "set_alerts.png"));
            yield return null; AssertNoOverflow(root, "settings · alerts");

            // T15: Görünüm — cards with SEÇİLİ; T20: Hologram + Neon cam, then a new tile style.
            hud.Press("settings-tab:2"); yield return null;
            Assert.That(root.Q<VisualElement>("setting-tableSkin-station").Q<Label>("setting-in-use"), Is.Not.Null);
            // Kare hızı (shared with the menu): Otomatik → 30 FPS cap, VSync off at once.
            Assert.That(root.Q<Label>("setting-fps-value").text, Does.StartWith(hud.L("Otomatik", "Auto")));
            Click(root, "setting-fps-next"); yield return null;
            Assert.That(hud.Settings.FrameRate, Is.EqualTo(30)); Assert.That((int)hud.Model.Settings["fps"], Is.EqualTo(30));
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0)); Assert.That(Application.targetFrameRate, Is.EqualTo(30));
            Assert.That(root.Q<Label>("setting-fps-value").text, Is.EqualTo("30 FPS"));
            if (!string.IsNullOrEmpty(dir)) yield return Capture(document, Path.Combine(dir, "set_look.png"));
            yield return null; AssertNoOverflow(root, "settings · look");
            Click(root, "setting-tableSkin-holo"); Click(root, "setting-rackSkin-neon"); yield return null;
            Assert.That(table.TableSkin, Is.EqualTo("holo")); Assert.That(table.RackSkin, Is.EqualTo("neon"));
            Assert.That(root.Q<VisualElement>("setting-tableSkin-holo").Q<Label>("setting-in-use"), Is.Not.Null, "the picked card shows SEÇİLİ");
            yield return null;
            foreach (var id in game.Rack.Tiles)
            {
                Assert.That(game.Rack.TryFind(id, out var slot), Is.True);
                Assert.That(Vector3.Distance(game.Board.ViewOf(id).transform.position, game.Board.SlotPose(slot).position), Is.LessThan(0.002f), "rack tiles sit on the new rack");
            }
            Click(root, "setting-tileSkin-gold"); yield return null;
            Assert.That(table.TileSkin, Is.EqualTo("gold"));
            var sample = game.Board.ViewOf(game.Rack.Tiles.First()).transform;
            Assert.That(sample.Find("tas_yuz").GetComponent<MeshRenderer>().sharedMaterial.name, Is.EqualTo("tas_yuz_gold"), "faces take the new atlas");
            Assert.That(sample.GetComponentsInChildren<Transform>(true).Count(t => t.name == "tas_govde"), Is.EqualTo(1), "one body per tile after the swap");
            hud.Press("settings-close"); yield return null;
            Assert.That(hud.Window, Is.Null);
            if (!string.IsNullOrEmpty(dir)) yield return Capture(document, Path.Combine(dir, "set_holo_neon_gold.png"));

            // T21–T22: Andromeda table and rack seen from above (1.3 s camera move).
            hud.Settings.Set("tableSkin", "andromeda"); hud.Settings.Set("rackSkin", "andromeda"); hud.Settings.Set("tileSkin", "classic"); hud.Settings.Set("view", "top");
            Assert.That(table.CameraRig.Current, Is.EqualTo(TableCameraRig.View.Top));
            yield return new WaitForSecondsRealtime(1.5f);
            if (!string.IsNullOrEmpty(dir)) yield return Capture(document, Path.Combine(dir, "set_top_andromeda.png"));
            hud.Settings.Set("tableSkin", "wood"); hud.Settings.Set("rackSkin", "walnut"); hud.Settings.Set("view", "seat");
            yield return new WaitForSecondsRealtime(1.5f);
            if (!string.IsNullOrEmpty(dir)) yield return Capture(document, Path.Combine(dir, "set_wood_walnut.png"));

            // Varsayılana dön: every value back, the scene too.
            hud.Press("settings"); yield return null;
            Click(root, "settings-reset"); yield return null;
            Assert.That(hud.Settings.AutoSort && hud.Settings.DoubleClick && hud.Settings.Highlight && hud.Settings.TurnHints, Is.True);
            Assert.That(hud.Settings.WarnSeconds, Is.EqualTo(5)); Assert.That(hud.Settings.Int("music"), Is.EqualTo(55));
            Assert.That(table.TableSkin, Is.EqualTo("station")); Assert.That(table.RackSkin, Is.EqualTo("walnut")); Assert.That(table.TileSkin, Is.EqualTo("classic"));
            Assert.That(table.CameraRig.Current, Is.EqualTo(TableCameraRig.View.Player));
            Assert.That(hud.Settings.FrameRate, Is.EqualTo(FramePacing.Auto)); Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => hud.Settings.Set("warn", 7), "only 3 / 5 / 10 / off");
            Click(root, "settings-done"); yield return null;
            Assert.That(hud.Window, Is.Null);
            AssertBoardMatchesEngine(game);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator PlayerCardChatAndGiftDrawerWork()
        {
            var game = Build(TableGame.Me, 8080, true);
            game.Configure(TableGame.Me, 8080, 90, false);
            game.DemoGifts = false; // the demo treats would interleave with the chat lines checked below
            string dir = Environment.GetEnvironmentVariable("GALAXY_TABLE_SOCIAL_CAPTURE_DIR");
            var hud = hudOwner.GetComponent<TableHud>(); var document = hudOwner.GetComponent<UIDocument>(); var root = hud.Canvas;
            hud.SocialRandom = new System.Random(4);
            var sent = new List<(string Kind, int From, int To)>(); hud.GiftSent += (kind, from, to) => sent.Add((kind, from, to));
            Time.timeScale = 3;
            yield return Until(() => game.CanDiscard && game.SyncedVersion == game.Session.GetView(TableGame.Me).Version, 40, "my first turn");
            Time.timeScale = 1;

            // T07: Zeynep's card beside her avatar, a free quick treat, a friend request.
            Click(root, "opponent-1"); yield return null;
            Assert.That(hud.Window, Is.EqualTo("card")); Assert.That(hud.CardSeat, Is.EqualTo(1));
            Assert.That(root.Q<VisualElement>("player-card-1"), Is.Not.Null);
            Click(root, "card-add-friend"); yield return null;
            Assert.That(hud.FriendRequested(1), Is.True);
            Assert.That(root.Q<Label>("card-add-friend").text, Is.EqualTo(TableStrings.Get("friendSent", hud.L("tr", "en"))));
            if (!string.IsNullOrEmpty(dir)) yield return Capture(document, Path.Combine(dir, "social_card.png"));
            yield return null; AssertNoOverflow(root, "player card");
            var card = root.Q<VisualElement>("player-card-1").worldBound; var avatar = root.Q<VisualElement>("opponent-1").worldBound;
            Assert.That(card.xMax, Is.LessThan(avatar.xMin), "the right seat's card opens to the left of the avatar");
            Click(root, "card-gift-tea"); yield return null;
            Assert.That(hud.Window, Is.Null, "a quick treat closes the card");
            Assert.That(hud.Chat.Last().Seat, Is.EqualTo(-1)); Assert.That(hud.Chat.Last().Text, Is.EqualTo(hud.L("Sen, Zeynep için çay ısmarladın", "You sent Zeynep tea")));
            Assert.That(sent, Is.EqualTo(new[] { ("tea", 0, 1) }));
            yield return new WaitForSecondsRealtime(2.1f);
            Assert.That(hud.Chat.Last().Seat, Is.EqualTo(1), "Zeynep thanks me 1.9 s later");

            // Unread count while the chat is closed.
            hud.BotChatter(2, 1); yield return null;
            Assert.That(hud.Unread, Is.EqualTo(2), "the thanks and the chatter are unread");
            Assert.That(root.Q<VisualElement>("table-chat-open").Query<Label>().ToList().Any(l => l.text == "2"), Is.True, "red count on the chat button");

            // T13: the chat drawer, quick phrases, typed text (80 characters at most), my bubble, a bot answer.
            hud.Press("chat"); yield return null;
            Assert.That(hud.Window, Is.EqualTo("chat")); Assert.That(hud.Unread, Is.EqualTo(0));
            Click(root, "chat-quick-0"); yield return null;
            Assert.That(hud.Chat.Last().Seat, Is.EqualTo(0)); Assert.That(hud.Chat.Last().Text, Is.EqualTo(TableStrings.Get("quick.0", hud.L("tr", "en"))));
            Assert.That(root.Q<Label>("balloon-0"), Is.Not.Null, "my bubble next to my avatar");
            hud.BotReplyChance = 1;
            root.Q<TextField>("chat-input").value = new string('a', 79) + "bc";
            Click(root, "chat-send"); yield return null;
            Assert.That(hud.Chat.Last().Text.Length, Is.EqualTo(80));
            Assert.That(root.Q<TextField>("chat-input").value, Is.Empty);
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(hud.Chat.Last().Seat, Is.InRange(1, 3), "a bot answers after 1.4 s");
            Assert.That(root.Q<VisualElement>("table-chat-log").Query<Label>("chat-bubble").ToList().Count, Is.GreaterThanOrEqualTo(4));
            if (!string.IsNullOrEmpty(dir)) yield return Capture(document, Path.Combine(dir, "social_chat.png"));
            yield return null; AssertNoOverflow(root, "chat");
            hud.Press("chat"); yield return null;
            Assert.That(hud.Window, Is.Null);

            // T08–T10: the gift drawer: KİME, tabs, free items, Gönder.
            hud.Press("gift"); yield return null;
            Assert.That(hud.Window, Is.EqualTo("gift")); Assert.That(hud.GiftTarget, Is.EqualTo("2"), "Ayşe by default");
            Assert.That(root.Q<Label>("gift-send").text, Is.EqualTo(TableStrings.Get("pick", hud.L("tr", "en"))));
            Click(root, "gift-item-tea"); yield return null;
            Assert.That(hud.GiftSelection, Is.EqualTo("tea")); Assert.That(root.Q<Label>("gift-send").text, Is.EqualTo(TableStrings.Get("send", hud.L("tr", "en"))), "free on a bot table");
            Assert.That(root.Q<VisualElement>("table-gifts").Query<Label>().ToList().Any(l => l.text == TableStrings.Get("balance", hud.L("tr", "en"))), Is.False, "no balance row on a bot table");
            if (!string.IsNullOrEmpty(dir)) yield return Capture(document, Path.Combine(dir, "social_gift_treats.png"));
            yield return null; AssertNoOverflow(root, "gift drawer · treats");
            Click(root, "gift-tab-1"); yield return null;
            Assert.That(hud.GiftSelection, Is.Null, "a tab change clears the pick");
            Click(root, "gift-item-cake"); Click(root, "gift-to-all"); yield return null;
            if (!string.IsNullOrEmpty(dir)) yield return Capture(document, Path.Combine(dir, "social_gift_gifts.png"));
            yield return null; AssertNoOverflow(root, "gift drawer · gifts");
            sent.Clear();
            Click(root, "gift-send"); yield return null;
            Assert.That(hud.Window, Is.Null);
            yield return new WaitForSecondsRealtime(.7f);
            Assert.That(sent.Select(x => x.To), Is.EqualTo(new[] { 1, 2, 3 }), "one cake per opponent, 180 ms apart");
            Assert.That(hud.Chat.Last(l => l.Seat < 0).Text, Is.EqualTo(hud.L("Sen herkese pasta gönderdin", "You sent everyone a cake")));
            hud.Press("gift"); yield return null;
            Assert.That(hud.GiftTarget, Is.EqualTo("all"), "the drawer remembers the last recipient");
            Click(root, "gift-tab-2"); Click(root, "gift-to-3"); yield return null;
            Click(root, "gift-item-emoji-5"); yield return null;
            if (!string.IsNullOrEmpty(dir)) yield return Capture(document, Path.Combine(dir, "social_gift_emoji.png"));
            yield return null; AssertNoOverflow(root, "gift drawer · emoji");
            sent.Clear(); Click(root, "gift-send"); yield return new WaitForSecondsRealtime(.4f);
            Assert.That(sent, Is.EqualTo(new[] { ("e:🔥", 0, 3) }));

            // Settings: "Sohbet balonları" off keeps the log but drops the bubbles.
            hud.Settings.Set("bubbles", false);
            hud.Say(2, "Bakalım bakalım"); yield return null;
            Assert.That(root.Query<Label>("balloon-2").ToList().Any(l => l.text == "Bakalım bakalım"), Is.False, "no chat bubble");
            Assert.That(hud.Chat.Last().Text, Is.EqualTo("Bakalım bakalım"));
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator GiftsFlyLandIdleAndLeaveWithTheHand()
        {
            var game = Build(TableGame.Me, 9090, true);
            game.Configure(TableGame.Me, 9090, 90, false);
            string dir = Environment.GetEnvironmentVariable("GALAXY_TABLE_GIFT_CAPTURE_DIR");
            var hud = hudOwner.GetComponent<TableHud>(); var document = hudOwner.GetComponent<UIDocument>(); var root = hud.Canvas;
            Time.timeScale = 3;
            yield return Until(() => game.CanDiscard && game.SyncedVersion == game.Session.GetView(TableGame.Me).Version, 40, "my first turn");
            Time.timeScale = 1;

            // T06: Ayşe treats everyone to tea 1.8 s after the deal; mine lands and the notice card shows.
            yield return Until(() => game.Gifts.Count(0) == 1 && game.Gifts.Count(1) == 1 && game.Gifts.Count(3) == 1, 6, "Ayşe's tea for everyone");
            Assert.That(game.Gifts.Count(2), Is.EqualTo(0), "Ayşe does not treat herself");
            Assert.That(hud.Chat.Any(l => l.Seat == 2 && l.Text == TableStrings.Get("quick.4", hud.L("tr", "en"))), Is.True);
            Assert.That(hud.Chat.Any(l => l.Seat == -1 && l.Text == hud.L("Ayşe herkese çay ısmarladı", "Ayşe sent everyone tea")), Is.True);
            Assert.That(game.Gifts.Flying, Is.GreaterThan(0), "the cups are still in the air");
            yield return Until(() => hud.Notice != null, 3, "the received card");
            Assert.That(root.Q<Label>("gift-notice-text").text, Is.EqualTo(hud.L("Ayşe sana çay ısmarladı", "Ayşe sent you tea")));
            Assert.That(hud.Received, Is.EqualTo(new[] { "tea" }));
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(game.Fx.LiveHaze, Is.GreaterThan(0), "tea steams while it stands");
            if (!string.IsNullOrEmpty(dir)) yield return Capture(document, Path.Combine(dir, "gift_notice.png"));
            yield return null; AssertNoOverflow(root, "gift notice");
            Click(root, "gift-notice-thanks"); yield return null;
            Assert.That(hud.Notice, Is.Null);
            Assert.That(hud.Chat.Last().Seat, Is.EqualTo(0)); Assert.That(hud.Chat.Last().Text, Is.EqualTo(TableStrings.Get("thanksMsg", hud.L("tr", "en"))));
            yield return new WaitForSecondsRealtime(.45f);
            Assert.That(hud.EmojiFlights, Is.EqualTo(1), "a 🙏 flies to Ayşe");
            yield return new WaitForSecondsRealtime(1.8f);
            Assert.That(hud.EmojiFlights, Is.EqualTo(0));

            // T11–T12: my rocket for Zeynep tours the screen for 5.6 s with smoke, then lands with a shake.
            hud.SendGift("rocket", "1"); yield return null;
            Assert.That(hud.Chat.Last().Text, Is.EqualTo(hud.L("Sen, Zeynep için roket gönderdin", "You sent Zeynep a rocket")));
            yield return new WaitForSecondsRealtime(2.4f);
            Assert.That(game.Gifts.Flying, Is.EqualTo(1)); Assert.That(game.Fx.LiveHaze, Is.GreaterThan(10), "rocket smoke");
            if (!string.IsNullOrEmpty(dir)) yield return Capture(document, Path.Combine(dir, "gift_rocket.png"));
            yield return Until(() => game.Gifts.Flying == 0, 6, "the rocket landing");
            Assert.That(game.Gifts.Count(1), Is.EqualTo(2), "tea and rocket at Zeynep's corner");
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(hud.Chat.Last().Seat, Is.EqualTo(1), "Zeynep thanks me 6.3 s after the rocket");
            if (!string.IsNullOrEmpty(dir)) yield return Capture(document, Path.Combine(dir, "gift_rocket_landed.png"));

            // Three slots per seat: a fourth gift pushes the oldest out.
            hud.SendGift("cake", "1"); hud.SendGift("flower", "1"); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(game.Gifts.Count(1), Is.EqualTo(3));

            // Emoji fly in the HUD layer; "Emoji efektleri" off hides them.
            hud.SendEmoji("😂", "2"); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(hud.EmojiFlights, Is.EqualTo(1));
            hud.Settings.Set("emojiFx", false);
            yield return new WaitForSecondsRealtime(1.8f);
            hud.SendEmoji("😂", "2"); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(hud.EmojiFlights, Is.EqualTo(0));

            // "İkram bildirimleri" off: no card, the gift is still listed for the hand result.
            hud.Settings.Set("giftNotif", false);
            hud.BotGift(3, "simit", 0);
            yield return new WaitForSecondsRealtime(1.6f);
            Assert.That(hud.Notice, Is.Null); Assert.That(hud.Received, Is.EqualTo(new[] { "tea", "simit" }));

            // A new hand clears the table (0.35 s shrink) and the received list.
            game.Gifts.Clear(); hud.NewHand();
            yield return new WaitForSecondsRealtime(.5f);
            for (int k = 0; k < 4; k++) Assert.That(game.Gifts.Count(k), Is.EqualTo(0));
            Assert.That(sceneOwner.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("hediye_")), Is.EqualTo(0));
            Assert.That(hud.Received, Is.Empty);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator MenuOpensTheGalaxyTableAndExitReturnsToTheMenu()
        {
            savedTimeScale = Time.timeScale;
            var load = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            MainMenuController menu = null;
            yield return Until(() => (menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>()) != null && menu.View != null, 30, "the main menu");
            var open = menu.StartLocalAsync();
            yield return Until(() => open.IsCompleted, 60, "the table scene to load");
            Assert.That(open.IsFaulted, Is.False, open.Exception?.ToString());
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(MainMenuController.GalaxyTableScene));
            TableGame game = null;
            yield return Until(() => (game = UnityEngine.Object.FindAnyObjectByType<TableGame>()) != null && game.Current != TableGame.Phase.Waiting, 30, "the deal");

            game.RequestExit();
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(MainMenuController.GalaxyTableScene), "the exit button opens the confirmation card");
            Assert.That(game.ExitOpen, Is.True);
            game.CancelExit(); Assert.That(game.ExitOpen, Is.False, "Masada kal closes the card");
            game.RequestExit(); game.ConfirmExit();
            yield return Until(() => SceneManager.GetActiveScene().name == "Bootstrap" && UnityEngine.Object.FindAnyObjectByType<MainMenuController>() != null, 60, "the menu after leaving");
            Assert.That(UnityEngine.Object.FindAnyObjectByType<TableGame>(), Is.Null);
        }

        private static IEnumerator Capture(UIDocument document, string path)
        {
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var panel = UnityEngine.Object.Instantiate(document.panelSettings); var original = document.panelSettings;
            panel.targetTexture = target; document.panelSettings = panel;
            for (int i = 0; i < 20; i++) yield return null;
            yield return new WaitForSecondsRealtime(1.2f); // entrance animations settle
            var previous = RenderTexture.active; RenderTexture.active = target;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false); pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            RenderTexture.active = previous; File.WriteAllBytes(path, pixels.EncodeToPNG());
            document.panelSettings = original; UnityEngine.Object.Destroy(panel); target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels);
            Debug.Log("GALAXY101_TABLE_GAME_CAPTURE " + path);
        }
    }
}

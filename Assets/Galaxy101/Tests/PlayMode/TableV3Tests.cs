using System;
using System.Collections;
using System.IO;
using System.Linq;
using Galaxy101.Table;
using NUnit.Framework;
using Okey101.Core.Model;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>
    /// Table v3 (spec/22, spec/23, spec/25 scenarios) on the live table. GALAXY_V3_CAPTURE_DIR saves frames for the
    /// G13–G34 comparisons.
    /// </summary>
    public sealed class TableV3Tests
    {
        private GameObject gameOwner, sceneOwner, hudOwner;
        private string statePath;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            TableGame.PendingScenario = null;
            foreach (var go in new[] { hudOwner, gameOwner, sceneOwner }) if (go != null) UnityEngine.Object.Destroy(go);
            yield return null;
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        private static IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition()) { if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what); yield return null; }
        }

        private static string Dir => Environment.GetEnvironmentVariable("GALAXY_V3_CAPTURE_DIR");
        private IEnumerator Shot(string file) { if (!string.IsNullOrEmpty(Dir)) yield return TableGameRuntimeTests.Capture(hudOwner.GetComponent<UIDocument>(), Path.Combine(Dir, file)); }
        private TableHud Hud => hudOwner.GetComponent<TableHud>();

        private TableGame Table(string scenario, uint seed = 11)
        {
            gameOwner = new GameObject("TableGame"); var game = gameOwner.AddComponent<TableGame>();
            game.Configure(TableGame.Me, seed, 60, false, TableScenarios.Create(scenario));
            sceneOwner = new GameObject("GalaxyTable"); sceneOwner.AddComponent<TableScene>();
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-v3-" + Guid.NewGuid().ToString("N") + ".json");
            hudOwner = new GameObject("TableHud"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); Hud.Initialize(statePath);
            gameOwner.AddComponent<RackInput>();
            return game;
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ProcessingScenarioLaysTheCentreBoardOutWithTheDeckOnTheLeft()
        {
            var game = Table("processing");
            yield return Until(() => game.CanDraw, 30, "my draw turn");
            yield return Until(() => Mathf.Approximately(sceneOwner.GetComponent<TableScene>().DeckShift, TableScene.DeckLeft), 10, "the deck sliding left");
            var layout = game.Board.Layout;
            Assert.That(layout.Count, Is.EqualTo(15));
            Assert.That(layout.OpeningOrder.Count, Is.EqualTo(4));
            var cells = new System.Collections.Generic.HashSet<(int, int)>();
            foreach (var meld in game.Session.GetView(TableGame.Me).Table)
            {
                Assert.That(layout.TryGet(meld.Id, out var at), Is.True);
                for (int c = 0; c < at.Length; c++) Assert.That(cells.Add((at.Row, at.Column + c)), Is.True, "melds never overlap");
                if (meld.Group.Kind == MeldKind.Run) Assert.That(at.Column, Is.EqualTo(meld.Group.Tiles[0].RepresentedFace.Number - 1), "runs sit on their numbers");
                else Assert.That(at.Column, Is.GreaterThanOrEqualTo(14), "set " + meld.Id + " of seat " + meld.Owner + " kind " + meld.Group.Kind + " at row " + at.Row + " len " + at.Length);
            }
            yield return new WaitForSeconds(1f);
            yield return Shot("G14_processing.png");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator SelectingATileShowsEveryLayOffEndAndOkeySwap()
        {
            var game = Table("processing");
            yield return Until(() => game.CanDraw, 30, "my draw turn");
            var draw = game.Draw(DrawSource.Stock, null); yield return Until(() => draw.IsCompleted && game.CanDiscard, 10, "the draw");
            int y8 = TableScenarios.Id(TileColor.Yellow, 8), b12 = TableScenarios.Id(TileColor.Blue, 12), k13 = TableScenarios.Id(TileColor.Black, 13);
            game.SetDragTile(y8); yield return null;
            var ends = game.Targets.Where(t => t.Okey < 0).ToList();
            Assert.That(ends.Count, Is.GreaterThanOrEqualTo(2), "Y8 fits Y5-7 (right) and the 8s");
            Assert.That(game.Markers.Visible, Is.EqualTo(game.Targets.Count));
            yield return new WaitForSeconds(.4f);
            yield return Shot("G15_isleme_targets.png");
            game.SetDragTile(b12); yield return null;
            Assert.That(game.Targets.Any(t => t.Okey >= 0), Is.True, "B12 can take the okey standing for it");
            yield return new WaitForSeconds(.4f);
            yield return Shot("G16_okey_target.png");
            game.SetDragTile(k13); yield return null;
            var blackRun = game.Session.GetView(TableGame.Me).Table.First(m => m.Group.Kind == MeldKind.Run && m.Group.Tiles[0].RepresentedFace.Color == TileColor.Black && m.Group.Tiles[0].RepresentedFace.Number == 10);
            Assert.That(game.Targets.Select(t => (t.Meld, t.Prepend)), Is.EqualTo(new[] { (blackRun.Id, false) }), "K13 only ends K10-12");

            // Lay Y8 on the yellow run's right end through its marker.
            game.SetDragTile(y8); yield return null;
            var yellowRun = game.Session.GetView(TableGame.Me).Table.First(m => m.Owner == 0 && m.Group.Kind == MeldKind.Run && m.Group.Tiles[0].RepresentedFace.Color == TileColor.Yellow);
            var end = game.Targets.First(t => t.Meld == yellowRun.Id && !t.Prepend);
            game.SetDragTile(-1); game.UseTarget(y8, end); yield return null;
            Assert.That(game.Rack.Tiles.Contains(y8), Is.False);
            Assert.That(game.Board.Layout.TryGet(yellowRun.Id, out var at) && at.Length == 4, Is.True, "the run grew on the board");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator UndoTakesBackALayOff()
        {
            var game = Table("processing");
            yield return Until(() => game.CanDraw, 30, "my draw turn");
            var draw = game.Draw(DrawSource.Stock, null); yield return Until(() => draw.IsCompleted && game.CanDiscard, 10, "the draw");
            int y8 = TableScenarios.Id(TileColor.Yellow, 8), count = game.Rack.Count;
            game.SetDragTile(y8); yield return null; var target = game.Targets.First(t => t.Okey < 0); game.SetDragTile(-1);
            game.UseTarget(y8, target); yield return null;
            Assert.That(game.CanUndo, Is.True); Assert.That(game.HudState().CanUndo, Is.True);
            yield return null;
            Assert.That(Hud.Canvas.Q<VisualElement>("table-undo"), Is.Not.Null);
            yield return Shot("G18_geri_topla.png");
            Hud.Press("undo"); yield return null;
            Assert.That(game.Rack.Count, Is.EqualTo(count)); Assert.That(game.Rack.Tiles.Contains(y8), Is.True); Assert.That(game.CanUndo, Is.False);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator AShortOpeningShowsTheSetsThenCostsAHundredAndOne()
        {
            var game = Table("shortOpening");
            yield return Until(() => game.CanDraw, 30, "my draw turn");
            var draw = game.Draw(DrawSource.Stock, null); yield return Until(() => draw.IsCompleted && game.CanDiscard, 10, "the draw");
            game.SortRack(true); yield return null;
            var state = game.HudState();
            Assert.That(state.ShortOpen, Is.True); Assert.That(state.CanOpen, Is.True);
            yield return null;
            Assert.That(Hud.Canvas.Q<VisualElement>("table-open").ClassListContains("short"), Is.True, "Aç is orange below 101");
            yield return Shot("G20_short_open_button.png");
            int count = game.Rack.Count;
            game.OpenRuns(); yield return new WaitForSeconds(.5f);
            Assert.That(game.Rack.Count, Is.LessThan(count), "the sets are on the board for a moment");
            yield return Shot("G21_short_open_table.png");
            yield return Until(() => game.Rack.Count == count, 5, "the tiles coming back");
            Assert.That(game.Session.GetView(TableGame.Me).OwnPenalties, Is.EqualTo(101));
            yield return new WaitForSeconds(.4f);
            yield return Shot("G22_short_open_penalty.png");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator FinishingFromTheHandWinsTheHandAtDoubleScore()
        {
            var game = Table("fromHand");
            yield return Until(() => game.CanDiscard, 30, "my first turn (22 tiles)");
            int I(TileColor c, int n) => TableScenarios.Id(c, n);
            const TileColor R = TileColor.Red, Y = TileColor.Yellow, B = TileColor.Blue, K = TileColor.Black;
            int[][] rows =
            {
                new[] { I(R, 1), I(R, 2), I(R, 3), -1, I(Y, 6), I(Y, 7), I(Y, 8), I(Y, 9), -1, I(B, 9), I(B, 10), I(B, 11), I(B, 12) },
                new[] { I(K, 5), I(R, 5), I(Y, 5), I(B, 5), -1, I(B, 13), I(K, 13), I(R, 13), -1, I(Y, 1), I(Y, 2), I(Y, 3), -1, I(K, 8) },
            };
            var plan = new System.Collections.Generic.Dictionary<int, RackSlot>();
            for (int r = 0; r < 2; r++) for (int i = 0; i < rows[r].Length; i++) if (rows[r][i] >= 0) plan[rows[r][i]] = new RackSlot(r, i);
            game.ApplyRackPlan(plan); yield return null;
            Assert.That(game.CanFinishFromHand, Is.True, "21 tiles in sets, one spare");
            yield return null;
            Assert.That(Hud.Canvas.Q<VisualElement>("table-fromhand"), Is.Not.Null);
            yield return Shot("G25_elden_bit.png");
            Hud.Press("fromhand");
            yield return new WaitForSeconds(.6f); yield return Shot("G26_launch.png");
            yield return new WaitForSeconds(.75f); yield return Shot("G27_nuclear.png");
            yield return Until(() => game.Session.GetView(TableGame.Me).Result != null, 20, "the finished hand");
            var result = game.Session.GetView(TableGame.Me).Result;
            Assert.That(result.FinishInfo.Winner, Is.EqualTo(TableGame.Me)); Assert.That(result.FinishInfo.WinnerOpenedThisTurn, Is.True);
            Assert.That(result.Scores[1].M, Is.GreaterThanOrEqualTo(2), "opponents pay double");
            yield return Until(() => game.HudState().Moment != null, 10, "the finish banner");
            Assert.That(game.HudState().Moment.Title, Does.Contain("ELDEN").Or.Contain("FROM THE HAND"));
            yield return new WaitForSeconds(.8f); yield return Shot("G28_elden_bittin.png");
        }

        private TableGame ScenarioTable(string key)
        {
            TableGame.PendingScenario = key;
            gameOwner = new GameObject("TableGame"); var game = gameOwner.AddComponent<TableGame>();
            sceneOwner = new GameObject("GalaxyTable"); sceneOwner.AddComponent<TableScene>();
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-v3-" + Guid.NewGuid().ToString("N") + ".json");
            hudOwner = new GameObject("TableHud"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); Hud.Initialize(statePath);
            return game;
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheOpeningScenarioOpensOnTheCentreBoard()
        {
            var game = ScenarioTable("opening");
            yield return Until(() => game.CanDiscard, 30, "my first turn");
            Assert.That(game.Scenario, Is.EqualTo("opening"));
            int I(TileColor c, int n) => TableScenarios.Id(c, n);
            const TileColor R = TileColor.Red, Y = TileColor.Yellow, B = TileColor.Blue, K = TileColor.Black;
            int[] top = { I(R, 10), I(R, 11), I(R, 12), I(R, 13), -1, I(K, 11), I(B, 11), I(Y, 11), -1, I(Y, 7), I(Y, 8), I(Y, 9) };
            var plan = new System.Collections.Generic.Dictionary<int, RackSlot>();
            for (int i = 0; i < top.Length; i++) if (top[i] >= 0) plan[top[i]] = new RackSlot(0, i);
            int col = 0; foreach (var id in game.Rack.Tiles.Where(id => !top.Contains(id)).ToList()) plan[id] = new RackSlot(1, col++);
            game.ApplyRackPlan(plan); yield return null;
            Assert.That(game.HudState().ShortOpen, Is.False, "103 is enough");
            game.OpenRuns(); yield return null;
            Assert.That(game.Board.Layout.Count, Is.GreaterThanOrEqualTo(3));
            yield return new WaitForSeconds(1.2f);
            Assert.That(sceneOwner.GetComponent<TableScene>().DeckShift, Is.LessThan(-0.3f), "the deck is sliding left");
            yield return Shot("G13_opening.png");
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator ThePlayableTileScenarioPenalisesBotsThatThrowAPlayableTile()
        {
            var game = ScenarioTable("playable");
            Time.timeScale = 4;
            yield return Until(() => game.CanDraw, 30, "my draw turn");
            Assert.That(game.PlayableBots, Is.True);
            float until = Time.realtimeSinceStartup + 120; bool penalised = false;
            while (!penalised && Time.realtimeSinceStartup < until)
            {
                var view = game.Session.GetView(TableGame.Me);
                if (view.Result != null) break;
                penalised = view.Players.Skip(1).Any(p => p.Penalties >= 101);
                if (game.CanDraw) { var d = game.Draw(DrawSource.Stock, null); yield return Until(() => d.IsCompleted, 10, "draw"); continue; }
                if (game.CanDiscard) { int tile = game.Rack.Tiles.First(id => game.Session.GetView(TableGame.Me).Table.All(m => true)); game.Discard(game.LastDrawn >= 0 && game.Rack.Tiles.Contains(game.LastDrawn) ? game.LastDrawn : tile); yield return Until(() => !game.CanDiscard, 10, "discard"); continue; }
                yield return null;
            }
            Assert.That(penalised, Is.True, "a bot threw a playable tile and took +101");
            Time.timeScale = 1; yield return new WaitForSeconds(.5f);
            yield return Shot("G24_islek_tas_ceza.png");
        }
    }
}

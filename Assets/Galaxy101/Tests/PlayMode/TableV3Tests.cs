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
    }
}

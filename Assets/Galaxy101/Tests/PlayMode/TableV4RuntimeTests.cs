using System;
using System.Collections;
using System.IO;
using System.Linq;
using Galaxy101.Table;
using NUnit.Framework;
using Okey101.Core.V4;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>
    /// The 3D table on the v4 match flow (replaces the earlier engine's TableGameRuntimeTests, TableV3Tests,
    /// DealCeremonyTests and OnlineTableTests): the offline session runs the server's TableRuntime, the table shows the
    /// ceremony from its steps, and every action is one engine step. The rule and protocol cases themselves are the
    /// NUnit suite (tools/v4/nunit-table); these check the scene wiring.
    /// </summary>
    public sealed class TableV4RuntimeTests
    {
        private GameObject gameOwner, sceneOwner, hudOwner;
        private string statePath;

        private TableGame Build(TableLaunch launch)
        {
            // The offline match runs on scaled time so Time.timeScale speeds the server and the animations alike.
            launch.Clock = launch.Clock ?? (() => Time.timeAsDouble * 1000);
            TableGame.Pending = launch;
            gameOwner = new GameObject("TableGame"); var game = gameOwner.AddComponent<TableGame>(); gameOwner.AddComponent<RackInput>();
            sceneOwner = new GameObject("GalaxyTable"); sceneOwner.AddComponent<TableScene>();
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-table-v4-" + Guid.NewGuid().ToString("N") + ".json");
            hudOwner = new GameObject("TableHud"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
            return game;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1; TableGame.Pending = null;
            foreach (var go in new[] { hudOwner, gameOwner, sceneOwner }) if (go != null) UnityEngine.Object.Destroy(go);
            yield return null;
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        private static IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition()) { if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what); yield return null; }
        }

        private static TableLaunch Offline(string flow = null, uint seed = 42) => new TableLaunch
        {
            Setup = new TableSetup { Options = new MatchOptions { Mode = "solo", Hands = 5 }, Seed = seed, TurnMs = 60000 }, Table = TableGame.DefaultTable(), Flow = flow, SkipCountdown = flow == null,
        };

        [UnityTest]
        public IEnumerator ALocalReferenceTableKeepsGiftPricesWhenItIsNotAVsBotsTable()
        {
            var launch = Offline("play"); launch.Table.VsBots = false;
            var game = Build(launch);
            yield return Until(() => game.Board != null && game.Model.HasHand, 10, "the reference table");
            Assert.That(game.Session.Online, Is.False, "the diagnostic table uses the local host");
            Assert.That(hudOwner.GetComponent<TableHud>().FreeGifts, Is.False, "R-94 exempts bot mode, not every offline transport");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheCeremonyFollowsTheServerAndHandsOverToTheBoard()
        {
            Time.timeScale = 4;
            var game = Build(Offline());
            yield return Until(() => game.Ceremony != null && game.Ceremony.Running, 10, "the ceremony");
            Assert.That(game.HudState().Phase, Is.EqualTo("deal"));
            yield return Until(() => game.Model.Deal == null && !game.Ceremony.Running && game.Current != TableGame.Phase.Deal, 60, "the first turn");
            var m = game.Model;
            Assert.That(m.Deck, Is.EqualTo(20));
            Assert.That(game.Rack.Count, Is.EqualTo(m.Rack.Count));
            CollectionAssert.AreEquivalent(TileIds.ToInts(m.Rack), game.Rack.Tiles);
            Assert.That(game.Board.HiddenCount(1) + game.Board.HiddenCount(2) + game.Board.HiddenCount(3), Is.EqualTo(Enumerable.Range(1, 3).Sum(k => m.Counts[game.Abs(k)])));
            Assert.That(game.Board.ViewOf(TileIds.ToInt(m.Indicator)), Is.Not.Null, "the indicator lies on the deck");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator MyDrawAndDiscardAreEngineSteps()
        {
            var game = Build(Offline("play"));
            yield return Until(() => game.HumanTurn, 10, "my turn");
            Assert.That(game.Rack.Count, Is.EqualTo(21));
            Assert.That(game.CanDrawSide, Is.True);
            var draw = game.Draw(false, null);
            yield return Until(() => draw.IsCompleted, 5, "the draw");
            Assert.That(draw.Result, Is.True);
            Assert.That(game.Rack.Count, Is.EqualTo(22));
            Assert.That(game.CanDiscard, Is.True);
            int tile = game.LastDrawn;
            game.Discard(tile);
            yield return null;
            Assert.That(game.Model.Discards[0].Last(), Is.EqualTo(TileIds.ToV4(tile)));
            Assert.That(game.HumanTurn, Is.False);
            Assert.That(game.Board.ViewOf(tile), Is.Not.Null, "the tile lies on my pile");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator OpenLaysTheRackSegmentsAndGeriToplaTakesThemBack()
        {
            var game = Build(Offline("open"));
            yield return Until(() => game.HumanTurn && game.CanDiscard, 10, "my turn");
            Assert.That(game.HudState().OpenSum, Is.EqualTo(131));
            game.OpenRuns(); yield return null;
            Assert.That(game.Model.Opened[0], Is.EqualTo(131), "G13: Açtı · 131");
            Assert.That(game.Board.Layout.Count, Is.EqualTo(3 + 4), "Ayşe's three melds and my four");
            Assert.That(game.CanUndo, Is.True);
            game.Undo(); yield return null;
            Assert.That(game.Model.Opened[0], Is.EqualTo(0));
            Assert.That(game.Rack.Count, Is.EqualTo(22));
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator AShortOpeningLiesOnTheBoardThenReturnsWithItsPenalty()
        {
            var game = Build(Offline("penOpen"));
            yield return Until(() => game.HumanTurn && game.CanDiscard, 10, "my turn");
            Assert.That(game.HudState().ShortOpen, Is.True, "G20: Aç in the warning colour");
            game.OpenRuns(); yield return null;
            Assert.That(game.HudState().Opened[TableGame.Me], Is.EqualTo(66), "G21: Açtı · 66 for 1.5 s");
            Assert.That(game.Model.Pen[0], Is.EqualTo(101));
            yield return new WaitForSeconds(1.7f);
            Assert.That(game.HudState().Opened[TableGame.Me], Is.EqualTo(0), "G22: back on the rack");
            Assert.That(game.Rack.Count, Is.EqualTo(17));
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator TheOkeyIsTakenOnlyWithTheTileItStandsFor()
        {
            var game = Build(Offline("isle"));
            yield return Until(() => game.HumanTurn && game.CanDiscard, 10, "my turn");
            var okey = game.TableOkeys().First(o => o.Owner == 2);
            int k5 = game.Rack.Tiles.First(id => TileIds.Code(id) == "K5"), y4 = game.Rack.Tiles.First(id => TileIds.Code(id) == "Y4");
            game.TakeOkey(k5, okey.Id); yield return null;
            Assert.That(game.Rack.TryFind(k5, out _), Is.True, "T49: refused");
            game.SetSelected(y4);
            Assert.That(game.Targets.Any(t => t.Okey == okey.Id), Is.True, "G16: the green okey target");
            game.TakeOkey(y4, okey.Id); yield return null;
            Assert.That(game.Rack.TryFind(okey.Id, out _), Is.True, "G17: the okey is on my rack");
        }
    }
}

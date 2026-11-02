using System;
using System.Collections;
using System.Collections.Generic;
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
        public IEnumerator PreparedRackRefreshesTheHudAfterItsAuthoredGapsAreApplied()
        {
            var launch = Offline(); launch.Manual = true;
            var game = Build(launch);
            yield return Until(() => game.Session != null, 10, "the router's manually started table");
            game.RunFlow("play");
            yield return Until(() => game.HumanTurn && game.CanDraw, 10, "the prepared play turn");
            Assert.That(RackEval.Of(game.Rack, game.Model).Sum, Is.EqualTo(88), "HAND_DEAL's real gap-free melds");
            Assert.That(game.HudState().OpenSum, Is.EqualTo(88), "The game evaluates the arranged rack");
            Assert.That(hudOwner.GetComponent<TableHud>().Current.OpenSum, Is.EqualTo(88), "The rendered HUD must follow the final arrangement, not the initial dealt order");
        }

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
        public IEnumerator OpenFlowIsTheReferencesShortOpening()
        {
            // G13: the reference's B3 for the third B12 leaves 95 — Aç is the orange-red warning and opening is short (R-23).
            var game = Build(Offline("open"));
            yield return Until(() => game.HumanTurn && game.CanDiscard, 10, "my turn");
            var hud = game.HudState();
            Assert.That(hud.OpenSum, Is.EqualTo(95));
            Assert.That(hud.CanOpen && hud.ShortOpen, Is.True, "Aç · 95/101 warns, it is not disabled");
            game.OpenRuns();
            yield return Until(() => game.Model.Pen[game.Model.Seat] > 0, 10, "short-opening penalty");
            Assert.That(game.Model.Opened[0], Is.EqualTo(0));
            Assert.That(game.CanUndo, Is.False);
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
            Assert.That(game.Rack.TryFind(y4, out var replacedSlot), Is.True);
            game.TakeOkey(y4, okey.Id); yield return null;
            Assert.That(game.Rack.TryFind(okey.Id, out var acquiredSlot), Is.True, "G17: the okey is on my rack");
            Assert.That(acquiredSlot, Is.EqualTo(replacedSlot), "Swapping preserves the player's rack arrangement");
            Assert.That(hudOwner.GetComponent<TableHud>().ActiveToast, Is.EqualTo(TableStrings.Get("okeyTaken", "tr")));
        }

        private static void PrepareIsleHand(TableGame game, bool pairs, params string[][] segments)
        {
            var melds = new List<SpecMeld>();
            if (pairs)
                foreach (string face in new[] { "R1", "R3", "B1", "B3", "K1" })
                    melds.Add(new SpecMeld { Owner = 0, Kind = G101Rules.KindPair, Tiles = new List<string> { face + "a", face + "b" } });
            else
            {
                melds.Add(new SpecMeld { Owner = 0, Kind = G101Rules.KindRun, Tiles = Enumerable.Range(1, 13).Select(n => "R" + n + "a").ToList() });
                melds.Add(new SpecMeld { Owner = 0, Kind = G101Rules.KindGroup, Tiles = new List<string> { "R12b", "B12a", "Y12a" } });
            }
            // Seat 1 opened 101; K6 can extend its first run after the new rack segments are laid.
            melds.Add(new SpecMeld { Owner = 1, Kind = G101Rules.KindRun, Tiles = new List<string> { "K7a", "K8a", "K9a" } });
            melds.Add(new SpecMeld { Owner = 1, Kind = G101Rules.KindRun, Tiles = new List<string> { "B8a", "B9a", "B10a", "B11a" } });
            melds.Add(new SpecMeld { Owner = 1, Kind = G101Rules.KindGroup, Tiles = new List<string> { "R13b", "B13a", "Y13a" } });
            game.Local.Resume(G101Rules.FromSpec(new HandSpec {
                Opts = new MatchOptions { Mode = "solo", Hands = 5 }, Seed = 42, HandNo = 1, Dealer = 3,
                Indicator = "B6a", Racks = new List<List<string>> { segments.SelectMany(s => s).ToList(), new List<string> { "R8b" }, new List<string> { "R9b" }, new List<string> { "R10b" } },
                Melds = melds, Opened = new[] { pairs ? 5 : 127, 101, 0, 0 },
                OpenMode = new[] { pairs ? G101Rules.KindPair : G101Rules.KindRun, G101Rules.KindRun, null, null },
                TurnSeat = 0, TurnPhase = G101Rules.PhasePlay, TurnN = 3,
            }));
            game.Rack.Clear();
            int row = 0, column = 0;
            foreach (var segment in segments)
            {
                if (column + segment.Length > RackGrid.Columns) { row++; column = 0; }
                foreach (string tile in segment) game.Rack.Set(new RackSlot(row, column++), TileIds.ToInt(tile));
                column++;
            }
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator IsleAllLaysNewSegmentsBeforeAddingAndKeepsTheRealOkeyOnTheRack()
        {
            var game = Build(Offline("isle"));
            yield return Until(() => game.HumanTurn && game.CanDiscard, 10, "my opened turn");
            PrepareIsleHand(game, false, new[] { "Y3a", "Y4a", "Y5a" }, new[] { "K6a" }, new[] { "B7a" }, new[] { "K4a" });
            var events = new List<string>();
            game.Model.Applied += message => { if (message.E == "laid" || message.E == "added") events.Add(message.E); };
            game.IsleAll(); yield return null;
            Assert.That(events, Is.EqualTo(new[] { "laid", "added" }), "one gLay ACK precedes each gAdd");
            Assert.That(game.Model.Melds.Any(m => m.Owner == 0 && m.Tiles.SequenceEqual(new[] { "Y3a", "Y4a", "Y5a" })), Is.True);
            Assert.That(game.Model.Melds.Any(m => m.Owner == 1 && m.Tiles.SequenceEqual(new[] { "K6a", "K7a", "K8a", "K9a" })), Is.True);
            CollectionAssert.AreEquivalent(new[] { "B7a", "K4a" }, game.Model.Rack, "automatic processing never spends the real okey");
            Assert.That(game.Model.Pen[0], Is.Zero);
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator IsleAllDropsWholeTrailingSegmentsToPreserveTheDiscardTile()
        {
            var game = Build(Offline("isle"));
            yield return Until(() => game.HumanTurn && game.CanDiscard, 10, "my opened turn");
            PrepareIsleHand(game, false, new[] { "Y3a", "Y4a", "Y5a" }, new[] { "K1a", "K2a", "K3a" });
            Assert.That(RackEval.Of(game.Rack, game.Model).Groups.Count, Is.EqualTo(2));
            game.IsleAll(); yield return null;
            CollectionAssert.AreEquivalent(new[] { "K1a", "K2a", "K3a" }, game.Model.Rack, "trim from the last segment, rather than emptying the rack or splitting a valid run");
            Assert.That(game.Model.Melds.Any(m => m.Owner == 0 && m.Tiles.SequenceEqual(new[] { "Y3a", "Y4a", "Y5a" })), Is.True);
            Assert.That(game.CanDiscard, Is.True); Assert.That(game.Model.Pen[0], Is.Zero);
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator IsleAllOnAPairOpeningLaysOnlyPairsAndLeavesRunSegments()
        {
            var game = Build(Offline("isle"));
            yield return Until(() => game.HumanTurn && game.CanDiscard, 10, "my opened turn");
            PrepareIsleHand(game, true, new[] { "Y3a", "Y3b" }, new[] { "Y4a", "Y5a", "Y6a" }, new[] { "K4a" });
            game.IsleAll(); yield return null;
            Assert.That(game.Model.Melds.Any(m => m.Owner == 0 && m.Kind == G101Rules.KindPair && m.Tiles.SequenceEqual(new[] { "Y3a", "Y3b" })), Is.True);
            CollectionAssert.AreEquivalent(new[] { "Y4a", "Y5a", "Y6a", "K4a" }, game.Model.Rack);
            Assert.That(game.Model.Pen[0], Is.Zero, "R-24 is enforced by the same local runtime as the server");
        }

        private static void PrepareAnchorUndoHand(TableGame game, bool drawSide = false)
        {
            var state = G101Rules.FromSpec(new HandSpec {
                Opts = new MatchOptions { Mode = "solo", Hands = 5 }, Seed = 42, HandNo = 1, Dealer = 3,
                Indicator = "B6a", Racks = new List<List<string>> {
                    new List<string> { "Y6a", "Y5a", "Y4a", "B2a" }, new List<string> { "R8b" }, new List<string> { "R9b" }, new List<string> { "R10b" } },
                Discards = new List<List<string>> { new List<string>(), new List<string>(), new List<string>(), drawSide ? new List<string> { "B2b" } : new List<string>() },
                Melds = new List<SpecMeld> {
                    new SpecMeld { Owner = 0, Kind = G101Rules.KindRun, Tiles = new List<string> { "R3a", "R4a", "R5a" } },
                    new SpecMeld { Owner = 0, Kind = G101Rules.KindRun, Tiles = new List<string> { "Y7a", "Y8a", "Y9a" } },
                    new SpecMeld { Owner = 0, Kind = G101Rules.KindRun, Tiles = Enumerable.Range(1, 13).Select(n => "K" + n + "a").ToList() },
                },
                Opened = new[] { 127, 0, 0, 0 }, OpenMode = new[] { G101Rules.KindRun, null, null, null },
                TurnSeat = 0, TurnPhase = drawSide ? G101Rules.PhaseDraw : G101Rules.PhasePlay, TurnN = 7,
            });
            game.Local.Resume(state);
            game.Board.Clear(); game.SyncBoard(true);
            Assert.That(game.Board.Layout.TryGet(2, out var at) && at.Row == 0 && at.Column == 8, Is.True, "source run anchor before the turn's additions");
        }

        private static void GrowTheAnchoredRun(TableGame game)
        {
            foreach (string tile in new[] { "Y6a", "Y5a", "Y4a" }) game.LayOff(TileIds.ToInt(tile), 2, true);
            Assert.That(game.Board.Layout.TryGet(2, out var at) && at.Row == 2 && at.Column == 3, Is.True, "the third prepend collides with the first run; the full second row forces relocation");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator UndoRestoresTheFirstSuccessfulMovesOriginalBoardAnchors()
        {
            var game = Build(Offline("isle"));
            yield return Until(() => game.HumanTurn && game.CanDiscard, 10, "my opened turn");
            PrepareAnchorUndoHand(game);
            game.LayOff(TileIds.ToInt("Y4a"), 2, true);
            Assert.That(game.CanUndo, Is.False, "a refused add must not begin a presentation snapshot");
            GrowTheAnchoredRun(game);
            game.Undo(); yield return null;
            Assert.That(game.Board.Layout.TryGet(2, out var at) && at.Row == 0 && at.Column == 8, Is.True, "source undo restores row/x0/base/sequence, not only the old tile list");
            Assert.That(game.Model.MeldById("m2").Tiles, Is.EqualTo(new[] { "Y7a", "Y8a", "Y9a" }));
            CollectionAssert.AreEquivalent(new[] { "Y6a", "Y5a", "Y4a", "B2a" }, game.Model.Rack);
            Assert.That(game.CanUndo, Is.False);
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator ReturningTheSideTileRestoresAllTurnMoveBoardAnchors()
        {
            var game = Build(Offline("isle"));
            yield return Until(() => game.HumanTurn && game.CanDiscard, 10, "my opened turn");
            PrepareAnchorUndoHand(game, true);
            var draw = game.Draw(true, null);
            yield return Until(() => draw.IsCompleted, 5, "the side draw"); Assert.That(draw.Result, Is.True);
            GrowTheAnchoredRun(game);
            game.PutBack(); yield return null;
            Assert.That(game.Board.Layout.TryGet(2, out var at) && at.Row == 0 && at.Column == 8, Is.True);
            Assert.That(game.Model.MeldById("m2").Tiles, Is.EqualTo(new[] { "Y7a", "Y8a", "Y9a" }));
            CollectionAssert.AreEquivalent(new[] { "Y6a", "Y5a", "Y4a", "B2a" }, game.Model.Rack);
            Assert.That(game.Model.Discards[3].Last(), Is.EqualTo("B2b")); Assert.That(game.CanDraw, Is.True);
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator TheNextTurnUsesItsOwnBoardSnapshotInsteadOfThePreviousTurns()
        {
            var game = Build(Offline("isle"));
            yield return Until(() => game.HumanTurn && game.CanDiscard, 10, "my opened turn");
            PrepareAnchorUndoHand(game); GrowTheAnchoredRun(game);
            game.Discard(TileIds.ToInt("B2a"));
            // Resume the same hand at my next turn, preserving the committed table and its relocated anchors.
            var next = G101Rules.FromSpec(new HandSpec {
                Opts = new MatchOptions { Mode = "solo", Hands = 5 }, Seed = 42, HandNo = 1, Dealer = 3, Indicator = game.Model.Indicator,
                Melds = game.Model.Melds.Select(m => new SpecMeld { Owner = m.Owner, Kind = m.Kind, Tiles = m.Tiles.ToList() }).ToList(),
                Racks = new List<List<string>> { new List<string> { "Y3a", "Y2a", "Y1a", "B3a" }, new List<string> { "R8b" }, new List<string> { "R9b" }, new List<string> { "R10b" } },
                Discards = game.Model.Discards.Select(p => p.ToList()).ToList(), Opened = (int[])game.Model.Opened.Clone(), OpenMode = (string[])game.Model.OpenMode.Clone(),
                TurnSeat = 0, TurnPhase = G101Rules.PhasePlay, TurnN = game.Model.TurnN + 1,
            });
            game.Local.Resume(next);
            Assert.That(game.Board.Layout.TryGet(2, out var before) && before.Row == 2 && before.Column == 3, Is.True);
            game.LayOff(TileIds.ToInt("Y3a"), 2, true); game.Undo(); yield return null;
            Assert.That(game.Board.Layout.TryGet(2, out var after) && after.Row == 2 && after.Column == 3, Is.True, "the previous turn's original (0,8) anchor is no longer an undo target");
            Assert.That(game.Model.MeldById("m2").Tiles, Is.EqualTo(new[] { "Y4a", "Y5a", "Y6a", "Y7a", "Y8a", "Y9a" }));
        }
    }
}

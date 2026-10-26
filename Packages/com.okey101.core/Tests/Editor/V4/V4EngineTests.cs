using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Okey101.Core.V4.Tests
{
    /// <summary>K-02 RNG and record versions, plus the few C# guards rules.js leaves to a TypeError.</summary>
    public sealed class V4EngineTests
    {
        // Golden values from rules.js rngNext / rngInt (node, logic/rules.js v0.4.0).
        [TestCase(0u, new uint[] { 1144304738, 1416247, 958946056, 627933444, 2007157716 }, 567894473u)]
        [TestCase(1u, new uint[] { 2693262067, 11749833, 2265367787, 4213581821, 4159151403 }, 567894474u)]
        [TestCase(42u, new uint[] { 2581720956, 1925393290, 3661312704, 2876485805, 750819978 }, 567894515u)]
        [TestCase(4294967295u, new uint[] { 3850105811, 813802916, 3073704848, 4054706436, 3630262831 }, 567894472u)]
        public void Mulberry32MatchesRulesJs(uint seed, uint[] expected, uint stateAfter)
        {
            var r = new RngState(seed);
            var got = new uint[5];
            for (var i = 0; i < 5; i++) got[i] = G101Rules.RngNext(r);
            Assert.That(got, Is.EqualTo(expected));
            Assert.That(r.S, Is.EqualTo(stateAfter));
        }

        [Test]
        public void RngIntIsNextModuloN()
        {
            var r = new RngState(42);
            var got = Enumerable.Range(0, 8).Select(_ => G101Rules.RngInt(r, 6)).ToArray();
            Assert.That(got, Is.EqualTo(new[] { 0, 4, 0, 5, 0, 3, 4, 5 }));
        }

        [Test]
        public void RecordVersionsNameTheNewEngine()
        {
            Assert.That(G101Rules.ShuffleVersion, Is.EqualTo("mulberry32-v4"));
            Assert.That(G101Rules.RuleVersion, Is.EqualTo("101-v4"));
            Assert.That(G101Rules.Version, Is.EqualTo("0.4.0"));
            Assert.That(G101Rules.ShuffleVersion, Is.Not.EqualTo(Okey101.Core.Random.SeededRandom.AlgorithmVersion));
        }

        [Test]
        public void TheDealOrderIsDealerThenShuffleThenDice()
        {
            // D-02: dealer, Fisher–Yates and the two dice come from one stream in this order.
            var s = G101Rules.Apply(G101Rules.NewMatch(new MatchOptions(), 42), GameAction.StartHand()).State;
            var r = new RngState(42);
            var dealer = G101Rules.RngInt(r, 4);
            var ids = new List<string>(G101Rules.AllTiles);
            for (var i = ids.Count - 1; i > 0; i--) { var j = G101Rules.RngInt(r, i + 1); var t = ids[i]; ids[i] = ids[j]; ids[j] = t; }
            var dice = new[] { 1 + G101Rules.RngInt(r, 6), 1 + G101Rules.RngInt(r, 6) };
            Assert.That(s.Hand.Deal.Dealer, Is.EqualTo(dealer));
            Assert.That(s.Hand.Deal.Order, Is.EqualTo(ids));
            Assert.That(s.Hand.Deal.Dice, Is.EqualTo(dice));
            Assert.That(s.Rng.S, Is.EqualTo(r.S));
        }

        [Test]
        public void TheNextHandDealerIsTheNextSeatAndNeedsNoRng()
        {
            var s = G101Rules.NewMatch(new MatchOptions { Hands = 3 }, 7);
            var sim = G101Bots.Simulate(new MatchOptions { Hands = 3 }, 7);
            Assert.That(sim.State.History.Count, Is.EqualTo(3));
            s = G101Rules.Apply(s, GameAction.StartHand()).State;
            var first = s.Hand.Deal.Dealer;
            for (var guard = 0; s.Status == G101Rules.StatusPlaying && guard < 2000; guard++)
            {
                var r = G101Rules.Apply(s, G101Bots.NextAction(s, s.Hand.Turn.Seat));
                Assert.That(r.Ok, Is.True, r.Error);
                s = r.State;
            }
            s = G101Rules.Apply(s, GameAction.NextHand()).State;
            Assert.That(s.Hand.Deal.Dealer, Is.EqualTo((first + 1) % 4));
        }

        [Test]
        public void ASwapIndexOutsideTheMeldIsRejectedNotThrown()
        {
            // rules.js would throw a TypeError on tiles[index] === undefined; the port answers E_SWAP_INVALID.
            var s = G101Rules.FromSpec(new HandSpec
            {
                Indicator = "B6a",
                Racks = new List<List<string>> { new List<string> { "K4a", "R1a" }, new List<string>(), new List<string>(), new List<string>() },
                Melds = new List<SpecMeld> { new SpecMeld { Owner = 1, Kind = "run", Tiles = new List<string> { "K3a", "B7a", "K5a" } } },
                Opened = new[] { 120, 0, 0, 0 },
                TurnSeat = 0, TurnPhase = "play", TurnN = 7
            });
            var r = G101Rules.Apply(s, GameAction.Swap(0, "K4a", "m1", 9));
            Assert.That(r.Error, Is.EqualTo("E_SWAP_INVALID"));
            var ok = G101Rules.Apply(s, GameAction.Swap(0, "K4a", "m1", 1));
            Assert.That(ok.Ok, Is.True, ok.Error);
            Assert.That(ok.State.Hand.Turn.Pending.Single().Code, Is.EqualTo("P_OKEY_TAKEN"));
        }

        // An opened player took the side tile R4a; laying off B4a would leave only R4a, which may not be discarded (R-32).
        private static MatchState SideTileLeftAlone(string phase) => G101Rules.FromSpec(new HandSpec
        {
            Indicator = "K13a", TurnSeat = 0, TurnPhase = phase, TurnN = 9,
            Opened = new[] { 101, 101, 0, 0 }, OpenMode = new[] { "run", "run", null, null },
            Racks = new List<List<string>> { new List<string> { "B4a" }, new List<string>(), new List<string>(), new List<string>() },
            Discards = new List<List<string>> { new List<string>(), new List<string>(), new List<string>(), new List<string> { "R4a" } },
            Melds = new List<SpecMeld>
            {
                new SpecMeld { Owner = 1, Kind = "run", Tiles = new List<string> { "B1a", "B2a", "B3a" } },
                new SpecMeld { Owner = 1, Kind = "run", Tiles = new List<string> { "R1a", "R2a", "R3a" } },
            },
            Deck = new List<string> { "Y13a", "Y12a" }
        });

        [Test]
        public void ABotThatTakesTheSideTileNeverStrandsIt()
        {
            var s = SideTileLeftAlone("draw");
            for (var step = 0; step < 6 && s.Hand.Turn.Seat == 0 && s.Status == G101Rules.StatusPlaying; step++)
            {
                var a = G101Bots.NextAction(s, 0);
                var r = G101Rules.Apply(s, a);
                Assert.That(r.Ok, Is.True, $"{a.T} {a.Tile}: {r.Error}");
                s = r.State;
            }
            Assert.That(s.Hand == null || s.Hand.Turn.Seat != 0 || s.Status != G101Rules.StatusPlaying, Is.True, "the bot's turn ended");
        }

        [Test]
        public void ATimeoutWithOnlyTheSideTileLeftReturnsItAndDrawsFromTheDeck()
        {
            var s = G101Rules.Apply(SideTileLeftAlone("draw"), GameAction.Draw(0, "side")).State;
            s = G101Rules.Apply(s, GameAction.Add(0, "B4a", "m1", "post")).State;
            Assert.That(G101Rules.AutoDiscardTile(s, 0), Is.Null);
            var r = G101Rules.Apply(s, GameAction.Timeout(0));
            Assert.That(r.Ok, Is.True, r.Error);
            Assert.That(r.Events.Select(e => e.E), Is.EqualTo(new[] { "auto", "sideReturned", "drew", "discarded", "turn" }));
            Assert.That(r.State.Hand.Discards[3].Last(), Is.EqualTo("R4a"));
            Assert.That(r.State.Hand.Melds[0].Tiles, Is.EqualTo(new[] { "B1a", "B2a", "B3a" }), "the lay-off of the turn is undone");
            Assert.That(r.State.Hand.Turn.Seat, Is.EqualTo(1));
        }

        [Test]
        public void AGroupOkeyIsTakenOnlyWithTheTileItStandsFor()
        {
            // R08/R09: in R5 B5 okey the okey stands for Y5 (shown so); K5 would also make a group but is not its face.
            var s = G101Rules.FromSpec(new HandSpec
            {
                Indicator = "K13a", TurnSeat = 0, TurnPhase = "play", TurnN = 9,
                Opened = new[] { 101, 101, 0, 0 }, OpenMode = new[] { "run", "run", null, null },
                Racks = new List<List<string>> { new List<string> { "K5a", "Y5a", "B12a" }, new List<string>(), new List<string>(), new List<string>() },
                Melds = new List<SpecMeld> { new SpecMeld { Owner = 1, Kind = "group", Tiles = new List<string> { "R5a", "B5a", "K1a" } } }
            });
            Assert.That(G101Rules.AnalyzeMeld(s.Hand.Okey, "group", s.Hand.Melds[0].Tiles).Repr[2].C, Is.EqualTo("Y"));
            Assert.That(G101Rules.SwapFits(s, "m1", 2, "Y5a"), Is.True);
            Assert.That(G101Rules.SwapFits(s, "m1", 2, "K5a"), Is.False);
            var wrong = G101Rules.Apply(s, GameAction.Swap(0, "K5a", "m1", 2));
            Assert.That(wrong.Error, Is.EqualTo("E_SWAP_INVALID"));
            // Adding Y5 would turn the okey into K5: refused; K5 keeps it Y5.
            Assert.That(G101Rules.AddSides(s, "m1", "Y5a"), Is.Empty);
            Assert.That(G101Rules.AddSides(s, "m1", "K5a"), Is.EqualTo(new[] { "post" }));
        }

        [Test]
        public void CanonicalJsonSortsKeysAndEscapesLikeJsonStringify()
        {
            var tree = new Dictionary<string, object> { { "b", new List<object> { 1, "x\"y", null, true } }, { "a", -5L }, { "c\n", "\u0001" } };
            Assert.That(V4Tree.Canonical(tree), Is.EqualTo("{\"a\":-5,\"b\":[1,\"x\\\"y\",null,true],\"c\\n\":\"\\u0001\"}"));
        }

        [Test]
        public void LegalFollowsThePhase()
        {
            var s = G101Rules.Apply(G101Rules.NewMatch(new MatchOptions(), 42), GameAction.StartHand()).State;
            var seat = s.Hand.Turn.Seat;
            var l = G101Rules.Legal(s, seat);
            Assert.That((l.Draw, l.Discard, l.Open, l.Undo, l.Add), Is.EqualTo((false, true, true, false, false)));
            Assert.That(G101Rules.Legal(s, (seat + 1) % 4).Discard, Is.False);
            s = G101Rules.Apply(s, GameAction.Discard(seat, s.Hand.Racks[seat][0])).State;
            l = G101Rules.Legal(s, (seat + 1) % 4);
            Assert.That((l.Draw, l.Side, l.Discard), Is.EqualTo((true, true, false)));
        }
    }
}

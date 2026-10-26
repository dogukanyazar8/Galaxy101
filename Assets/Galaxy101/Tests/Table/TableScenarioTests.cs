using System;
using System.Collections.Generic;
using System.Linq;
using Galaxy101.Contracts;
using NUnit.Framework;
using Okey101.Core.V4;

namespace Galaxy101.Table.Tests
{
    /// <summary>
    /// The offline table (LocalTableHost: the server's TableRuntime on the client) and the prepared test flows
    /// (client-tests.json hands on the v4 engine): each flow builds a legal hand, plays the way its catalogue states
    /// expect, and the package scenarios R43 / R44 give their penalties.
    /// </summary>
    [TestFixture]
    public sealed class TableScenarioTests
    {
        private const long Start = 5_000_000;

        private static V4Table TestTable(int hands = 5) => TableTestHost.Table(new MatchOptions { Mode = "solo", Hands = hands }, 30, true, new[] { false, true, true, true });

        private static LocalTableHost Local(uint seed, int hands = 5, DealRig rig = null)
        {
            var host = new LocalTableHost(0, Start);
            host.Start(new TableSetup { Options = new MatchOptions { Mode = "solo", Hands = hands }, Seed = seed, TurnMs = 30000, FirstHandRig = rig }, TestTable(hands));
            return host;
        }

        private static IList<IList<string>> Segments(string[][] rows)
        {
            var list = new List<IList<string>>();
            foreach (var row in rows)
            {
                int i = 0;
                while (i < row.Length)
                {
                    if (row[i] == null) { i++; continue; }
                    int j = i; var seg = new List<string>();
                    while (j < row.Length && row[j] != null) seg.Add(row[j++]);
                    list.Add(seg); i = j;
                }
            }
            return list;
        }

        [TestCaseSource(typeof(TableScenarios), nameof(TableScenarios.Names))]
        public void EveryFlowIsALegalHand(string flow)
        {
            var sc = TableScenarios.Create(flow);
            var s = sc.State();
            Assert.That(G101Rules.Invariants(s), Is.Empty);
            var rack = sc.Rows.SelectMany(r => r).Where(x => x != null).ToList();
            CollectionAssert.AreEquivalent(rack, s.Hand.Racks[0]);
            Assert.That(s.Seats[0].Bot, Is.False);
            Assert.That(s.Seats.Skip(1).All(x => x.Bot), Is.True);
            foreach (var pair in sc.Melds) Assert.That(s.Hand.Melds.Any(m => m.Id == pair.Value && m.Owner == pair.Key.Owner), Is.True, pair.Value);
        }

        [Test]
        public void TheFlowsShowTheReferenceCounts()
        {
            var play = TableScenarios.Create("play");
            var ok = G101Rules.OkeyFromIndicator(play.Spec.Indicator);
            Assert.That(RackMelds.Of(Segments(play.Rows), ok).Sum, Is.EqualTo(88), "T30: 88 / 101");
            Assert.That(play.State().Hand.Racks.Select(r => r.Count), Is.EqualTo(new[] { 21, 21, 9, 21 }));
            var open = TableScenarios.Create("open");
            Assert.That(RackMelds.Of(Segments(open.Rows), ok).Sum, Is.EqualTo(131), "G13: Açtı · 131");
            var bomb = TableScenarios.Create("bomb");
            var bm = RackMelds.Of(Segments(bomb.Rows), ok);
            Assert.That(bm.Sum, Is.EqualTo(89), "G25: 89 / 101");
            Assert.That(bm.FromHand(101, 5), Is.Null, "R-53: no Elden bit below the bar");
            var pen = TableScenarios.Create("penOpen");
            Assert.That(RackMelds.Of(Segments(pen.Rows), ok).Sum, Is.EqualTo(66), "G20: 66 / 101");
        }

        private static (LocalTableHost Host, TableScenario Sc) Flow(string flow)
        {
            var host = Local(42);
            var sc = TableScenarios.Create(flow);
            host.ResumeFrom(sc.State());
            return (host, sc);
        }

        private static List<string> Events(LocalTableHost host)
        {
            var list = new List<string>();
            host.Client.Applied += m => { if (m.Type == V4Messages.Game && m.E != null) list.Add(m.E); };
            return list;
        }

        [Test]
        public void OpenFlowOpensWithTheRackSegments()
        {
            var (host, sc) = Flow("open");
            Assert.That(host.Client.MyTurn, Is.True);
            Assert.That(host.Client.Rack.Count, Is.EqualTo(22));
            var melds = RackMelds.Of(Segments(sc.Rows), new ColorNum(host.Client.OkeyColor, host.Client.OkeyNumber));
            Assert.That(host.Move(GameAction.Open(0, melds.Groups)), Is.Null);
            Assert.That(host.Client.Opened[0], Is.EqualTo(131));
            Assert.That(host.Client.Legal().Undo, Is.True, "G13: Geri topla");
        }

        [Test]
        public void PenOpenFlowIsAShortOpeningWithItsPenalty()
        {
            var (host, sc) = Flow("penOpen");
            var events = Events(host);
            var melds = RackMelds.Of(Segments(sc.Rows), new ColorNum(host.Client.OkeyColor, host.Client.OkeyNumber));
            Assert.That(host.Move(GameAction.Open(0, melds.Groups)), Is.Null);
            Assert.That(events, Is.EqualTo(new[] { "openShort", "penalty" }));
            Assert.That(host.Client.Pen[0], Is.EqualTo(101));
            Assert.That(host.Client.Opened[0], Is.EqualTo(0));
            Assert.That(host.Client.Rack.Count, Is.EqualTo(17));
        }

        [Test]
        public void IsleFlowTakesTheOkeyLaysOffAndUndoes()
        {
            var (host, sc) = Flow("isle");
            var c = host.Client;
            var y4 = c.Rack.First(id => id.StartsWith("Y4"));
            var m = sc.Melds[(2, 0)];
            Assert.That(c.SwapFits(m, 1, y4), Is.True, "G16: Y4 takes Ayşe's okey");
            var k5 = c.Rack.First(id => id.StartsWith("K5"));
            Assert.That(c.SwapFits(m, 1, k5), Is.False, "T49: K5 does not");
            Assert.That(host.Move(GameAction.Swap(0, y4, m, 1)), Is.Null, "G17");
            Assert.That(c.Rack.Any(id => c.IsOkey(id)), Is.True);
            Assert.That(host.Move(GameAction.Undo(0)), Is.Null);
            // G18: İşle lays off every tile that fits, in rack order.
            int added = 0;
            for (bool any = true; any;)
            {
                any = false;
                foreach (var id in c.Rack.ToList())
                {
                    if (c.Rack.Count <= 1) break;
                    foreach (var meld in c.Melds)
                    {
                        var sides = c.AddSides(meld.Id, id);
                        if (sides.Count == 0) continue;
                        Assert.That(host.Move(GameAction.Add(0, id, meld.Id, sides[0])), Is.Null);
                        added++; any = true; break;
                    }
                    if (any) break;
                }
            }
            Assert.That(added, Is.EqualTo(5));
            Assert.That(c.Rack.Select(id => id.Substring(0, id.Length - 1)), Is.EquivalentTo(new[] { "Y4", "K13" }));
            Assert.That(host.Move(GameAction.Undo(0)), Is.Null, "G19: Geri topla");
            Assert.That(c.Rack.Count, Is.EqualTo(7));
        }

        [Test]
        public void PenIslekFlowChargesThePlayableDiscard()
        {
            var (host, _) = Flow("penIslek");
            var events = Events(host);
            var b4 = host.Client.Rack.First(id => id.StartsWith("B4"));
            Assert.That(host.Client.IsIslek(b4), Is.True);
            Assert.That(host.Move(GameAction.Discard(0, b4)), Is.Null);
            Assert.That(events, Does.Contain("penalty"));
            Assert.That(host.Client.Pen[0], Is.EqualTo(101), "G24: işlek +101");
        }

        [Test]
        public void EndFlowAyseLaysOffAndFinishes()
        {
            var (host, _) = Flow("end");
            var events = Events(host);
            long t0 = host.Now;
            while (host.Client.Status == G101Rules.StatusPlaying && host.Now - t0 < 5000) host.Step();
            Assert.That(events, Is.EqualTo(new[] { "added", "added", "finished", "handEnd" }));
            var score = TableClient.Ints(host.Client.Result["score"]);
            Assert.That(score, Is.EqualTo(new[] { 202, 202, -101, 202 }), "T24: Burak −101, the others +202");
            Assert.That(host.Now - t0, Is.LessThan(3200), "T23: the finish within the hold");
        }

        [Test]
        public void PlayFlowWaitsForMyDraw()
        {
            var (host, _) = Flow("play");
            var c = host.Client;
            Assert.That(c.MyTurn && c.TurnPhase == G101Rules.PhaseDraw, Is.True);
            Assert.That(c.Legal().Side, Is.True);
            Assert.That(host.Move(GameAction.Draw(0, "side")), Is.Null, "T38: Yerden alındı");
            Assert.That(c.SideTake, Does.StartWith("R9"));
            Assert.That(c.Legal().ReturnSide, Is.True, "Geri bırak");
            Assert.That(host.Move(GameAction.ReturnSide(0)), Is.Null);
            Assert.That(c.SideReturned && c.TurnPhase == G101Rules.PhaseDraw, Is.True);
        }

        /// <summary>R43.json / R44.json: the deal is rigged, the rest is the real runtime with real bots (T34, T36).</summary>
        [TestCase("R43", "P_OPENED_WITH", 90)]
        [TestCase("R44", "P_OKEY_TAKEN", 101)]
        public void PackageScenariosGiveTheirPenalty(string id, string code, int amount)
        {
            var host = Local(TableScenarios.ScenarioSeed, 5, TableScenarios.Rig(id));
            var penalties = new List<IDictionary<string, object>>();
            host.Client.Applied += m => { if (m.E == "penalty") penalties.Add(m.Ev); };
            bool acted = false; int guard = 0;
            while (penalties.Count == 0 && guard++ < 2000)
            {
                var c = host.Client;
                if (!acted && c.MyTurn)
                {
                    acted = true;
                    if (id == "R43") Assert.That(host.Move(GameAction.Discard(0, "K9b")), Is.Null);
                    else
                    {
                        var melds = new List<MeldSpec>
                        {
                            new MeldSpec("run", new List<string> { "R10a", "R11a", "Y5a", "R13a" }), new MeldSpec("group", new List<string> { "K13a", "B13a", "Y13a" }),
                            new MeldSpec("run", new List<string> { "K10a", "K11a", "K12a" }),
                        };
                        Assert.That(host.Move(GameAction.Open(0, melds)), Is.Null);
                        Assert.That(host.Client.Opened[0], Is.EqualTo(118));
                        Assert.That(host.Move(GameAction.Discard(0, "R6a")), Is.Null);
                    }
                    continue;
                }
                if (!host.Step()) break;
            }
            Assert.That(penalties, Is.Not.Empty);
            var p = penalties[0];
            Assert.That((string)p["code"], Is.EqualTo(code));
            Assert.That(TableClient.I(p["seat"]), Is.EqualTo(0));
            Assert.That(TableClient.I(p["amount"]), Is.EqualTo(amount));
            Assert.That(TableClient.I(p["by"]), Is.EqualTo(1));
        }

        [Test]
        public void AgainstBotsAMatchEndsWithItsResultAndXp()
        {
            // The person never moves: every turn times out (R-70) and the server's move is played; no chips (R-94), XP (D-42).
            var host = Local(42, hands: 2);
            var xp = new List<(long, string)>();
            host.Xp += (amount, reason) => xp.Add((amount, reason));
            bool ended = false; host.Ended += () => ended = true;
            int guard = 0;
            while (!ended && guard++ < 100000) if (!host.Step()) break;
            Assert.That(ended, Is.True);
            var r = host.Client.MatchResult;
            Assert.That(r, Is.Not.Null);
            Assert.That(r.Pool, Is.EqualTo(0));
            Assert.That(r.Payouts.All(x => x == 0), Is.True);
            Assert.That(host.Client.Status, Is.EqualTo(G101Rules.StatusMatchOver));
            Assert.That(xp.Count(x => x.Item2 == "hand"), Is.EqualTo(2));
            Assert.That(host.XpEarned, Is.EqualTo(xp.Sum(x => x.Item1)));
            Assert.That(host.Client.History.Count, Is.EqualTo(2));
        }

        [Test]
        public void TheDealCeremonyFollowsTheServerSchedule()
        {
            // D-24: dealStart → first turn 21 686 ms with a bot dealer; the client sees every step in order.
            var host = Local(42);
            var steps = new List<(string, long)>();
            host.Delivered += (type, payload) =>
            {
                if (type == V4Messages.DealStart) steps.Add(("dealStart", host.Now));
                if (type == V4Messages.DealStep) steps.Add((((V4DealStep)payload).Step, host.Now));
                if (type == V4Messages.DealDie) steps.Add(("die", host.Now));
                if (type == V4Messages.HandDealt) steps.Add(("handDealt", host.Now));
                if (type == V4Messages.Turn) steps.Add(("turn", host.Now));
            };
            while (!steps.Any(s => s.Item1 == "turn")) Assert.That(host.Step(), Is.True);
            var names = steps.Select(s => s.Item1).ToList();
            Assert.That(names, Is.EqualTo(new[] { "dealStart", "dice", "die", "die", "indicator", "extra", "distribute", "center", "handDealt", "turn" }));
            var dealer = host.Runtime.Match.Hand.Deal.Dealer;
            if (dealer != 0) Assert.That(steps.Last().Item2 - steps[0].Item2, Is.EqualTo(TableTimings.DealTotalBotDealer));
            Assert.That(host.Client.Rack.Count, Is.EqualTo(dealer == 3 ? 22 : 21));
        }
    }
}

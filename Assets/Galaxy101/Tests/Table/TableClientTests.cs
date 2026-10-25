using System;
using System.Collections.Generic;
using System.Linq;
using Galaxy101.Contracts;
using NUnit.Framework;
using Okey101.Core.V4;

namespace Galaxy101.Table.Tests
{
    /// <summary>
    /// The client model against the server's match code: a <see cref="TableRuntime"/> runs whole matches on a test host,
    /// seat 0's person plays by a varied policy (the bots' moves, side draws, returns, undos, wrong moves, timeouts, dice,
    /// ready), and after every step of the server (each timer, each move) the model each client built from its events alone
    /// must give exactly the runtime's <c>GameView(seat)</c> — for the person and for a spectator.
    /// </summary>
    [TestFixture]
    public sealed class TableClientTests
    {
        private const long Start = 1_000_000;

        public static string Canon(object tree) => V4Tree.Canonical(tree);

        public static void Same(TableClient client, TableRuntime rt, long now, int seat, string where)
        {
            var want = rt.GameView(seat);
            var got = client.View(now);
            if (Canon(want) == Canon(got)) return;
            var keys = new SortedSet<string>(want.Keys.Concat(got.Keys));
            var diffs = new List<string>();
            foreach (var k in keys)
            {
                want.TryGetValue(k, out var a); got.TryGetValue(k, out var b);
                string ca = Canon(a), cb = Canon(b);
                if (ca != cb) diffs.Add(k + ": server " + Trim(ca) + " · client " + Trim(cb));
            }
            Assert.Fail("seat " + seat + " view differs at " + where + " (t=" + (now - Start) + "):\n" + string.Join("\n", diffs));
        }

        private static string Trim(string s) => s.Length > 400 ? s.Substring(0, 400) + "…" : s;

        public sealed class Run
        {
            public TableTestHost Host;
            public TableRuntime Rt;
            public Random Rng;
            public int Steps, Moves, Errors, Timeouts, Undos, Returns, Sides, Rolls, Readies, LegalChecks, Hands;
            public readonly HashSet<string> Events = new HashSet<string>();

            public void Check(string where)
            {
                Host.Deliver();
                foreach (var pair in Host.Clients) Same(pair.Value, Rt, Host.Now, pair.Key, where);
            }
        }

        /// <summary>A match with the person at seat 0 (and the given extra people), a spectator, all clients checked after every step.</summary>
        public static Run Play(MatchOptions o, uint seed, int policy, int[] people = null, Action<Run> each = null, int maxSteps = 200000)
        {
            people = people ?? new[] { 0 };
            var host = new TableTestHost(Start);
            var bots = new bool[4];
            for (int k = 0; k < 4; k++) bots[k] = !people.Contains(k);
            var setup = new TableSetup { Options = o, Seed = seed, TurnMs = 20000 };
            var rt = new TableRuntime(host, setup, bots, new RngState(seed + 7));
            var run = new Run { Host = host, Rt = rt, Rng = new Random(policy) };
            foreach (var k in people) Hook(run, host.Watch(k), k);
            Hook(run, host.Watch(-1), -1);
            rt.Start(TableTestHost.Table(o, 20, bots: bots));
            run.Check("matchStart");
            string turnKey = null; int acts = 0; bool waiting = false, readySent = false;
            while (!host.Ended && run.Steps++ < maxSteps)
            {
                each?.Invoke(run);
                var s = rt.Match;
                if (rt.Dealing)
                {
                    var view = rt.GameView(0);
                    var deal = (Dictionary<string, object>)view["deal"];
                    if (deal != null && (string)deal["step"] == "dice" && s.Hand.Deal.Dealer == 0 && people.Contains(0) && run.Rng.NextDouble() < 0.3)
                    {
                        Delay(run, 3000);
                        if (rt.RollDice(0) == null) run.Rolls++;
                        run.Check("gRollDice");
                        continue;
                    }
                }
                else if (s.Status == G101Rules.StatusPlaying && s.Hand.Turn.Seat == 0 && people.Contains(0) && !rt.IsBotSeat(0) && rt.Connected[0])
                {
                    var key = s.HandNo + ":" + s.Hand.Turn.N;
                    if (key != turnKey) { turnKey = key; acts = 0; waiting = run.Rng.NextDouble() < 0.07; }
                    if (!waiting && acts < 14)
                    {
                        acts++;
                        Delay(run, 1200);
                        Act(run, s);
                        run.Check("move " + run.Moves);
                        continue;
                    }
                }
                else if (s.Status == G101Rules.StatusHandOver && people.Contains(0))
                {
                    if (!readySent && run.Rng.NextDouble() < 0.5) { readySent = true; Delay(run, 2000); if (rt.ReadyUp(0) == null) run.Readies++; run.Check("gReady"); continue; }
                }
                if (s.Status == G101Rules.StatusPlaying) readySent = false;
                if (!host.StepTime()) break;
                run.Check("timer");
            }
            Assert.That(host.Ended, Is.True, "the match ended (steps " + run.Steps + ")");
            run.Hands = rt.Match.HandNo;
            return run;
        }

        private static void Hook(Run run, TableClient c, int seat)
        {
            c.Applied += m =>
            {
                if (m.Type == V4Messages.Game && m.E != null) run.Events.Add(m.E);
                // The legal message is sent last for its step: the model's own legality must agree with it.
                if (m.Type == V4Messages.Legal && seat >= 0)
                {
                    run.LegalChecks++;
                    Assert.That(Canon(c.Legal().ToTree()), Is.EqualTo(Canon(c.LegalEvent)), "legal for seat " + seat);
                }
            };
        }

        private static void Delay(Run run, int max)
        {
            long d = run.Rng.Next(0, max);
            var next = run.Host.NextDue;
            if (!next.HasValue || run.Host.Now + d < next.Value) run.Host.Now += d;
        }

        /// <summary>One move of the person: mostly the bot's choice, sometimes a side draw, a return, an undo or a wrong move.</summary>
        private static void Act(Run run, MatchState s)
        {
            var rt = run.Rt; var h = s.Hand; var t = h.Turn; var r = run.Rng.NextDouble();
            var legal = G101Rules.Legal(s, 0);
            GameAction a;
            if (r < 0.06)
            {
                // Wrong moves: refused, nothing sent, nothing changes.
                a = t.Phase == G101Rules.PhaseDraw ? GameAction.Discard(0, h.Racks[0][0]) : GameAction.Draw(0, "deck");
                var before = Canon(rt.GameView(0));
                Assert.That(rt.Move(0, a, out _), Is.Not.Null, "a wrong move is refused");
                run.Errors++;
                Assert.That(Canon(rt.GameView(0)), Is.EqualTo(before));
                return;
            }
            if (t.Phase == G101Rules.PhaseDraw && legal.Side && r < 0.3) { a = GameAction.Draw(0, "side"); run.Sides++; }
            else if (t.Phase == G101Rules.PhasePlay && legal.ReturnSide && r < 0.45) { a = GameAction.ReturnSide(0); run.Returns++; }
            else if (t.Phase == G101Rules.PhasePlay && legal.Undo && r < 0.35) { a = GameAction.Undo(0); run.Undos++; }
            else a = G101Bots.NextAction(s, 0);
            var error = rt.Move(0, a, out _);
            if (error == null) run.Moves++; else run.Errors++;
        }

        private static IEnumerable<TestCaseData> Matches()
        {
            yield return new TestCaseData(new MatchOptions { Mode = "solo", Hands = 3 }, 42u, 1).SetName("Solo seed 42");
            yield return new TestCaseData(new MatchOptions { Mode = "solo", Hands = 3 }, 7u, 2).SetName("Solo seed 7");
            yield return new TestCaseData(new MatchOptions { Mode = "solo", Hands = 2, Katlama = true }, 99u, 3).SetName("Solo katlamali seed 99");
            yield return new TestCaseData(new MatchOptions { Mode = "pairs", Hands = 2, Katlama = true, KatlamaPartner = false }, 5u, 4).SetName("Pairs katlamali rakip seed 5");
            yield return new TestCaseData(new MatchOptions { Mode = "pairs", Hands = 2 }, 1234u, 5).SetName("Pairs seed 1234");
            yield return new TestCaseData(new MatchOptions { Mode = "solo", Hands = 2, Penalties = false, Isle = false }, 3u, 6).SetName("Solo no penalties no isle seed 3");
            for (uint seed = 100; seed < 106; seed++) yield return new TestCaseData(new MatchOptions { Mode = "solo", Hands = 2 }, seed, (int)seed).SetName("Solo seed " + seed);
        }

        [TestCaseSource(nameof(Matches))]
        public void EventsRebuildTheServerViewAfterEveryStep(MatchOptions o, uint seed, int policy)
        {
            var run = Play(o, seed, policy);
            Assert.That(run.Hands, Is.EqualTo(o.Hands));
            Assert.That(run.Moves, Is.GreaterThan(10));
            TestContext.WriteLine($"steps {run.Steps} moves {run.Moves} errors {run.Errors} sides {run.Sides} returns {run.Returns} undos {run.Undos} rolls {run.Rolls} ready {run.Readies} legal {run.LegalChecks} events {string.Join(",", run.Events.OrderBy(x => x))}");
        }

        [Test]
        public void TheMatchesCoverEveryEngineEvent()
        {
            var seen = new HashSet<string>();
            foreach (var data in Matches())
            {
                var run = Play((MatchOptions)data.Arguments[0], (uint)data.Arguments[1], (int)data.Arguments[2]);
                seen.UnionWith(run.Events);
            }
            foreach (var e in new[] { "handStart", "turn", "drew", "discarded", "opened", "laid", "added", "penalty", "auto", "handEnd", "matchEnd", "undone", "sideReturned" })
                Assert.That(seen, Does.Contain(e), e);
            TestContext.WriteLine(string.Join(",", seen.OrderBy(x => x)));
        }

        [Test]
        public void TwoPeopleSeeTheirOwnViewsWhenOneLeavesAndTheSeatIsTaken()
        {
            bool left = false, taken = false;
            Run run = null;
            run = Play(new MatchOptions { Mode = "solo", Hands = 3 }, 11u, 11, new[] { 0, 2 }, r =>
            {
                var s = r.Rt.Match;
                if (!left && s.HandNo == 1 && s.Status == G101Rules.StatusPlaying && !r.Rt.Dealing && s.Hand.Turn.N >= 6)
                {
                    left = true;
                    Assert.That(r.Rt.Leave(2), Is.Null);
                    r.Host.Unwatch(2);   // the leaver gets nothing more
                    r.Check("seatLeft");
                }
                else if (left && !taken && s.HandNo == 2 && s.Status == G101Rules.StatusPlaying && !r.Rt.Dealing && s.Hand.Turn.Seat != 2)
                {
                    taken = true;
                    Assert.That(r.Rt.TakeSeat(2), Is.Null);
                    // The new person starts from the server's snapshot.
                    var c = r.Host.Watch(2);
                    c.Apply(V4Messages.Snapshot, new V4Snapshot { Screen = "table", Game = r.Rt.GameView(2), At = r.Host.Now });
                    r.Check("seatTaken");
                }
            });
            Assert.That(left && taken, Is.True);
            Assert.That(run.Events, Does.Contain("seatLeft").And.Contain("seatTaken"));
        }

        [Test]
        public void ADisconnectedSeatIsPlayedAndTheSnapshotBringsThePersonBack()
        {
            bool dropped = false, back = false;
            var run = Play(new MatchOptions { Mode = "solo", Hands = 2 }, 21u, 21, null, r =>
            {
                var s = r.Rt.Match;
                if (!dropped && s.HandNo == 1 && !r.Rt.Dealing && s.Status == G101Rules.StatusPlaying && s.Hand.Turn.N >= 3)
                {
                    dropped = true;
                    r.Rt.Disconnect(0);
                    r.Check("disconnect");
                }
                else if (dropped && !back && r.Rt.Autoplay[0] && s.Status == G101Rules.StatusPlaying && !r.Rt.Dealing && s.Hand.Turn.Seat == 0)
                {
                    back = true;
                    // hello: the server stops playing the seat, the person gets a fresh client from the snapshot.
                    r.Rt.Reconnect(0);
                    var c = r.Host.Watch(0);
                    c.Apply(V4Messages.Snapshot, new V4Snapshot { Screen = "table", Game = r.Rt.GameView(0), At = r.Host.Now });
                    r.Rt.AnnounceSeat(0);
                    r.Check("reconnect");
                }
            });
            Assert.That(dropped && back, Is.True, "dropped and back");
        }

        [Test]
        public void TheWireCarriesTheSameModel()
        {
            // Every message of a match goes through JSON (camelCase, as the server sends it) into a second client.
            var o = new MatchOptions { Mode = "solo", Hands = 2 };
            var host = new TableTestHost(Start);
            var bots = new[] { false, true, true, true };
            var rt = new TableRuntime(host, new TableSetup { Options = o, Seed = 77, TurnMs = 20000 }, bots, new RngState(1));
            var direct = host.Watch(0);
            var wire = new TableClient(0) { TrustViews = false };
            rt.Start(TableTestHost.Table(o, 20, bots: bots));
            int seen = 0;
            while (!host.Ended && host.StepTime())
            {
                var s = rt.Match;
                if (!rt.Dealing && s.Status == G101Rules.StatusPlaying && s.Hand.Turn.Seat == 0) rt.Move(0, G101Bots.NextAction(s, 0), out _);
                host.Deliver();
                for (; seen < host.Log.Count; seen++)
                {
                    var (seat, type, payload) = host.Log[seen];
                    if (seat != 0) continue;
                    var json = V4Wire.ToJson(payload).ToString(Newtonsoft.Json.Formatting.None);
                    wire.Apply(type, V4Wire.Payload(type, Newtonsoft.Json.Linq.JToken.Parse(json)));
                }
                Assert.That(Canon(wire.View(host.Now)), Is.EqualTo(Canon(direct.View(host.Now))));
                Same(wire, rt, host.Now, 0, "wire");
            }
            Assert.That(host.Ended, Is.True);
        }

        [Test]
        public void TrustedViewsKeepTheModelEqual()
        {
            // Production mode: tableView / snapshot replace the model; it must stay what the events give.
            var o = new MatchOptions { Mode = "solo", Hands = 2 };
            var host = new TableTestHost(Start);
            var bots = new[] { false, true, true, true };
            var rt = new TableRuntime(host, new TableSetup { Options = o, Seed = 3, TurnMs = 20000 }, bots, new RngState(1));
            var c = host.Watch(0, trustViews: true);
            rt.Start(TableTestHost.Table(o, 20, bots: bots));
            while (!host.Ended && host.StepTime())
            {
                var s = rt.Match;
                if (!rt.Dealing && s.Status == G101Rules.StatusPlaying && s.Hand.Turn.Seat == 0) rt.Move(0, G101Bots.NextAction(s, 0), out _);
                host.Deliver();
                Same(c, rt, host.Now, 0, "trusted");
            }
        }

        [Test]
        public void TileIdsAreOneBijection()
        {
            var seen = new HashSet<int>();
            foreach (var id in G101Rules.AllTiles)
            {
                int n = TileIds.ToInt(id);
                Assert.That(n, Is.InRange(0, 105), id);
                Assert.That(seen.Add(n), Is.True, id);
                Assert.That(TileIds.ToV4(n), Is.EqualTo(id));
                if (G101Rules.IsFake(id)) Assert.That(TileIds.Code(n), Is.EqualTo("joker"));
                else Assert.That(TileIds.Code(n), Is.EqualTo(G101Rules.CodeOf(id)));
            }
            Assert.That(seen.Count, Is.EqualTo(106));
            Assert.That(TileIds.ToInt("R1a"), Is.EqualTo(0));
            Assert.That(TileIds.ToInt("Y1a"), Is.EqualTo(26));
            Assert.That(TileIds.ToInt("B13b"), Is.EqualTo(77));
            Assert.That(TileIds.ToInt("Jb"), Is.EqualTo(105));
            Assert.That(TileIds.MeldInt("m12"), Is.EqualTo(12));
        }
    }
}

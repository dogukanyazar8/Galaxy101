using System;
using System.Collections.Generic;
using System.Linq;
using Galaxy101.Contracts;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Okey101.Core.V4;

namespace Galaxy101.Table.Tests
{
    public sealed class DealSkipTests
    {
        private static string Skip(TableRuntime runtime, int seat)
        {
            var method = typeof(TableRuntime).GetMethod("SkipDeal");
            Assert.That(method, Is.Not.Null, "The authoritative runtime must implement gSkipDeal.");
            return (string)method.Invoke(runtime, new object[] { seat });
        }

        private static (TableRuntime Runtime, TableTestHost Host) Start(params int[] humans)
        {
            var host = new TableTestHost(1_000_000); var bots = new bool[4];
            for (int i = 0; i < 4; i++) bots[i] = !humans.Contains(i);
            foreach (int seat in humans) host.Watch(seat);
            host.Watch(-1);
            var setup = new TableSetup
            {
                Seed = 7, Options = new MatchOptions { Hands = 1 },
                FirstHandRig = G101Rules.RigFromHands(new ScenarioDeal { Dealer = 3, Dice = new[] { 4, 3 }, Indicator = "Y4a" }),
            };
            var runtime = new TableRuntime(host, setup, bots, new RngState(1));
            runtime.Start(TableTestHost.Table(setup.Options, bots: bots));
            host.StepTime(); host.Deliver();
            return (runtime, host);
        }

        private static void Check(TableRuntime runtime, TableTestHost host)
        {
            host.Deliver();
            foreach (var client in host.Clients) TableClientTests.Same(client.Value, runtime, host.Now, client.Key, "deal skip");
        }

        [TestCase("build")]
        [TestCase("dice")]
        [TestCase("rolling")]
        [TestCase("indicator")]
        [TestCase("extra")]
        [TestCase("distribute")]
        [TestCase("center")]
        public void Dealer_skip_cancels_the_ceremony_but_preserves_the_authoritative_hand_and_fill_delay(string stage)
        {
            var (runtime, host) = Start(0, 1, 2, 3);
            while (stage != "build")
            {
                var deal = host.Clients[0].Deal;
                if (stage == "rolling" ? deal.Dice.Count > 0 && deal.Dice[0].DoneAt > host.Now : deal.Step == stage) break;
                Assert.That(host.StepTime(), Is.True); Check(runtime, host);
            }
            string hand = V4Tree.Canonical(runtime.Match.Hand.ToTree());
            int before = host.Log.Count; long now = host.Now;
            Assert.That(Skip(runtime, 3), Is.Null);
            Check(runtime, host);
            Assert.That(runtime.Dealing, Is.True, "A skip does not grant a playable turn before the server fill timer.");
            Assert.That(V4Tree.Canonical(runtime.Match.Hand.ToTree()), Is.EqualTo(hand));
            Assert.That(host.NextDue, Is.EqualTo(now + 1100), "All old deal/roll timers must be replaced by the source skipFill timer.");
            foreach (var pair in host.Clients)
            {
                var client = pair.Value;
                Assert.That(client.Deal.Step, Is.EqualTo("fill"));
                Assert.That(client.Deal.Dice.Select(die => die.Value), Is.EqualTo(new[] { 4, 3 }));
                Assert.That(client.MyTurn, Is.False);
                Assert.That(client.Rack == null, Is.EqualTo(pair.Key < 0), "Only a seated client receives its own rack.");
            }
            var mine = host.Log.Skip(before).Where(message => message.Seat == 0).ToList();
            Assert.That(mine.FindIndex(message => message.Type == "dealSkip"), Is.LessThan(mine.FindIndex(message => message.Type == V4Messages.HandDealt)));
            Assert.That(Skip(runtime, 3), Is.EqualTo(V4Errors.E_PHASE));
            Assert.That(host.StepTime(), Is.True); Check(runtime, host);
            Assert.That(host.Now, Is.EqualTo(now + 1100)); Assert.That(runtime.Dealing, Is.False);
        }

        [Test]
        public void Nondealer_votes_wait_for_all_humans_and_do_not_reveal_hidden_information()
        {
            var (runtime, host) = Start(0, 1); // The dealer at seat 3 is a bot.
            Assert.That(Skip(runtime, 0), Is.Null); Check(runtime, host);
            Assert.That(host.Clients[0].Deal.Step, Is.EqualTo("build"));
            Assert.That(host.Clients[0].Indicator, Is.Null); Assert.That(host.Clients[0].Rack, Is.Null);
            Assert.That(host.Log.Count(message => message.Type == "dealSkip"), Is.Zero);
            int votes = host.Log.Count(message => message.Type == "dealSkipVote");
            Assert.That(Skip(runtime, 0), Is.Null); Check(runtime, host);
            Assert.That(host.Log.Count(message => message.Type == "dealSkipVote"), Is.EqualTo(votes), "A seat's repeated request is one vote.");
            Assert.That(Skip(runtime, 1), Is.Null); Check(runtime, host);
            Assert.That(host.Clients[0].Deal.Step, Is.EqualTo("fill"));
        }

        [Test]
        public void Single_human_bot_table_skips_immediately_and_rejects_spectators_and_bot_seats()
        {
            var (runtime, host) = Start(0);
            Assert.That(Skip(runtime, -1), Is.EqualTo(V4Errors.E_SPECTATOR));
            Assert.That(Skip(runtime, 4), Is.EqualTo(V4Errors.E_NOT_SEATED));
            Assert.That(Skip(runtime, 3), Is.EqualTo(V4Errors.E_NOT_SEATED));
            Assert.That(Skip(runtime, 0), Is.Null); Check(runtime, host);
            Assert.That(host.Clients[0].Deal.Step, Is.EqualTo("fill"));
        }

        [Test]
        public void Skip_events_and_snapshots_round_trip_with_votes_and_fast_fill_state()
        {
            var (runtime, host) = Start(0, 1);
            Assert.That(Skip(runtime, 0), Is.Null); Check(runtime, host);
            var resumed = new TableClient(0); resumed.Load(runtime.GameView(0), true);
            Assert.That(V4Tree.Canonical(resumed.View(0)), Is.EqualTo(V4Tree.Canonical(runtime.GameView(0))));
            Assert.That(Skip(runtime, 1), Is.Null); Check(runtime, host);
            resumed.Load(runtime.GameView(0), true);
            Assert.That(V4Tree.Canonical(resumed.View(0)), Is.EqualTo(V4Tree.Canonical(runtime.GameView(0))));
            var skipped = host.Log.First(message => message.Seat == 0 && message.Type == "dealSkip");
            var frame = V4Wire.ToJson(skipped.Payload);
            Assert.That((int)frame["stack"], Is.EqualTo(runtime.Match.Hand.Deal.IndStack));
            Assert.That((string)frame["indicator"], Is.EqualTo(runtime.Match.Hand.Indicator));
            Assert.That(frame["rack"], Is.Null);
            var replay = new TableClient(0);
            foreach (var message in host.Log.Where(message => message.Seat == 0))
                replay.Apply(message.Type, V4Wire.Payload(message.Type, V4Wire.ToJson(message.Payload)));
            Assert.That(V4Tree.Canonical(replay.View(host.Now)), Is.EqualTo(V4Tree.Canonical(runtime.GameView(0))));
        }
    }
}

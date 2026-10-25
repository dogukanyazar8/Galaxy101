using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Okey101.Core.Model;
using Okey101.Core.Turns;
using Okey101.Core.V4;

namespace Okey101.Application.Tests
{
    /// <summary>K-02: 101-v4 / mulberry32-v4 records open with the v4 engine, 101-v1 / xorshift32-fy-v1 with the old one, anything else fails.</summary>
    public sealed class V4ReplayTests
    {
        private string root;
        [SetUp] public void Setup() { root = Path.Combine(Path.GetTempPath(), "okey-v4-replay-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); }
        [TearDown] public void Cleanup() { Directory.Delete(root, true); }

        /// <summary>A four-bot match with every accepted action recorded (a rejected bot action is replaced by the timeout, as G101Bots.Simulate does).</summary>
        private static (List<GameAction> actions, MatchState final) PlayBots(MatchOptions options, uint seed, DealRig rig = null)
        {
            var actions = new List<GameAction> { GameAction.StartHand(rig) };
            var s = G101Rules.Apply(G101Rules.NewMatch(options, seed), actions[0]).State;
            for (var guard = 0; guard < 20000 && s.Status != G101Rules.StatusMatchOver; guard++)
            {
                if (s.Status == G101Rules.StatusHandOver) { actions.Add(GameAction.NextHand()); s = G101Rules.Apply(s, actions[actions.Count - 1]).State; continue; }
                var seat = s.Hand.Turn.Seat;
                var a = G101Bots.NextAction(s, seat);
                var r = G101Rules.Apply(s, a);
                if (!r.Ok) { a = GameAction.Timeout(seat); r = G101Rules.Apply(s, a); }
                Assert.That(r.Ok, Is.True, r.Error);
                actions.Add(a); s = r.State;
            }
            return (actions, s);
        }

        private static string LegacyFile(ReplayCodec codec, string name)
        {
            var state = RoundFactory.Create(71, Guid.NewGuid(), 0);
            codec.Write(new ReplayRecord(state, Array.Empty<GameCommand>(), ReplayCodec.Hash(state)), name);
            return name;
        }

        [Test] public void AV4MatchRecordReplaysToTheSameState()
        {
            var options = new MatchOptions { Mode = "pairs", Hands = 3, Katlama = true };
            var (actions, final) = PlayBots(options, 99);
            var codec = new ReplayCodec(root);
            codec.WriteV4(V4ReplayRecord.Capture(options, 99, actions, final), "match.json");
            var json = File.ReadAllText(Path.Combine(root, "match.json"));
            StringAssert.Contains("\"RuleVersion\":\"101-v4\"", json);
            StringAssert.Contains("\"ShuffleVersion\":\"mulberry32-v4\"", json);
            var read = codec.ReadAny("match.json");
            Assert.That(read.Success, Is.True, read.Error.ToString());
            Assert.That(read.Value.Engine, Is.EqualTo(RecordEngine.V4));
            Assert.That(read.Value.V4.Actions.Count, Is.EqualTo(actions.Count));
            var restored = V4Replay.Restore(read.Value.V4);
            Assert.That(restored.Success, Is.True);
            Assert.That(V4Replay.Hash(restored.Value), Is.EqualTo(V4Replay.Hash(final)).And.Length.EqualTo(64));
            Assert.That(restored.Value.Totals, Is.EqualTo(final.Totals));
            Assert.That(restored.Value.Status, Is.EqualTo(G101Rules.StatusMatchOver));
            // The old engine cannot open a v4 record.
            Assert.That(codec.Read("match.json").Success, Is.False);
            Assert.That(new ScenarioLoader(root).Load("match.json").Success, Is.False);
        }

        [Test] public void TheSameSeedWritesTheSameRecord()
        {
            var options = new MatchOptions { Hands = 1 };
            var a = PlayBots(options, 42); var b = PlayBots(options, 42);
            var codec = new ReplayCodec(root);
            codec.WriteV4(V4ReplayRecord.Capture(options, 42, a.actions, a.final), "a.json");
            codec.WriteV4(V4ReplayRecord.Capture(options, 42, b.actions, b.final), "b.json");
            Assert.That(File.ReadAllBytes(Path.Combine(root, "a.json")), Is.EqualTo(File.ReadAllBytes(Path.Combine(root, "b.json"))));
        }

        [Test] public void ALegacyRecordStillOpensWithTheOldEngine()
        {
            var codec = new ReplayCodec(root);
            LegacyFile(codec, "old.json");
            var json = File.ReadAllText(Path.Combine(root, "old.json"));
            StringAssert.Contains("\"RuleVersion\":\"101-v1\"", json);
            StringAssert.Contains("\"ShuffleVersion\":\"xorshift32-fy-v1\"", json);
            var any = codec.ReadAny("old.json");
            Assert.That(any.Success, Is.True);
            Assert.That(any.Value.Engine, Is.EqualTo(RecordEngine.Legacy));
            Assert.That(ReplayCodec.Restore(any.Value.Legacy, CancellationToken.None).Success, Is.True);
            Assert.That(codec.Read("old.json").Success, Is.True);
            Assert.That(new ScenarioLoader(root).Load("old.json").Success, Is.True);
            Assert.That(new ScenarioLoader(root).LoadV4("old.json").Success, Is.False);
        }

        [TestCase("\"RuleVersion\":\"101-v4\"", "\"RuleVersion\":\"101-v5\"")]
        [TestCase("\"ShuffleVersion\":\"mulberry32-v4\"", "\"ShuffleVersion\":\"mulberry32-v5\"")]
        [TestCase("\"ShuffleVersion\":\"mulberry32-v4\"", "\"ShuffleVersion\":\"xorshift32-fy-v1\"")]
        [TestCase("\"RuleVersion\":\"101-v4\"", "\"RuleVersion\":\"101-v1\"")]
        [TestCase("\"SchemaVersion\":1", "\"SchemaVersion\":2")]
        public void UnknownOrMixedVersionsOfAV4RecordAreRejected(string from, string to)
        {
            var options = new MatchOptions { Hands = 1 };
            var (actions, final) = PlayBots(options, 7);
            var codec = new ReplayCodec(root);
            codec.WriteV4(V4ReplayRecord.Capture(options, 7, actions, final), "base.json");
            var json = File.ReadAllText(Path.Combine(root, "base.json"));
            StringAssert.Contains(from, json);
            File.WriteAllText(Path.Combine(root, "bad.json"), json.Replace(from, to));
            Assert.That(codec.ReadAny("bad.json").Success, Is.False);
            Assert.That(codec.Read("bad.json").Success, Is.False);
        }

        [TestCase("\"ShuffleVersion\":\"xorshift32-fy-v1\"", "\"ShuffleVersion\":\"mulberry32-v4\"")]
        [TestCase("\"RuleVersion\":\"101-v1\"", "\"RuleVersion\":\"101-v4\"")]
        [TestCase("\"RuleVersion\":\"101-v1\"", "\"RuleVersion\":\"101-v0\"")]
        public void UnknownOrMixedVersionsOfALegacyRecordAreRejected(string from, string to)
        {
            var codec = new ReplayCodec(root);
            LegacyFile(codec, "old.json");
            var json = File.ReadAllText(Path.Combine(root, "old.json"));
            StringAssert.Contains(from, json);
            File.WriteAllText(Path.Combine(root, "bad.json"), json.Replace(from, to));
            Assert.That(codec.ReadAny("bad.json").Success, Is.False);
            Assert.That(codec.Read("bad.json").Success, Is.False);
        }

        [Test] public void ATamperedV4RecordIsRejected()
        {
            var options = new MatchOptions { Hands = 1 };
            var (actions, final) = PlayBots(options, 7);
            var codec = new ReplayCodec(root);
            Assert.Throws<ArgumentException>(() => codec.WriteV4(new V4ReplayRecord(options, 7, actions, new string('0', 64)), "bad.json"));
            Assert.Throws<ArgumentException>(() => codec.WriteV4(V4ReplayRecord.Capture(options, 8, actions, final), "bad.json"));
            codec.WriteV4(V4ReplayRecord.Capture(options, 7, actions, final), "base.json");
            var json = File.ReadAllText(Path.Combine(root, "base.json"));
            var hash = V4Replay.Hash(final);
            File.WriteAllText(Path.Combine(root, "hash.json"), json.Replace(hash, new string('a', 64)));
            Assert.That(codec.ReadAny("hash.json").Success, Is.False);
            File.WriteAllText(Path.Combine(root, "kind.json"), json.Replace("{\"T\":\"startHand\"}", "{\"T\":\"System.Process\"}"));
            Assert.That(codec.ReadAny("kind.json").Success, Is.False);
            var firstDiscard = actions.First(a => a.T == "discard");
            File.WriteAllText(Path.Combine(root, "tile.json"), json.Replace("\"Tile\":\"" + firstDiscard.Tile + "\"", "\"Tile\":\"Q9a\""));
            Assert.That(codec.ReadAny("tile.json").Success, Is.False);
            File.WriteAllText(Path.Combine(root, "seed.json"), json.Replace("\"Seed\":7", "\"Seed\":8"));
            Assert.That(codec.ReadAny("seed.json").Success, Is.False);
            Assert.That(codec.ReadAny("base.json").Success, Is.True);
        }

        [Test] public void AV4ScenarioImportsAndLoadsAsItsRiggedHand()
        {
            // logic/scenarios/R43.json: rigFromHands fixes the first hand, the deal ceremony stays real.
            var deal = new ScenarioDeal
            {
                Dealer = 3, Dice = new[] { 4, 3 }, Indicator = "Y4a",
                Racks = new List<List<string>>
                {
                    new List<string> { "K9b", "R1a", "R3a", "R5b", "R7b", "R9a", "R11b", "B2a", "B5a", "B7b", "B11a", "B13b", "K4a", "K7b", "Y1a", "Y3b", "Y6a", "Y10b", "Y12b", "R10b", "B4b", "K2b" },
                    new List<string> { "R13a", "B13a", "Y13a", "R12a", "B12a", "Y12a", "K10a", "K11a", "R2a", "R4a", "R6a", "R8a", "B1a", "B3a", "B6a", "B9a", "K1a", "K3a", "K5a", "Y8a", "Y11a" }
                }
            };
            var options = new MatchOptions();
            var start = GameAction.StartHand(G101Rules.RigFromHands(deal));
            var state = G101Rules.Apply(G101Rules.NewMatch(options, 7), start).State;
            var codec = new ReplayCodec(root);
            codec.WriteV4(V4ReplayRecord.Capture(options, 7, new[] { start }, state), "r43.json");
            var loader = new ScenarioLoader(root);
            var imported = loader.Import(File.ReadAllText(Path.Combine(root, "r43.json")));
            Assert.That(imported.Success, Is.True);
            var loaded = loader.LoadV4(imported.Value);
            Assert.That(loaded.Success, Is.True);
            Assert.That(loaded.Value.Hand.Indicator, Is.EqualTo("Y4a"));
            Assert.That(loaded.Value.Hand.Racks[0], Is.EquivalentTo(deal.Racks[0]));
            Assert.That(loaded.Value.Hand.Racks[1], Is.EquivalentTo(deal.Racks[1]));
            Assert.That(G101Rules.Invariants(loaded.Value), Is.Empty);
            Assert.That(loader.Load(imported.Value).Success, Is.False, "the old engine does not open v4 scenarios");
        }

        [Test] public void ABadRigIsRejectedBeforeItReachesTheEngine()
        {
            var options = new MatchOptions();
            var start = GameAction.StartHand(G101Rules.RigFromHands(new ScenarioDeal { Dealer = 3, Dice = new[] { 4, 3 }, Indicator = "Y4a" }));
            var state = G101Rules.Apply(G101Rules.NewMatch(options, 7), start).State;
            var codec = new ReplayCodec(root);
            codec.WriteV4(V4ReplayRecord.Capture(options, 7, new[] { start }, state), "rig.json");
            var json = File.ReadAllText(Path.Combine(root, "rig.json"));
            File.WriteAllText(Path.Combine(root, "dice.json"), json.Replace("\"Dice\":[4,3]", "\"Dice\":[4,9]"));
            Assert.That(codec.ReadAny("dice.json").Success, Is.False);
            File.WriteAllText(Path.Combine(root, "order.json"), json.Replace("\"Order\":[\"", "\"Order\":[\"R1a\",\""));
            Assert.That(codec.ReadAny("order.json").Success, Is.False);
            Assert.That(codec.ReadAny("rig.json").Success, Is.True);
        }

        [Test] public void RecordVersionsClassifyOnlyTheTwoKnownPairs()
        {
            Assert.That(RecordVersions.Classify(1, "101-v1", "xorshift32-fy-v1"), Is.EqualTo(RecordEngine.Legacy));
            Assert.That(RecordVersions.Classify(1, "101-v4", "mulberry32-v4"), Is.EqualTo(RecordEngine.V4));
            Assert.That(RecordVersions.Classify(1, "101-v4", "xorshift32-fy-v1"), Is.EqualTo(RecordEngine.Unknown));
            Assert.That(RecordVersions.Classify(1, "101-v1", "mulberry32-v4"), Is.EqualTo(RecordEngine.Unknown));
            Assert.That(RecordVersions.Classify(2, "101-v4", "mulberry32-v4"), Is.EqualTo(RecordEngine.Unknown));
            Assert.That(RecordVersions.Classify(1, null, null), Is.EqualTo(RecordEngine.Unknown));
        }
    }
}

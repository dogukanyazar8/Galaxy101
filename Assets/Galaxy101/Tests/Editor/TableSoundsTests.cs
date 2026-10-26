using System.Collections.Generic;
using System.Linq;
using Galaxy101.Audio;
using NUnit.Framework;
using Okey101.Core.V4;

namespace Galaxy101.Tests
{
    /// <summary>The table's engine/protocol event → sound map (pkg/sound/README.md, events.json engineEvents, G101-Masa sfx()).</summary>
    public sealed class TableSoundsTests
    {
        private const int Me = 0;
        private static SoundCatalog Catalog => SoundCatalog.Load();
        private static TableSounds Map => new TableSounds(Catalog);

        private static string Id(SoundCue? cue) => cue?.Id;

        [Test]
        public void EngineEventsMapAsTheReadmeSays()
        {
            var map = Map;
            Assert.That(Id(map.ForEngine("drew", Me, Me)), Is.EqualTo("tile.draw"));
            Assert.That(map.ForEngine("drew", Me, Me).Value.Volume, Is.EqualTo(1f));
            Assert.That(map.ForEngine("drew", 2, Me).Value.Volume, Is.EqualTo(Catalog.OpponentDrawVolume), "an opponent's draw plays at 70 %");
            Assert.That(map.ForEngine("drew", 2, Me).Value.Seat, Is.EqualTo(2));
            Assert.That(Id(map.ForEngine("discarded", 3, Me)), Is.EqualTo("tile.discard"));
            Assert.That(Id(map.ForEngine("opened", 1, Me)), Is.EqualTo("meld.lay"));
            Assert.That(Id(map.ForEngine("laid", 1, Me)), Is.EqualTo("meld.lay"));
            Assert.That(Id(map.ForEngine("added", 1, Me)), Is.EqualTo("meld.add"));
            Assert.That(Id(map.ForEngine("okeySwapped", 1, Me)), Is.EqualTo("okey.swap"));
            Assert.That(Id(map.ForEngine("penalty", 1, Me)), Is.EqualTo("penalty"));
            Assert.That(Id(map.ForEngine("finished", 1, Me)), Is.EqualTo("finish"));
            Assert.That(Id(map.ForEngine("finished", 1, Me, nuke: true)), Is.EqualTo("finish.nuke"));
            Assert.That(Id(map.ForEngine("handEnd", null, Me)), Is.EqualTo("hand.end"));
            Assert.That(Id(map.ForEngine("turn", Me, Me)), Is.EqualTo("turn.mine"));
            Assert.That(map.ForEngine("turn", Me, Me, dealing: true), Is.Null, "turn.mine waits for the deal to end");
            Assert.That(map.ForEngine("turn", 1, Me), Is.Null);
            foreach (var silent in new[] { "handStart", "openShort", "undone", "sideReturned", "auto", "draw", "seatLeft", "matchEnd", null })
                Assert.That(map.ForEngine(silent, 1, Me), Is.Null, silent);
        }

        [Test]
        public void ProtocolAndTableEventsMapToTheirSounds()
        {
            var map = Map;
            Assert.That(Id(map.DealStart()), Is.EqualTo("deal.shuffle"));
            Assert.That(Id(map.DealDie()), Is.EqualTo("dice.roll"));
            Assert.That(Id(map.DealStep("indicator")), Is.EqualTo("deal.indicator"));
            Assert.That(Id(map.DealStep("distribute")), Is.EqualTo("deal.distribute"));
            foreach (var step in new[] { "dice", "extra", "center", null }) Assert.That(map.DealStep(step), Is.Null, step);
            Assert.That(Id(map.GiftFlight("tea", 2)), Is.EqualTo("gift.send"));
            Assert.That(Id(map.GiftFlight("rocket", 2)), Is.EqualTo("gift.rocket"));
            Assert.That(Id(map.EmojiFlight(Me)), Is.EqualTo("gift.send"));
            Assert.That(Id(map.GiftReceived(1)), Is.EqualTo("gift.receive"));
            Assert.That(Id(map.Chat(1, Me)), Is.EqualTo("chat.message"));
            Assert.That(map.Chat(Me, Me), Is.Null, "my own line is silent");
            Assert.That(Id(map.Chat(-1, Me)), Is.EqualTo("chat.message"), "system lines pop like the prototype's");
            Assert.That(Id(map.Countdown(3)), Is.EqualTo("count.tick"));
            Assert.That(Id(map.Countdown(1)), Is.EqualTo("count.tick"));
            Assert.That(Id(map.Countdown(null)), Is.EqualTo("count.go"));
            Assert.That(Id(map.Countdown(0)), Is.EqualTo("count.go"));
            Assert.That(Id(map.TurnWarning()), Is.EqualTo("turn.warn"));
            Assert.That(Id(map.RackMove()), Is.EqualTo("tile.slide"));
            Assert.That(Id(map.Panel(true)), Is.EqualTo("ui.open"));
            Assert.That(Id(map.Panel(false)), Is.EqualTo("ui.close"));
            foreach (var ui in new[] { "toast", "shake", "inline" }) Assert.That(Id(map.ErrorUi(ui)), Is.EqualTo("ui.error"), ui);
            foreach (var ui in new[] { "silent", "dialog", "screen", "resync", null }) Assert.That(map.ErrorUi(ui), Is.Null, ui);
        }

        [Test]
        public void EveryCatalogueEngineEventHasASoundAndStaysInsideItsCandidates()
        {
            var map = Map;
            var produced = new Dictionary<string, HashSet<string>>();
            void Add(string name, SoundCue? cue) { if (cue.HasValue) (produced.TryGetValue(name, out var s) ? s : produced[name] = new HashSet<string>()).Add(cue.Value.Id); }
            foreach (var name in Catalog.EngineEvents.Keys)
            {
                Add(name, map.ForEngine(name, Me, Me));
                Add(name, map.ForEngine(name, 1, Me));
                Add(name, map.ForEngine(name, Me, Me, nuke: true));
            }
            Add("dealStart", map.DealStart());
            Add("dealDie", map.DealDie());
            Add("dealStep", map.DealStep("indicator")); Add("dealStep", map.DealStep("distribute"));
            Add("gift", map.GiftFlight("tea", 1)); Add("gift", map.GiftFlight("rocket", 1)); Add("gift", map.GiftReceived(1));
            Add("chat", map.Chat(1, Me));
            foreach (var pair in Catalog.EngineEvents)
            {
                Assert.That(produced.ContainsKey(pair.Key), pair.Key + " has no sound");
                Assert.That(produced[pair.Key], Is.EquivalentTo(pair.Value), pair.Key);
            }
        }

        [Test]
        public void ABatchFinishIsANukeWhenTheResultIsEldenOrOkey()
        {
            var map = Map;
            List<GameEvent> Batch(bool elden, bool okey) => new List<GameEvent>
            {
                new GameEvent("finished").With("seat", 2).With("tile", "R5a"),
                new GameEvent("handEnd").With("handNo", 1).With("result", new Dictionary<string, object> { { "reason", "finish" }, { "elden", elden }, { "okeyFinish", okey } }),
            };
            Assert.That(map.ForEngine(Batch(false, false), Me).Select(c => c.Id), Is.EqualTo(new[] { "finish", "hand.end" }));
            Assert.That(map.ForEngine(Batch(true, false), Me).Select(c => c.Id), Is.EqualTo(new[] { "finish.nuke", "hand.end" }));
            Assert.That(map.ForEngine(Batch(false, true), Me).Select(c => c.Id), Is.EqualTo(new[] { "finish.nuke", "hand.end" }));
            Assert.That(map.ForEngine(Batch(false, false), Me).First().Seat, Is.EqualTo(2));
            Assert.That(TableSounds.IsNuke(null), Is.False);
            Assert.That(map.ForEngine((IReadOnlyList<GameEvent>)null, Me), Is.Empty);
        }

        [Test]
        public void TheRulesEngineEventsOfARealTurnMapToSounds()
        {
            var map = Map;
            var start = G101Rules.Apply(G101Rules.NewMatch(new MatchOptions(), 42), GameAction.StartHand());
            Assert.That(start.Ok, Is.True);
            int seat = start.State.Hand.Turn.Seat, other = (seat + 1) % 4;
            Assert.That(map.ForEngine(start.Events, seat).Select(c => c.Id), Is.EqualTo(new[] { "turn.mine" }));
            Assert.That(map.ForEngine(start.Events, seat, dealing: true), Is.Empty);
            Assert.That(map.ForEngine(start.Events, other), Is.Empty);
            var state = start.State;
            if (state.Hand.Turn.Phase == "draw")
            {
                var drew = G101Rules.Apply(state, GameAction.Draw(seat, "deck"));
                Assert.That(drew.Ok, Is.True, drew.Error);
                var mine = map.ForEngine(drew.Events, seat).Single(c => c.Id == "tile.draw");
                Assert.That(mine.Volume, Is.EqualTo(1f));
                Assert.That(map.ForEngine(drew.Events, other).Single(c => c.Id == "tile.draw").Volume, Is.EqualTo(Catalog.OpponentDrawVolume));
                state = drew.State;
            }
            var tile = state.Hand.Racks[seat].Last();
            var discarded = G101Rules.Apply(state, GameAction.Discard(seat, tile));
            Assert.That(discarded.Ok, Is.True, discarded.Error);
            int next = discarded.State.Hand.Turn.Seat;
            var cues = map.ForEngine(discarded.Events, next).Select(c => c.Id).ToList();
            Assert.That(cues, Does.Contain("tile.discard"));
            Assert.That(cues, Does.Contain("turn.mine"), "the next seat hears its turn");
            Assert.That(map.ForEngine(discarded.Events, seat).Select(c => c.Id), Does.Not.Contain("turn.mine"));
        }
    }
}

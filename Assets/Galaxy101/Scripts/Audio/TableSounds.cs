using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Okey101.Core.V4;

namespace Galaxy101.Audio
{
    /// <summary>A sound to play: the event id, a volume share (the prototype's {vol}) and the seat it belongs to.</summary>
    public readonly struct SoundCue
    {
        public readonly string Id;
        public readonly float Volume;
        public readonly int? Seat;
        public SoundCue(string id, float volume = 1, int? seat = null) { Id = id; Volume = volume; Seat = seat; }
        public override string ToString() => Id + (Volume != 1 ? " ×" + Volume.ToString("0.##") : "") + (Seat.HasValue ? " @" + Seat.Value : "");
    }

    /// <summary>
    /// The table's event → sound map (pkg/sound/README.md, events.json engineEvents, G101-Masa sfx()). The table calls
    /// these at the moment its presentation shows the event and plays the result with <see cref="GalaxyAudio.Play(SoundCue)"/>.
    /// Rules engine events (Okey101.Core.V4 GameEvent, and the protocol's <c>game {ev}</c> which carries the same names):
    /// drew → tile.draw (an opponent's at volume.opponentDrawVolume) · discarded → tile.discard · opened/laid → meld.lay ·
    /// added → meld.add · okeySwapped → okey.swap · penalty → penalty · finished → finish, or finish.nuke for an okey /
    /// elden finish · handEnd → hand.end · turn (my seat, after the deal) → turn.mine. Protocol / table events:
    /// dealStart → deal.shuffle · dealDie → dice.roll · dealStep indicator/distribute → deal.indicator/deal.distribute ·
    /// gift → gift.send (rocket: gift.rocket), received → gift.receive · chat from someone else → chat.message ·
    /// countdown → count.tick/count.go · turn timer warning → turn.warn · rack rearranged → tile.slide · a table panel
    /// opening/closing → ui.open/ui.close.
    /// </summary>
    public sealed class TableSounds
    {
        public const string Rocket = "rocket";

        private readonly SoundCatalog catalog;
        private static TableSounds shared;

        public TableSounds(SoundCatalog catalog) { this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); }

        /// <summary>The map over the Resources catalogue.</summary>
        public static TableSounds Default => shared ?? (shared = new TableSounds(SoundCatalog.Load()));

        /// <summary>
        /// One apply()/protocol batch of engine events. A finish is a nuke when the hand result in the same batch is an
        /// elden or okey finish (R-53, R-54; protocol/timings.md "okeyle bitiş nuke"). While <paramref name="dealing"/>,
        /// turn.mine waits: call <see cref="Turn"/> again once the deal ceremony ends.
        /// </summary>
        public IReadOnlyList<SoundCue> ForEngine(IReadOnlyList<GameEvent> events, int mySeat, bool dealing = false)
        {
            var cues = new List<SoundCue>();
            if (events == null) return cues;
            bool nuke = false;
            foreach (var e in events)
                if (e.E == "handEnd" && IsNuke(e["result"])) nuke = true;
            foreach (var e in events)
            {
                var cue = ForEngine(e.E, e.IntOrNull("seat"), mySeat, nuke, dealing);
                if (cue.HasValue) cues.Add(cue.Value);
            }
            return cues;
        }

        /// <summary>One engine event by name (rules.js ev.e). Events without a sound (openShort, undone, auto…) give null.</summary>
        public SoundCue? ForEngine(string name, int? seat, int mySeat, bool nuke = false, bool dealing = false)
        {
            if (name == null || !catalog.EngineEvents.TryGetValue(name, out var sounds)) return null;
            switch (name)
            {
                case "drew":
                    return Cue(sounds, "tile.draw", seat.HasValue && seat.Value != mySeat ? catalog.OpponentDrawVolume : 1, seat);
                case "finished":
                    return Cue(sounds, nuke ? "finish.nuke" : "finish", 1, seat);
                case "turn":
                    return seat.HasValue ? Turn(seat.Value, mySeat, dealing) : null;
                case "chat":
                    return seat.HasValue ? Chat(seat.Value, mySeat) : null;
                case "dealStep":
                case "gift":
                    return null; // need their payload: DealStep(step), GiftFlight(gift, from)
                default:
                    return sounds.Length == 1 ? new SoundCue(sounds[0], 1, seat) : (SoundCue?)null;
            }
        }

        /// <summary>handEnd result (Dictionary from the engine, JObject from the protocol): an elden or okey finish.</summary>
        public static bool IsNuke(object result)
        {
            switch (result)
            {
                case IDictionary<string, object> tree:
                    return tree.TryGetValue("elden", out var elden) && elden is bool e && e || tree.TryGetValue("okeyFinish", out var okey) && okey is bool o && o;
                case JObject json:
                    return (bool?)json["elden"] == true || (bool?)json["okeyFinish"] == true;
                case IDictionary map:
                    return map["elden"] is bool me && me || map["okeyFinish"] is bool mo && mo;
                default:
                    return false;
            }
        }

        /// <summary>turn: turn.mine when the turn is mine and no deal ceremony is running (prototype: !deal && active becomes 0).</summary>
        public SoundCue? Turn(int seat, int mySeat, bool dealing) => seat == mySeat && !dealing ? Cue("turn", "turn.mine", 1, seat) : null;

        /// <summary>dealStart: the stacks are being built.</summary>
        public SoundCue? DealStart() => Cue("dealStart", "deal.shuffle");

        /// <summary>dealDie: a die is rolling (dice.roll lasts mixing.sync 1450 ms with the 3D die).</summary>
        public SoundCue? DealDie() => Cue("dealDie", "dice.roll");

        /// <summary>dealStep: indicator → deal.indicator, distribute → deal.distribute; dice/extra/center have no sound.</summary>
        public SoundCue? DealStep(string step)
        {
            switch (step)
            {
                case "indicator": return Cue("dealStep", "deal.indicator");
                case "distribute": return Cue("dealStep", "deal.distribute");
                default: return null;
            }
        }

        /// <summary>gift: a treat's flight starts (gift.send; the rocket plays gift.rocket for its 5.6 s flight).</summary>
        public SoundCue? GiftFlight(string gift, int from) => Cue("gift", gift == Rocket ? "gift.rocket" : "gift.send", 1, from);

        /// <summary>An emoji flies across the table (prototype fly(): a new flight → gift.send).</summary>
        public SoundCue? EmojiFlight(int from) => Cue("gift", "gift.send", 1, from);

        /// <summary>The treat card for a gift to me appears (only when treat alerts are on, like the prototype's recv).</summary>
        public SoundCue? GiftReceived(int from) => Cue("gift", "gift.receive", 1, from);

        /// <summary>chat: a line from anyone but me (the prototype also pops for system lines, seat −1).</summary>
        public SoundCue? Chat(int seat, int mySeat) => seat != mySeat ? Cue("chat", "chat.message", 1, seat) : (SoundCue?)null;

        /// <summary>The start countdown: a number (3, 2, 1) ticks; the end (null or 0) is count.go.</summary>
        public SoundCue? Countdown(int? number) => Event(number.HasValue && number.Value > 0 ? "count.tick" : "count.go");

        /// <summary>The turn timer entered the warning threshold (setting warn).</summary>
        public SoundCue? TurnWarning() => Event("turn.warn");

        /// <summary>A tile moved on my rack without leaving it.</summary>
        public SoundCue? RackMove() => Event("tile.slide");

        /// <summary>A table panel (gifts, chat, settings, leave confirmation) opened or closed.</summary>
        public SoundCue? Panel(bool opened) => Event(opened ? "ui.open" : "ui.close");

        /// <summary>
        /// A protocol error's ui kind (protocol.md "Hata kodları": silent, inline, toast, dialog, shake, screen, resync):
        /// toast, shake and inline play ui.error (events.json ui.error trigger); the others are silent.
        /// </summary>
        public SoundCue? ErrorUi(string ui) => ui == "toast" || ui == "shake" || ui == "inline" ? Event("ui.error") : null;

        private SoundCue? Event(string id) => catalog.Events.ContainsKey(id) ? new SoundCue(id) : (SoundCue?)null;

        private SoundCue? Cue(string engineEvent, string id, float volume = 1, int? seat = null) =>
            catalog.EngineEvents.TryGetValue(engineEvent, out var sounds) ? Cue(sounds, id, volume, seat) : null;

        private static SoundCue? Cue(string[] sounds, string id, float volume, int? seat) =>
            Array.IndexOf(sounds, id) >= 0 ? new SoundCue(id, volume, seat) : (SoundCue?)null;
    }
}

using System;
using System.Collections.Generic;

namespace Okey101.Core.V4
{
    /// <summary>
    /// An action for <see cref="G101Rules.Apply"/>, the rules.js action object: <c>{t, seat, src, melds, tile, meld, side, index, rig}</c>.
    /// t: startHand | nextHand | draw | returnSide | open | lay | add | swap | undo | discard | timeout | leave | takeSeat.
    /// </summary>
    public sealed class GameAction
    {
        public string T;
        /// <summary>-1 when absent (rules.js: undefined never equals the turn seat).</summary>
        public int Seat = -1;
        /// <summary>'deck' | 'side' (draw).</summary>
        public string Src;
        public List<MeldSpec> Melds;
        public string Tile;
        /// <summary>Table meld id (add, swap).</summary>
        public string Meld;
        /// <summary>'pre' | 'post' (add).</summary>
        public string Side;
        /// <summary>Position of the okey in the meld (swap); -1 when absent.</summary>
        public int Index = -1;
        /// <summary>startHand only, tests and scenarios: a fixed deal.</summary>
        public DealRig Rig;

        public static GameAction StartHand(DealRig rig = null) => new GameAction { T = "startHand", Rig = rig };
        public static GameAction NextHand() => new GameAction { T = "nextHand" };
        public static GameAction Draw(int seat, string src) => new GameAction { T = "draw", Seat = seat, Src = src };
        public static GameAction ReturnSide(int seat) => new GameAction { T = "returnSide", Seat = seat };
        public static GameAction Open(int seat, List<MeldSpec> melds) => new GameAction { T = "open", Seat = seat, Melds = melds };
        public static GameAction Lay(int seat, List<MeldSpec> melds) => new GameAction { T = "lay", Seat = seat, Melds = melds };
        public static GameAction Add(int seat, string tile, string meld, string side) => new GameAction { T = "add", Seat = seat, Tile = tile, Meld = meld, Side = side };
        public static GameAction Swap(int seat, string tile, string meld, int index) => new GameAction { T = "swap", Seat = seat, Tile = tile, Meld = meld, Index = index };
        public static GameAction Undo(int seat) => new GameAction { T = "undo", Seat = seat };
        public static GameAction Discard(int seat, string tile) => new GameAction { T = "discard", Seat = seat, Tile = tile };
        public static GameAction Timeout(int seat) => new GameAction { T = "timeout", Seat = seat };
        public static GameAction Leave(int seat) => new GameAction { T = "leave", Seat = seat };
        public static GameAction TakeSeat(int seat) => new GameAction { T = "takeSeat", Seat = seat };

        public Dictionary<string, object> ToTree()
        {
            var d = new Dictionary<string, object> { { "t", T } };
            if (Seat >= 0) d["seat"] = Seat;
            if (Src != null) d["src"] = Src;
            if (Melds != null) { var l = new List<object>(); foreach (var m in Melds) l.Add(m.ToTree()); d["melds"] = l; }
            if (Tile != null) d["tile"] = Tile;
            if (Meld != null) d["meld"] = Meld;
            if (Side != null) d["side"] = Side;
            if (Index >= 0) d["index"] = Index;
            return d;
        }
    }

    /// <summary>A meld the player proposes: <c>{kind, tiles}</c>, kind 'run' | 'group' | 'pair' (R-16: the client says the kind).</summary>
    public sealed class MeldSpec
    {
        public string Kind;
        public List<string> Tiles;
        public MeldSpec() { }
        public MeldSpec(string kind, List<string> tiles) { Kind = kind; Tiles = tiles; }
        public Dictionary<string, object> ToTree() => new Dictionary<string, object> { { "kind", Kind }, { "tiles", V4Tree.Strings(Tiles) } };
    }

    /// <summary>A fixed deal for startHand: <c>{dealer, dice, order}</c>; each part optional (absent parts come from the RNG).</summary>
    public sealed class DealRig
    {
        public int? Dealer;
        public int[] Dice;
        public List<string> Order;
    }

    /// <summary>rigFromHands input: <c>{dealer, dice, indicator, racks, deckTop}</c>; short racks are filled from the remaining tiles.</summary>
    public sealed class ScenarioDeal
    {
        public int? Dealer;
        public int[] Dice;
        public string Indicator;
        public List<List<string>> Racks;
        public List<string> DeckTop;
    }

    /// <summary>A table meld in a fromSpec hand: <c>{owner, kind, tiles}</c>.</summary>
    public sealed class SpecMeld
    {
        public int Owner;
        public string Kind;
        public List<string> Tiles;
    }

    /// <summary>fromSpec input (tests): a hand in progress. Absent values follow rules.js defaults (`||` semantics).</summary>
    public sealed class HandSpec
    {
        public MatchOptions Opts;
        public uint? Seed;
        public int? HandNo;
        public int? Dealer;
        public int[] Totals;
        public SeatInfo[] Seats;
        public string Indicator;
        public List<List<string>> Racks;
        public List<List<string>> Discards;
        public List<SpecMeld> Melds;
        public List<string> Deck;
        public int? DeckCount;
        public int[] Opened;
        public string[] OpenMode;
        public int[] Pen;
        public int? TurnSeat;
        public string TurnPhase;
        public int? TurnN;
    }

    /// <summary>fromSpec / rigFromHands rejection; Message is "CODE detail" as in rules.js (E_SPEC_TILE, E_SPEC_DUP, E_SPEC_MELD, E_SPEC_IND, E_SPEC_RACK, E_SPEC_COUNT).</summary>
    public sealed class SpecException : Exception
    {
        public readonly string Code;
        public SpecException(string message) : base(message)
        {
            var space = message.IndexOf(' ');
            Code = space < 0 ? message : message.Substring(0, space);
        }
    }

    /// <summary>An engine event, the rules.js event object: name <c>e</c> plus fields in insertion order, values in tree form.</summary>
    public sealed class GameEvent
    {
        public readonly string E;
        private readonly List<KeyValuePair<string, object>> fields = new List<KeyValuePair<string, object>>();
        public GameEvent(string e) { E = e; }
        public GameEvent With(string key, object value) { fields.Add(new KeyValuePair<string, object>(key, value)); return this; }
        public IReadOnlyList<KeyValuePair<string, object>> Fields => fields;
        public bool Has(string key) { foreach (var f in fields) if (f.Key == key) return true; return false; }
        public object this[string key] { get { foreach (var f in fields) if (f.Key == key) return f.Value; return null; } }
        public int Int(string key) => Convert.ToInt32(this[key]);
        public int? IntOrNull(string key) { var v = this[key]; return v == null ? (int?)null : Convert.ToInt32(v); }
        public string Str(string key) => (string)this[key];
        public bool Bool(string key) => this[key] is bool b && b;
        public Dictionary<string, object> ToTree()
        {
            var d = new Dictionary<string, object> { { "e", E } };
            foreach (var f in fields) d[f.Key] = f.Value;
            return d;
        }
        public override string ToString() => E;
    }

    /// <summary>apply() result: <c>{ok, error, detail, state, events}</c>. On error State is the unchanged input and Events is empty.</summary>
    public sealed class ApplyResult
    {
        public bool Ok;
        public string Error;
        /// <summary>The error object (<c>{error, tile?, index?, need?}</c>) on failure, null on success.</summary>
        public Dictionary<string, object> Detail;
        public MatchState State;
        public List<GameEvent> Events = new List<GameEvent>();
    }

    /// <summary>rules.js legal(): which controls the seat may use now.</summary>
    public sealed class LegalMoves
    {
        public bool Draw, Side, Discard, Open, Undo, ReturnSide, Add;
        public Dictionary<string, object> ToTree() => new Dictionary<string, object>
        {
            { "draw", Draw }, { "side", Side }, { "discard", Discard }, { "open", Open }, { "undo", Undo }, { "returnSide", ReturnSide }, { "add", Add }
        };
    }

    /// <summary>analyzeMeld() result. Bad melds are <c>{ok:false}</c>; run adds c/start/end, group adds n.</summary>
    public sealed class MeldAnalysis
    {
        public static readonly MeldAnalysis Bad = new MeldAnalysis();
        public bool Ok;
        public string Kind;
        public int Value;
        public string C;
        public int Start;
        public int End;
        public int N;
        public List<ColorNum> Repr;
        public Dictionary<string, object> ToTree()
        {
            var d = new Dictionary<string, object> { { "ok", Ok } };
            if (!Ok) return d;
            d["kind"] = Kind;
            d["value"] = Value;
            if (Kind == "run") { d["c"] = C; d["start"] = Start; d["end"] = End; }
            if (Kind == "group") d["n"] = N;
            var repr = new List<object>();
            foreach (var r in Repr) repr.Add(r.ToTree());
            d["repr"] = repr;
            return d;
        }
    }
}

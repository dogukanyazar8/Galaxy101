using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Okey101.Core.V4
{
    // State shapes of logic/rules.js (handoff v4), field for field. ToTree() projects each object to the plain tree
    // rules.js would hold (Dictionary / List / string / int / long / bool / null) under the same property names, so
    // vector paths such as "hand.racks.0" or "seats.1.left" resolve without per-vector code and the server can send the
    // same shapes on the wire. Clone() is a deep copy, the JSON round trip of rules.js clone().

    /// <summary>mulberry32 state, <c>rng: {s}</c> in rules.js.</summary>
    public sealed class RngState
    {
        public uint S;
        public RngState(uint s) { S = s; }
        public RngState Clone() => new RngState(S);
        public Dictionary<string, object> ToTree() => new Dictionary<string, object> { { "s", (long)S } };
    }

    /// <summary>A colour and number, <c>{c, n}</c>: the okey and the face an okey stands for in a meld (repr).</summary>
    public sealed class ColorNum
    {
        public readonly string C;
        public readonly int N;
        public ColorNum(string c, int n) { C = c; N = n; }
        public Dictionary<string, object> ToTree() => new Dictionary<string, object> { { "c", C }, { "n", N } };
        public override string ToString() => C + N;
    }

    /// <summary>Normalised match options (rules.js newMatch): mode 'solo' | 'pairs', hands, penalties, isle, katlama, katlamaPartner.</summary>
    public sealed class MatchOptions
    {
        public string Mode = "solo";
        public int Hands = 5;
        public bool Penalties = true;
        public bool Isle = true;
        public bool Katlama;
        public bool KatlamaPartner = true;
        public bool IsPairs => Mode == "pairs";
        public MatchOptions Clone() => new MatchOptions { Mode = Mode, Hands = Hands, Penalties = Penalties, Isle = Isle, Katlama = Katlama, KatlamaPartner = KatlamaPartner };
        public Dictionary<string, object> ToTree() => new Dictionary<string, object>
        {
            { "mode", Mode }, { "hands", Hands }, { "penalties", Penalties }, { "isle", Isle }, { "katlama", Katlama }, { "katlamaPartner", KatlamaPartner }
        };
    }

    public sealed class SeatInfo
    {
        public bool Bot;
        public bool Left;
        public int LeftSeq;
        public SeatInfo Clone() => new SeatInfo { Bot = Bot, Left = Left, LeftSeq = LeftSeq };
        public Dictionary<string, object> ToTree() => new Dictionary<string, object> { { "bot", Bot }, { "left", Left }, { "leftSeq", LeftSeq } };
    }

    /// <summary>A meld on the table: <c>{id, owner, kind, tiles, placedBy}</c>.</summary>
    public sealed class TableMeld
    {
        public string Id;
        public int Owner;
        public string Kind;
        public List<string> Tiles = new List<string>();
        public List<int> PlacedBy = new List<int>();
        public TableMeld Clone() => new TableMeld { Id = Id, Owner = Owner, Kind = Kind, Tiles = new List<string>(Tiles), PlacedBy = new List<int>(PlacedBy) };
        public Dictionary<string, object> ToTree() => new Dictionary<string, object>
        {
            { "id", Id }, { "owner", Owner }, { "kind", Kind }, { "tiles", V4Tree.Strings(Tiles) }, { "placedBy", V4Tree.Ints(PlacedBy) }
        };
    }

    /// <summary>A penalty, pending or written: <c>{seat, code, amount, by, tile, value?}</c>.</summary>
    public sealed class PenaltyEntry
    {
        public int Seat;
        public string Code;
        public int Amount;
        public int By;
        public string Tile;
        public int? Value;
        public PenaltyEntry Clone() => new PenaltyEntry { Seat = Seat, Code = Code, Amount = Amount, By = By, Tile = Tile, Value = Value };
        public Dictionary<string, object> ToTree()
        {
            var d = new Dictionary<string, object> { { "seat", Seat }, { "code", Code }, { "amount", Amount }, { "by", By }, { "tile", Tile } };
            if (Value.HasValue) d["value"] = Value.Value;
            return d;
        }
    }

    /// <summary>R-34 undo snapshot taken before the first changing action of a turn.</summary>
    public sealed class TurnSnapshot
    {
        public List<string> Rack;
        public List<TableMeld> Melds;
        public int MeldSeq;
        public int Opened;
        public string OpenMode;
        public int OpenedTurn;
        public List<PenaltyEntry> Pending;
        public bool SideMust;
        public TurnSnapshot Clone() => new TurnSnapshot
        {
            Rack = new List<string>(Rack), Melds = V4Tree.CloneMelds(Melds), MeldSeq = MeldSeq, Opened = Opened, OpenMode = OpenMode,
            OpenedTurn = OpenedTurn, Pending = V4Tree.ClonePenalties(Pending), SideMust = SideMust
        };
        public Dictionary<string, object> ToTree() => new Dictionary<string, object>
        {
            { "rack", V4Tree.Strings(Rack) }, { "melds", V4Tree.Melds(Melds) }, { "meldSeq", MeldSeq }, { "opened", Opened }, { "openMode", OpenMode },
            { "openedTurn", OpenedTurn }, { "pending", V4Tree.Penalties(Pending) }, { "sideMust", SideMust }
        };
    }

    /// <summary>The turn in progress: <c>{seat, phase, n, sideTake, sideMust, sideReturned, snap, pending, drawn}</c>; phase 'draw' | 'play' | 'over'.</summary>
    public sealed class TurnState
    {
        public int Seat;
        public string Phase;
        public int N;
        public string SideTake;
        public bool SideMust;
        public bool SideReturned;
        public TurnSnapshot Snap;
        public List<PenaltyEntry> Pending = new List<PenaltyEntry>();
        public string Drawn;
        public TurnState Clone() => new TurnState
        {
            Seat = Seat, Phase = Phase, N = N, SideTake = SideTake, SideMust = SideMust, SideReturned = SideReturned,
            Snap = Snap?.Clone(), Pending = V4Tree.ClonePenalties(Pending), Drawn = Drawn
        };
        public Dictionary<string, object> ToTree() => new Dictionary<string, object>
        {
            { "seat", Seat }, { "phase", Phase }, { "n", N }, { "sideTake", SideTake }, { "sideMust", SideMust }, { "sideReturned", SideReturned },
            { "snap", Snap?.ToTree() }, { "pending", V4Tree.Penalties(Pending) }, { "drawn", Drawn }
        };
    }

    public sealed class DealPlanStep
    {
        public int Seat;
        public List<int> Stacks = new List<int>();
        public DealPlanStep Clone() => new DealPlanStep { Seat = Seat, Stacks = new List<int>(Stacks) };
        public Dictionary<string, object> ToTree() => new Dictionary<string, object> { { "seat", Seat }, { "stacks", V4Tree.Ints(Stacks) } };
    }

    /// <summary>The deal ceremony as dealt (R-02…R-05): what the client animates.</summary>
    public sealed class DealRecord
    {
        public int Dealer;
        public List<string> Order;
        public List<List<string>> Stacks;
        public string Extra;
        public int[] Dice;
        public int IndStack;
        public int IndLevel;
        public int RolledLevel;
        public bool FakeSkip;
        public int ExtraStack;
        public List<DealPlanStep> Plan;
        public int[] CenterStacks;
        public DealRecord Clone()
        {
            var plan = new List<DealPlanStep>(Plan.Count);
            foreach (var p in Plan) plan.Add(p.Clone());
            return new DealRecord
            {
                Dealer = Dealer, Order = new List<string>(Order), Stacks = V4Tree.CloneLists(Stacks), Extra = Extra, Dice = (int[])Dice.Clone(),
                IndStack = IndStack, IndLevel = IndLevel, RolledLevel = RolledLevel, FakeSkip = FakeSkip, ExtraStack = ExtraStack, Plan = plan,
                CenterStacks = (int[])CenterStacks.Clone()
            };
        }
        public Dictionary<string, object> ToTree()
        {
            var plan = new List<object>(Plan.Count);
            foreach (var p in Plan) plan.Add(p.ToTree());
            return new Dictionary<string, object>
            {
                { "dealer", Dealer }, { "order", V4Tree.Strings(Order) }, { "stacks", V4Tree.StringLists(Stacks) }, { "extra", Extra }, { "dice", V4Tree.Ints(Dice) },
                { "indStack", IndStack }, { "indLevel", IndLevel }, { "rolledLevel", RolledLevel }, { "fakeSkip", FakeSkip }, { "extraStack", ExtraStack },
                { "plan", plan }, { "centerStacks", V4Tree.Ints(CenterStacks) }
            };
        }
    }

    /// <summary>Hand result (R-50…R-58): reason 'finish' | 'deckOut'.</summary>
    public sealed class HandResult
    {
        public string Reason;
        public int Finisher;
        public bool Elden;
        public bool OkeyFinish;
        public bool PairFinish;
        public int Mul = 1;
        public int[] Base = new int[4];
        public int[] PairX = { 1, 1, 1, 1 };
        public int[] Pen = new int[4];
        public int[] Score = new int[4];
        public int[] TeamScore;
        public HandResult Clone() => new HandResult
        {
            Reason = Reason, Finisher = Finisher, Elden = Elden, OkeyFinish = OkeyFinish, PairFinish = PairFinish, Mul = Mul,
            Base = (int[])Base.Clone(), PairX = (int[])PairX.Clone(), Pen = (int[])Pen.Clone(), Score = (int[])Score.Clone(), TeamScore = (int[])TeamScore?.Clone()
        };
        public Dictionary<string, object> ToTree() => new Dictionary<string, object>
        {
            { "reason", Reason }, { "finisher", Finisher }, { "elden", Elden }, { "okeyFinish", OkeyFinish }, { "pairFinish", PairFinish }, { "mul", Mul },
            { "base", V4Tree.Ints(Base) }, { "pairX", V4Tree.Ints(PairX) }, { "pen", V4Tree.Ints(Pen) }, { "score", V4Tree.Ints(Score) },
            { "teamScore", TeamScore == null ? null : V4Tree.Ints(TeamScore) }
        };
    }

    /// <summary>One hand: deck, racks, discard piles, table melds, openings, penalties, the turn and the result.</summary>
    public sealed class HandState
    {
        public string Indicator;
        public ColorNum Okey;
        public List<string> Deck = new List<string>();
        public List<string>[] Racks = { new List<string>(), new List<string>(), new List<string>(), new List<string>() };
        public List<string>[] Discards = { new List<string>(), new List<string>(), new List<string>(), new List<string>() };
        public List<TableMeld> Melds = new List<TableMeld>();
        public int MeldSeq;
        /// <summary>Opening value per seat (0 = not opened): series total or number of pairs.</summary>
        public int[] Opened = new int[4];
        /// <summary>null | 'run' | 'pair'.</summary>
        public string[] OpenMode = new string[4];
        public int[] OpenedTurn = { -1, -1, -1, -1 };
        public int[] Pen = new int[4];
        public List<PenaltyEntry> PenLog = new List<PenaltyEntry>();
        public string FinishTile;
        /// <summary>Tests only (fromSpec): tiles not in play.</summary>
        public List<string> Out = new List<string>();
        public TurnState Turn;
        public DealRecord Deal;
        public HandResult Result;

        public HandState Clone() => new HandState
        {
            Indicator = Indicator, Okey = Okey, Deck = new List<string>(Deck), Racks = V4Tree.CloneLists(Racks), Discards = V4Tree.CloneLists(Discards),
            Melds = V4Tree.CloneMelds(Melds), MeldSeq = MeldSeq, Opened = (int[])Opened.Clone(), OpenMode = (string[])OpenMode.Clone(),
            OpenedTurn = (int[])OpenedTurn.Clone(), Pen = (int[])Pen.Clone(), PenLog = V4Tree.ClonePenalties(PenLog), FinishTile = FinishTile,
            Out = Out == null ? null : new List<string>(Out), Turn = Turn?.Clone(), Deal = Deal?.Clone(), Result = Result?.Clone()
        };

        public Dictionary<string, object> ToTree() => new Dictionary<string, object>
        {
            { "indicator", Indicator }, { "okey", Okey?.ToTree() }, { "deck", V4Tree.Strings(Deck) }, { "racks", V4Tree.StringLists(Racks) },
            { "discards", V4Tree.StringLists(Discards) }, { "melds", V4Tree.Melds(Melds) }, { "meldSeq", MeldSeq }, { "opened", V4Tree.Ints(Opened) },
            { "openMode", V4Tree.Strings(OpenMode) }, { "openedTurn", V4Tree.Ints(OpenedTurn) }, { "pen", V4Tree.Ints(Pen) },
            { "penLog", V4Tree.Penalties(PenLog) }, { "finishTile", FinishTile }, { "out", Out == null ? null : V4Tree.Strings(Out) },
            { "turn", Turn?.ToTree() }, { "deal", Deal?.ToTree() }, { "result", Result?.ToTree() }
        };
    }

    public sealed class HistoryEntry
    {
        public int HandNo;
        public int[] Score;
        public string Reason;
        public int Finisher;
        public int Mul;
        public HistoryEntry Clone() => new HistoryEntry { HandNo = HandNo, Score = (int[])Score.Clone(), Reason = Reason, Finisher = Finisher, Mul = Mul };
        public Dictionary<string, object> ToTree() => new Dictionary<string, object>
        {
            { "handNo", HandNo }, { "score", V4Tree.Ints(Score) }, { "reason", Reason }, { "finisher", Finisher }, { "mul", Mul }
        };
    }

    /// <summary>A ranking unit (R-59, R-60): one seat, or one team in pairs (then Team and Leavers are set).</summary>
    public sealed class RankUnit
    {
        public int? Team;
        public int[] Seats;
        public int Total;
        public bool Left;
        public int LeftSeq;
        public int[] Leavers;
        public int Rank;
        public RankUnit Clone() => new RankUnit
        {
            Team = Team, Seats = (int[])Seats.Clone(), Total = Total, Left = Left, LeftSeq = LeftSeq, Leavers = (int[])Leavers?.Clone(), Rank = Rank
        };
        public Dictionary<string, object> ToTree()
        {
            var d = new Dictionary<string, object>();
            if (Team.HasValue) d["team"] = Team.Value;
            d["seats"] = V4Tree.Ints(Seats);
            d["total"] = Total;
            d["left"] = Left;
            d["leftSeq"] = LeftSeq;
            if (Leavers != null) d["leavers"] = V4Tree.Ints(Leavers);
            d["rank"] = Rank;
            return d;
        }
    }

    /// <summary>The whole match: rules.js newMatch() plus everything apply() changes. Status 'lobby' | 'playing' | 'handOver' | 'matchOver'.</summary>
    public sealed class MatchState
    {
        public string V = G101Rules.Version;
        public MatchOptions Opts = new MatchOptions();
        public uint Seed;
        public RngState Rng;
        public string Status = "lobby";
        public int HandNo;
        public int Dealer = -1;
        public int[] Totals = new int[4];
        public List<HistoryEntry> History = new List<HistoryEntry>();
        public SeatInfo[] Seats = { new SeatInfo(), new SeatInfo(), new SeatInfo(), new SeatInfo() };
        public int LeaveSeq;
        public HandState Hand;
        public List<RankUnit> Ranking;

        public MatchState Clone()
        {
            var history = new List<HistoryEntry>(History.Count);
            foreach (var h in History) history.Add(h.Clone());
            var seats = new SeatInfo[Seats.Length];
            for (var i = 0; i < Seats.Length; i++) seats[i] = Seats[i].Clone();
            List<RankUnit> ranking = null;
            if (Ranking != null) { ranking = new List<RankUnit>(Ranking.Count); foreach (var u in Ranking) ranking.Add(u.Clone()); }
            return new MatchState
            {
                V = V, Opts = Opts.Clone(), Seed = Seed, Rng = Rng.Clone(), Status = Status, HandNo = HandNo, Dealer = Dealer, Totals = (int[])Totals.Clone(),
                History = history, Seats = seats, LeaveSeq = LeaveSeq, Hand = Hand?.Clone(), Ranking = ranking
            };
        }

        public Dictionary<string, object> ToTree()
        {
            var history = new List<object>(History.Count);
            foreach (var h in History) history.Add(h.ToTree());
            var seats = new List<object>(Seats.Length);
            foreach (var s in Seats) seats.Add(s.ToTree());
            return new Dictionary<string, object>
            {
                { "v", V }, { "opts", Opts.ToTree() }, { "seed", (long)Seed }, { "rng", Rng.ToTree() }, { "status", Status }, { "handNo", HandNo },
                { "dealer", Dealer }, { "totals", V4Tree.Ints(Totals) }, { "history", history }, { "seats", seats }, { "leaveSeq", LeaveSeq },
                { "hand", Hand?.ToTree() }, { "ranking", Ranking == null ? null : V4Tree.Ranking(Ranking) }
            };
        }
    }

    /// <summary>Tree projection and deep-copy helpers shared by the V4 model.</summary>
    public static class V4Tree
    {
        public static List<object> Ints(IEnumerable<int> xs) { var l = new List<object>(); foreach (var x in xs) l.Add(x); return l; }
        public static List<object> Strings(IEnumerable<string> xs) { var l = new List<object>(); foreach (var x in xs) l.Add(x); return l; }
        public static List<object> StringLists(IEnumerable<List<string>> xs) { var l = new List<object>(); foreach (var x in xs) l.Add(Strings(x)); return l; }
        public static List<object> Melds(IEnumerable<TableMeld> xs) { var l = new List<object>(); foreach (var x in xs) l.Add(x.ToTree()); return l; }
        public static List<object> Penalties(IEnumerable<PenaltyEntry> xs) { var l = new List<object>(); foreach (var x in xs) l.Add(x.ToTree()); return l; }
        public static List<object> Ranking(IEnumerable<RankUnit> xs) { var l = new List<object>(); foreach (var x in xs) l.Add(x.ToTree()); return l; }
        public static List<TableMeld> CloneMelds(List<TableMeld> xs) { var l = new List<TableMeld>(xs.Count); foreach (var x in xs) l.Add(x.Clone()); return l; }
        public static List<PenaltyEntry> ClonePenalties(List<PenaltyEntry> xs) { var l = new List<PenaltyEntry>(xs.Count); foreach (var x in xs) l.Add(x.Clone()); return l; }
        public static List<string>[] CloneLists(List<string>[] xs) { var a = new List<string>[xs.Length]; for (var i = 0; i < xs.Length; i++) a[i] = new List<string>(xs[i]); return a; }
        public static List<List<string>> CloneLists(List<List<string>> xs) { var l = new List<List<string>>(xs.Count); foreach (var x in xs) l.Add(new List<string>(x)); return l; }

        /// <summary>
        /// Canonical JSON of a tree: object keys sorted ordinally, no whitespace, strings escaped as JSON.stringify does.
        /// The same bytes rules.js gives for its own object with sorted keys, so state and event hashes compare across the two.
        /// </summary>
        public static string Canonical(object tree)
        {
            var sb = new StringBuilder();
            WriteCanonical(sb, tree);
            return sb.ToString();
        }

        private static void WriteCanonical(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); return;
                case string s: Quote(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); return;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); return;
                case uint u: sb.Append(u.ToString(CultureInfo.InvariantCulture)); return;
                case IDictionary<string, object> d:
                {
                    var keys = new List<string>(d.Keys);
                    keys.Sort(string.CompareOrdinal);
                    sb.Append('{');
                    for (var k = 0; k < keys.Count; k++)
                    {
                        if (k > 0) sb.Append(',');
                        Quote(sb, keys[k]);
                        sb.Append(':');
                        WriteCanonical(sb, d[keys[k]]);
                    }
                    sb.Append('}');
                    return;
                }
                case IList list:
                {
                    sb.Append('[');
                    for (var k = 0; k < list.Count; k++) { if (k > 0) sb.Append(','); WriteCanonical(sb, list[k]); }
                    sb.Append(']');
                    return;
                }
                default: throw new ArgumentException("Not a tree value: " + v.GetType().Name);
            }
        }

        private static void Quote(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(ch);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}

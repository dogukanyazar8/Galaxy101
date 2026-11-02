using System;
using System.Collections;
using System.Collections.Generic;
using Galaxy101.Contracts;
using Okey101.Core.V4;

namespace Galaxy101.Table
{
    /// <summary>A meld on the table as the client holds it (the engine's <c>{id, owner, kind, tiles, placedBy}</c>).</summary>
    public sealed class ClientMeld
    {
        public string Id;
        public int Owner;
        public string Kind;
        public List<string> Tiles = new List<string>();
        public List<int> PlacedBy = new List<int>();

        public ClientMeld Clone() => new ClientMeld { Id = Id, Owner = Owner, Kind = Kind, Tiles = new List<string>(Tiles), PlacedBy = new List<int>(PlacedBy) };

        public Dictionary<string, object> ToTree() => new Dictionary<string, object>
        {
            { "id", Id }, { "owner", Owner }, { "kind", Kind }, { "tiles", V4Tree.Strings(Tiles) }, { "placedBy", V4Tree.Ints(PlacedBy) },
        };

        public static ClientMeld From(object tree)
        {
            var d = (IDictionary<string, object>)tree;
            return new ClientMeld { Id = (string)d["id"], Owner = TableClient.I(d["owner"]), Kind = (string)d["kind"], Tiles = TableClient.Strings(d["tiles"]), PlacedBy = TableClient.Ints(d["placedBy"]) };
        }
    }

    /// <summary>The deal ceremony as the server announced it so far (D-24): what the table animates and the view's <c>deal</c>.</summary>
    public sealed class DealProgress
    {
        /// <summary>build, dice, afterDice, indicator, extra, distribute, center, fill.</summary>
        public string Step;
        public int Dealer;
        /// <summary>Server time of dealStart (the view's elapsed counts from it) and of the current step.</summary>
        public long T0, StepAt;
        public bool HumanDealer;
        public long BuildMs, RollWaitMs;
        /// <summary>Dice announced by dealDie: value, server time of the roll and when it lands (at + rollMs).</summary>
        public readonly List<(int Value, long At, long DoneAt)> Dice = new List<(int, long, long)>();
        public int IndicatorStack = -1, IndicatorLevel = -1, RolledLevel = -1, ExtraStack = -1;
        public bool FakeSkip;
        /// <summary>Only dealSkip (or its authoritative snapshot) activates fast fill; a request/vote never does.</summary>
        public bool Skipped;
        public long SkipAt;
        public readonly HashSet<int> SkipVotes = new HashSet<int>();
        public List<V4DealPlan> Plan;
        public List<int> CenterStacks;

        /// <summary>The dice that have landed by <paramref name="now"/> (the server adds a die when its roll ends).</summary>
        public List<int> Landed(long now)
        {
            var l = new List<int>();
            foreach (var d in Dice) if (now >= d.DoneAt) l.Add(d.Value);
            return l;
        }

        /// <summary>The server's step at <paramref name="now"/>: "afterDice" once the second die has landed.</summary>
        public string StepAtTime(long now) => Step == "dice" && Dice.Count == 2 && now >= Dice[1].DoneAt ? "afterDice" : Step;
    }

    /// <summary>One S2C message as applied (the table animates and announces from these).</summary>
    public readonly struct ClientMessage
    {
        public readonly string Type;
        public readonly object Payload;
        /// <summary>The engine event of a <c>game</c> message (its <c>e</c> names it), else null.</summary>
        public readonly IDictionary<string, object> Ev;
        public ClientMessage(string type, object payload, IDictionary<string, object> ev) { Type = type; Payload = payload; Ev = ev; }
        public string E => Ev != null && Ev.TryGetValue("e", out var e) ? e as string : null;
    }

    /// <summary>
    /// The client's model of one v4 match (protocol.json S2C): my rack as engine ids, the others' tile counts, discards,
    /// melds, indicator and okey, deck, deal ceremony, turn, openings, penalties, totals, results and the legal moves — built
    /// only from the server's events (matchStart, dealStart/Step/Die, handDealt, turn, legal, game, handWait, readyState,
    /// matchResult, seatConnection) and synchronised by tableView / snapshot, which carry the same view the server's
    /// <c>TableRuntime.GameView(seat)</c> gives. <see cref="View"/> produces that view again, so the model can be checked
    /// against the server's after every event. No rule is decided here: legality and every rule helper are the v4 engine's
    /// (<see cref="G101Rules"/>) on <see cref="Project"/>, the state as this seat may see it.
    /// </summary>
    public sealed class TableClient
    {
        /// <param name="seat">My seat 0–3, or -1 for a spectator.</param>
        public TableClient(int seat) { Seat = seat; }

        /// <summary>My seat (0–3), -1 when watching.</summary>
        public int Seat { get; private set; }
        public void SetSeat(int seat) { Seat = seat; Changed(); }

        /// <summary>tableView and snapshot replace the model (the server's view wins). Tests switch it off to check that the events alone rebuild the view.</summary>
        public bool TrustViews = true;

        public V4Table Table { get; private set; }
        public string Status { get; private set; } = "none";
        public int HandNo { get; private set; }
        public int Hands { get; private set; } = 5;
        public int Dealer { get; private set; } = -1;
        public readonly int[] Totals = new int[4];
        public readonly List<Dictionary<string, object>> History = new List<Dictionary<string, object>>();
        private sealed class ObservedOpening
        {
            public int Finisher;
            public int[] Score;
            public IReadOnlyList<int> Opened;
        }
        // Presentation notes observed at handEnd; never serialized into the protocol's History / View.
        private readonly Dictionary<int, ObservedOpening> observedOpenings = new Dictionary<int, ObservedOpening>();
        public readonly bool[] SeatBot = new bool[4], SeatLeft = new bool[4];
        public readonly int[] SeatLeftSeq = new int[4];
        public readonly bool[] Connected = { true, true, true, true };
        public readonly bool[] Autoplay = new bool[4];

        /// <summary>A hand exists (the engine's Hand is not null).</summary>
        public bool HasHand { get; private set; }
        public DealProgress Deal { get; private set; }
        /// <summary>Indicator and okey once announced (dealStep:indicator, handDealt or a view); null while hidden.</summary>
        public string Indicator { get; private set; }
        public string OkeyColor { get; private set; }
        public int OkeyNumber { get; private set; }
        public bool OkeyKnown => OkeyColor != null;
        public int Deck { get; private set; }
        /// <summary>My rack in the engine's order, null while hidden (the deal before fill, a spectator).</summary>
        public List<string> Rack { get; private set; }
        public readonly int[] Counts = new int[4];
        public readonly List<string>[] Discards = { new List<string>(), new List<string>(), new List<string>(), new List<string>() };
        public readonly List<ClientMeld> Melds = new List<ClientMeld>();
        public readonly int[] Opened = new int[4];
        public readonly string[] OpenMode = new string[4];
        public readonly int[] OpenedTurn = { -1, -1, -1, -1 };
        public readonly int[] Pen = new int[4];

        // Turn (the engine's turn) and the runtime's timer.
        public int TurnSeat { get; private set; } = -1;
        public string TurnPhase { get; private set; }
        public int TurnN { get; private set; }
        public string SideTake { get; private set; }
        public bool SideMust { get; private set; }
        public bool SideReturned { get; private set; }
        /// <summary>The turn's deadline (server ms); kept until the next turn message, as the server keeps it.</summary>
        public long Deadline { get; private set; }
        public long TurnMs { get; private set; } = TableTimings.DefaultTurnMs;
        /// <summary>Server time of the last turn message (the ring counts from it).</summary>
        public long TurnAt { get; private set; }
        /// <summary>The turn has a geri topla snapshot (R-34): something was laid this turn.</summary>
        public bool TurnHasSnap { get; private set; }

        public Dictionary<string, object> Result { get; private set; }
        public List<List<string>> RacksRevealed { get; private set; }
        public long NextAt { get; private set; }
        public long NextHandIn { get; private set; }
        public readonly HashSet<int> Ready = new HashSet<int>();
        public List<Dictionary<string, object>> Ranking { get; private set; }
        public V4MatchResult MatchResult { get; private set; }
        /// <summary>The last legal message (sent to the seat in turn); <see cref="Legal"/> is computed and agrees with it.</summary>
        public Dictionary<string, object> LegalEvent { get; private set; }
        public Dictionary<string, object> LastView { get; private set; }
        public long MatchStartAt { get; private set; }
        public long CountdownMs { get; private set; }
        public int MeldSeq { get; private set; }

        /// <summary>Bumped on every change (views and projections are cached by it).</summary>
        public int Version { get; private set; }
        /// <summary>Every message after it is applied.</summary>
        public event Action<ClientMessage> Applied;

        private sealed class TurnSnap
        {
            public List<string> Rack;
            public int Count, Opened, OpenedTurn, MeldSeq;
            public string OpenMode;
            public List<ClientMeld> Melds;
            public bool SideMust;
        }
        private TurnSnap snap;

        private void Changed() { Version++; projected = null; }

        // ---- Applying messages --------------------------------------------------------------------------------------

        /// <summary>Applies one S2C message (protocol.json name, payload as the contracts type with engine parts as trees).</summary>
        public void Apply(string type, object payload)
        {
            IDictionary<string, object> ev = null;
            switch (type)
            {
                case V4Messages.MatchStart: OnMatchStart((V4MatchStart)payload); break;
                case V4Messages.DealStart: OnDealStart((V4DealStart)payload); break;
                case V4Messages.DealStep: OnDealStep((V4DealStep)payload); break;
                case V4Messages.DealDie: OnDealDie((V4DealDie)payload); break;
                case V4Messages.DealSkip: OnDealSkip((V4DealSkip)payload); break;
                case V4Messages.DealSkipVote: if (Deal != null) Deal.SkipVotes.Add(((V4DealSkipVote)payload).Seat); break;
                case V4Messages.HandDealt: OnHandDealt((V4HandDealt)payload); break;
                case V4Messages.Turn: OnTurn((V4Turn)payload); break;
                case V4Messages.Legal: LegalEvent = Tree(((V4Legal)payload).Legal); break;
                case V4Messages.Game: ev = (IDictionary<string, object>)((V4GameEvent)payload).Ev; OnGame(ev); break;
                case V4Messages.HandWait: { var w = (V4HandWait)payload; NextAt = w.NextAt; NextHandIn = w.NextHandIn; break; }
                case V4Messages.ReadyState: Ready.Clear(); foreach (var k in ((V4ReadyState)payload).Ready ?? new List<int>()) Ready.Add(k); break;
                case V4Messages.MatchResult: MatchResult = (V4MatchResult)payload; break;
                case V4Messages.SeatConnection:
                {
                    var c = (V4SeatConnection)payload;
                    if (c.Seat >= 0 && c.Seat < 4) { Connected[c.Seat] = c.Connected; if (c.Autoplay.HasValue) Autoplay[c.Seat] = c.Autoplay.Value; }
                    break;
                }
                case V4Messages.TableView:
                {
                    var view = Tree(((V4TableView)payload).View);
                    LastView = view;
                    if (TrustViews && view != null) Load(view);
                    break;
                }
                case V4Messages.Snapshot:
                {
                    var s = (V4Snapshot)payload;
                    if (s.Table != null)
                    {
                        if (Table?.Id != s.Table.Id) observedOpenings.Clear();
                        Table = s.Table;
                    }
                    var view = Tree(s.Game);
                    if (view != null) Load(view, true);
                    break;
                }
            }
            Changed();
            Applied?.Invoke(new ClientMessage(type, payload, ev));
        }

        private void OnMatchStart(V4MatchStart m)
        {
            Table = m.Table;
            MatchStartAt = m.At; CountdownMs = m.CountdownMs;
            Status = G101Rules.StatusLobby; HandNo = 0; Dealer = -1; HasHand = false; Deal = null;
            Hands = m.Table != null && m.Table.Hands > 0 ? m.Table.Hands : 5;
            Array.Clear(Totals, 0, 4); History.Clear(); Ranking = null; MatchResult = null; NextAt = 0; Deadline = 0; Ready.Clear(); leaveSeq = 0;
            observedOpenings.Clear();
            for (int k = 0; k < 4; k++)
            {
                var seat = m.Table?.Seats != null && k < m.Table.Seats.Count ? m.Table.Seats[k] : null;
                SeatBot[k] = seat == null || seat.Bot; SeatLeft[k] = false; SeatLeftSeq[k] = 0;
                Connected[k] = seat == null || seat.Connected; Autoplay[k] = false;
            }
            if (m.Table?.TurnSec > 0) TurnMs = m.Table.TurnSec * 1000L;
            ResetHand();
        }

        private void ResetHand()
        {
            Rack = null; Indicator = null; OkeyColor = null; OkeyNumber = 0; Deck = 0;
            for (int k = 0; k < 4; k++) { Counts[k] = 0; Discards[k].Clear(); Opened[k] = 0; OpenMode[k] = null; OpenedTurn[k] = -1; Pen[k] = 0; }
            Melds.Clear(); MeldSeq = 0; Result = null; RacksRevealed = null;
            TurnSeat = -1; TurnPhase = null; TurnN = 0; SideTake = null; SideMust = false; SideReturned = false; TurnHasSnap = false; snap = null;
            LegalEvent = null;
        }

        private void OnDealStart(V4DealStart d)
        {
            Rack = null; // A new ceremony never exposes the previous hand before its handStart / handDealt arrives.
            Deal = new DealProgress { Step = "build", Dealer = d.Dealer, T0 = d.At, StepAt = d.At, HumanDealer = d.HumanDealer, BuildMs = d.BuildMs };
            HandNo = d.HandNo; Dealer = d.Dealer;
        }

        private void OnDealStep(V4DealStep s)
        {
            if (Deal == null) Deal = new DealProgress { Dealer = s.Dealer ?? Dealer, T0 = s.At };
            Deal.Step = s.Step; Deal.StepAt = s.At;
            switch (s.Step)
            {
                case "dice": Deal.RollWaitMs = s.RollWaitMs ?? 0; break;
                case "indicator":
                    Deal.IndicatorStack = s.Stack ?? -1; Deal.IndicatorLevel = s.Level ?? -1; Deal.RolledLevel = s.RolledLevel ?? -1; Deal.FakeSkip = s.FakeSkip == true;
                    Indicator = s.Indicator;
                    if (s.Okey != null) { OkeyColor = s.Okey.C; OkeyNumber = s.Okey.N; }
                    break;
                case "extra": Deal.ExtraStack = s.Stack ?? -1; break;
                case "distribute": Deal.Plan = s.Plan; break;
                case "center": Deal.CenterStacks = s.Stacks; if (s.Deck.HasValue) Deck = s.Deck.Value; break;
            }
        }

        private void OnDealDie(V4DealDie d)
        {
            if (Deal == null) Deal = new DealProgress { Step = "dice", Dealer = Dealer, T0 = d.At };
            Deal.Dice.Add((d.Value, d.At, d.At + d.RollMs));
        }

        private void OnDealSkip(V4DealSkip message)
        {
            if (Deal == null) Deal = new DealProgress { Dealer = Dealer, T0 = message.At };
            Deal.Step = "fill"; Deal.StepAt = message.At; Deal.Skipped = true; Deal.SkipAt = message.At;
            Deal.Dice.Clear();
            foreach (int value in message.Dice) Deal.Dice.Add((value, message.At, message.At));
            Deal.IndicatorStack = message.Stack; Deal.IndicatorLevel = message.Level;
            Deal.RolledLevel = message.Dice.Count > 1 ? message.Dice[1] - 1 : message.Level;
            Deal.FakeSkip = message.FakeSkip; Deal.ExtraStack = message.ExtraStack; Deal.SkipVotes.Clear();
            CompleteSkippedLayout(Deal);
            Indicator = message.Indicator;
            if (message.Okey != null) { OkeyColor = message.Okey.C; OkeyNumber = message.Okey.N; }
        }

        private static void CompleteSkippedLayout(DealProgress deal)
        {
            int Stack(int offset) => (deal.IndicatorStack + offset) % 15;
            if (deal.Plan == null)
            {
                deal.Plan = new List<V4DealPlan>();
                for (int seat = 0; seat < 4; seat++) deal.Plan.Add(new V4DealPlan { Seat = (deal.Dealer + 1 + seat) % 4, Stacks = new List<int> { Stack(1 + seat), Stack(5 + seat) } });
                for (int seat = 0; seat < 4; seat++) deal.Plan.Add(new V4DealPlan { Seat = (deal.Dealer + 1 + seat) % 4, Stacks = new List<int> { Stack(9 + seat) } });
            }
            if (deal.CenterStacks == null) deal.CenterStacks = new List<int> { deal.IndicatorStack, Math.Max(Stack(13), Stack(14)), Math.Min(Stack(13), Stack(14)) };
        }

        private void OnHandDealt(V4HandDealt h)
        {
            if (Deal != null) { Deal.Step = "fill"; Deal.StepAt = h.At; }
            HandNo = h.HandNo;
            Rack = Seat >= 0 && h.Rack != null ? new List<string>(h.Rack) : null;
            if (h.Counts != null) for (int k = 0; k < 4 && k < h.Counts.Count; k++) Counts[k] = h.Counts[k];
            Deck = h.Deck;
            Indicator = h.Indicator;
            if (h.Okey != null) { OkeyColor = h.Okey.C; OkeyNumber = h.Okey.N; }
        }

        private void OnTurn(V4Turn t)
        {
            Deal = null;
            TurnSeat = t.Seat; TurnPhase = t.Phase; TurnN = t.N; Deadline = t.Deadline; TurnMs = t.TurnMs; TurnAt = t.At;
        }

        private void OnGame(IDictionary<string, object> e)
        {
            if (e == null || !e.TryGetValue("e", out var name)) return;
            int seat = e.TryGetValue("seat", out var sv) && sv != null ? I(sv) : -1;
            switch ((string)name)
            {
                case "handStart":
                    ResetHand();
                    HasHand = true; Status = G101Rules.StatusPlaying;
                    HandNo = I(e["handNo"]); Dealer = I(e["dealer"]);
                    var counts = Ints(e["counts"]);
                    for (int k = 0; k < 4 && k < counts.Count; k++) Counts[k] = counts[k];
                    Deck = I(e["deck"]);
                    if (e.TryGetValue("indicator", out var ind) && ind != null) Indicator = (string)ind;
                    if (e.TryGetValue("okey", out var ok) && ok is IDictionary<string, object> okd) { OkeyColor = (string)okd["c"]; OkeyNumber = I(okd["n"]); }
                    NewTurn(I(e["starter"]), G101Rules.PhasePlay, 1);
                    break;
                case "turn":
                    NewTurn(seat, (string)e["phase"], I(e["n"]));
                    break;
                case "drew":
                {
                    var src = (string)e["src"];
                    var tile = e.TryGetValue("tile", out var tv) ? tv as string : null;
                    if (src == "side")
                    {
                        int from = I(e["from"]);
                        var pile = Discards[from];
                        if (pile.Count > 0) pile.RemoveAt(pile.Count - 1);
                        SideTake = tile; SideMust = e.TryGetValue("mustOpen", out var mo) && mo is bool b && b;
                    }
                    Counts[seat]++;
                    if (seat == Seat && Rack != null && tile != null) Rack.Add(tile);
                    Deck = I(e["deck"]);
                    TurnPhase = G101Rules.PhasePlay;
                    break;
                }
                case "sideReturned":
                {
                    Restore(seat);
                    var tile = (string)e["tile"];
                    Counts[seat]--;
                    if (seat == Seat && Rack != null) Rack.Remove(tile);
                    Discards[I(e["to"])].Add(tile);
                    SideTake = null; SideMust = false; SideReturned = true; TurnPhase = G101Rules.PhaseDraw; TurnHasSnap = false; snap = null;
                    break;
                }
                case "opened":
                    Snap(seat);
                    PlaceMelds(seat, e["melds"]);
                    Opened[seat] = I(e["value"]); OpenMode[seat] = (string)e["mode"]; OpenedTurn[seat] = TurnN;
                    SideMust = false;
                    break;
                case "laid":
                    Snap(seat);
                    PlaceMelds(seat, e["melds"]);
                    break;
                case "added":
                {
                    Snap(seat);
                    var tile = (string)e["tile"];
                    var m = MeldById((string)e["meld"]);
                    if (seat == Seat && Rack != null) Rack.Remove(tile);
                    Counts[seat]--;
                    if (m != null)
                    {
                        if ((string)e["side"] == "pre") { m.Tiles.Insert(0, tile); m.PlacedBy.Insert(0, seat); }
                        else { m.Tiles.Add(tile); m.PlacedBy.Add(seat); }
                    }
                    break;
                }
                case "okeySwapped":
                {
                    Snap(seat);
                    var tile = (string)e["tile"];
                    var okey = (string)e["okey"];
                    var m = MeldById((string)e["meld"]);
                    int index = I(e["index"]);
                    if (m != null && index >= 0 && index < m.Tiles.Count) { m.Tiles[index] = tile; m.PlacedBy[index] = seat; }
                    if (seat == Seat && Rack != null) { Rack.Remove(tile); Rack.Add(okey); }
                    break;
                }
                case "undone":
                    Restore(seat);
                    TurnHasSnap = false; snap = null;
                    break;
                case "discarded":
                {
                    var tile = (string)e["tile"];
                    if (seat == Seat && Rack != null) Rack.Remove(tile);
                    Counts[seat]--;
                    Discards[seat].Add(tile);
                    TurnHasSnap = false; snap = null;
                    break;
                }
                case "finished":
                {
                    var tile = (string)e["tile"];
                    if (seat == Seat && Rack != null) Rack.Remove(tile);
                    Counts[seat]--;
                    TurnHasSnap = false; snap = null;
                    break;
                }
                case "penalty":
                    Pen[seat] = I(e["total"]);
                    break;
                case "seatLeft":
                    SeatLeft[seat] = true; SeatBot[seat] = true; SeatLeftSeq[seat] = NextLeaveSeq();
                    Connected[seat] = true; Autoplay[seat] = false; Ready.Remove(seat);
                    break;
                case "seatTaken":
                    SeatBot[seat] = false; SeatLeft[seat] = false; SeatLeftSeq[seat] = 0;
                    Connected[seat] = true; Autoplay[seat] = false;
                    break;
                case "handEnd":
                {
                    Result = Tree(e["result"]);
                    var totals = Ints(e["totals"]);
                    for (int k = 0; k < 4 && k < totals.Count; k++) Totals[k] = totals[k];
                    RacksRevealed = new List<List<string>>();
                    if (e["racks"] is IList racks) foreach (var r in racks) RacksRevealed.Add(Strings(r));
                    Status = G101Rules.StatusHandOver;
                    TurnPhase = G101Rules.PhaseOver;
                    History.Add(new Dictionary<string, object>
                    {
                        { "handNo", I(e["handNo"]) }, { "score", V4Tree.Ints(Ints(Result["score"])) }, { "reason", (string)Result["reason"] },
                        { "finisher", I(Result["finisher"]) }, { "mul", I(Result["mul"]) },
                    });
                    observedOpenings[I(e["handNo"])] = new ObservedOpening {
                        Finisher = I(Result["finisher"]), Score = Ints(Result["score"]).ToArray(),
                        Opened = Array.AsReadOnly((int[])Opened.Clone()),
                    };
                    break;
                }
                case "matchEnd":
                {
                    Status = G101Rules.StatusMatchOver;
                    Ranking = new List<Dictionary<string, object>>();
                    if (e["ranking"] is IList ranking) foreach (var u in ranking) Ranking.Add(Tree(u));
                    break;
                }
            }
        }

        /// <summary>The engine's match-wide leave counter (R-60: the later leaver ranks ahead).</summary>
        private int NextLeaveSeq() => ++leaveSeq;
        private int leaveSeq;

        private void NewTurn(int seat, string phase, int n)
        {
            TurnSeat = seat; TurnPhase = phase; TurnN = n;
            SideTake = null; SideMust = false; SideReturned = false; TurnHasSnap = false; snap = null;
        }

        private void PlaceMelds(int seat, object melds)
        {
            if (!(melds is IList list)) return;
            foreach (var t in list)
            {
                var m = ClientMeld.From(t);
                Melds.Add(m);
                MeldSeq = Math.Max(MeldSeq, TileIds.MeldInt(m.Id));
                foreach (var id in m.Tiles) if (seat == Seat && Rack != null) Rack.Remove(id);
                Counts[seat] -= m.Tiles.Count;
            }
        }

        /// <summary>R-34: the state before the turn's first laying action (the engine's Snap).</summary>
        private void Snap(int seat)
        {
            if (TurnHasSnap) return;
            TurnHasSnap = true;
            var melds = new List<ClientMeld>();
            foreach (var m in Melds) melds.Add(m.Clone());
            snap = new TurnSnap
            {
                Rack = seat == Seat && Rack != null ? new List<string>(Rack) : null, Count = Counts[seat], Melds = melds, MeldSeq = MeldSeq,
                Opened = Opened[seat], OpenMode = OpenMode[seat], OpenedTurn = OpenedTurn[seat], SideMust = SideMust,
            };
        }

        /// <summary>Back to the snapshot (undone, sideReturned). Unknown after a mid-turn view load: the next tableView brings it.</summary>
        private void Restore(int seat)
        {
            var p = snap;
            if (p == null) return;
            if (seat == Seat && p.Rack != null) Rack = p.Rack;
            Counts[seat] = p.Count;
            Melds.Clear(); Melds.AddRange(p.Melds); MeldSeq = p.MeldSeq;
            Opened[seat] = p.Opened; OpenMode[seat] = p.OpenMode; OpenedTurn[seat] = p.OpenedTurn;
            SideMust = p.SideMust;
            snap = null; TurnHasSnap = false;
        }

        public ClientMeld MeldById(string id) { foreach (var m in Melds) if (m.Id == id) return m; return null; }

        // ---- The view (TableRuntime.GameView) ----------------------------------------------------------------------

        /// <summary>The server's <c>GameView(seat)</c> rebuilt from the model at server time <paramref name="now"/>.</summary>
        public Dictionary<string, object> View(long now)
        {
            if (!HasHand)
                return new Dictionary<string, object> { { "status", Status }, { "handNo", HandNo }, { "totals", V4Tree.Ints(Totals) } };
            var step = Deal?.StepAtTime(now);
            bool hidden = Deal != null && (step == "build" || step == "dice" || step == "afterDice");
            bool rackShown = Seat >= 0 && (Deal == null || step == "fill");
            var seats = new List<object>();
            for (int k = 0; k < 4; k++) seats.Add(new Dictionary<string, object> { { "bot", SeatBot[k] }, { "left", SeatLeft[k] }, { "leftSeq", SeatLeftSeq[k] } });
            var history = new List<object>();
            foreach (var h in History) history.Add(h);
            var discards = new List<object>();
            foreach (var d in Discards) discards.Add(V4Tree.Strings(d));
            var melds = new List<object>();
            foreach (var m in Melds) melds.Add(m.ToTree());
            Dictionary<string, object> turn = null;
            if (TurnSeat >= 0)
                turn = new Dictionary<string, object>
                {
                    { "seat", TurnSeat }, { "phase", TurnPhase }, { "n", TurnN }, { "sideTake", SideTake }, { "sideMust", SideMust }, { "deadline", Deadline },
                };
            List<object> revealed = null;
            if (Result != null && RacksRevealed != null) { revealed = new List<object>(); foreach (var r in RacksRevealed) revealed.Add(V4Tree.Strings(r)); }
            Dictionary<string, object> dealView = null;
            if (Deal != null)
            {
                dealView = new Dictionary<string, object> { { "step", step }, { "dice", V4Tree.Ints(Deal.Landed(now)) }, { "dealer", Deal.Dealer }, { "elapsed", now - Deal.T0 } };
                if (Deal.SkipVotes.Count > 0)
                {
                    var votes = new List<int>(Deal.SkipVotes); votes.Sort(); dealView["skip"] = V4Tree.Ints(votes);
                }
                if (Deal.Skipped)
                {
                    dealView["skipped"] = true; dealView["skipAt"] = Deal.SkipAt;
                    dealView["stack"] = Deal.IndicatorStack; dealView["level"] = Deal.IndicatorLevel;
                    dealView["fakeSkip"] = Deal.FakeSkip; dealView["extraStack"] = Deal.ExtraStack;
                }
            }
            return new Dictionary<string, object>
            {
                { "status", Status }, { "handNo", HandNo }, { "hands", Hands }, { "dealer", Dealer }, { "totals", V4Tree.Ints(Totals) },
                { "history", history }, { "seats", seats },
                { "deal", dealView },
                { "indicator", hidden ? null : Indicator }, { "okey", hidden || !OkeyKnown ? null : new Dictionary<string, object> { { "c", OkeyColor }, { "n", OkeyNumber } } },
                { "autoplay", Bools(Autoplay) }, { "connected", Bools(Connected) }, { "deck", Deck },
                { "rack", rackShown && Rack != null ? V4Tree.Strings(Rack) : null }, { "counts", V4Tree.Ints(Counts) },
                { "discards", discards }, { "melds", melds }, { "opened", V4Tree.Ints(Opened) }, { "openMode", V4Tree.Strings(OpenMode) }, { "pen", V4Tree.Ints(Pen) },
                { "turn", turn }, { "legal", Seat >= 0 ? Legal().ToTree() : null }, { "result", Result },
                { "racksRevealed", revealed }, { "nextAt", Status == G101Rules.StatusHandOver ? (object)NextAt : null },
            };
        }

        /// <summary>
        /// Takes a server view (tableView.view, snapshot.game). The turn's geri topla snapshot survives when the view is of the
        /// same turn (it is not in the view); a view of another turn drops it.
        /// </summary>
        public void Load(IDictionary<string, object> v, bool resume = false)
        {
            if (v == null) return;
            Status = (string)v["status"];
            HandNo = I(v["handNo"]);
            var totals = Ints(v["totals"]);
            for (int k = 0; k < 4 && k < totals.Count; k++) Totals[k] = totals[k];
            if (!v.ContainsKey("turn") && !v.ContainsKey("rack"))
            {
                HasHand = false; Deal = null;
                return;
            }
            HasHand = true;
            if (v.TryGetValue("hands", out var hands) && hands != null) Hands = I(hands);
            Dealer = I(v["dealer"]);
            History.Clear();
            if (v["history"] is IList history) foreach (var h in history) History.Add(Tree(h));
            PruneObservedOpenings();
            if (v["seats"] is IList seats)
                for (int k = 0; k < 4 && k < seats.Count; k++)
                {
                    var s = (IDictionary<string, object>)seats[k];
                    SeatBot[k] = (bool)s["bot"]; SeatLeft[k] = (bool)s["left"]; SeatLeftSeq[k] = I(s["leftSeq"]);
                    leaveSeq = Math.Max(leaveSeq, SeatLeftSeq[k]);
                }
            var deal = v["deal"] as IDictionary<string, object>;
            if (deal == null) Deal = null;
            else
            {
                // A view inside the ceremony: the client resumes the ceremony from its step (protocol.json reconnect.screens.table).
                var keep = Deal;
                long elapsed = deal.TryGetValue("elapsed", out var el) && el != null ? Convert.ToInt64(el) : 0;
                var d = new DealProgress { Step = (string)deal["step"], Dealer = I(deal["dealer"]), T0 = keep?.T0 ?? 0 };
                if (keep == null) d.T0 = -elapsed;   // no clock reference: elapsed counts from 0
                foreach (var x in Ints(deal["dice"])) d.Dice.Add((x, long.MinValue, long.MinValue));
                if (keep != null && keep.Dealer == d.Dealer)
                {
                    d.HumanDealer = keep.HumanDealer; d.BuildMs = keep.BuildMs; d.RollWaitMs = keep.RollWaitMs; d.StepAt = keep.StepAt;
                    d.IndicatorStack = keep.IndicatorStack; d.IndicatorLevel = keep.IndicatorLevel; d.RolledLevel = keep.RolledLevel; d.FakeSkip = keep.FakeSkip;
                    d.ExtraStack = keep.ExtraStack; d.Plan = keep.Plan; d.CenterStacks = keep.CenterStacks;
                    // Dice already announced but still rolling stay pending.
                    for (int i = d.Dice.Count; i < keep.Dice.Count; i++) d.Dice.Add(keep.Dice[i]);
                }
                if (d.Step == "afterDice") d.Step = "dice";
                if (deal.TryGetValue("skip", out var votes) && votes != null) foreach (int voter in Ints(votes)) d.SkipVotes.Add(voter);
                if (deal.TryGetValue("skipped", out var skipped) && skipped is bool fast && fast)
                {
                    d.Skipped = true; d.SkipAt = Convert.ToInt64(deal["skipAt"]); d.StepAt = d.SkipAt;
                    d.IndicatorStack = I(deal["stack"]); d.IndicatorLevel = I(deal["level"]);
                    d.FakeSkip = deal["fakeSkip"] is bool fake && fake; d.ExtraStack = I(deal["extraStack"]);
                    d.RolledLevel = d.Dice.Count > 1 ? d.Dice[1].Value - 1 : d.IndicatorLevel;
                    CompleteSkippedLayout(d);
                }
                Deal = d;
            }
            Indicator = v["indicator"] as string ?? (Deal != null ? Indicator : null);
            if (v["okey"] is IDictionary<string, object> ok) { OkeyColor = (string)ok["c"]; OkeyNumber = I(ok["n"]); }
            else if (Deal == null) { OkeyColor = null; OkeyNumber = 0; }
            var autoplay = v["autoplay"] as IList; var connected = v["connected"] as IList;
            for (int k = 0; k < 4; k++)
            {
                if (autoplay != null && k < autoplay.Count) Autoplay[k] = (bool)autoplay[k];
                if (connected != null && k < connected.Count) Connected[k] = (bool)connected[k];
            }
            Deck = I(v["deck"]);
            if (v["rack"] is IList rack) Rack = Strings(rack);
            else if (Deal != null || Seat < 0) Rack = null;
            var counts = Ints(v["counts"]);
            for (int k = 0; k < 4 && k < counts.Count; k++) Counts[k] = counts[k];
            if (v["discards"] is IList discards) for (int k = 0; k < 4 && k < discards.Count; k++) { Discards[k].Clear(); Discards[k].AddRange(Strings(discards[k])); }
            Melds.Clear(); MeldSeq = 0;
            if (v["melds"] is IList melds) foreach (var m in melds) { var cm = ClientMeld.From(m); Melds.Add(cm); MeldSeq = Math.Max(MeldSeq, TileIds.MeldInt(cm.Id)); }
            var opened = Ints(v["opened"]);
            var modes = v["openMode"] as IList;
            var pen = Ints(v["pen"]);
            for (int k = 0; k < 4; k++)
            {
                if (k < opened.Count) Opened[k] = opened[k];
                if (modes != null && k < modes.Count) OpenMode[k] = modes[k] as string;
                if (k < pen.Count) Pen[k] = pen[k];
            }
            var turn = v["turn"] as IDictionary<string, object>;
            if (turn == null) { TurnSeat = -1; TurnPhase = null; TurnN = 0; TurnHasSnap = false; snap = null; }
            else
            {
                int seat = I(turn["seat"]), n = I(turn["n"]);
                bool same = seat == TurnSeat && n == TurnN && (string)turn["phase"] == TurnPhase;
                TurnSeat = seat; TurnPhase = (string)turn["phase"]; TurnN = n;
                SideTake = turn["sideTake"] as string; SideMust = turn["sideMust"] is bool sm && sm;
                if (turn.TryGetValue("deadline", out var dl) && dl != null) Deadline = Convert.ToInt64(dl);
                if (!same) { snap = null; TurnHasSnap = false; SideReturned = false; }
                // Facts the view has only through the legal moves of my own turn.
                if (v["legal"] is IDictionary<string, object> legal && seat == Seat)
                {
                    if (legal.TryGetValue("undo", out var u) && u is bool undo) { TurnHasSnap = undo; if (!undo) snap = null; }
                    if (TurnPhase == G101Rules.PhaseDraw && legal.TryGetValue("side", out var sd) && sd is bool side)
                        SideReturned = !side && Discards[G101Rules.LeftOf(seat)].Count > 0;
                }
            }
            Result = v["result"] is IDictionary<string, object> r ? Tree(r) : null;
            RacksRevealed = null;
            if (v["racksRevealed"] is IList rr) { RacksRevealed = new List<List<string>>(); foreach (var x in rr) RacksRevealed.Add(Strings(x)); }
            if (v.TryGetValue("nextAt", out var next) && next != null) NextAt = Convert.ToInt64(next);
            if (resume) LegalEvent = null;
        }

        // ---- The rules engine on what this seat sees -------------------------------------------------------------

        private MatchState projected;

        /// <summary>
        /// The match as this seat may see it, in the engine's shapes: other racks empty, the deck as a count. The engine's view
        /// helpers (legal, openNeed, isIslek, addSides, swapFits, autoDiscardTile, analyzeMeld) need nothing hidden.
        /// </summary>
        public MatchState Project()
        {
            if (projected != null) return projected;
            var o = Options();
            var s = G101Rules.NewMatch(o, 1);
            s.Status = Status == "none" ? G101Rules.StatusLobby : Status;
            s.HandNo = HandNo; s.Dealer = Dealer;
            for (int k = 0; k < 4; k++) { s.Totals[k] = Totals[k]; s.Seats[k].Bot = SeatBot[k]; s.Seats[k].Left = SeatLeft[k]; s.Seats[k].LeftSeq = SeatLeftSeq[k]; }
            if (HasHand)
            {
                var h = new HandState
                {
                    Indicator = Indicator, Okey = OkeyKnown ? new ColorNum(OkeyColor, OkeyNumber) : null, Deck = new List<string>(new string[Math.Max(0, Deck)]),
                    MeldSeq = MeldSeq, Opened = (int[])Opened.Clone(), OpenMode = (string[])OpenMode.Clone(), OpenedTurn = (int[])OpenedTurn.Clone(), Pen = (int[])Pen.Clone(),
                };
                for (int k = 0; k < 4; k++) h.Discards[k] = new List<string>(Discards[k]);
                if (Seat >= 0 && Rack != null) h.Racks[Seat] = new List<string>(Rack);
                foreach (var m in Melds) h.Melds.Add(new TableMeld { Id = m.Id, Owner = m.Owner, Kind = m.Kind, Tiles = new List<string>(m.Tiles), PlacedBy = new List<int>(m.PlacedBy) });
                if (TurnSeat >= 0)
                    h.Turn = new TurnState
                    {
                        Seat = TurnSeat, Phase = TurnPhase, N = TurnN, SideTake = SideTake, SideMust = SideMust, SideReturned = SideReturned,
                        Snap = TurnHasSnap ? new TurnSnapshot() : null,
                    };
                s.Hand = h;
            }
            return projected = s;
        }

        /// <summary>The table's rule options (the engine's normalised MatchOptions).</summary>
        public MatchOptions Options()
        {
            var t = Table;
            return new MatchOptions
            {
                Mode = t?.Mode == "pairs" ? "pairs" : "solo", Hands = Hands, Penalties = t?.Rules?.Penalties ?? true, Isle = t?.Rules?.Isle ?? true,
                Katlama = t?.Katlama ?? false, KatlamaPartner = t?.KatlamaPartner ?? true,
            };
        }

        /// <summary>The controls this seat may use now (G101Rules.Legal on the projection).</summary>
        public LegalMoves Legal()
        {
            if (Seat < 0 || !HasHand || TurnSeat < 0) return new LegalMoves();
            return G101Rules.Legal(Project(), Seat);
        }

        public bool MyTurn => Seat >= 0 && HasHand && Deal == null && Status == G101Rules.StatusPlaying && TurnSeat == Seat;

        /// <summary>Opening notes are known only for a hand this client saw end with the same authoritative result.</summary>
        public bool TryObservedOpenings(IDictionary<string, object> hand, out IReadOnlyList<int> opened)
        {
            opened = null;
            if (hand == null || !hand.TryGetValue("handNo", out var number) || !hand.TryGetValue("finisher", out var finisher) ||
                !hand.TryGetValue("score", out var points) || !observedOpenings.TryGetValue(I(number), out var known) || known.Finisher != I(finisher)) return false;
            var score = Ints(points);
            if (score.Count != known.Score.Length) return false;
            for (int k = 0; k < score.Count; k++) if (score[k] != known.Score[k]) return false;
            opened = known.Opened; return true;
        }

        private void PruneObservedOpenings()
        {
            var retained = new HashSet<int>();
            foreach (var hand in History)
                if (!hand.ContainsKey("opened") && TryObservedOpenings(hand, out _)) retained.Add(I(hand["handNo"]));
            foreach (int number in new List<int>(observedOpenings.Keys)) if (!retained.Contains(number)) observedOpenings.Remove(number);
        }

        public bool IsOkey(string id) => OkeyKnown && G101Rules.IsOkey(new ColorNum(OkeyColor, OkeyNumber), id);

        /// <summary>The face a rack tile shows for rack heuristics: the fake okey stands for the okey, the okey itself is wild (null).</summary>
        public (string C, int N)? FaceOf(string id)
        {
            if (!OkeyKnown) return G101Rules.IsFake(id) ? ((string, int)?)null : (G101Rules.ColorOf(id), G101Rules.NumOf(id));
            var ok = new ColorNum(OkeyColor, OkeyNumber);
            if (G101Rules.IsOkey(ok, id)) return null;
            var r = G101Rules.RealOf(ok, id);
            return (r.C, r.N);
        }

        /// <summary>R-20, R-21, R-64: the opening bar for my seat (katlamalı raises it).</summary>
        public int OpenNeed(string mode) => HasHand && Seat >= 0 ? G101Rules.OpenNeed(Project(), Seat, mode) : mode == G101Rules.KindPair ? G101Rules.Const.OpenPairMin : G101Rules.Const.OpenRunMin;

        public MeldAnalysis Analyze(string kind, IList<string> tiles) => OkeyKnown ? G101Rules.AnalyzeMeld(new ColorNum(OkeyColor, OkeyNumber), kind, tiles) : MeldAnalysis.Bad;

        /// <summary>R-41: the tile would extend a table meld or take an okey's place.</summary>
        public bool IsIslek(string id) => OkeyKnown && HasHand && G101Rules.IsIslek(Project(), id);

        /// <summary>"pre" / "post" ends of a table meld the tile extends (R-25).</summary>
        public List<string> AddSides(string meld, string id) => OkeyKnown && HasHand ? G101Rules.AddSides(Project(), meld, id) : new List<string>();

        /// <summary>R-26: the tile can take the okey at <paramref name="index"/> of the meld.</summary>
        public bool SwapFits(string meld, int index, string id) => OkeyKnown && HasHand && G101Rules.SwapFits(Project(), meld, index, id);

        /// <summary>R-71: the tile the server would discard for my seat now.</summary>
        public string AutoDiscardTile() => OkeyKnown && Seat >= 0 && Rack != null && Rack.Count > 0 ? G101Rules.AutoDiscardTile(Project(), Seat) : null;

        /// <summary>The face an okey stands for at <paramref name="index"/> of a table meld (its position, R-13).</summary>
        public ColorNum Represents(ClientMeld meld, int index)
        {
            var a = Analyze(meld.Kind, meld.Tiles);
            return a.Ok && a.Repr != null && index < a.Repr.Count ? a.Repr[index] : null;
        }

        // ---- Tree helpers ------------------------------------------------------------------------------------------

        public static int I(object v) => v == null ? 0 : Convert.ToInt32(v);

        public static List<int> Ints(object v)
        {
            var l = new List<int>();
            if (v is IList list) foreach (var x in list) l.Add(I(x));
            return l;
        }

        public static List<string> Strings(object v)
        {
            var l = new List<string>();
            if (v is IList list) foreach (var x in list) l.Add(x as string);
            return l;
        }

        public static Dictionary<string, object> Tree(object v)
        {
            if (v == null) return null;
            if (v is Dictionary<string, object> d) return d;
            if (v is IDictionary<string, object> id) return new Dictionary<string, object>(id);
            if (v is LegalMoves legal) return legal.ToTree();
            throw new ArgumentException("Not a tree object: " + v.GetType().Name);
        }

        private static List<object> Bools(bool[] xs) { var l = new List<object>(); foreach (var x in xs) l.Add(x); return l; }
    }
}

using System;
using System.Collections.Generic;
using Galaxy101.Contracts;
using Okey101.Core.V4;

namespace Galaxy101.Table
{
    /// <summary>
    /// What a <see cref="TableRuntime"/> needs from its host. Every call into the runtime and every scheduled action runs on
    /// the host's single thread (the server's hub loop, the client's main thread), so the runtime has no locks.
    /// </summary>
    public interface ITableHost
    {
        /// <summary>The time of the action being run (ms). Scheduled actions see their own due time.</summary>
        long Now { get; }
        /// <summary>Runs <paramref name="action"/> at <paramref name="atMs"/> (on the host thread, with <see cref="Now"/> = atMs).</summary>
        object Schedule(long atMs, Action action);
        void Cancel(object handle);
        /// <summary>
        /// An event for the person at <paramref name="seat"/> (0–3; dropped when no person sits there), or for every spectator (-1).
        /// Names and fields = protocol.json.
        /// </summary>
        void Send(int seat, string type, object payload);
        /// <summary>Engine events just applied (after they were sent): the host counts XP, quests and stats from them.</summary>
        void Applied(TableRuntime table, IReadOnlyList<GameEvent> events);
        /// <summary>The last hand ended the match: the host settles and sends matchResult (<see cref="TableSettlement"/>).</summary>
        void MatchEnded(TableRuntime table);
    }

    /// <summary>A match's fixed setup.</summary>
    public sealed class TableSetup
    {
        public MatchOptions Options = new MatchOptions();
        public uint Seed;
        /// <summary>Turn time (ms), table option 15 / 20 / 30 / 45 s.</summary>
        public int TurnMs = TableTimings.DefaultTurnMs;
        /// <summary>R-63 chat rule "open" | "quick" | "off": bots stay silent when off.</summary>
        public string Chat = "open";
        /// <summary>Tests and scenarios only (package DEMO-17): the first hand's fixed deal.</summary>
        public DealRig FirstHandRig;
    }

    /// <summary>
    /// One match on the v4 rules engine: the table part of the package's <c>logic/mockServer.js</c> without transport. It
    /// schedules the deal ceremony (D-24, dice wait D-23), turns and their timer (R-70 auto move), bot turns (bots.js, D-20),
    /// the server playing a disconnected seat (R-66, D-35, D-36), the hand end wait and gReady (D-25), leaving (R-60) and
    /// taking a seat (R-65), and sends each seat its own view of every event (protocol.json hiddenInfo).
    /// </summary>
    public sealed class TableRuntime
    {
        private readonly ITableHost host;
        private readonly TableSetup setup;
        private readonly RngState chatRng;
        private readonly bool[] botSeat = new bool[4];
        private readonly Dictionary<string, object> timers = new Dictionary<string, object>();
        private DealRun deal;

        private sealed class DealRun
        {
            public string Step;
            public readonly List<int> Dice = new List<int>();
            public int Dealer;
            public long T0;
            public bool Rolling;
        }

        /// <param name="bots">Seats a bot holds from the start.</param>
        /// <param name="chatRng">The table's seeded chat stream (bot lines), shared with the host's chat replies.</param>
        public TableRuntime(ITableHost host, TableSetup setup, bool[] bots, RngState chatRng)
        {
            this.host = host;
            this.setup = setup;
            this.chatRng = chatRng;
            for (var k = 0; k < 4; k++) { botSeat[k] = bots[k]; Connected[k] = true; }
        }

        public MatchState Match { get; private set; }
        public TableSetup Setup => setup;
        /// <summary>Connection per seat (seatConnection.connected).</summary>
        public readonly bool[] Connected = new bool[4];
        /// <summary>The server plays the seat (seatConnection.autoplay, R-66).</summary>
        public readonly bool[] Autoplay = new bool[4];
        public readonly int[] Timeouts = new int[4];
        public readonly HashSet<int> Ready = new HashSet<int>();
        public long TurnDeadline { get; private set; }
        public long NextAt { get; private set; }
        public bool Dealing => deal != null;
        public bool Over => Match != null && Match.Status == G101Rules.StatusMatchOver;
        /// <summary>A person sits here (not a bot, not left).</summary>
        public bool IsHuman(int k) => !botSeat[k];
        /// <summary>mockServer isBotSeat: the server plays the seat (a bot, left, or autoplay).</summary>
        public bool IsBotSeat(int k) => botSeat[k] || Autoplay[k] || (Match != null && Match.Seats[k].Bot);

        // ---- Match start -------------------------------------------------------------------------------------------

        /// <summary>matchStart, then the first hand after preDealCountdown.</summary>
        public void Start(V4Table table)
        {
            Match = G101Rules.NewMatch(setup.Options, setup.Seed);
            for (var k = 0; k < 4; k++) if (botSeat[k]) Match.Seats[k].Bot = true;
            Emit(V4Messages.MatchStart, new V4MatchStart { Table = table, CountdownMs = TableTimings.PreDealCountdown, At = host.Now });
            Arm("pre", host.Now + TableTimings.PreDealCountdown, StartHand);
        }

        private void StartHand()
        {
            Ready.Clear();
            for (var k = 0; k < 4; k++) Timeouts[k] = 0;
            var rig = Match.HandNo == 0 ? setup.FirstHandRig : null;
            var r = G101Rules.Apply(Match, GameAction.StartHand(rig));
            Match = r.State;
            var h = Match.Hand;
            var dealer = h.Deal.Dealer;
            var human = !IsBotSeat(dealer);
            deal = new DealRun { Step = "build", Dealer = dealer, T0 = host.Now };
            Emit(V4Messages.DealStart, new V4DealStart { HandNo = Match.HandNo, Dealer = dealer, BuildMs = TableTimings.DealBuild, HumanDealer = human, At = host.Now });
            // handStart goes out with dice, indicator and okey emptied: the ceremony reveals them in time (hiddenInfo).
            foreach (var e in r.Events) if (e.E == "handStart") Route(new List<GameEvent> { e });
            host.Applied(this, r.Events);
            Arm("deal", host.Now + TableTimings.DealBuild, () =>
            {
                deal.Step = "dice";
                var wait = IsBotSeat(dealer) ? TableTimings.BotRollDelay : TableTimings.HumanRollWait;
                Emit(V4Messages.DealStep, new V4DealStep { Step = "dice", Dealer = dealer, RollWaitMs = wait, At = host.Now });
                Arm("roll", host.Now + wait, Roll);
            });
        }

        /// <summary>gRollDice: the human dealer rolls the next die at once (D-23).</summary>
        public string RollDice(int seat)
        {
            var x = deal;
            if (x == null || x.Step != "dice" || x.Rolling) return V4Errors.E_PHASE;
            if (x.Dealer != seat) return V4Errors.E_NOT_DEALER;
            Roll();
            return null;
        }

        private void Roll()
        {
            var x = deal;
            if (x == null || x.Step != "dice" || x.Rolling) return;
            Disarm("roll");
            var i = x.Dice.Count;
            var value = Match.Hand.Deal.Dice[i];
            var ms = i == 0 ? TableTimings.Roll1 : TableTimings.Roll2;
            x.Rolling = true;
            Emit(V4Messages.DealDie, new V4DealDie { Index = i, Value = value, RollMs = ms, At = host.Now });
            Arm("roll", host.Now + ms, () =>
            {
                x.Rolling = false;
                x.Dice.Add(value);
                if (x.Dice.Count == 2) { DealRest(); return; }
                Arm("roll", host.Now + (IsBotSeat(x.Dealer) ? TableTimings.BetweenRolls : TableTimings.HumanRollWait), Roll);
            });
        }

        private void DealRest()
        {
            var h = Match.Hand;
            var d = h.Deal;
            deal.Step = "afterDice";
            var t = host.Now + TableTimings.AfterDice;
            Arm("d1", t, () =>
            {
                deal.Step = "indicator";
                Emit(V4Messages.DealStep, new V4DealStep
                {
                    Step = "indicator", Stack = d.IndStack, Level = d.IndLevel, RolledLevel = d.RolledLevel, FakeSkip = d.FakeSkip,
                    Indicator = h.Indicator, Okey = Face(h.Okey), At = host.Now,
                });
            });
            t += TableTimings.Indicator;
            Arm("d2", t, () => { deal.Step = "extra"; Emit(V4Messages.DealStep, new V4DealStep { Step = "extra", Stack = d.ExtraStack, At = host.Now }); });
            t += TableTimings.Extra;
            Arm("d3", t, () =>
            {
                deal.Step = "distribute";
                var plan = new List<V4DealPlan>();
                foreach (var p in d.Plan) plan.Add(new V4DealPlan { Seat = p.Seat, Stacks = new List<int>(p.Stacks) });
                Emit(V4Messages.DealStep, new V4DealStep { Step = "distribute", Plan = plan, At = host.Now });
            });
            t += TableTimings.Distribute;
            Arm("d4", t, () =>
            {
                deal.Step = "center";
                Emit(V4Messages.DealStep, new V4DealStep { Step = "center", Stacks = new List<int>(d.CenterStacks), Deck = h.Deck.Count, At = host.Now });
            });
            t += TableTimings.Center;
            Arm("d5", t, () =>
            {
                deal.Step = "fill";
                // Each person gets only their own rack; everyone the counts.
                for (var k = 0; k < 4; k++)
                    if (IsHuman(k)) host.Send(k, V4Messages.HandDealt, Dealt(k));
                PushView();
                host.Send(-1, V4Messages.HandDealt, Dealt(-1));
            });
            t += TableTimings.Fill;
            Arm("d6", t, () => { deal = null; StartTurn(); });
        }

        private V4HandDealt Dealt(int seat)
        {
            var h = Match.Hand;
            return new V4HandDealt
            {
                HandNo = Match.HandNo, Rack = seat >= 0 ? new List<string>(h.Racks[seat]) : null, Counts = Counts(), Deck = h.Deck.Count,
                Indicator = h.Indicator, Okey = Face(h.Okey), Starter = h.Turn.Seat, At = host.Now,
            };
        }

        // ---- Turns -------------------------------------------------------------------------------------------------

        private void StartTurn()
        {
            var turn = Match.Hand.Turn;
            TurnDeadline = host.Now + setup.TurnMs;
            PushView();
            Emit(V4Messages.Turn, new V4Turn { Seat = turn.Seat, Phase = turn.Phase, N = turn.N, TurnMs = setup.TurnMs, Deadline = TurnDeadline, At = host.Now });
            SendLegal(turn.Seat);
            DriveTurn();
        }

        private void SendLegal(int seat)
        {
            if (IsHuman(seat)) host.Send(seat, V4Messages.Legal, new V4Legal { Legal = G101Rules.Legal(Match, seat).ToTree(), At = host.Now });
        }

        private void DriveTurn()
        {
            if (Match.Status != G101Rules.StatusPlaying) return;
            var k = Match.Hand.Turn.Seat;
            Disarm("turn");
            Disarm("bot");
            if (IsBotSeat(k)) { BotStep(null); return; }
            var left = Math.Max(0, TurnDeadline - host.Now);
            Arm("turn", host.Now + left, () => Timeout(k));
        }

        private void BotStep(GameAction prev)
        {
            var s = Match;
            if (s.Status != G101Rules.StatusPlaying) return;
            var k = s.Hand.Turn.Seat;
            var a = G101Bots.NextAction(s, k);
            Arm("bot", host.Now + G101Bots.DelayFor(prev, a), () =>
            {
                if (Match != s || !IsBotSeat(k))
                {
                    // Something changed meanwhile (a seat was taken, the player came back): plan again.
                    if (Match.Status == G101Rules.StatusPlaying && Match.Hand.Turn.Seat == k) DriveTurn();
                    return;
                }
                var r = G101Rules.Apply(s, a);
                if (!r.Ok) r = G101Rules.Apply(s, GameAction.Timeout(k));
                Commit(r);
                if (a.T == "discard" && setup.Chat != "off")
                {
                    var line = G101Bots.ChatAfterDiscard(chatRng);
                    if (line != null) Emit(V4Messages.Chat, new V4Chat { Seat = k, Quick = line, Bot = true, At = host.Now });
                }
            });
        }

        private void Timeout(int k)
        {
            var s = Match;
            if (s.Status != G101Rules.StatusPlaying || s.Hand.Turn.Seat != k) return;
            Timeouts[k]++;
            var r = G101Rules.Apply(s, GameAction.Timeout(k));
            // R-66 / D-35: only a disconnected seat is played by the server after consecutive timeouts.
            if (Timeouts[k] >= TableTimings.AutoplayAfterTimeouts && !Connected[k] && !Autoplay[k])
            {
                Autoplay[k] = true;
                Emit(V4Messages.SeatConnection, new V4SeatConnection { Seat = k, Connected = false, Autoplay = true, At = host.Now });
            }
            Commit(r);
        }

        /// <summary>The engine's result: state, events by visibility, then the next step.</summary>
        private void Commit(ApplyResult r)
        {
            if (!r.Ok) return;
            Match = r.State;
            Route(r.Events);
            PushView();
            host.Applied(this, r.Events);
            var s = Match;
            if (s.Status == G101Rules.StatusHandOver || s.Status == G101Rules.StatusMatchOver) { HandOver(); return; }
            var turnChanged = false;
            foreach (var e in r.Events) if (e.E == "turn") turnChanged = true;
            if (turnChanged) StartTurn();
            else if (IsBotSeat(s.Hand.Turn.Seat)) BotStep(LastAction(r));
            else SendLegal(s.Hand.Turn.Seat);
        }

        private static GameAction LastAction(ApplyResult r)
        {
            if (r.Events.Count == 0) return null;
            return new GameAction { T = r.Events[r.Events.Count - 1].E == "drew" ? "draw" : "open" };
        }

        /// <summary>
        /// A person's move (gDraw, gReturnSide, gOpen, gLay, gAdd, gSwap, gUndo, gDiscard). Null when applied, else the engine
        /// error with its detail. A move ends the server's play of the seat (D-36): the turn time starts over.
        /// </summary>
        public string Move(int seat, GameAction a, out Dictionary<string, object> detail)
        {
            detail = null;
            if (Match == null) return V4Errors.E_NOT_SEATED;
            if (deal != null) return V4Errors.E_DEALING;
            a.Seat = seat;
            var r = G101Rules.Apply(Match, a);
            if (!r.Ok) { detail = r.Detail; return r.Error; }
            var wasAuto = Autoplay[seat];
            Timeouts[seat] = 0;
            Autoplay[seat] = false;
            if (wasAuto) Emit(V4Messages.SeatConnection, new V4SeatConnection { Seat = seat, Connected = Connected[seat], Autoplay = false, At = host.Now });
            var before = Match;
            Commit(r);
            if (wasAuto && Match.Status == G101Rules.StatusPlaying && deal == null && Match.Hand.Turn.Seat == seat && Match.Hand.Turn.N == before.Hand.Turn.N)
                StartTurn();
            return null;
        }

        // ---- Hand end ----------------------------------------------------------------------------------------------

        private void HandOver()
        {
            Disarm("turn");
            Disarm("bot");
            if (Match.Status == G101Rules.StatusMatchOver) { host.MatchEnded(this); return; }
            NextAt = host.Now + TableTimings.HandEndWait;
            Emit(V4Messages.HandWait, new V4HandWait { NextHandIn = TableTimings.HandEndWait, NextAt = NextAt, HandNo = Match.HandNo, Hands = Match.Opts.Hands, At = host.Now });
            Arm("next", NextAt, StartHand);
        }

        /// <summary>gReady ("Sonraki el"): when every person at the table is ready the 15 s are not waited (D-25).</summary>
        public string ReadyUp(int seat)
        {
            if (Match == null || Match.Status != G101Rules.StatusHandOver) return V4Errors.E_PHASE;
            Ready.Add(seat);
            var all = true;
            for (var k = 0; k < 4; k++) if (IsHuman(k) && !Ready.Contains(k)) all = false;
            var ready = new List<int>(Ready);
            ready.Sort();
            Emit(V4Messages.ReadyState, new V4ReadyState { Ready = ready, At = host.Now });
            if (all) { Disarm("next"); StartHand(); }
            return null;
        }

        /// <summary>gRackOrder: the arrangement is the client's; only the set of tiles is checked.</summary>
        public string RackOrder(int seat, IList<string> tiles)
        {
            if (Match?.Hand == null) return V4Errors.E_RACK_MISMATCH;
            var have = new List<string>(Match.Hand.Racks[seat]);
            var got = new List<string>();
            if (tiles != null) foreach (var t in tiles) if (t != null) got.Add(t);
            have.Sort(string.CompareOrdinal);
            got.Sort(string.CompareOrdinal);
            return string.Join(",", have) == string.Join(",", got) ? null : V4Errors.E_RACK_MISMATCH;
        }

        // ---- Seats: leaving, taking, connection --------------------------------------------------------------------

        /// <summary>R-60: the player leaves the running match; a bot plays the seat from now on. Null or the engine error.</summary>
        public string Leave(int seat)
        {
            var r = G101Rules.Apply(Match, GameAction.Leave(seat));
            if (!r.Ok) return r.Error;
            Match = r.State;
            botSeat[seat] = true;
            Autoplay[seat] = false;
            Connected[seat] = true;
            Ready.Remove(seat);
            Route(r.Events);
            host.Applied(this, r.Events);
            if (Match.Status == G101Rules.StatusPlaying && deal == null && Match.Hand.Turn.Seat == seat) DriveTurn();
            if (Match.Status == G101Rules.StatusHandOver) CheckAllReady();
            return null;
        }

        private void CheckAllReady()
        {
            if (Ready.Count == 0) return;
            for (var k = 0; k < 4; k++) if (IsHuman(k) && !Ready.Contains(k)) return;
            Disarm("next");
            StartHand();
        }

        /// <summary>R-65: a person takes a bot's or a leaver's seat and plays on from its total. Null or the engine error.</summary>
        public string TakeSeat(int seat)
        {
            var r = G101Rules.Apply(Match, GameAction.TakeSeat(seat));
            if (!r.Ok) return r.Error;
            Match = r.State;
            Disarm("bot");
            botSeat[seat] = false;
            Connected[seat] = true;
            Timeouts[seat] = 0;
            Autoplay[seat] = false;
            Route(r.Events);
            host.Applied(this, r.Events);
            if (Match.Status == G101Rules.StatusPlaying && deal == null)
            {
                if (Match.Hand.Turn.Seat == seat) StartTurn();
                else DriveTurn();
            }
            return null;
        }

        /// <summary>hello: the person is back; the server stops playing the seat and a running turn starts over (D-36).</summary>
        public void Reconnect(int seat)
        {
            Connected[seat] = true;
            Timeouts[seat] = 0;
            if (!Autoplay[seat]) return;
            Autoplay[seat] = false;
            if (Match != null && Match.Status == G101Rules.StatusPlaying && deal == null && Match.Hand.Turn.Seat == seat) StartTurn();
        }

        /// <summary>seatConnection to the table (after the returning player got their snapshot).</summary>
        public void AnnounceSeat(int seat) =>
            Emit(V4Messages.SeatConnection, new V4SeatConnection { Seat = seat, Connected = Connected[seat], Autoplay = Autoplay[seat], At = host.Now });

        public void Disconnect(int seat)
        {
            Connected[seat] = false;
            Emit(V4Messages.SeatConnection, new V4SeatConnection { Seat = seat, Connected = false, At = host.Now });
        }

        /// <summary>Stops every timer (the table closes).</summary>
        public void Stop()
        {
            foreach (var handle in timers.Values) host.Cancel(handle);
            timers.Clear();
        }

        // ---- Bot chat (D-20) ---------------------------------------------------------------------------------------

        /// <summary>A person wrote: 60 % one of the bots answers after 1400 ms.</summary>
        public void BotsHeard(int fromSeat) => BotChat.Reply(host, chatRng, fromSeat, IsBotSeat, () => true);

        // ---- Views (protocol.json hiddenInfo) ----------------------------------------------------------------------

        /// <summary>The engine event as <paramref name="seat"/> may see it (-1: a spectator).</summary>
        public static Dictionary<string, object> ViewEvent(GameEvent e, int seat)
        {
            var v = e.ToTree();
            if (e.E == "drew" && e.Str("src") == "deck" && e.Int("seat") != seat) v["tile"] = null;
            if (e.E == "openShort" && e.Int("seat") != seat) v["tiles"] = null;
            if (e.E == "handStart") { v["dice"] = null; v["indicator"] = null; v["okey"] = null; }
            return v;
        }

        private void Route(IReadOnlyList<GameEvent> events)
        {
            foreach (var e in events)
            {
                for (var k = 0; k < 4; k++)
                    if (IsHuman(k)) host.Send(k, V4Messages.Game, new V4GameEvent { Ev = ViewEvent(e, k), At = host.Now });
                host.Send(-1, V4Messages.Game, new V4GameEvent { Ev = ViewEvent(e, -1), At = host.Now });
            }
        }

        /// <summary>tableView: after every applied step each person gets their own view (spectators the public one).</summary>
        private void PushView()
        {
            for (var k = 0; k < 4; k++)
                if (IsHuman(k)) host.Send(k, V4Messages.TableView, new V4TableView { View = GameView(k), At = host.Now });
            host.Send(-1, V4Messages.TableView, new V4TableView { View = GameView(-1), At = host.Now });
        }

        /// <summary>mockServer gameView(table, seat): snapshot.game and tableView.view. Never another seat's rack or the deck order.</summary>
        public Dictionary<string, object> GameView(int seat)
        {
            var s = Match;
            if (s == null || s.Hand == null)
                return new Dictionary<string, object>
                {
                    { "status", s == null ? "none" : s.Status }, { "handNo", s == null ? 0 : s.HandNo }, { "totals", V4Tree.Ints(s == null ? new int[4] : s.Totals) },
                };
            var h = s.Hand;
            var hidden = deal != null && (deal.Step == "build" || deal.Step == "dice" || deal.Step == "afterDice");
            var rackShown = seat >= 0 && (deal == null || deal.Step == "fill");
            var tree = s.ToTree();
            var hand = h.ToTree();
            Dictionary<string, object> turn = null;
            if (h.Turn != null)
                turn = new Dictionary<string, object>
                {
                    { "seat", h.Turn.Seat }, { "phase", h.Turn.Phase }, { "n", h.Turn.N }, { "sideTake", h.Turn.SideTake }, { "sideMust", h.Turn.SideMust },
                    { "deadline", TurnDeadline },
                };
            return new Dictionary<string, object>
            {
                { "status", s.Status }, { "handNo", s.HandNo }, { "hands", s.Opts.Hands }, { "dealer", s.Dealer }, { "totals", tree["totals"] },
                { "history", tree["history"] }, { "seats", tree["seats"] },
                { "deal", deal == null ? null : new Dictionary<string, object>
                    { { "step", deal.Step }, { "dice", V4Tree.Ints(deal.Dice) }, { "dealer", deal.Dealer }, { "elapsed", host.Now - deal.T0 } } },
                { "indicator", hidden ? null : h.Indicator }, { "okey", hidden ? null : h.Okey?.ToTree() },
                { "autoplay", Bools(Autoplay) }, { "connected", Bools(Connected) }, { "deck", h.Deck.Count },
                { "rack", rackShown ? V4Tree.Strings(h.Racks[seat]) : null }, { "counts", V4Tree.Ints(Counts()) },
                { "discards", hand["discards"] }, { "melds", hand["melds"] }, { "opened", hand["opened"] }, { "openMode", hand["openMode"] }, { "pen", hand["pen"] },
                { "turn", turn }, { "legal", seat >= 0 ? G101Rules.Legal(s, seat).ToTree() : null }, { "result", hand["result"] },
                { "racksRevealed", h.Result != null ? hand["racks"] : null }, { "nextAt", s.Status == G101Rules.StatusHandOver ? (object)NextAt : null },
            };
        }

        private List<int> Counts()
        {
            var counts = new List<int>();
            foreach (var r in Match.Hand.Racks) counts.Add(r.Count);
            return counts;
        }

        private static List<object> Bools(bool[] xs) { var l = new List<object>(); foreach (var x in xs) l.Add(x); return l; }

        private static V4OkeyFace Face(ColorNum c) => c == null ? null : new V4OkeyFace { C = c.C, N = c.N };

        // ---- Helpers -----------------------------------------------------------------------------------------------

        /// <summary>To every person seated and every spectator.</summary>
        private void Emit(string type, object payload)
        {
            for (var k = 0; k < 4; k++) if (IsHuman(k)) host.Send(k, type, payload);
            host.Send(-1, type, payload);
        }

        private void Arm(string name, long atMs, Action action)
        {
            Disarm(name);
            object handle = null;
            handle = host.Schedule(atMs, () =>
            {
                if (timers.TryGetValue(name, out var current) && current == handle) timers.Remove(name);
                action();
            });
            timers[name] = handle;
        }

        private void Disarm(string name)
        {
            if (timers.TryGetValue(name, out var handle)) { host.Cancel(handle); timers.Remove(name); }
        }
    }

    /// <summary>Bot table talk outside the turn loop (D-20): answers to a person's line and thanks for a gift.</summary>
    public static class BotChat
    {
        /// <summary>60 %: a random bot seat (not the writer) answers with a random line after 1400 ms.</summary>
        public static void Reply(ITableHost host, RngState rng, int fromSeat, Func<int, bool> isBot, Func<bool> stillOpen)
        {
            if (G101Rules.RngNext(rng) / 4294967296.0 >= G101Bots.Chat.ReplyChance) return;
            var bots = new List<int>();
            for (var k = 0; k < 4; k++) if (k != fromSeat && isBot(k)) bots.Add(k);
            if (bots.Count == 0) return;
            var who = bots[G101Rules.RngInt(rng, bots.Count)];
            host.Schedule(host.Now + G101Bots.Chat.ReplyDelay, () =>
            {
                // The line is drawn when it is said, as in mockServer chatSend.
                if (stillOpen()) Broadcast(host, V4Messages.Chat, new V4Chat { Seat = who, Quick = G101Bots.Chat.Lines[G101Rules.RngInt(rng, G101Bots.Chat.Lines.Count)], Bot = true, At = host.Now });
            });
        }

        /// <summary>A bot that got a gift thanks after 1900 ms (6300 ms for the rocket).</summary>
        public static void Thanks(ITableHost host, int seat, string gift, Func<bool> stillOpen) =>
            host.Schedule(host.Now + (gift == "rocket" ? G101Bots.Chat.ThanksDelayRocket : G101Bots.Chat.ThanksDelay), () =>
            {
                if (stillOpen()) Broadcast(host, V4Messages.Chat, new V4Chat { Seat = seat, Quick = "bot.thanks", Bot = true, At = host.Now });
            });

        private static void Broadcast(ITableHost host, string type, object payload)
        {
            for (var k = 0; k < 4; k++) host.Send(k, type, payload);
            host.Send(-1, type, payload);
        }
    }
}

using System;
using System.Collections.Generic;
using Galaxy101.Contracts;
using Okey101.Core.V4;

namespace Galaxy101.Table
{
    /// <summary>
    /// The offline table's host for a <see cref="TableRuntime"/> (K-09: the server's match code, run on the client): a
    /// logical clock the owner advances (the Unity table from <c>GameClock</c>, tests by hand), the runtime's timers on it
    /// (each runs with <see cref="Now"/> = its due time, as on the server's hub loop), and the messages for the person's
    /// seat queued and delivered to the client model after each runtime call, never from inside one. Against bots (R-94) no
    /// chips move; the person still earns XP (D-42: hand XP R-93 on every handEnd, the place bonus with matchResult).
    /// </summary>
    public sealed class LocalTableHost : ITableHost
    {
        private sealed class Timer { public long Due, Seq; public Action Work; public bool Cancelled; }

        private readonly List<Timer> timers = new List<Timer>();
        private readonly Queue<(string Type, object Payload)> outbox = new Queue<(string, object)>();
        private long seq;
        private bool flushing;

        /// <param name="seat">The person's seat (the others are bots).</param>
        /// <param name="start">Logical time of the table's start (ms).</param>
        public LocalTableHost(int seat, long start) { Seat = seat; Now = start; }

        public int Seat { get; }
        public long Now { get; private set; }
        public TableRuntime Runtime { get; private set; }
        /// <summary>The model of the person's seat; every message for the seat is applied to it.</summary>
        public TableClient Client { get; private set; }
        /// <summary>Every message for the person's seat, after the client applied it.</summary>
        public event Action<string, object> Delivered;
        /// <summary>XP the person earned (amount, "hand" | "match"), R-93 / D-42.</summary>
        public event Action<long, string> Xp;
        /// <summary>The match is over and matchResult was sent.</summary>
        public event Action Ended;
        /// <summary>XP values (R-93): finisher, everyone else, place bonus.</summary>
        public int XpHandWin = TableEconomy.XpHandWin, XpHand = TableEconomy.XpHand;
        public int[] XpPlace = TableEconomy.XpPlace;
        public long XpEarned { get; private set; }

        /// <summary>
        /// Creates the runtime (the person at <see cref="Seat"/>, bots elsewhere) and the seat's client, then starts the match
        /// (matchStart, the first deal after preDealCountdown).
        /// </summary>
        public TableRuntime Start(TableSetup setup, V4Table table)
        {
            var bots = new bool[4];
            for (int k = 0; k < 4; k++) bots[k] = k != Seat;
            Client = new TableClient(Seat);
            Runtime = new TableRuntime(this, setup, bots, new RngState(setup.Seed ^ 0x9E3779B9u));
            Runtime.Start(table);
            Flush();
            return Runtime;
        }

        // ---- ITableHost ---------------------------------------------------------------------------------------------

        public object Schedule(long atMs, Action action)
        {
            var t = new Timer { Due = Math.Max(atMs, Now), Seq = ++seq, Work = action };
            int i = timers.Count;
            while (i > 0 && (timers[i - 1].Due > t.Due || timers[i - 1].Due == t.Due && timers[i - 1].Seq > t.Seq)) i--;
            timers.Insert(i, t);
            return t;
        }

        public void Cancel(object handle) { if (handle is Timer t) t.Cancelled = true; }

        public void Send(int seat, string type, object payload)
        {
            if (seat != Seat) return;
            outbox.Enqueue((type, payload));
        }

        public void Applied(TableRuntime table, IReadOnlyList<GameEvent> events)
        {
            foreach (var e in events)
            {
                if (e.E != "handEnd") continue;
                var result = (Dictionary<string, object>)e["result"];
                int finisher = Convert.ToInt32(result["finisher"]);
                long gained = finisher == Seat ? XpHandWin : XpHand;
                XpEarned += gained;
                Xp?.Invoke(gained, "hand");
            }
        }

        public void MatchEnded(TableRuntime table) => Schedule(Now, () => Settle(table));

        /// <summary>R-94: no pool and no weekly points against bots; the place bonus XP (R-93) only.</summary>
        private void Settle(TableRuntime table)
        {
            var s = table.Match;
            var person = new bool[4];
            person[Seat] = !s.Seats[Seat].Left;
            var shares = TableSettlement.Compute(s.Ranking, person, 0, null, null, XpPlace);
            var ranking = new List<object>();
            foreach (var u in s.Ranking) ranking.Add(u.ToTree());
            Send(Seat, V4Messages.MatchResult, new V4MatchResult
            {
                Ranking = ranking, Totals = new List<int>(s.Totals), History = s.ToTree()["history"], Pool = 0, Rake = TableEconomy.Rake,
                Payouts = new List<long>(shares.Payouts), XpBonus = new List<int>(shares.XpBonus), Lp = new List<int?>(shares.Lp), Weekly = new List<int>(shares.Weekly), At = Now,
            });
            if (shares.XpBonus[Seat] > 0) { XpEarned += shares.XpBonus[Seat]; Xp?.Invoke(shares.XpBonus[Seat], "match"); }
            table.Stop();
            Flush();
            Ended?.Invoke();
        }

        // ---- Driving ------------------------------------------------------------------------------------------------

        /// <summary>The next timer's due time, or null when none is armed.</summary>
        public long? NextDue
        {
            get
            {
                foreach (var t in timers) if (!t.Cancelled) return t.Due;
                return null;
            }
        }

        /// <summary>Runs every timer due by <paramref name="now"/> in order (each at its own time), then moves the clock to now.</summary>
        public void Advance(long now)
        {
            while (true)
            {
                Timer next = null;
                while (timers.Count > 0 && timers[0].Cancelled) timers.RemoveAt(0);
                if (timers.Count > 0 && timers[0].Due <= now) { next = timers[0]; timers.RemoveAt(0); }
                if (next == null) break;
                Now = Math.Max(Now, next.Due);
                next.Work();
                Flush();
            }
            Now = Math.Max(Now, now);
        }

        /// <summary>Runs the timers due by the next due time (one step of the server's loop). False when nothing is armed.</summary>
        public bool Step()
        {
            var due = NextDue;
            if (!due.HasValue) return false;
            Advance(due.Value);
            return true;
        }

        /// <summary>
        /// A move of the person (gDraw … gDiscard): null when the engine took it, else its error code. Runs on the
        /// logical clock's current time; the events reach the client before this returns.
        /// </summary>
        public string Move(GameAction action, out Dictionary<string, object> detail)
        {
            var error = Runtime.Move(Seat, action, out detail);
            Flush();
            return error;
        }

        public string Move(GameAction action) => Move(action, out _);

        public string RollDice() { var e = Runtime.RollDice(Seat); Flush(); return e; }
        public string Ready() { var e = Runtime.ReadyUp(Seat); Flush(); return e; }
        public string RackOrder(IList<string> tiles) => Runtime.RackOrder(Seat, tiles);

        /// <summary>A line the person said: the bots may answer (D-20).</summary>
        public void Chat(string text, string quick)
        {
            Send(Seat, V4Messages.Chat, new V4Chat { Seat = Seat, Text = text, Quick = quick, At = Now });
            Runtime.BotsHeard(Seat);
            Flush();
        }

        /// <summary>A gift the person sent (free against bots); a bot that got one thanks (D-20).</summary>
        public void Gift(string gift, IList<int> to)
        {
            Send(Seat, V4Messages.Gift, new V4Gift { From = Seat, To = new List<int>(to), Gift = gift, Cost = 0, At = Now });
            foreach (var k in to) if (k != Seat && Runtime.IsBotSeat(k)) BotChat.Thanks(this, k, gift, () => !Runtime.Over);
            Flush();
        }

        /// <summary>Masadan kalk (R-60): a bot plays the seat to the end.</summary>
        public string Leave() { var e = Runtime.Leave(Seat); Flush(); return e; }

        public void Stop() { Runtime?.Stop(); timers.Clear(); outbox.Clear(); }

        /// <summary>Delivers the queued messages to the client (re-entrant calls from a handler queue behind).</summary>
        public void Flush()
        {
            if (flushing) return;
            flushing = true;
            try
            {
                while (outbox.Count > 0)
                {
                    var (type, payload) = outbox.Dequeue();
                    Client?.Apply(type, payload);
                    Delivered?.Invoke(type, payload);
                }
            }
            finally { flushing = false; }
        }

        /// <summary>
        /// Test and scenario seam (package DEMO-17/18, dev only): the match continues from <paramref name="state"/> (a hand
        /// in progress built by <see cref="G101Rules.FromSpec"/>) at its turn. The runtime has no public entry for this, so
        /// it is set through its private members (docs/v4/table/EKSIKLER.md EK-503); the seat gets the server's view.
        /// </summary>
        public void ResumeFrom(MatchState state)
        {
            var rt = Runtime;
            rt.Stop();
            var type = typeof(TableRuntime);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            type.GetProperty("Match", flags).GetSetMethod(true).Invoke(rt, new object[] { state });
            type.GetField("deal", flags)?.SetValue(rt, null);
            type.GetMethod("StartTurn", flags).Invoke(rt, null);
            Flush();
        }
    }
}

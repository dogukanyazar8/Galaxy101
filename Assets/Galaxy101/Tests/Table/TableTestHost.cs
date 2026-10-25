using System;
using System.Collections.Generic;
using Galaxy101.Contracts;
using Okey101.Core.V4;

namespace Galaxy101.Table.Tests
{
    /// <summary>
    /// A server-like host for a <see cref="TableRuntime"/> in tests: logical time (a timer runs at its due time), one
    /// message queue per seat (and the spectators', -1), delivered in order to that seat's <see cref="TableClient"/>.
    /// </summary>
    public sealed class TableTestHost : ITableHost
    {
        private sealed class Timer { public long Due, Seq; public Action Work; public bool Cancelled; }

        private readonly List<Timer> timers = new List<Timer>();
        private readonly Dictionary<int, Queue<(string Type, object Payload)>> inbox = new Dictionary<int, Queue<(string, object)>>();
        private long seq;

        public long Now { get; set; }
        public readonly Dictionary<int, TableClient> Clients = new Dictionary<int, TableClient>();
        public bool Ended { get; private set; }
        public int Sent { get; private set; }
        /// <summary>Every message as sent (seat, type, payload): the wire round trip test replays them.</summary>
        public readonly List<(int Seat, string Type, object Payload)> Log = new List<(int, string, object)>();

        public TableTestHost(long start) { Now = start; }

        public TableClient Watch(int seat, bool trustViews = false)
        {
            var c = new TableClient(seat) { TrustViews = trustViews };
            Clients[seat] = c;
            inbox[seat] = new Queue<(string, object)>();
            return c;
        }

        public void Unwatch(int seat) { Clients.Remove(seat); inbox.Remove(seat); }

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
            Sent++;
            Log.Add((seat, type, payload));
            if (inbox.TryGetValue(seat, out var q)) q.Enqueue((type, payload));
        }

        public void Applied(TableRuntime table, IReadOnlyList<GameEvent> events) { }
        public void MatchEnded(TableRuntime table) => Ended = true;

        public long? NextDue
        {
            get
            {
                foreach (var t in timers) if (!t.Cancelled) return t.Due;
                return null;
            }
        }

        /// <summary>Runs every timer due at the earliest due time (also those it schedules for the same time). False when none is armed.</summary>
        public bool StepTime()
        {
            var due = NextDue;
            if (!due.HasValue) return false;
            Now = Math.Max(Now, due.Value);
            while (true)
            {
                while (timers.Count > 0 && timers[0].Cancelled) timers.RemoveAt(0);
                if (timers.Count == 0 || timers[0].Due > Now) break;
                var t = timers[0]; timers.RemoveAt(0);
                t.Work();
            }
            return true;
        }

        /// <summary>Delivers every queued message to its client.</summary>
        public void Deliver()
        {
            foreach (var pair in inbox)
            {
                if (!Clients.TryGetValue(pair.Key, out var c)) { pair.Value.Clear(); continue; }
                while (pair.Value.Count > 0) { var (type, payload) = pair.Value.Dequeue(); c.Apply(type, payload); }
            }
        }

        public static V4Table Table(MatchOptions o, int turnSec = 30, bool vsBots = false, bool[] bots = null)
        {
            var t = new V4Table
            {
                Id = "t1", Name = "Test", Mode = o.Mode, Hands = o.Hands, Bet = 0, Katlama = o.Katlama, KatlamaPartner = o.IsPairs && o.Katlama ? o.KatlamaPartner : (bool?)null,
                TurnSec = turnSec, Rules = new V4TableRules { Isle = o.Isle, Penalties = o.Penalties }, VsBots = vsBots, Status = "playing",
            };
            for (int k = 0; k < 4; k++) t.Seats.Add(new V4Seat { User = new V4User { Id = "u" + k, Name = "P" + k }, Bot = bots != null && bots[k] });
            return t;
        }
    }
}

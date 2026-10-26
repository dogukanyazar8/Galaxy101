using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Okey101.Core.V4;

namespace Galaxy101.Table
{
    /// <summary>
    /// What the table plays against: the v4 match flow (K-09) on this device (<see cref="LocalTableSession"/>, the server's
    /// TableRuntime with bots) or on the server (Galaxy101.Online.V4TableSession, the v4 protocol on the socket). Either way
    /// the table reads only <see cref="Client"/> — the model of its seat built from the S2C messages — and sends its intents
    /// as the protocol's step messages; every answer is the engine's (null = accepted, else the error code).
    /// </summary>
    public interface ITableSession : IDisposable
    {
        TableClient Client { get; }
        /// <summary>The server's clock now (ms), for deadlines and the deal's progress.</summary>
        long ServerNow { get; }
        bool Online { get; }
        /// <summary>Per frame on the main thread (the offline runtime's timers run here).</summary>
        void Tick();
        Task<string> RollDice();
        Task<string> Draw(string src);
        Task<string> ReturnSide();
        Task<string> Open(List<MeldSpec> melds);
        Task<string> Lay(List<MeldSpec> melds);
        Task<string> Add(string tile, string meld, string side);
        Task<string> Swap(string tile, string meld, int index);
        Task<string> Undo();
        Task<string> Discard(string tile);
        Task<string> Ready();
        Task<string> RackOrder(IList<string> tiles);
        Task<string> Chat(string text, string quick);
        /// <param name="to">A seat (absolute) or null for everyone ("all").</param>
        Task<string> Gift(string gift, int? to);
        Task<string> Leave();
    }

    /// <summary>
    /// Offline (Botlara karşı, the state router's Test masası): the server's match code with three bots on a logical clock
    /// driven by the shared GameClock (D-60), so a frozen clock freezes the table and a state opened twice plays the same.
    /// </summary>
    public sealed class LocalTableSession : ITableSession
    {
        private readonly Func<double> clock;
        private readonly Func<bool> paused;
        private double lastClock;

        /// <param name="clock">Milliseconds of the shared clock (GameClock.NowMs); only its forward steps count.</param>
        /// <param name="paused">The clock is frozen (D-61): the match stands still and the freeze's jump to the canonical instant is not played.</param>
        public LocalTableSession(TableSetup setup, V4Table table, Func<double> clock, int seat = 0, Func<bool> paused = null)
        {
            this.clock = clock; this.paused = paused;
            lastClock = clock();
            Host = new LocalTableHost(seat, 1_000_000);
            Setup = setup; Table = table;
        }

        public LocalTableHost Host { get; }
        public TableSetup Setup { get; }
        public V4Table Table { get; }
        public TableClient Client => Host.Client;
        public long ServerNow => Host.Now;
        public bool Online => false;
        public bool Started { get; private set; }

        /// <summary>matchStart: the 3-2-1 and then the first deal (or, with <paramref name="skipCountdown"/>, the deal at once).</summary>
        public void Begin(bool skipCountdown = false)
        {
            if (Started) return;
            Started = true;
            Host.Start(Setup, Table);
            if (skipCountdown) Host.Advance(Host.Now + TableTimings.PreDealCountdown);
        }

        /// <summary>Dev/state flows: the hand continues from a prepared state at its turn (TableScenarios).</summary>
        public void Resume(MatchState state) { if (!Started) Begin(); Host.ResumeFrom(state); }

        public void Tick()
        {
            double now = clock();
            double delta = now - lastClock; lastClock = now;
            if (!Started || delta <= 0 || paused != null && paused()) return;   // a reset clock never runs time backwards; a frozen one not at all
            Host.Advance(Host.Now + (long)Math.Round(delta));
        }

        /// <summary>Jumps the table's time (the router's fast-forward); timers due on the way run in order.</summary>
        public void Skip(long ms) { if (Started && ms > 0) Host.Advance(Host.Now + ms); }

        private static Task<string> Done(string error) => Task.FromResult(error);

        public Task<string> RollDice() => Done(Host.RollDice());
        public Task<string> Draw(string src) => Done(Host.Move(GameAction.Draw(Host.Seat, src)));
        public Task<string> ReturnSide() => Done(Host.Move(GameAction.ReturnSide(Host.Seat)));
        public Task<string> Open(List<MeldSpec> melds) => Done(Host.Move(GameAction.Open(Host.Seat, melds)));
        public Task<string> Lay(List<MeldSpec> melds) => Done(Host.Move(GameAction.Lay(Host.Seat, melds)));
        public Task<string> Add(string tile, string meld, string side) => Done(Host.Move(GameAction.Add(Host.Seat, tile, meld, side)));
        public Task<string> Swap(string tile, string meld, int index) => Done(Host.Move(GameAction.Swap(Host.Seat, tile, meld, index)));
        public Task<string> Undo() => Done(Host.Move(GameAction.Undo(Host.Seat)));
        public Task<string> Discard(string tile) => Done(Host.Move(GameAction.Discard(Host.Seat, tile)));
        public Task<string> Ready() => Done(Host.Ready());
        public Task<string> RackOrder(IList<string> tiles) => Done(Host.RackOrder(tiles));
        public Task<string> Chat(string text, string quick) { Host.Chat(text, quick); return Done(null); }

        public Task<string> Gift(string gift, int? to)
        {
            var list = new List<int>();
            if (to.HasValue) list.Add(to.Value); else for (int k = 0; k < 4; k++) if (k != Host.Seat) list.Add(k);
            Host.Gift(gift, list);
            return Done(null);
        }

        public Task<string> Leave() => Done(Host.Leave());

        public void Dispose() => Host.Stop();
    }
}

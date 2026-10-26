using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Galaxy101.Table;
using Newtonsoft.Json.Linq;
using Okey101.Core.V4;
using UnityEngine;

namespace Galaxy101.Online
{
    /// <summary>
    /// A server match on the v4 protocol (K-04: the existing socket, frames <c>{type, id, data}</c>, an ack per request):
    /// every table message (protocol.json s2c: matchStart … tableView, snapshot) goes into the seat's
    /// <see cref="TableClient"/>, every move is its step message (gRollDice gDraw gReturnSide gOpen gLay gAdd gSwap gUndo
    /// gDiscard gReady gRackOrder, chatSend, giftSend, tableLeave). When the socket drops the session reconnects the way
    /// protocol.json reconnect says: "Bağlantı koptu · Deneme n/5", 3 s apart, each a fresh socket and <c>hello</c> (the
    /// server answers session + snapshot and stops playing the seat); after the 5th the table offers "Lobiye dön".
    /// </summary>
    public sealed class V4TableSession : ITableSession
    {
        /// <summary>The match being played (or watched); null between matches.</summary>
        public static V4TableSession Current { get; private set; }
        /// <summary>A server match started (matchStart); the menu opens the table with it.</summary>
        public static event Action<V4TableSession> Started;

        private static GalaxyOnline watched;
        private static Action unwatch;

        /// <summary>Listen for matchStart on the session's socket (waiting room, invite, ranked, vsBotsStart): each starts a session.</summary>
        public static void Watch(GalaxyOnline online)
        {
            if (online == null || watched == online) return;
            unwatch?.Invoke();
            watched = online;
            unwatch = online.Realtime.On(V4Messages.MatchStart, data =>
            {
                if (Current != null && !Current.Closed && Current.Client.Status != G101Rules.StatusMatchOver) return;   // its own subscription has it
                var session = new V4TableSession(online);
                session.Receive(V4Messages.MatchStart, data);
                Started?.Invoke(session);
                UnityEngine.Object.FindAnyObjectByType<MainMenuController>()?.OnMatchStarted(session);
            });
        }

        private readonly GalaxyOnline online;
        private readonly List<Action> subscriptions = new List<Action>();
        private long offset;
        private bool offsetKnown;
        private string me;

        public V4TableSession(GalaxyOnline online)
        {
            this.online = online;
            Client = new TableClient(-1);
            me = online.Profile?.Id.ToString();
            Current = this;
            // matchStart reaches the session through Watch (it creates the session for it).
            foreach (var type in V4Wire.TableMessages)
                if (type != V4Messages.MatchStart) { var t = type; subscriptions.Add(online.Realtime.On(t, data => Receive(t, data))); }
            subscriptions.Add(online.Realtime.On(V4Messages.Session, data => { var s = (V4Session)V4Wire.Payload(V4Messages.Session, data); if (s?.Me?.Id != null) me = s.Me.Id; Clock(s?.ServerTime ?? 0); }));
            online.Realtime.Closed += OnClosed;
            subscriptions.Add(() => online.Realtime.Closed -= OnClosed);
        }

        public TableClient Client { get; }
        public bool Online => true;
        public bool Closed { get; private set; }
        public long ServerNow => (long)(Time.realtimeSinceStartupAsDouble * 1000) + offset;
        /// <summary>Reconnecting: the attempt (1–5); 0 when connected.</summary>
        public int Attempt { get; private set; }
        public bool Dropped { get; private set; }
        /// <summary>The fifth attempt failed: only "Lobiye dön" is left.</summary>
        public bool GaveUp { get; private set; }
        public event Action ConnectionChanged;

        public void Tick() { }

        /// <summary>One table message as the socket delivered it (main thread).</summary>
        public void Receive(string type, JToken data)
        {
            if (Closed) return;
            if (data is JObject o && o["at"] != null) Clock((long)o["at"]);
            var payload = V4Wire.Payload(type, data);
            if (payload is V4MatchStart start) SeatFrom(start.Table, false);
            if (payload is V4Snapshot snap)
            {
                if (snap.Screen != "table") { Close(); return; }
                SeatFrom(snap.Table, snap.Spectator == true);
            }
            Client.Apply(type, payload);
            if (type == V4Messages.LeftTable || type == V4Messages.TableClosed) Close();
        }

        /// <summary>My seat at the table: the seat whose user is me (a spectator has none).</summary>
        private void SeatFrom(V4Table table, bool spectator)
        {
            if (table?.Seats == null) return;
            int seat = -1;
            if (!spectator && me != null)
                for (int k = 0; k < table.Seats.Count; k++)
                    if (table.Seats[k]?.User != null && string.Equals(table.Seats[k].User.Id, me, StringComparison.OrdinalIgnoreCase)) seat = k;
            if (seat != Client.Seat) Client.SetSeat(seat);
        }

        /// <summary>Server time = local time + offset; an event's <c>at</c> is never later than its arrival, so the largest gap wins.</summary>
        private void Clock(long at)
        {
            if (at <= 0) return;
            long gap = at - (long)(Time.realtimeSinceStartupAsDouble * 1000);
            if (!offsetKnown || gap > offset) { offset = gap; offsetKnown = true; }
        }

        private async Task<string> Request(string type, object data = null)
        {
            if (Closed) return V4Errors.E_NOT_SEATED;
            var ack = await online.Realtime.RequestAsync(type, data ?? new object());
            return ack.Ok ? null : ack.Code ?? "error";
        }

        public Task<string> RollDice() => Request(V4Messages.GRollDice);
        public Task<string> Draw(string src) => Request(V4Messages.GDraw, new V4DrawRequest { Src = src });
        public Task<string> ReturnSide() => Request(V4Messages.GReturnSide);
        public Task<string> Open(List<MeldSpec> melds) => Request(V4Messages.GOpen, Melds(melds));
        public Task<string> Lay(List<MeldSpec> melds) => Request(V4Messages.GLay, Melds(melds));
        /// <summary>gAdd: the engine's "pre" / "post" (the server takes them as well as the protocol's "L" / "R"; null for a group).</summary>
        public Task<string> Add(string tile, string meld, string side) => Request(V4Messages.GAdd, new V4AddRequest { Tile = tile, Meld = meld, Side = side });
        public Task<string> Swap(string tile, string meld, int index) => Request(V4Messages.GSwap, new V4SwapRequest { Tile = tile, Meld = meld, Index = index });
        public Task<string> Undo() => Request(V4Messages.GUndo);
        public Task<string> Discard(string tile) => Request(V4Messages.GDiscard, new V4DiscardRequest { Tile = tile });
        public Task<string> Ready() => Request(V4Messages.GReady);
        public Task<string> RackOrder(IList<string> tiles) => Request(V4Messages.GRackOrder, new V4RackOrderRequest { Tiles = new List<string>(tiles) });
        public Task<string> Chat(string text, string quick) => Request(V4Messages.ChatSend, new V4ChatSendRequest { Text = text, Quick = quick });
        public Task<string> Gift(string gift, int? to) => Request(V4Messages.GiftSend, new V4GiftSendRequest { Gift = gift, To = to.HasValue ? (object)to.Value : "all" });

        public async Task<string> Leave()
        {
            var error = await Request(V4Messages.TableLeave);
            return error;
        }

        private static V4MeldsRequest Melds(List<MeldSpec> melds)
        {
            var r = new V4MeldsRequest();
            foreach (var m in melds) r.Melds.Add(new V4Meld { Kind = m.Kind, Tiles = new List<string>(m.Tiles) });
            return r;
        }

        // ---- reconnect (protocol.json reconnect; timings.md clientRetries × clientRetryMs) ---------------------------

        private bool reconnecting;

        private void OnClosed(string reason)
        {
            if (Closed || reason == GalaxyRealtime.ReplacedReason) return;
            _ = Reconnect();
        }

        /// <summary>"Şimdi dene": the next attempt at once (the count goes on).</summary>
        public void RetryNow() { if (Dropped && !reconnecting && !GaveUp) _ = Reconnect(); else retryNow = true; }
        private bool retryNow;

        private async Task Reconnect()
        {
            if (reconnecting) return;
            reconnecting = true; Dropped = true; GaveUp = false;
            online.SuspendReconnect = true;
            try
            {
                for (int n = Math.Max(1, Attempt + 1); n <= TableTimings.ClientRetries && !Closed; n++)
                {
                    Attempt = n; ConnectionChanged?.Invoke();
                    float until = Time.realtimeSinceStartup + TableTimings.ClientRetryMs / 1000f;
                    while (Time.realtimeSinceStartup < until && !retryNow) await Task.Yield();
                    retryNow = false;
                    try
                    {
                        await online.Realtime.ConnectAsync();
                        var ack = await online.Realtime.RequestAsync(V4Messages.Hello, new V4HelloRequest { Token = "" });
                        if (ack.Ok) { Dropped = false; Attempt = 0; ConnectionChanged?.Invoke(); return; }
                    }
                    catch (Exception e) { Debug.LogWarning("GALAXY101_TABLE_RECONNECT attempt=" + n + " " + e.Message); }
                }
                if (Dropped) { GaveUp = true; ConnectionChanged?.Invoke(); }
            }
            finally { reconnecting = false; online.SuspendReconnect = false; }
        }

        private void Close()
        {
            if (Closed) return;
            Closed = true;
            if (Current == this) Current = null;
            ConnectionChanged?.Invoke();
        }

        public void Dispose()
        {
            foreach (var off in subscriptions) off();
            subscriptions.Clear();
            Close();
        }
    }
}

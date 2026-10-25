using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Galaxy101.Contracts;

namespace Galaxy101.Online
{
    /// <summary>
    /// What the Klasik lobby (spec/20, v4 S09–S15) needs from the server: the live table list, the waiting room state and
    /// the table actions. Every action answers null when accepted or the server's error code (v4 E_* codes, or the
    /// current server's <see cref="TableErrors"/> / <see cref="ErrorCodes"/>, "offline", "timeout").
    /// <para>
    /// v4 package protocol (protocol.md): lobbySubscribe / lobby / lobbyTable / lobbyTableRemoved feed
    /// <see cref="Tables"/>; tableCreate, tableJoin, tableJoinCode, tableSpectate, tableLeave, seatAddBot, seatKick,
    /// tableStart are the actions; tableState and countdown become <see cref="Room"/>; leftTable{reason}, kicked and
    /// tableClosed{reason} close it (<see cref="TableStateMessage.Reason"/>: "left" | "disconnect" | "kicked" |
    /// "ownerLeft"); inviteSend / inviteCancel / inviteResult are the invite calls and <see cref="InviteAnswered"/>.
    /// docs/v4/lobby/RAPOR.md lists the field mapping.
    /// </para>
    /// </summary>
    public interface ILobbyBackend
    {
        bool Online { get; }
        Guid Me { get; }
        long Chips { get; }
        IReadOnlyList<TableSummary> Tables { get; }
        /// <summary>The waiting room you sit in; null outside one.</summary>
        TableStateMessage Room { get; }
        event Action TablesChanged;
        event Action<TableStateMessage> RoomChanged;
        event Action ChipsChanged;
        /// <summary>inviteResult: a player answered (or let lapse) your invite — "accepted", "declined", "expired" or "failed".</summary>
        event Action<Guid, string> InviteAnswered;
        Task<string> SubscribeAsync();
        void Unsubscribe();
        Task<string> CreateAsync(TableOptions options);
        Task<string> JoinAsync(TableJoinMessage join);
        /// <summary>İzle: a playing table that allows spectators (PIN when locked).</summary>
        Task<string> WatchAsync(TableJoinMessage join);
        Task<string> LeaveAsync();
        Task<string> AddBotAsync(int seat);
        Task<string> KickAsync(int seat);
        Task<string> StartAsync();
        Task<List<FriendResponse>> FriendsAsync();
        /// <summary>Son oynadıkların: players you shared a table with, newest first.</summary>
        Task<List<RecentPlayer>> RecentAsync();
        /// <summary>inviteSend: to a friend (or a recent player), into <paramref name="seat"/> when one was picked.</summary>
        Task<string> InviteAsync(Guid player, int? seat, bool recent);
        /// <summary>inviteCancel: take back a pending invite (the prototype's "İptal etmek için tıkla").</summary>
        Task<string> CancelInviteAsync(Guid player);
    }

    /// <summary>A Son oynadıkların row: the player with presence, and when you last played together (null when unknown).</summary>
    public sealed class RecentPlayer
    {
        public FriendResponse Friend { get; set; }
        public DateTimeOffset? PlayedAt { get; set; }
    }

    /// <summary>The lobby over the signed-in session's WebSocket (<see cref="GalaxyOnline"/>).</summary>
    public sealed class LobbyClient : ILobbyBackend, IDisposable
    {
        private readonly GalaxyOnline online;
        private readonly List<Action> subscriptions = new List<Action>();
        private List<TableSummary> tables = new List<TableSummary>();
        private bool subscribed;

        public LobbyClient(GalaxyOnline online)
        {
            this.online = online;
            subscriptions.Add(online.Realtime.On<LobbyTablesMessage>(LobbyMessageTypes.Tables, m => { tables = m?.Tables ?? new List<TableSummary>(); TablesChanged?.Invoke(); }));
            subscriptions.Add(online.Realtime.On<TableStateMessage>(LobbyMessageTypes.State, m => { Room = m == null || m.Closed ? null : m; RoomChanged?.Invoke(m); }));
            online.Changed += OnChanged;
            online.Realtime.Closed += OnClosed;
            online.Reconnected += OnReconnected;
        }

        /// <summary>The server drops a disconnected player from their waiting room: so does the client.</summary>
        private void OnClosed(string reason)
        {
            if (Room == null) return;
            Room = null;
            RoomChanged?.Invoke(new TableStateMessage { Closed = true, Reason = "offline" });
        }

        private void OnReconnected() { if (subscribed) _ = Request(LobbyMessageTypes.Subscribe, null); }

        public bool Online => online != null && online.SignedIn && online.Realtime.Connected;
        public Guid Me => online.Profile?.Id ?? Guid.Empty;
        public long Chips => online.Wallet?.Chips ?? 0;
        public IReadOnlyList<TableSummary> Tables => tables;
        public TableStateMessage Room { get; private set; }
        public event Action TablesChanged;
        public event Action<TableStateMessage> RoomChanged;
        public event Action ChipsChanged;
        /// <summary>The current server answers invites by seating the player (the room state): it never reports a refusal.</summary>
        public event Action<Guid, string> InviteAnswered { add { } remove { } }

        private void OnChanged() => ChipsChanged?.Invoke();

        public async Task<string> SubscribeAsync()
        {
            subscribed = true;
            if (!online.Realtime.Connected)
            {
                try { await online.Realtime.ConnectAsync(); }
                catch (Exception) { return "offline"; }
            }
            return await Request(LobbyMessageTypes.Subscribe, null);
        }

        public void Unsubscribe() { subscribed = false; if (online.Realtime.Connected) _ = Request(LobbyMessageTypes.Unsubscribe, null); }
        public Task<string> CreateAsync(TableOptions options) => Request(LobbyMessageTypes.Create, new TableCreateMessage { Options = options });
        public Task<string> JoinAsync(TableJoinMessage join) => Request(LobbyMessageTypes.Join, join);
        public Task<string> WatchAsync(TableJoinMessage join) => Request(LobbyMessageTypes.Watch, join);
        public Task<string> LeaveAsync() => Request(LobbyMessageTypes.Leave, null);
        public Task<string> AddBotAsync(int seat) => Request(LobbyMessageTypes.AddBot, new TableSeatMessage { Seat = seat });
        public Task<string> KickAsync(int seat) => Request(LobbyMessageTypes.Kick, new TableSeatMessage { Seat = seat });
        public Task<string> StartAsync() => Request(LobbyMessageTypes.Start, null);

        public async Task<List<FriendResponse>> FriendsAsync()
        {
            try { return (await online.Api.GetAsync<FriendsResponse>("/v1/friends"))?.Friends ?? new List<FriendResponse>(); }
            catch (ApiException) { return new List<FriendResponse>(); }
        }

        /// <summary>/v1/players/recent has no "played at" yet (EK-804): <see cref="RecentPlayer.PlayedAt"/> stays null.</summary>
        public async Task<List<RecentPlayer>> RecentAsync()
        {
            try { return (await online.Api.RecentPlayersAsync() ?? new List<FriendResponse>()).ConvertAll(f => new RecentPlayer { Friend = f }); }
            catch (ApiException) { return new List<RecentPlayer>(); }
        }

        /// <summary>The current server invites by the room code over HTTP (no seat, no recent-player invites).</summary>
        public async Task<string> InviteAsync(Guid player, int? seat, bool recent)
        {
            var code = Room?.Code; if (code == null) return TableErrors.Closed;
            try { await online.Api.InviteAsync(player, code); return null; }
            catch (ApiException e) { return e.Offline ? "offline" : e.Code; }
        }

        /// <summary>The HTTP invite cannot be taken back; the row only stops waiting.</summary>
        public Task<string> CancelInviteAsync(Guid player) => Task.FromResult<string>(null);

        private async Task<string> Request(string type, object data)
        {
            try { var ack = await online.Realtime.RequestAsync(type, data); return ack.Ok ? null : ack.Code ?? "error"; }
            catch (ApiException) { return "offline"; }
            catch (Exception) { return "offline"; }
        }

        public void Dispose()
        {
            foreach (var off in subscriptions) off();
            subscriptions.Clear();
            if (online != null) { online.Changed -= OnChanged; online.Reconnected -= OnReconnected; online.Realtime.Closed -= OnClosed; }
        }
    }
}

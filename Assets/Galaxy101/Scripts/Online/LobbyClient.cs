using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Galaxy101.Contracts;

namespace Galaxy101.Online
{
    /// <summary>
    /// What the Klasik lobby (spec/20) needs from the server: the live table list, the waiting room state and the
    /// table actions. Every action answers null when accepted or the server's error code (<see cref="TableErrors"/>,
    /// <see cref="ErrorCodes"/>, "offline", "timeout").
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
        Task<string> SubscribeAsync();
        void Unsubscribe();
        Task<string> CreateAsync(TableOptions options);
        Task<string> JoinAsync(TableJoinMessage join);
        Task<string> LeaveAsync();
        Task<string> AddBotAsync(int seat);
        Task<string> KickAsync(int seat);
        Task<string> StartAsync();
        Task<List<FriendResponse>> FriendsAsync();
        Task<List<FriendResponse>> RecentAsync();
        Task<string> InviteAsync(Guid friend, string code);
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
        public Task<string> LeaveAsync() => Request(LobbyMessageTypes.Leave, null);
        public Task<string> AddBotAsync(int seat) => Request(LobbyMessageTypes.AddBot, new TableSeatMessage { Seat = seat });
        public Task<string> KickAsync(int seat) => Request(LobbyMessageTypes.Kick, new TableSeatMessage { Seat = seat });
        public Task<string> StartAsync() => Request(LobbyMessageTypes.Start, null);

        public async Task<List<FriendResponse>> FriendsAsync()
        {
            try { return (await online.Api.GetAsync<FriendsResponse>("/v1/friends"))?.Friends ?? new List<FriendResponse>(); }
            catch (ApiException) { return new List<FriendResponse>(); }
        }

        public async Task<List<FriendResponse>> RecentAsync()
        {
            try { return await online.Api.RecentPlayersAsync() ?? new List<FriendResponse>(); }
            catch (ApiException) { return new List<FriendResponse>(); }
        }

        public async Task<string> InviteAsync(Guid friend, string code)
        {
            try { await online.Api.InviteAsync(friend, code); return null; }
            catch (ApiException e) { return e.Offline ? "offline" : e.Code; }
        }

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

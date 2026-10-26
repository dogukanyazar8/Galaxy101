using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Galaxy101.Contracts;

namespace Galaxy101.Online
{
    /// <summary>
    /// What the Klasik lobby (v4 S09–S15) needs from the server: the live table list, the waiting room state and the
    /// table actions. Every action answers null when accepted or the server's error code (<see cref="V4Errors"/>; a fake or
    /// the earlier server may answer <see cref="TableErrors"/> / <see cref="ErrorCodes"/>), "offline" or "timeout".
    /// <para>
    /// v4 package protocol (protocol.md, docs/v4/hat-b/ISTEMCI_NOTU.md §3): lobbySubscribe / lobby / lobbyTable /
    /// lobbyTableRemoved feed <see cref="Tables"/>; tableCreate, tableJoin, tableJoinCode, tableSpectate, tableLeave,
    /// seatAddBot, seatKick, tableStart are the actions; tableState and countdown become <see cref="Room"/>; leftTable{reason},
    /// kicked and tableClosed{reason} close it (<see cref="TableStateMessage.Reason"/>: "left" | "disconnect" | "kicked" |
    /// "ownerLeft"); matchStart and snapshot{screen: "table"} hand the table over (Closed + MatchId); inviteSend /
    /// inviteResult are the invite calls and <see cref="InviteAnswered"/>. docs/v4/lobby/RAPOR.md lists the field mapping.
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
        /// <summary>inviteIn: someone invites you to their table (shown while the lobby is open).</summary>
        event Action<IncomingInvite> InviteReceived;
        /// <summary>inviteRespond{inviteId, accept}: accepting seats you (the waiting room follows as tableState).</summary>
        Task<string> RespondInviteAsync(IncomingInvite invite, bool accept);
    }

    /// <summary>An invite to someone else's table (protocol inviteIn: inviteId, from, table, expiresAt).</summary>
    public sealed class IncomingInvite
    {
        public string InviteId { get; set; }
        public PlayerSummary From { get; set; }
        public TableSummary Table { get; set; }
        /// <summary>The table's code when the server sends one (private tables), for an invite answered by joining with it.</summary>
        public string Code { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }

    /// <summary>A Son oynadıkların row: the player with presence, and when you last played together (null when unknown).</summary>
    public sealed class RecentPlayer
    {
        public FriendResponse Friend { get; set; }
        public DateTimeOffset? PlayedAt { get; set; }
    }

    /// <summary>
    /// The lobby over the signed-in session's WebSocket in the v4 package protocol (lane B's hub, docs/v4/PROTOKOL_ESLEME.md):
    /// each <see cref="V4Table"/> becomes the screen's <see cref="TableSummary"/>, the waiting room events a
    /// <see cref="TableStateMessage"/>. Friends and recent players stay on HTTP (ISTEMCI_NOTU §3: social is HTTP).
    /// </summary>
    public sealed class LobbyClient : ILobbyBackend, IDisposable
    {
        private readonly GalaxyOnline online;
        private readonly List<Action> subscriptions = new List<Action>();
        private readonly List<TableSummary> tables = new List<TableSummary>();
        /// <summary>Invites still waiting for inviteResult.</summary>
        private readonly HashSet<Guid> invites = new HashSet<Guid>();
        private V4Table room;
        private bool subscribed;

        public LobbyClient(GalaxyOnline online)
        {
            this.online = online;
            var rt = online.Realtime;
            // lobby{tables}, then lobbyTable{table} (added or changed) and lobbyTableRemoved{id}.
            subscriptions.Add(rt.On<V4LobbyEvent>(V4Messages.Lobby, m => { tables.Clear(); if (m?.Tables != null) tables.AddRange(m.Tables.Select(Summary)); TablesChanged?.Invoke(); }));
            subscriptions.Add(rt.On<V4TableEvent>(V4Messages.LobbyTable, OnLobbyTable));
            subscriptions.Add(rt.On<V4IdEvent>(V4Messages.LobbyTableRemoved, m => { if (tables.RemoveAll(x => x.Id == Id(m?.Id)) > 0) TablesChanged?.Invoke(); }));
            // The waiting room: tableState{table} and countdown{n, cancelled}; leftTable / kicked / tableClosed end it, matchStart hands it over.
            subscriptions.Add(rt.On<V4TableEvent>(V4Messages.TableState, m => { if (m?.Table != null && Seat(m.Table) >= 0 && m.Table.Status != "playing") SetRoom(m.Table); }));
            subscriptions.Add(rt.On<V4Countdown>(V4Messages.Countdown, OnCountdown));
            subscriptions.Add(rt.On<V4LeftTable>(V4Messages.LeftTable, m => { if (room != null) CloseRoom(m?.Reason == "disconnect" ? "disconnect" : "left"); }));
            subscriptions.Add(rt.On<V4Kicked>(V4Messages.Kicked, m => { if (room != null) CloseRoom("kicked"); }));
            subscriptions.Add(rt.On<V4TableClosed>(V4Messages.TableClosed, m => { if (room != null) CloseRoom(m?.Reason ?? "ownerLeft"); }));
            subscriptions.Add(rt.On<V4MatchStart>(V4Messages.MatchStart, m => { if (m?.Table != null && Seat(m.Table) >= 0) Handover(m.Table, "started"); }));
            // tableSpectate, Oyuna katıl (R-65) and a return after hello: snapshot{screen} says where the player is.
            subscriptions.Add(rt.On<V4Snapshot>(V4Messages.Snapshot, OnSnapshot));
            // balance{chips} (bet on sitting R-90, betRefund on leaving the waiting room D-11): the session's wallet follows.
            subscriptions.Add(rt.On<V4Balance>(V4Messages.Balance, m => { if (m != null) online.Apply(new WalletResponse { Chips = m.Chips, Gems = m.Gems, OwnedItems = online.Wallet?.OwnedItems ?? new List<string>() }); }));
            subscriptions.Add(rt.On<V4InviteIn>(V4Messages.InviteIn, m =>
            {
                if (m?.InviteId == null || m.Table == null) return;
                var t = Summary(m.Table);
                InviteReceived?.Invoke(new IncomingInvite
                {
                    InviteId = m.InviteId, Table = t, Code = m.Table.Code, ExpiresAt = DateTimeOffset.FromUnixTimeMilliseconds(m.ExpiresAt),
                    From = m.From == null ? null : new PlayerSummary { Id = Id(m.From.Id), Name = m.From.Name, Color = m.From.Color, Level = m.From.Level, Frame = m.From.Frame },
                });
            }));
            subscriptions.Add(rt.On<V4InviteResult>(V4Messages.InviteResult, m => { if (m?.UserId == null) return; var who = Id(m.UserId); invites.Remove(who); InviteAnswered?.Invoke(who, m.Result); }));
            online.Changed += OnChanged;
            rt.Closed += OnClosed;
            online.Reconnected += OnReconnected;
        }

        private void OnLobbyTable(V4TableEvent m)
        {
            if (m?.Table == null) return;
            var t = Summary(m.Table); int i = tables.FindIndex(x => x.Id == t.Id);
            if (i >= 0) tables[i] = t; else tables.Add(t);
            TablesChanged?.Invoke();
        }

        private void OnCountdown(V4Countdown m)
        {
            if (room == null || m == null) return;
            bool cancelled = m.Cancelled == true;
            room.Countdown = cancelled ? 0 : m.N; room.Status = cancelled ? "waiting" : "countdown";
            SetRoom(room);
        }

        private void OnSnapshot(V4Snapshot s)
        {
            if (s == null) return;
            if (s.Table == null || s.Screen == "menu") { if (room != null) CloseRoom("disconnect"); return; }
            if (s.Screen == "table") Handover(s.Table, s.Spectator == true ? "spectate" : "takeover");
            else if (s.Screen == "waitroom" && s.Spectator != true && Seat(s.Table) >= 0) SetRoom(s.Table);
        }

        /// <summary>The server drops a disconnected player from their waiting room (R-66, stake back): so does the client.</summary>
        private void OnClosed(string reason) { if (room != null) CloseRoom("offline"); }

        private void OnReconnected() { if (subscribed) _ = Request(V4Messages.LobbySubscribe, null); }

        public bool Online => online != null && online.SignedIn && online.Realtime.Connected;
        public Guid Me => online.Profile?.Id ?? Guid.Empty;
        public long Chips => online.Wallet?.Chips ?? 0;
        public IReadOnlyList<TableSummary> Tables => tables;
        public TableStateMessage Room { get; private set; }
        public event Action TablesChanged;
        public event Action<TableStateMessage> RoomChanged;
        public event Action ChipsChanged;
        public event Action<Guid, string> InviteAnswered;
        public event Action<IncomingInvite> InviteReceived;

        private void OnChanged() => ChipsChanged?.Invoke();

        private void SetRoom(V4Table t)
        {
            room = t;
            Room = new TableStateMessage { Table = Summary(t), Code = t.Code, YourSeat = Seat(t), Countdown = t.Status == "countdown" ? t.Countdown : 0 };
            RoomChanged?.Invoke(Room);
        }

        private void CloseRoom(string reason)
        {
            var last = room; room = null; Room = null; invites.Clear();
            RoomChanged?.Invoke(new TableStateMessage { Table = last == null ? null : Summary(last), Closed = true, Reason = reason, YourSeat = last == null ? -1 : Seat(last) });
        }

        /// <summary>matchStart / snapshot{screen: "table"}: the lobby hands the table over (MatchId = the table's id).</summary>
        private void Handover(V4Table t, string reason)
        {
            room = null; Room = null; invites.Clear();
            RoomChanged?.Invoke(new TableStateMessage { Table = Summary(t), Closed = true, Reason = reason, MatchId = Id(t.Id), YourSeat = Seat(t) });
        }

        public async Task<string> SubscribeAsync()
        {
            subscribed = true;
            if (!online.Realtime.Connected)
            {
                try { await online.Realtime.ConnectAsync(); }
                catch (Exception) { return "offline"; }
            }
            return await Request(V4Messages.LobbySubscribe, null);
        }

        public void Unsubscribe() { subscribed = false; if (online.Realtime.Connected) _ = Request(V4Messages.LobbyUnsubscribe, null); }

        /// <summary>
        /// tableCreate. The form's two penalty switches (Okey atma, İşlek taş atma) meet one protocol flag (EK-811):
        /// rules.penalties is on while either is; rules.isle is İşleme (always on from this form); katlamaPartner only on
        /// Eşli + Katlamalı (protocol: "yalnız eşli + katlamalı").
        /// </summary>
        public Task<string> CreateAsync(TableOptions o) => Request(V4Messages.TableCreate, new V4TableCreateRequest
        {
            Name = o.Name, Mode = o.Type == "pairs" ? "pairs" : "solo", Private = o.Visibility == "private", Pin = o.Pin, Hands = o.Hands, Bet = o.Stake,
            Katlama = o.Doubling, TurnSec = o.TurnSeconds, KatlamaPartner = o.Type == "pairs" && o.Doubling ? o.PartnerRaises : (bool?)null,
            Rules = new V4TableRules { Spectate = o.Spectators, Chat = o.Chat == "on" ? "open" : o.Chat, Gifts = o.Gifts, Isle = o.Processing, Penalties = o.Penalties || o.OkeyPenalty },
        });

        /// <summary>tableJoin (a listed table; on a playing one the seat is taken over, R-65) or tableJoinCode (GX-XXXX).</summary>
        public Task<string> JoinAsync(TableJoinMessage join) => join.Code != null
            ? Request(V4Messages.TableJoinCode, new V4TableJoinCodeRequest { Code = join.Code, Pin = join.Pin })
            : Request(V4Messages.TableJoin, new V4TableJoinRequest { TableId = join.TableId?.ToString(), Pin = join.Pin, Seat = join.Seat });

        /// <summary>tableSpectate{tableId}: the protocol takes no PIN (EK-807).</summary>
        public Task<string> WatchAsync(TableJoinMessage join) => Request(V4Messages.TableSpectate, new V4TableSpectateRequest { TableId = join.TableId?.ToString() });
        public Task<string> LeaveAsync() => Request(V4Messages.TableLeave, null);
        public Task<string> AddBotAsync(int seat) => Request(V4Messages.SeatAddBot, new V4SeatRequest { Seat = seat });
        public Task<string> KickAsync(int seat) => Request(V4Messages.SeatKick, new V4SeatRequest { Seat = seat });
        public Task<string> StartAsync() => Request(V4Messages.TableStart, null);

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

        /// <summary>inviteSend{userId, seat?, recent?}: 30 s; the answer comes as inviteResult.</summary>
        public async Task<string> InviteAsync(Guid player, int? seat, bool recent)
        {
            string error = await Request(V4Messages.InviteSend, new V4InviteSendRequest { UserId = player.ToString(), Seat = seat, Recent = recent ? true : (bool?)null });
            if (error == null) invites.Add(player);
            return error;
        }

        /// <summary>
        /// inviteCancel needs the inviteId of inviteSend's ack, which <see cref="AckMessage"/> does not carry (EK-812): the row
        /// stops waiting here and the invite lapses on the server after its 30 s.
        /// </summary>
        public Task<string> CancelInviteAsync(Guid player) { invites.Remove(player); return Task.FromResult<string>(null); }

        public Task<string> RespondInviteAsync(IncomingInvite invite, bool accept) => Request(V4Messages.InviteRespond, new V4InviteRespondRequest { InviteId = invite?.InviteId, Accept = accept });

        private async Task<string> Request(string type, object data)
        {
            try { var ack = await online.Realtime.RequestAsync(type, data); return ack.Ok ? null : ack.Code ?? "error"; }
            catch (ApiException) { return "offline"; }
            catch (Exception) { return "offline"; }
        }

        // ---------- V4Table → TableSummary ----------
        /// <summary>A v4 id: an account or table Guid, or a bot's "b12" folded into a stable Guid of its own.</summary>
        private static Guid Id(string id)
        {
            if (Guid.TryParse(id, out var g)) return g;
            var bytes = new byte[16]; var text = System.Text.Encoding.UTF8.GetBytes(id ?? "");
            for (int i = 0; i < text.Length; i++) bytes[i % 16] = (byte)(bytes[i % 16] * 31 + text[i]);
            return new Guid(bytes);
        }

        private int Seat(V4Table t)
        {
            string me = Me.ToString();
            return t.Seats == null ? -1 : t.Seats.FindIndex(s => s?.User != null && !s.Bot && string.Equals(s.User.Id, me, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Table → the lobby's summary: mode solo/pairs → single/pairs, bet, katlama (+ partner, R-64), rules (chat "open" →
        /// "on", penalties → both penalty switches), status (countdown counts as a waiting table), and while it plays the
        /// bot seats a newcomer may take over (R-65; not at ranked or against-bots tables).
        /// </summary>
        public static TableSummary Summary(V4Table t)
        {
            var r = t.Rules ?? new V4TableRules();
            var seats = new List<TableSeat>();
            for (int k = 0; k < 4; k++)
            {
                var s = t.Seats != null && k < t.Seats.Count ? t.Seats[k] : null;
                seats.Add(s?.User == null ? null : new TableSeat { Bot = s.Bot, Player = new PlayerSummary { Id = Id(s.User.Id), Name = s.User.Name, Color = s.User.Color, Level = s.User.Level, Frame = s.User.Frame } });
            }
            bool playing = t.Status == "playing" || t.Status == "over";
            return new TableSummary
            {
                Id = Id(t.Id), Name = t.Name, Type = t.Mode == "pairs" ? "pairs" : "single", Stake = t.Bet, Hands = t.Hands, TurnSeconds = t.TurnSec,
                Doubling = t.Katlama, PartnerRaises = t.KatlamaPartner ?? true, Locked = t.Locked, Private = t.Private,
                Spectators = r.Spectate, Chat = r.Chat == null || r.Chat == "open" ? "on" : r.Chat, Gifts = r.Gifts, Processing = r.Isle, Penalties = r.Penalties, OkeyPenalty = r.Penalties,
                Status = playing ? "playing" : "waiting", Hand = t.HandNo,
                OpenSeats = playing && !t.Ranked && !t.VsBots ? seats.Count(s => s != null && s.Bot) : 0,
                HostId = Id(t.Owner), Seats = seats,
            };
        }

        public void Dispose()
        {
            foreach (var off in subscriptions) off();
            subscriptions.Clear();
            if (online != null) { online.Changed -= OnChanged; online.Reconnected -= OnReconnected; online.Realtime.Closed -= OnClosed; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Galaxy101.Online;
using Okey101.Application;
using Okey101.Core.Model;
using UnityEngine;

namespace Galaxy101.Table
{
    /// <summary>What the table plays against: the local engine with bots, or a server match.</summary>
    public interface ITableSession : IDisposable
    {
        PlayerView GetView(int seat);
        Task<CommandReceipt> SubmitAsync(GameCommand command, CancellationToken token);
    }

    /// <summary>Offline: the in-process engine (LocalSession) that also plays the bots' seats.</summary>
    public sealed class LocalTableSession : ITableSession
    {
        public LocalSession Engine { get; } = new LocalSession();
        public PlayerView GetView(int seat) => Engine.GetView(seat);
        public Task<CommandReceipt> SubmitAsync(GameCommand command, CancellationToken token) => Engine.SubmitAsync(command, token);
        public void Dispose() => Engine.Dispose();
    }

    /// <summary>
    /// Online: one server match over the session socket. The server is authoritative — the view is the latest
    /// <c>match.state</c> turned so that I am seat 0; a move is accepted when its ack arrives and the state moved past it.
    /// Opponents (people or the server's bots) move by themselves; <see cref="Changed"/> tells the table.
    /// </summary>
    public sealed class NetworkTableSession : ITableSession
    {
        private readonly GalaxyOnline online;
        private readonly List<Action> subscriptions = new List<Action>();
        private PlayerView view;

        public Guid MatchId { get; }
        public MatchWire Wire { get; private set; }
        public MatchStartMessage Start { get; private set; }
        /// <summary>İzle: watching from seat 0 with nobody's tiles; the table never plays a turn.</summary>
        public bool Spectating => Start?.Spectator == true;
        /// <summary>Someone took a free seat during the match (Start.Seats is updated first).</summary>
        public event Action SeatsChanged;
        public MatchStateMessage State { get; private set; }
        public HandEndMessage HandEnd { get; private set; }
        public MatchEndMessage End { get; private set; }
        /// <summary>A treat or gift the server confirmed (mine too): sender, kind, recipient or null for the whole table (local seats).</summary>
        public event Action<int, string, int?> GiftReceived;
        /// <summary>An emoji the server passed on (mine too): sender's local seat, emoji.</summary>
        public event Action<int, string> EmoteReceived;
        /// <summary>A chat line the server passed on (mine too): sender's local seat, text, or a ready phrase's index.</summary>
        public event Action<int, string, int?> ChatReceived;
        /// <summary>Hands played so far in the match (0-based index of the current hand).</summary>
        public int HandIndex => State?.HandIndex ?? 0;
        public event Action Changed;

        public NetworkTableSession(GalaxyOnline online, Guid matchId)
        {
            this.online = online; MatchId = matchId;
            subscriptions.Add(online.Realtime.On<MatchStartMessage>(MessageTypes.MatchStart, m => { if (m?.MatchId != MatchId) return; Start = m; Wire = new MatchWire(m.YourSeat); Changed?.Invoke(); }));
            subscriptions.Add(online.Realtime.On<MatchSeatsMessage>(MessageTypes.MatchSeats, m =>
            {
                if (m?.MatchId != MatchId || Start == null) return;
                Start.Seats = m.Seats; SeatsChanged?.Invoke();
            }));
            subscriptions.Add(online.Realtime.On<MatchStateMessage>(MessageTypes.MatchState, m =>
            {
                if (m?.MatchId != MatchId || m.View == null) return;
                if (Wire == null) Wire = new MatchWire(m.View.Seat);
                State = m; view = Wire.ToView(m.View); Changed?.Invoke();
            }));
            subscriptions.Add(online.Realtime.On<HandEndMessage>(MessageTypes.MatchHandEnd, m => { if (m?.MatchId != MatchId) return; HandEnd = m; Changed?.Invoke(); }));
            subscriptions.Add(online.Realtime.On<MatchEndMessage>(MessageTypes.MatchEnd, m => { if (m?.MatchId != MatchId) return; End = m; Changed?.Invoke(); }));
            subscriptions.Add(online.Realtime.On<GiftMessage>(MessageTypes.MatchGift, m =>
            {
                if (m?.MatchId != MatchId || Wire == null || string.IsNullOrEmpty(m.Gift)) return;
                GiftReceived?.Invoke(Wire.FromSeat(m.Seat), m.Gift, m.TargetSeat is int to ? Wire.FromSeat(to) : (int?)null);
            }));
            subscriptions.Add(online.Realtime.On<EmoteMessage>(MessageTypes.MatchEmote, m => { if (m?.MatchId == MatchId && Wire != null && !string.IsNullOrEmpty(m.Emote)) EmoteReceived?.Invoke(Wire.FromSeat(m.Seat), m.Emote); }));
            subscriptions.Add(online.Realtime.On<ChatMessage>(MessageTypes.MatchChat, m => { if (m?.MatchId == MatchId && Wire != null) ChatReceived?.Invoke(Wire.FromSeat(m.Seat), m.Text, m.Quick); }));
            // Back after a dropped socket: the match went on without us (the server played the seat), ask for the snapshot.
            Action resync = () => { if (End == null) _ = online.Realtime.RequestAsync(MessageTypes.MatchSync, new MatchCommandMessage { MatchId = MatchId }); };
            online.Reconnected += resync; subscriptions.Add(() => online.Reconnected -= resync);
        }

        /// <summary>Ask the server for the full snapshot (match.start + match.state) and wait for it.</summary>
        public async Task<bool> ConnectAsync(float seconds = 10)
        {
            if (!online.Realtime.Connected) { try { await online.Realtime.ConnectAsync(); } catch (Exception e) { Debug.LogWarning("Match socket: " + e.Message); return false; } }
            var ack = await online.Realtime.RequestAsync(MessageTypes.MatchSync, new MatchCommandMessage { MatchId = MatchId });
            if (!ack.Ok) { Debug.LogWarning("match.sync refused: " + ack.Code); return false; }
            float until = Time.realtimeSinceStartup + seconds;
            while ((view == null || Start == null) && Time.realtimeSinceStartup < until) await Task.Yield();
            return view != null && Start != null;
        }

        /// <summary>The latest view (only my own seat exists online).</summary>
        public PlayerView GetView(int seat) => view ?? Empty;

        /// <summary>Before the first snapshot: an empty table (no hand, no tiles), so the table's views never fail.</summary>
        private static readonly PlayerView Empty = new PlayerView(new Guid("00000000-0000-0000-0000-000000000001"), 0, 0, 0, RoundPhase.AwaitingDraw, Array.Empty<TileId>(), OpeningKind.None, 0, 0,
            new TileId(0), 0, new[] { new PublicPlayerInfo(0, OpeningKind.None, 0), new PublicPlayerInfo(0, OpeningKind.None, 0), new PublicPlayerInfo(0, OpeningKind.None, 0), new PublicPlayerInfo(0, OpeningKind.None, 0) },
            Array.Empty<Meld>(), new IReadOnlyList<TileId>[] { Array.Empty<TileId>(), Array.Empty<TileId>(), Array.Empty<TileId>(), Array.Empty<TileId>() }, null, 0);
        public bool HasView => view != null;

        public async Task<CommandReceipt> SubmitAsync(GameCommand command, CancellationToken token)
        {
            if (view == null) return new CommandReceipt(false, ErrorCode.InvalidState, 0, Array.Empty<VisibleEvent>());
            long before = view.Version;
            var ack = await online.Realtime.RequestAsync(MessageTypes.MatchCommand, MatchWire.ToMessage(command, MatchId, before));
            if (!ack.Ok) return new CommandReceipt(false, MatchWire.Error(ack.Code), before, Array.Empty<VisibleEvent>());
            // Accepted only once the authoritative state shows the move: the table reads the drawn tile and the new hand
            // right after. A state that never came (dropped, or the socket blinked) is asked for once more as a snapshot.
            if (!await StateAfter(before, 5, token) && !token.IsCancellationRequested)
            {
                var sync = await online.Realtime.RequestAsync(MessageTypes.MatchSync, new MatchCommandMessage { MatchId = MatchId });
                if (sync.Ok) await StateAfter(before, 5, token);
            }
            return view.Version > before ? new CommandReceipt(true, ErrorCode.None, view.Version, Array.Empty<VisibleEvent>())
                : new CommandReceipt(false, ErrorCode.Cancelled, before, Array.Empty<VisibleEvent>());
        }

        private async Task<bool> StateAfter(long version, float seconds, CancellationToken token)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (view.Version <= version)
            {
                if (token.IsCancellationRequested || Time.realtimeSinceStartup >= until) return false;
                await Task.Yield();
            }
            return true;
        }

        /// <summary>Treat a seat (local; null: everyone). The server charges and shows it at every seat. Null, or the error code.</summary>
        public async Task<string> GiftAsync(string gift, int? target)
        {
            if (Wire == null) return "offline";
            var ack = await online.Realtime.RequestAsync(MessageTypes.MatchGift, new GiftMessage { MatchId = MatchId, Gift = gift, TargetSeat = target is int to ? Wire.ToSeat(to) : (int?)null });
            return ack.Ok ? null : ack.Code ?? "error";
        }

        public async Task<string> ChatAsync(string text, int? quick)
        {
            var ack = await online.Realtime.RequestAsync(MessageTypes.MatchChat, new ChatMessage { MatchId = MatchId, Text = text, Quick = quick });
            return ack.Ok ? null : ack.Code ?? "error";
        }

        /// <summary>A friend request to the player in a local seat, by their friend code. Null, or the error code.</summary>
        /// <summary>The account behind a seat; null for a server bot (no friend code, nothing to block or report).</summary>
        private Guid? AccountOf(int seat)
        {
            var player = Wire == null ? null : Start?.Seats?.Find(s => s.Seat == Wire.ToSeat(seat))?.Player;
            return string.IsNullOrEmpty(player?.FriendCode) ? (Guid?)null : player.Id;
        }

        public bool IsPerson(int seat) => AccountOf(seat) != null;

        /// <summary>Engelle / Engeli kaldır: the blocked player's chat and emotes stop reaching me (this table included).</summary>
        public async Task<string> BlockAsync(int seat, bool on)
        {
            if (!(AccountOf(seat) is Guid id)) return "not_found";
            try
            {
                if (on) await online.Api.PostAsync<Newtonsoft.Json.Linq.JObject>("/v1/players/" + id + "/block", null);
                else await online.Api.SendAsync<Newtonsoft.Json.Linq.JObject>("DELETE", "/v1/players/" + id + "/block");
                return null;
            }
            catch (ApiException e) { return e.Offline ? "offline" : e.Code; }
        }

        /// <summary>Şikayet et ("chat", "name" or "cheating"), with this match for the moderators.</summary>
        public async Task<string> ReportAsync(int seat, string reason)
        {
            if (!(AccountOf(seat) is Guid id)) return "not_found";
            try { await online.Api.PostAsync<Newtonsoft.Json.Linq.JObject>("/v1/players/" + id + "/report", new ReportRequest { Reason = reason, MatchId = MatchId }); return null; }
            catch (ApiException e) { return e.Offline ? "offline" : e.Code; }
        }

        public async Task<string> FriendRequestAsync(int seat)
        {
            var code = Wire == null ? null : Start?.Seats?.Find(s => s.Seat == Wire.ToSeat(seat))?.Player?.FriendCode;
            if (string.IsNullOrEmpty(code)) return "not_found"; // a server bot has no account
            try { await online.Api.PostAsync<Newtonsoft.Json.Linq.JObject>("/v1/friends/requests", new FriendCodeRequest { Code = code }); return null; }
            catch (ApiException e) { return e.Offline ? "offline" : e.Code; }
        }

        public async Task<string> EmoteAsync(string emote)
        {
            var ack = await online.Realtime.RequestAsync(MessageTypes.MatchEmote, new EmoteMessage { MatchId = MatchId, Emote = emote });
            return ack.Ok ? null : ack.Code ?? "error";
        }

        /// <summary>Masadan kalk: the seat is forfeited; the server plays it to the end.</summary>
        public Task LeaveAsync() => online.Realtime.RequestAsync(MessageTypes.MatchLeave, new MatchCommandMessage { MatchId = MatchId });

        public void Dispose()
        {
            foreach (var off in subscriptions) off();
            subscriptions.Clear();
        }
    }
}

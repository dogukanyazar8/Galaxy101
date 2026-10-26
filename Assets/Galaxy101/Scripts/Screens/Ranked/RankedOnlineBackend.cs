using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Galaxy101.Online;
using Galaxy101.Table;
using UnityEngine;

namespace Galaxy101
{
    /// <summary>
    /// The Dereceli screen on the v4 game server (lane B, docs/v4/hat-b/ISTEMCI_NOTU.md §3): c2s <c>rankedQueue {mode, party}</c>,
    /// <c>rankedAccept {matchId, accept}</c>, <c>rankedCancel</c>; s2c <c>rankedSearch</c> (±2 after 30 s), <c>rankedFound</c>
    /// (10 s), <c>rankedAccepted</c>, <c>rankedCancelled</c>, <c>rankedReady</c> (loadingMs 1800) → <c>matchStart</c>, and at the
    /// match's end <c>rankChange</c> → the result card. The standing comes from the profile's season progress (HTTP), the party
    /// from the friends list (<c>/v1/friends</c>).
    /// <para>One instance per <see cref="GalaxyOnline"/> (<see cref="For"/>): it listens for the whole session, so a rankChange that
    /// arrives while the table is up, or a queue a party leader put this player in while the screen was closed, is not lost.</para>
    /// </summary>
    public sealed class RankedOnlineBackend : IRankedBackend
    {
        private static RankedOnlineBackend current;

        /// <summary>The session's backend (made on first use, again for a new session).</summary>
        public static RankedOnlineBackend For(GalaxyOnline online)
        {
            if (current != null && !current.detached && current.online == online && online != null && current.generation == online.Generation) return current;
            current?.Detach();
            RankedScreen.Pending = null;
            return current = new RankedOnlineBackend(online);
        }

        private readonly GalaxyOnline online;
        private readonly int generation;
        private bool detached;
        private bool Owns => !detached && online != null && online.Owns(generation) && !online.Replaced;
        private readonly List<Action> offs = new List<Action>();
        private List<RankedPlayer> friends = new List<RankedPlayer>();
        private string mode = "solo", readyTable;

        private RankedOnlineBackend(GalaxyOnline online)
        {
            this.online = online ?? throw new ArgumentNullException(nameof(online));
            generation = online.Generation;
            var r = online.Realtime;
            offs.Add(r.On<V4RankedSearch>(V4Messages.RankedSearch, OnSearch));
            offs.Add(r.On<V4RankedFoundEvent>(V4Messages.RankedFound, e => OnFound(e?.Found, e?.At ?? 0)));
            offs.Add(r.On<V4RankedAccepted>(V4Messages.RankedAccepted, OnAccepted));
            offs.Add(r.On<V4RankedCancelled>(V4Messages.RankedCancelled, OnCancelled));
            offs.Add(r.On<V4RankedReady>(V4Messages.RankedReady, OnReady));
            offs.Add(r.On<V4MatchStart>(V4Messages.MatchStart, OnMatchStart));
            offs.Add(r.On<V4RankChange>(V4Messages.RankChange, OnRankChange));
            offs.Add(r.On<V4Snapshot>(V4Messages.Snapshot, OnSnapshot));
            online.Reconnected += OnReconnected;
            offs.Add(() => online.Reconnected -= OnReconnected);
            online.Changed += OnOnlineChanged;
            offs.Add(() => online.Changed -= OnOnlineChanged);
            if (r.Connected) _ = RestoreSessionAsync();
        }

        private void Detach() { detached = true; foreach (var off in offs) off(); offs.Clear(); Queue = null; Pending = null; friends.Clear(); readyTable = null; }

        public string MeId => online.Profile?.Id.ToString() ?? "me";

        /// <summary>ProgressResponse: League (b3 … m1, gx) → idx, DivisionLp, Protected, BestLeague, SeasonRank, Season / SeasonEndsAt.</summary>
        public RankedProfile Profile => From(online.Profile?.Progress);

        public static RankedProfile From(ProgressResponse g)
        {
            var p = new RankedProfile();
            if (g == null) return p;
            p.Idx = RankedLadder.FromCode(g.League); p.Lp = Math.Max(0, g.DivisionLp); p.Protected = g.Protected; p.SeasonRank = g.SeasonRank;
            p.Peak = Math.Max(p.Idx, RankedLadder.FromCode(g.BestLeague)); p.Season = Math.Max(1, g.Season);
            p.DaysLeft = g.SeasonEndsAt == default ? 0 : Math.Max(0, (int)Math.Ceiling((g.SeasonEndsAt - DateTimeOffset.UtcNow).TotalDays));
            // games / top / hist are not kept by the server (lane B EK-213): EK-703.
            return p;
        }

        public IReadOnlyList<RankedPlayer> Friends => friends;
        public RankedSearch Queue { get; private set; }
        public RankedFound Pending { get; private set; }

        public event Action Changed;
        public event Action<string, bool> PartyAnswered;
        public event Action<RankedSearch> Searching;
        public event Action<RankedFound> Found;
        public event Action<IReadOnlyList<bool>> Accepted;
        public event Action<string> Cancelled;
        public event Action<int> Ready;
        public event Action<RankedStart> Started;
        public event Action<RankedResult> RankChanged;
        /// <summary>The server put this player in a ranked search or match while no screen listens (a party leader queued them): the menu opens the screen.</summary>
        public event Action Incoming;

        /// <summary>The screen opened: the standing (profile) and the friends who can join a party.</summary>
        public async void Refresh()
        {
            if (!Owns) return;
            try
            {
                var list = await online.Api.GetAsync<FriendsResponse>("/v1/friends");
                if (!Owns) return;
                friends = (list?.Friends ?? new List<FriendResponse>()).Where(f => f?.Player != null).Select(f => new RankedPlayer
                {
                    Id = f.Player.Id.ToString(), Name = f.Player.Name, Color = f.Player.Color, Idx = RankedLadder.FromCode(f.Player.League),
                    Online = f.Presence == "online" // in a game or a queue the server leaves them out of the party (Hub.RankedQueueAsync)
                }).ToList();
                await online.RefreshAsync(); // raises Changed
            }
            catch (ApiException e) { if (!e.Offline) Debug.LogWarning("Ranked data not loaded: " + e.Code); }
            catch (Exception e) { Debug.LogException(e); }
        }

        /// <summary>
        /// There is no party invite in the protocol (EK-702): the leader's rankedQueue names the party and the server puts the
        /// friends straight into the search (lane B EK-218). The seat fills at once.
        /// </summary>
        public Task<string> InviteAsync(RankedPlayer friend)
        {
            if (!Owns) return Task.FromResult("offline");
            PartyAnswered?.Invoke(friend.Id, true);
            return Task.FromResult<string>(null);
        }

        public async Task<string> QueueAsync(string queueMode, IReadOnlyList<string> party)
        {
            if (!Owns) return "offline";
            mode = queueMode;
            var ack = await online.Realtime.RequestAsync(V4Messages.RankedQueue, new V4RankedQueueRequest { Mode = queueMode, Party = party.ToList() });
            return !Owns ? "offline" : ack.Ok ? null : ack.Code ?? "error";
        }

        public async Task CancelAsync()
        {
            if (!Owns) return;
            Queue = null; Pending = null;
            await online.Realtime.RequestAsync(V4Messages.RankedCancel, new { });
        }

        public async Task<string> AcceptAsync(string matchId, bool accept)
        {
            if (!Owns) return "offline";
            if (!accept) { Queue = null; Pending = null; }
            var ack = await online.Realtime.RequestAsync(V4Messages.RankedAccept, new V4RankedAcceptRequest { MatchId = matchId, Accept = accept });
            return !Owns ? "offline" : ack.Ok ? null : ack.Code ?? "error";
        }

        // ---------- server events ----------
        private void OnOnlineChanged()
        {
            if (!Owns) { Detach(); if (current == this) RankedScreen.Pending = null; return; }
            Changed?.Invoke();
        }

        private void OnReconnected() { if (Owns) _ = RestoreSessionAsync(); }

        private async Task RestoreSessionAsync()
        {
            if (!Owns || !online.Realtime.Connected) return;
            // hello restores human control as well as the snapshot; resync alone leaves a disconnected seat unchanged.
            var ack = await online.Realtime.RequestAsync(V4Messages.Hello, new V4HelloRequest { Token = "" });
            if (Owns && !ack.Ok) Debug.LogWarning("Ranked snapshot not restored: " + (ack.Code ?? "error"));
        }

        private void OnSnapshot(V4Snapshot snapshot)
        {
            if (!Owns || snapshot == null) return;
            var ranked = snapshot.Ranked;
            if (ranked?.Mode != null) mode = ranked.Mode;
            if (snapshot.Screen == "ranked.search" && ranked != null)
                OnSearch(new V4RankedSearch { Mode = ranked.Mode, Since = ranked.Since, Range = ranked.Range, Filled = ranked.Filled, At = snapshot.At });
            else if (snapshot.Screen == "ranked.found" && ranked?.Found != null)
                OnFound(ranked.Found, snapshot.At);
            else if (snapshot.Screen == "ranked.loading" && snapshot.Table != null)
                OnReady(new V4RankedReady { Table = snapshot.Table, At = snapshot.At, LoadingMs = TableTimings.RankedLoading });
            else if (snapshot.Screen == "menu" && (Queue != null || Pending != null || readyTable != null))
            { readyTable = null; OnCancelled(new V4RankedCancelled { Reason = "cancel", At = snapshot.At }); }
        }

        private void OnSearch(V4RankedSearch s)
        {
            if (s == null || !Owns) return;
            if (s.Mode != null) mode = s.Mode;
            Pending = null;
            // filled counts this party's own seats until the match is found (Hub.QueuePub): no opponent is known before rankedFound.
            Queue = new RankedSearch { Mode = s.Mode, Range = s.Range, Filled = 0 };
            if (Searching == null) Incoming?.Invoke();
            Searching?.Invoke(Queue);
        }

        private void OnFound(V4RankedFound f, long at)
        {
            if (f == null || !Owns) return;
            string me = MeId;
            Queue = null;
            Pending = new RankedFound
            {
                MatchId = f.MatchId,
                Players = (f.Players ?? new List<V4RankedPlayer>()).Select(p => Player(p.User, me, p.Bot)).ToList(),
                Accepted = (f.Accepted ?? new List<bool>()).ToList(),
                // expiresAt is the server's clock: the window is expiresAt − at (rankedAccept 10 000 ms).
                Seconds = f.ExpiresAt > at && at > 0 ? (f.ExpiresAt - at) / 1000f : 10,
            };
            while (Pending.Accepted.Count < Pending.Players.Count) Pending.Accepted.Add(false);
            if (Found == null) Incoming?.Invoke();
            Found?.Invoke(Pending);
        }

        private static RankedPlayer Player(V4User u, string me, bool bot) => u == null ? new RankedPlayer { Id = "?", Name = "?", Bot = bot }
            : new RankedPlayer { Id = u.Id, Name = u.Name, Color = u.Color, Idx = u.RankIdx, Me = u.Id == me, Bot = bot };

        private void OnAccepted(V4RankedAccepted a)
        {
            if (a?.Accepted == null || !Owns) return;
            if (Pending != null) Pending.Accepted = a.Accepted.ToList();
            Accepted?.Invoke(a.Accepted);
        }

        private void OnCancelled(V4RankedCancelled c)
        {
            if (!Owns) return;
            Queue = null; Pending = null;
            Cancelled?.Invoke(c?.Reason ?? "cancel");
        }

        private void OnReady(V4RankedReady r)
        {
            if (!Owns) return;
            readyTable = r?.Table?.Id;
            Ready?.Invoke((int)(r?.LoadingMs ?? 0));
        }

        /// <summary>matchStart of the table rankedReady announced (lobby and bot tables send matchStart too).</summary>
        private void OnMatchStart(V4MatchStart m)
        {
            if (!Owns || m?.Table == null || readyTable == null || m.Table.Id != readyTable) return;
            readyTable = null; Pending = null; Queue = null;
            string me = MeId;
            var seats = m.Table.Seats ?? new List<V4Seat>();
            var start = new RankedStart { Mode = mode, Match = m, Seat = seats.FindIndex(s => s?.User?.Id == me) };
            start.Players = seats.Select(s => s?.User == null ? null : Player(s.User, me, s.Bot)).Where(p => p != null).ToList();
            Started?.Invoke(start);
        }

        /// <summary>rankChange at the match's end: the result card now, or when the menu opens the screen again.</summary>
        private void OnRankChange(V4RankChange c)
        {
            if (!Owns || c?.Before == null || c.After == null) return;
            var result = RankedRules.FromChange(c.Before.Idx, c.Before.Lp, c.After.Idx, c.After.Lp, c.Delta, c.Place, c.TierUp, c.LeagueUp, c.Down, c.ProtectedLeft, mode);
            if (RankChanged == null) RankedScreen.Pending = result; else RankChanged.Invoke(result);
            _ = RefreshProfile();
        }

        private async Task RefreshProfile()
        {
            if (!Owns) return;
            try { await online.RefreshAsync(); }
            catch (Exception e) { Debug.LogWarning("Profile not refreshed after rankChange: " + e.Message); }
        }
    }
}

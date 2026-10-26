using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Galaxy101.Online;

namespace Galaxy101
{
    /// <summary>
    /// The Dereceli screen on today's game server (<see cref="GalaxyOnline"/> / <see cref="GalaxyRealtime"/>): the standing comes
    /// from the profile's season progress, MAÇ ARA is <c>queue.join {mode: ranked}</c>, İptal <c>queue.leave</c>, and
    /// <c>match.start</c> opens the table; <c>match.end</c> of that match becomes the result card (LP from the server's
    /// placement). This server matches Solo only and has no ready check: there is no rankedFound / rankedAccept phase yet.
    /// <para>ADAPTER SEAM (docs/v4/ranked/RAPOR.md): when lane B's package protocol lands (protocol.json rankedQueue {mode, party},
    /// rankedAccept {matchId, accept}, rankedCancel; s2c rankedSearch, rankedFound, rankedAccepted, rankedCancelled, rankedReady,
    /// rankChange), each of those maps one to one onto <see cref="IRankedBackend"/>: replace the bodies of QueueAsync / CancelAsync /
    /// AcceptAsync and raise Searching / Found / Accepted / Cancelled / Ready / RankChanged from the s2c messages.</para>
    /// </summary>
    public sealed class RankedOnlineBackend : IRankedBackend
    {
        private readonly GalaxyOnline online;
        private Action offStart;
        private int queueRun;
        private static readonly List<RankedPlayer> NoFriends = new List<RankedPlayer>();

        public RankedOnlineBackend(GalaxyOnline online) { this.online = online ?? throw new ArgumentNullException(nameof(online)); }

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
            return p;
        }

        /// <summary>Party invites have no message in today's protocol (EK-702): Duo / Squad cannot fill.</summary>
        public IReadOnlyList<RankedPlayer> Friends => NoFriends;

        public event Action Changed { add { online.Changed += value; } remove { online.Changed -= value; } }
        public event Action<string, bool> PartyAnswered { add { } remove { } }
        public event Action<RankedSearch> Searching;
        public event Action<RankedFound> Found { add { } remove { } }
        public event Action<IReadOnlyList<bool>> Accepted { add { } remove { } }
        public event Action<string> Cancelled { add { } remove { } }
        public event Action<int> Ready { add { } remove { } }
        public event Action<RankedStart> Started;
        public event Action<RankedResult> RankChanged { add { } remove { } }

        public Task<string> InviteAsync(RankedPlayer friend) => Task.FromResult("E_MODE");

        public async Task<string> QueueAsync(string mode, IReadOnlyList<string> party)
        {
            if (mode != "solo") return "E_MODE";
            int run = ++queueRun;
            offStart?.Invoke();
            offStart = online.Realtime.On<MatchStartMessage>(MessageTypes.MatchStart, m =>
            {
                if (run != queueRun || m == null || m.Spectator || m.Mode != GameModes.Ranked) return;
                offStart?.Invoke(); offStart = null; queueRun++;
                Watch(m, Profile);
                Started?.Invoke(new RankedStart { Mode = "solo", Match = m, Players = m.Seats.Where(s => s?.Player != null).Select(s => new RankedPlayer
                {
                    Id = s.Player.Id.ToString(), Name = s.Player.Name, Color = s.Player.Color, Idx = RankedLadder.FromCode(s.Player.League), Me = s.Seat == m.YourSeat
                }).ToList() });
            });
            var ack = await online.Realtime.RequestAsync(MessageTypes.QueueJoin, new QueueJoinMessage { Mode = GameModes.Ranked, Stake = 0 });
            if (!ack.Ok)
            {
                if (run == queueRun) { offStart?.Invoke(); offStart = null; }
                return ack.Code ?? "error"; // conflict, offline, timeout: RankedScreen.ErrorText
            }
            Searching?.Invoke(new RankedSearch { Range = 1 });
            Widen(run);
            return null;
        }

        /// <summary>The server widens the window itself after rankedWiden (30 000 ms, protocol/timings.md) without saying so.</summary>
        private async void Widen(int run)
        {
            await Task.Delay(30000);
            if (run == queueRun) Searching?.Invoke(new RankedSearch { Range = 2 });
        }

        public Task CancelAsync()
        {
            queueRun++; offStart?.Invoke(); offStart = null;
            return online.Realtime.RequestAsync(MessageTypes.QueueLeave, new { });
        }

        public Task<string> AcceptAsync(string matchId, bool accept) => Task.FromResult<string>(null);

        /// <summary>
        /// match.end of the ranked match, while the table is up: the placement's LP (RankedRules for the tier / league event and
        /// D-30 protection) waits in <see cref="RankedScreen.Pending"/> for the menu, which opens the screen again on return.
        /// </summary>
        private void Watch(MatchStartMessage start, RankedProfile before)
        {
            Action off = null;
            off = online.Realtime.On<MatchEndMessage>(MessageTypes.MatchEnd, end =>
            {
                if (end == null || end.MatchId != start.MatchId) return;
                off?.Invoke();
                var mine = end.Placements?.FirstOrDefault(p => p.Seat == start.YourSeat);
                if (end.Aborted || mine == null) return;
                var result = RankedRules.Apply(before.Clone(), mine.Left ? 4 : mine.Place, "solo", mine.LpDelta);
                result.Left = mine.Left;
                RankedScreen.Pending = result; // the screen is gone with the menu scene; it shows this when it opens again
            });
        }
    }
}

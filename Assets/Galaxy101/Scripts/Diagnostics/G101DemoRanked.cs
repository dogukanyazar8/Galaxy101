#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Galaxy101.Diagnostics
{
    /// <summary>
    /// The ranked season the prototype's state runs show (G101-Dereceli.dc.html FRIENDS, OPP, the default rankedOf()
    /// {idx 7, lp 72, prot 0, peak 7, games 0, top 0}, "Sezon 3 · 18 gün kaldı"), and its simulated matchmaking: invites answered
    /// after 1.3–2.2 s, opponents found one by one (1.5 s + 1–3.2 s each, ±1 league, ±2 after 30 s), found → 10 s accept with
    /// the others accepting after 0.6–3.2 s, rankedLoading 1800 ms, then the start. DEMO_LIST "Durum yönlendirici"; never in a
    /// release build — the real season and queue are the server's (<see cref="RankedOnlineBackend"/>).
    /// </summary>
    public sealed class G101DemoRanked : IRankedBackend
    {
        // FRIENDS: name, colour, league step, online.
        private static readonly (string Id, string Name, string Color, int Idx, bool Online)[] FriendList =
        {
            ("ay", "Ayşe Y.", "#3D7BFF", 6, true), ("bc", "Burak C.", "#8B5CF6", 9, true), ("eb", "Emre B.", "#3CCB6E", 5, true), ("mk", "Melis K.", "#E85D9A", 8, true),
            ("sa", "Selin A.", "#FF6B6B", 13, true), ("ht", "Hakan T.", "#22B8CF", 7, false), ("cy", "Can Y.", "#F5A623", 2, true)
        };
        // OPP: the opponents' names and colours.
        private static readonly (string Name, string Color)[] Opponents =
        {
            ("Kerem D.", "#5B8CFF"), ("Zeynep A.", "#E85D9A"), ("Mert Ç.", "#16B6A8"), ("Elif S.", "#FF8A5B"), ("Baran K.", "#8B5CF6"), ("Deniz Y.", "#3CCB6E"),
            ("Ece T.", "#FF6B6B"), ("Onur B.", "#22B8CF"), ("Cansu P.", "#F5A623"), ("Yiğit M.", "#7A8CFF"), ("Nehir G.", "#C86BFF"), ("Arda U.", "#2FBF8F")
        };

        private readonly Mulberry32 random = GameClock.Stream("ranked-demo");
        private readonly List<RankedPlayer> friends;
        private readonly Dictionary<string, RankedPlayer> party = new Dictionary<string, RankedPlayer>();
        private int run;
        private RankedFound found;
        private bool mine;
        private string mode = "solo";

        public G101DemoRanked()
        {
            friends = FriendList.Select(f => new RankedPlayer { Id = f.Id, Name = f.Name, Color = f.Color, Idx = f.Idx, Online = f.Online }).ToList();
        }

        public RankedProfile Profile { get; } = new RankedProfile { Idx = 7, Lp = 72, Protected = 0, Peak = 7, Games = 0, Top = 0, Season = 3, DaysLeft = 18, SeasonRank = 87 };
        public string MeId => "me";
        public IReadOnlyList<RankedPlayer> Friends => friends;
        /// <summary>The state account's name and colour (G101StateRouter.AccountName / AccountColor).</summary>
        public string MeName = G101StateRouter.AccountName, MeColor = G101StateRouter.AccountColor;

        public event Action Changed;
        public event Action<string, bool> PartyAnswered;
        public event Action<RankedSearch> Searching;
        public event Action<RankedFound> Found;
        public event Action<IReadOnlyList<bool>> Accepted;
        public event Action<string> Cancelled;
        public event Action<int> Ready;
        public event Action<RankedStart> Started;
        public event Action<RankedResult> RankChanged;

        private double Next() => random.NextDouble();
        private static Task Wait(double ms) => Task.Delay(Math.Max(1, (int)ms));

        public async Task<string> InviteAsync(RankedPlayer friend)
        {
            party[friend.Id] = friend;
            await Wait(1300 + Next() * 900);
            if (party.ContainsKey(friend.Id)) PartyAnswered?.Invoke(friend.Id, true);
            return null;
        }

        public Task<string> QueueAsync(string queueMode, IReadOnlyList<string> members)
        {
            mode = queueMode; int me = ++run;
            var team = members.Select(id => party.TryGetValue(id, out var p) ? p : friends.FirstOrDefault(f => f.Id == id)).Where(p => p != null).ToList();
            if (queueMode == "squad") { Find(me, team, new List<RankedPlayer>()); return Task.FromResult<string>(null); }
            _ = Search(me, queueMode == "duo" ? 2 : 3, team);
            return Task.FromResult<string>(null);
        }

        private async Task Search(int me, int n, List<RankedPlayer> team)
        {
            var started = DateTime.UtcNow; var opps = new List<RankedPlayer>(); double t = 1.5;
            var plan = new List<double>(); for (int i = 0; i < n; i++) { t += 1 + Next() * 2.2; plan.Add(t); }
            Searching?.Invoke(new RankedSearch { Range = 1, Opponents = opps.ToList() });
            while (opps.Count < n)
            {
                await Wait(250);
                if (me != run) return;
                double sec = (DateTime.UtcNow - started).TotalSeconds; bool wide = sec >= 30;
                bool changed = false;
                while (opps.Count < n && sec >= plan[opps.Count]) { opps.Add(Opponent(opps, wide)); changed = true; }
                if (changed || wide) Searching?.Invoke(new RankedSearch { Range = wide ? 2 : 1, Filled = opps.Count, Opponents = opps.ToList() });
            }
            await Wait(700);
            if (me != run) return;
            Find(me, team, opps);
        }

        /// <summary>mkOpp(): a name not yet used, a step within ±3 (±6 when wide) of yours, kept inside the league window.</summary>
        private RankedPlayer Opponent(List<RankedPlayer> used, bool wide)
        {
            var pool = Opponents.Where(o => used.All(u => u.Name != o.Name)).ToList(); var pick = pool[(int)(Next() * pool.Count)];
            int span = wide ? 6 : 3, idx = Math.Max(0, Math.Min(14, Profile.Idx + (int)Math.Round((Next() * 2 - 1) * span)));
            if (Math.Abs(RankedLadder.LeagueOf(Profile.Idx) - RankedLadder.LeagueOf(idx)) > (wide ? 2 : 1)) idx = Profile.Idx;
            return new RankedPlayer { Id = "o" + pick.Name, Name = pick.Name, Color = pick.Color, Idx = idx };
        }

        private void Find(int me, List<RankedPlayer> team, List<RankedPlayer> opps)
        {
            var self = new RankedPlayer { Id = MeId, Name = MeName, Color = MeColor, Idx = Profile.Idx, Me = true };
            List<RankedPlayer> players;
            if (mode == "duo") players = new List<RankedPlayer> { self, opps[0], team.FirstOrDefault() ?? opps[0], opps[1] }; // D-29: you and your partner at 0 and 2
            else if (mode == "squad") players = new[] { self }.Concat(team).ToList();
            else players = new[] { self }.Concat(opps).ToList();
            found = new RankedFound { MatchId = "rk-demo-" + me, Players = players, Accepted = players.Select(_ => false).ToList(), Seconds = 10 };
            mine = false;
            Found?.Invoke(found);
            for (int i = 0; i < players.Count; i++) if (!players[i].Me) _ = AcceptLater(me, i, 600 + Next() * 2600);
            _ = Expire(me);
        }

        private async Task AcceptLater(int me, int seat, double ms)
        {
            await Wait(ms);
            if (me != run || found == null) return;
            found.Accepted[seat] = true; Accepted?.Invoke(found.Accepted.ToList());
            Check(me);
        }

        /// <summary>rankedAccept: 10 000 ms, then back out with "timeout".</summary>
        private async Task Expire(int me)
        {
            await Wait(found.Seconds * 1000);
            if (me != run || found == null || found.Accepted.All(a => a)) return;
            run++; found = null; Cancelled?.Invoke("timeout");
        }

        private async void Check(int me)
        {
            if (found == null || !mine || !found.Accepted.All(a => a)) return;
            var players = found.Players; run++; int done = run;
            Ready?.Invoke(1800);
            await Wait(1800); // rankedLoading (protocol/timings.md)
            if (done != run) return;
            found = null;
            Started?.Invoke(new RankedStart { Mode = mode, Players = players });
        }

        public Task<string> AcceptAsync(string matchId, bool accept)
        {
            if (found == null || found.MatchId != matchId) return Task.FromResult<string>(null);
            if (!accept) { run++; found = null; return Task.FromResult<string>(null); }
            mine = true; int me = found.Players.FindIndex(p => p.Me); if (me >= 0) found.Accepted[me] = true;
            Accepted?.Invoke(found.Accepted.ToList());
            Check(run);
            return Task.FromResult<string>(null);
        }

        public Task CancelAsync() { run++; found = null; return Task.CompletedTask; }

        /// <summary>A finished match (applyRanked): the standing moves and the screen shows the result card.</summary>
        public RankedResult Finish(int place)
        {
            var result = RankedRules.Apply(Profile, place, mode);
            Changed?.Invoke(); RankChanged?.Invoke(result);
            return result;
        }
    }
}
#endif

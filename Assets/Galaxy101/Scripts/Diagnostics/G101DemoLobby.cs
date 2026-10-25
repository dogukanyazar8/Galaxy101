#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Galaxy101.Online;

namespace Galaxy101.Diagnostics
{
    /// <summary>
    /// The lobby the prototype's state runs show (G101-Lobi.dc.html SEED, NAMES, FRIENDS — prototype data, DEMO_LIST
    /// "Durum yönlendirici"): the state router feeds it to LobbyScreen so S09–S15 open on the same tables. Never in a
    /// release build; the real lobby is the server's.
    /// </summary>
    public sealed class G101DemoLobby : ILobbyBackend
    {
        private static readonly (string Name, string Color, int Level)[] Names =
        {
            ("Zeynep E.", "#16B6A8", 31), ("Ayşe Y.", "#3D7BFF", 42), ("Burak C.", "#8B5CF6", 18), ("Selin A.", "#FF6B6B", 52), ("Hakan T.", "#3D7BFF", 27),
            ("Derya K.", "#8B5CF6", 35), ("Emre B.", "#3CCB6E", 19), ("Okeyci_34", "#16B6A8", 61), ("Mert K.", "#E8A317", 22), ("Elif S.", "#E05FA8", 29),
            ("Can Ö.", "#FF8A5B", 33), ("Gizem T.", "#16B6A8", 24), ("Kaan D.", "#3D7BFF", 40), ("Nazlı R.", "#FF6B6B", 17), ("Oğuz P.", "#8B5CF6", 45),
            ("Ece M.", "#3CCB6E", 26), ("Tolga A.", "#E8A317", 38), ("Pelin Ş.", "#E05FA8", 21), ("Serkan Y.", "#FF8A5B", 49), ("İrem B.", "#3D7BFF", 30)
        };
        private static readonly (string Name, string Color, int Level, string Presence, bool Friend)[] FriendList =
        {
            ("Mert K.", "#16B6A8", 38, "online", true), ("Elif S.", "#FF6B6B", 44, "online", true), ("Can D.", "#8B5CF6", 21, "online", true), ("Ece T.", "#3D7BFF", 29, "playing", true),
            ("Kaan Y.", "#F5A623", 56, "online", true), ("Naz B.", "#16B6A8", 17, "offline", true), ("Oğuz A.", "#3D7BFF", 33, "playing", true), ("Defne Ç.", "#FF6B6B", 25, "offline", true),
            ("Berk U.", "#8B5CF6", 40, "online", false), ("İrem G.", "#F5A623", 19, "online", false), ("Tolga F.", "#16B6A8", 47, "offline", false), ("Sena M.", "#3D7BFF", 31, "playing", false)
        };

        private static Guid Id(string key) { var b = new byte[16]; var k = System.Text.Encoding.UTF8.GetBytes(key); Array.Copy(k, b, Math.Min(16, k.Length)); return new Guid(b); }
        private static PlayerSummary Player(int i) => new PlayerSummary { Id = Id("p" + i), Name = Names[i].Name, Color = Names[i].Color, Level = Names[i].Level };

        // mk(id, name, type, bet, hands, turn, kat, players, status, hand, extra) — SEED() in G101-Lobi.dc.html.
        private static TableSummary Mk(string id, string name, string type, long bet, int hands, int turn, bool kat, int?[] players, string status, int hand,
            string pass = null, bool okey = true, bool isle = true, bool spec = true, string chat = "on", bool gifts = true)
        {
            var t = new TableSummary
            {
                Id = Id(id), Name = name, Type = type == "esli" ? "pairs" : "solo", Stake = bet, Hands = hands, TurnSeconds = turn, Doubling = kat, Locked = pass != null,
                Spectators = spec, Chat = chat, Gifts = gifts, Processing = true, Penalties = isle, OkeyPenalty = okey, Status = status, Hand = hand,
                Seats = players.Select(p => p.HasValue ? new TableSeat { Player = Player(p.Value) } : null).ToList()
            };
            t.HostId = t.Seats.FirstOrDefault(s => s != null)?.Player.Id ?? Guid.Empty;
            if (status == "playing") t.OpenSeats = t.Seats.Count(s => s == null);
            return t;
        }

        private readonly List<TableSummary> tables = new List<TableSummary>
        {
            Mk("t1", "Gece Kuşları", "tekli", 5000, 5, 30, false, new int?[] { 0, 2, 4, null }, "waiting", 0),
            Mk("t2", "Akşam Keyfi", "esli", 10000, 7, 45, true, new int?[] { 5, 9, null, null }, "waiting", 0),
            Mk("t3", "Hızlı Eller", "tekli", 1000, 3, 15, false, new int?[] { 6, null, null, null }, "waiting", 0, isle: false),
            Mk("t4", "Usta Masası", "tekli", 50000, 5, 30, false, new int?[] { 7, 3, 14, 18 }, "playing", 3),
            Mk("t5", "Kadıköy 101", "esli", 5000, 5, 30, true, new int?[] { 10, null, 12, 16 }, "playing", 2),
            Mk("t6", "Şifreli Salon", "tekli", 10000, 5, 20, false, new int?[] { 1, 11, null, null }, "waiting", 0, pass: "1234"),
            Mk("t7", "Yıldız Tozu", "tekli", 100000, 7, 45, false, new int?[] { 18, 7, 3, 14 }, "playing", 5, spec: false),
            Mk("t8", "Çay Ocağı", "tekli", 1000, 5, 30, false, new int?[] { 13, 15, 17, null }, "waiting", 0, chat: "quick"),
            Mk("t9", "Takım Ruhu", "esli", 1000, 3, 20, false, new int?[] { 8, null, null, null }, "waiting", 0, gifts: false),
            Mk("t10", "Satürn Halkası", "tekli", 5000, 3, 30, false, new int?[] { 4, 19, 0, 6 }, "playing", 1),
            Mk("t11", "Gece Yarısı", "esli", 50000, 5, 30, false, new int?[] { 16, 12, 1, 5 }, "playing", 4, pass: "1234"),
            Mk("t12", "Sabah Sporu", "tekli", 5000, 7, 20, false, new int?[] { 2, 9, 11, 13 }, "waiting", 0, okey: false),
        };

        public bool Online => true;
        public Guid Me { get; } = Id("me");
        public long Chips { get; set; } = G101StateRouter.AccountChips;
        public IReadOnlyList<TableSummary> Tables => tables;
        public TableStateMessage Room { get; private set; }
        public event Action TablesChanged;
        public event Action<TableStateMessage> RoomChanged;
        public event Action ChipsChanged;

        /// <summary>resolveArg('$tbl:pass' | '$tbl:open' | '$tbl:mine' | '$tbl:full') of g101-state.js.</summary>
        public TableSummary Pick(string kind)
        {
            bool Free(TableSummary t) => t.Seats.Any(s => s == null);
            return tables.FirstOrDefault(t => kind == "pass" ? t.Locked
                : kind == "open" || kind == "mine" ? !t.Locked && !t.Private && Free(t) && t.Status != "playing"
                : kind == "full" && !Free(t)) ?? throw new InvalidOperationException("masa bulunamadı: " + kind);
        }

        /// <summary>enterWait(t, owner): the waiting room of <paramref name="t"/> as the prototype draws it — its players as they are, you as its owner.</summary>
        public void EnterWait(TableSummary t, bool owner)
        {
            if (owner) t.HostId = Me;
            Room = new TableStateMessage { Table = t, Code = "GX-7Q2M", YourSeat = 0 };
            RoomChanged?.Invoke(Room);
        }

        public Task<string> SubscribeAsync() { TablesChanged?.Invoke(); return Task.FromResult<string>(null); }
        public void Unsubscribe() { }
        public Task<string> CreateAsync(TableOptions options) => Task.FromResult("E_DEMO");
        public Task<string> JoinAsync(TableJoinMessage join) => Task.FromResult(join.Pin != null && join.Pin != "1234" ? "E_PIN_WRONG" : null);
        public Task<string> WatchAsync(TableJoinMessage join) => Task.FromResult<string>(null);
        public Task<string> LeaveAsync() { Room = null; return Task.FromResult<string>(null); }
        public Task<string> AddBotAsync(int seat) => Task.FromResult<string>(null);
        public Task<string> KickAsync(int seat) => Task.FromResult<string>(null);
        public Task<string> StartAsync() => Task.FromResult<string>(null);
        public Task<List<FriendResponse>> FriendsAsync() => Task.FromResult(FriendList.Where(f => f.Friend).Select(Friend).ToList());
        public Task<List<FriendResponse>> RecentAsync() => Task.FromResult(FriendList.Where(f => !f.Friend).Select(Friend).ToList());
        public Task<string> InviteAsync(Guid friend, string code) => Task.FromResult<string>(null);
        private static FriendResponse Friend((string Name, string Color, int Level, string Presence, bool Friend) f) =>
            new FriendResponse { Player = new PlayerSummary { Id = Id("f" + f.Name), Name = f.Name, Color = f.Color, Level = f.Level }, Presence = f.Presence };
        private void Unused() { ChipsChanged?.Invoke(); }
    }
}
#endif

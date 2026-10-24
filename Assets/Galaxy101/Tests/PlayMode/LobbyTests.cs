using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Galaxy101.Online;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>
    /// Klasik lobby (spec/20, S09–S15): list, filters, sit / PIN / code, Masa aç, waiting room, invites, match start.
    /// The UI tests drive a scripted backend; <see cref="LiveServer"/> plays the same flow against a running game server
    /// (GALAXY101_SERVER, default localhost:5173) and is Inconclusive without one.
    /// </summary>
    public sealed class LobbyTests
    {
        private GameObject owner;
        private string statePath;
        private FakeLobby fake;

        private sealed class FakeLobby : ILobbyBackend
        {
            public bool Online { get; set; } = true;
            public Guid Me { get; } = Guid.NewGuid();
            public long Chips { get; set; } = 25000;
            public List<TableSummary> List = new List<TableSummary>();
            public IReadOnlyList<TableSummary> Tables => List;
            public TableStateMessage Room { get; private set; }
            public event Action TablesChanged;
            public event Action<TableStateMessage> RoomChanged;
            public event Action ChipsChanged;
            public TableOptions Created;
            public TableJoinMessage Joined;
            public string JoinAnswer, CreateAnswer;
            public int Subscribes, Starts;
            public readonly List<(Guid, string)> Invites = new List<(Guid, string)>();
            public List<FriendResponse> Friends = new List<FriendResponse>();

            public Task<string> SubscribeAsync() { Subscribes++; return Task.FromResult(Online ? null : "offline"); }
            public void Unsubscribe() { }
            public void Push() => TablesChanged?.Invoke();
            public void Balance(long chips) { Chips = chips; ChipsChanged?.Invoke(); }

            public Task<string> CreateAsync(TableOptions options)
            {
                Created = options; if (CreateAnswer != null) return Task.FromResult(CreateAnswer);
                var t = Summary(options.Name, options.Type, options.Stake, Me, options.Visibility == "private", options.Pin != null);
                t.Seats[0] = new TableSeat { Player = Player(Me, "Test Oyuncu", "#FF8A5B") };
                Set(new TableStateMessage { Table = t, Code = "GX-AB12", YourSeat = 0 });
                return Task.FromResult<string>(null);
            }

            public Task<string> JoinAsync(TableJoinMessage join)
            {
                Joined = join; if (JoinAnswer != null) return Task.FromResult(JoinAnswer);
                var t = join.TableId != null ? List.First(x => x.Id == join.TableId) : List.First();
                int seat = t.Seats.FindIndex(s => s == null); t.Seats[seat] = new TableSeat { Player = Player(Me, "Test Oyuncu", "#FF8A5B") };
                Set(new TableStateMessage { Table = t, Code = "GX-CD34", YourSeat = seat });
                return Task.FromResult<string>(null);
            }

            public Task<string> LeaveAsync() { var t = Room.Table; Room = null; RoomChanged?.Invoke(new TableStateMessage { Table = t, Closed = true, Reason = "left", YourSeat = -1 }); return Task.FromResult<string>(null); }

            public Task<string> AddBotAsync(int seat)
            {
                var t = Room.Table; t.Seats[seat] = new TableSeat { Bot = true, Player = Player(Guid.NewGuid(), "Bot · Ayşe", "#5B6390") };
                Set(new TableStateMessage { Table = t, Code = Room.Code, YourSeat = Room.YourSeat, Countdown = t.Seats.All(s => s != null) ? 5 : 0 });
                return Task.FromResult<string>(null);
            }

            public Task<string> KickAsync(int seat) { var t = Room.Table; t.Seats[seat] = null; Set(new TableStateMessage { Table = t, Code = Room.Code, YourSeat = Room.YourSeat }); return Task.FromResult<string>(null); }
            public Task<string> StartAsync() { Starts++; Set(new TableStateMessage { Table = Room.Table, Code = Room.Code, YourSeat = Room.YourSeat, Countdown = 3 }); return Task.FromResult<string>(null); }
            public Task<List<FriendResponse>> FriendsAsync() => Task.FromResult(Friends);
            public Task<List<FriendResponse>> RecentAsync() => Task.FromResult(new List<FriendResponse>());
            public Task<string> InviteAsync(Guid friend, string code) { Invites.Add((friend, code)); return Task.FromResult<string>(null); }

            public void Set(TableStateMessage state) { Room = state.Closed ? null : state; RoomChanged?.Invoke(state); }
            public void Started() { var t = Room.Table; var seat = Room.YourSeat; Room = null; RoomChanged?.Invoke(new TableStateMessage { Table = t, Closed = true, MatchId = Guid.NewGuid(), Reason = "started", YourSeat = seat }); }
        }

        private static PlayerSummary Player(Guid id, string name, string color) => new PlayerSummary { Id = id, Name = name, Color = color, Level = 12 };

        private static TableSummary Summary(string name, string type, long stake, Guid host, bool priv = false, bool locked = false, string status = "waiting", int seated = 1, bool doubling = false)
        {
            var t = new TableSummary { Id = Guid.NewGuid(), Name = name, Type = type, Stake = stake, Hands = 5, TurnSeconds = 30, Status = status, HostId = host, Private = priv, Locked = locked,
                Spectators = true, Chat = "on", Gifts = true, Processing = true, Penalties = true, Doubling = doubling, Hand = status == "playing" ? 2 : 0, Seats = new List<TableSeat> { null, null, null, null } };
            for (int s = 0; s < seated; s++) t.Seats[s] = new TableSeat { Player = Player(s == 0 ? host : Guid.NewGuid(), "Oyuncu " + s, "#3D7BFF") };
            return t;
        }

        [SetUp]
        public void SetUp()
        {
            fake = new FakeLobby();
            var host = Guid.NewGuid();
            fake.List.Add(Summary("Hızlı Eller", "single", 1000, host));
            fake.List.Add(Summary("Çay Ocağı", "single", 5000, Guid.NewGuid(), seated: 3));
            fake.List.Add(Summary("Takım Ruhu", "pairs", 1000, Guid.NewGuid(), doubling: true));
            fake.List.Add(Summary("Şifreli Salon", "single", 10000, Guid.NewGuid(), locked: true, seated: 2));
            fake.List.Add(Summary("Kadıköy", "pairs", 5000, Guid.NewGuid(), status: "playing", seated: 3));
            fake.List.Add(Summary("Usta Masası", "single", 50000, Guid.NewGuid(), seated: 2));
            fake.List.Add(Summary("Dolu Masa", "single", 1000, Guid.NewGuid(), status: "playing", seated: 4));
            MainMenuController.LobbyBackend = () => fake; MainMenuController.OpenMatchTable = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MainMenuController.LobbyBackend = null; MainMenuController.AuthOnStart = null; MainMenuController.OpenMatchTable = true;
            if (owner != null) UnityEngine.Object.Destroy(owner);
            if (GalaxyOnline.Instance != null) UnityEngine.Object.Destroy(GalaxyOnline.Instance.gameObject);
            GalaxyOnline.Forget(); // the test's guest session must not be resumed by the next real start
            yield return null;
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        private MainMenuController Menu()
        {
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-lobby-" + Guid.NewGuid().ToString("N") + ".json");
            MainMenuController.AuthOnStart = false;
            owner = new GameObject("Menu"); owner.SetActive(false);
            owner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var menu = owner.AddComponent<MainMenuController>(); owner.SetActive(true); menu.Initialize(statePath);
            return menu;
        }

        private static void Click(VisualElement root, string name)
        {
            var e = root.Q<VisualElement>(name); Assert.That(e, Is.Not.Null, "element " + name);
            using (var evt = ClickEvent.GetPooled()) { evt.target = e; e.SendEvent(evt); }
        }

        private static IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition()) { if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what); yield return null; }
        }

        private static string Dir => Environment.GetEnvironmentVariable("GALAXY_LOBBY_CAPTURE_DIR");
        private IEnumerator Shot(string file) { if (!string.IsNullOrEmpty(Dir)) yield return DisplaySettingsTests.Shot(owner.GetComponent<UIDocument>(), 1280, 720, Path.Combine(Dir, file)); }

        private static int Cards(VisualElement root) => root.Query<VisualElement>(className: "lobby-card").ToList().Count;

        // G101-Lobi SEED(): the prototype's twelve tables, so the list can be measured against the design box for box.
        private static readonly string[] SeedNames = { "Zeynep E.", "Ayşe Y.", "Burak C.", "Selin A.", "Hakan T.", "Derya K.", "Emre B.", "Okeyci_34", "Mert K.", "Elif S.", "Can Ö.", "Gizem T.", "Kaan D.", "Nazlı R.", "Oğuz P.", "Ece M.", "Tolga A.", "Pelin Ş.", "Serkan Y.", "İrem B." };
        private static readonly string[] SeedColors = { "#16B6A8", "#3D7BFF", "#8B5CF6", "#FF6B6B", "#3D7BFF", "#8B5CF6", "#3CCB6E", "#16B6A8", "#E8A317", "#E05FA8", "#FF8A5B", "#16B6A8", "#3D7BFF", "#FF6B6B", "#8B5CF6", "#3CCB6E", "#E8A317", "#E05FA8", "#FF8A5B", "#3D7BFF" };

        private static TableSummary Seed(string name, string type, long stake, int hands, int turn, bool doubling, int[] players, string status, int hand,
            bool locked = false, bool spec = true, string chat = "on", bool gifts = true, bool okey = true, bool isle = true)
        {
            var t = new TableSummary { Id = Guid.NewGuid(), Name = name, Type = type == "esli" ? "pairs" : "single", Stake = stake, Hands = hands, TurnSeconds = turn, Doubling = doubling, Status = status, Hand = hand,
                Locked = locked, Spectators = spec, Chat = chat, Gifts = gifts, OkeyPenalty = okey, Penalties = isle, Processing = true, HostId = Guid.NewGuid(), Seats = new List<TableSeat>() };
            foreach (int p in players) t.Seats.Add(p < 0 ? null : new TableSeat { Player = new PlayerSummary { Id = Guid.NewGuid(), Name = SeedNames[p], Color = SeedColors[p], Level = 20 } });
            return t;
        }

        private static List<TableSummary> ReferenceSeed() => new List<TableSummary>
        {
            Seed("Gece Kuşları", "tekli", 5000, 5, 30, false, new[] { 0, 2, 4, -1 }, "waiting", 0),
            Seed("Akşam Keyfi", "esli", 10000, 7, 45, true, new[] { 5, 9, -1, -1 }, "waiting", 0),
            Seed("Hızlı Eller", "tekli", 1000, 3, 15, false, new[] { 6, -1, -1, -1 }, "waiting", 0, isle: false),
            Seed("Usta Masası", "tekli", 50000, 5, 30, false, new[] { 7, 3, 14, 18 }, "playing", 3),
            Seed("Kadıköy 101", "esli", 5000, 5, 30, true, new[] { 10, -1, 12, 16 }, "playing", 2),
            Seed("Şifreli Salon", "tekli", 10000, 5, 20, false, new[] { 1, 11, -1, -1 }, "waiting", 0, locked: true),
            Seed("Yıldız Tozu", "tekli", 100000, 7, 45, false, new[] { 18, 7, 3, 14 }, "playing", 5, spec: false),
            Seed("Çay Ocağı", "tekli", 1000, 5, 30, false, new[] { 13, 15, 17, -1 }, "waiting", 0, chat: "quick"),
            Seed("Takım Ruhu", "esli", 1000, 3, 20, false, new[] { 8, -1, -1, -1 }, "waiting", 0, gifts: false),
            Seed("Satürn Halkası", "tekli", 5000, 3, 30, false, new[] { 4, 19, 0, 6 }, "playing", 1),
            Seed("Gece Yarısı", "esli", 50000, 5, 30, false, new[] { 16, 12, 1, 5 }, "playing", 4, locked: true),
            Seed("Sabah Sporu", "tekli", 5000, 7, 20, false, new[] { 2, 9, 11, 13 }, "waiting", 0, okey: false),
        };

        /// <summary>The list with the design's own tables, dumped for tools/compare_ui.py (only with GALAXY_UI_DUMP_DIR).</summary>
        [UnityTest]
        public IEnumerator ReferenceListLayout()
        {
            fake.List = ReferenceSeed(); fake.Chips = 10000;
            var menu = Menu(); var root = menu.View.Root; yield return null;
            menu.Model.Profile["name"] = "Misafir1549";
            var play = root.Q<Button>("play-now"); using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = play; play.SendEvent(evt); }
            yield return null; yield return null;
            Assert.That(Cards(root), Is.EqualTo(12));
            var order = menu.Lobby.Filter(fake.List, fake.Chips).Select(t => t.Name).ToList();
            Assert.That(order, Is.EqualTo(new[] { "Hızlı Eller", "Çay Ocağı", "Takım Ruhu", "Gece Kuşları", "Akşam Keyfi", "Şifreli Salon", "Kadıköy 101", "Satürn Halkası", "Usta Masası", "Gece Yarısı", "Yıldız Tozu", "Sabah Sporu" }),
                "G101-Lobi rank: waiting with a seat, playing with a seat, playing, full; cheaper first");
            yield return new WaitForSeconds(1.2f);
            yield return Shot("S09_seed.png");
            yield return UiDump.Capture(root, "S09_lobby");
            if (UiDump.Dir == null) yield break;

            // The rest of tools/ref_capture.py's walk: Masa aç, Kodla katıl (+ a wrong code), then a new open table's room.
            Click(root, "lobby-create"); yield return new WaitForSeconds(.6f);
            yield return UiDump.Capture(root, "S10_create");
            Click(root, "create-cancel"); yield return new WaitForSeconds(.4f);
            Click(root, "lobby-code"); yield return new WaitForSeconds(.6f);
            yield return UiDump.Capture(root, "S12_code");
            fake.JoinAnswer = TableErrors.Closed;
            root.Q<TextField>("dialog-code").value = "GX-ZZ99"; Click(root, "dialog-submit");
            yield return Until(() => root.Q<Label>("dialog-error") != null, 3, "the wrong-code error");
            yield return UiDump.Capture(root, "S12_code_error");
            Click(root, "dialog-cancel"); fake.JoinAnswer = null; yield return new WaitForSeconds(.4f);
            Click(root, "lobby-create"); yield return null; Click(root, "create-submit");
            yield return Until(() => menu.Lobby.Screen == "wait", 3, "the waiting room");
            yield return new WaitForSeconds(1.2f);
            yield return UiDump.Capture(root, "S13_room");
        }

        [UnityTest]
        public IEnumerator HemenOynaOpensTheListWithFiltersSearchAndBalanceRules()
        {
            var menu = Menu(); var root = menu.View.Root; yield return null;
            menu.Model.SelectedMode = 0;
            if (UiDump.Dir != null) { yield return new WaitForSeconds(1f); yield return UiDump.Capture(root, "S06_menu"); }
            var play = root.Q<Button>("play-now"); using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = play; play.SendEvent(evt); }
            yield return null; yield return null;
            Assert.That(menu.Lobby, Is.Not.Null); Assert.That(menu.Lobby.Visible, Is.True); Assert.That(fake.Subscribes, Is.GreaterThan(0));
            Assert.That(root.Q<VisualElement>("main-layer").resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "the menu hides under the lobby");
            Assert.That(Cards(root), Is.EqualTo(7));
            yield return new WaitForSeconds(.7f);
            yield return Shot("S09_list.png");

            // Order: waiting with a free seat first (cheaper first), then playing with a seat, playing, full.
            var order = menu.Lobby.Filter(fake.List, fake.Chips).Select(t => t.Name).ToList();
            Assert.That(order.Take(4), Is.EqualTo(new[] { "Hızlı Eller", "Takım Ruhu", "Çay Ocağı", "Şifreli Salon" }));
            Assert.That(order.Last(), Is.EqualTo("Dolu Masa"));

            Click(root, "lobby-type-pairs"); yield return null; Assert.That(Cards(root), Is.EqualTo(2));
            Click(root, "lobby-doubling"); yield return null; Assert.That(Cards(root), Is.EqualTo(1));
            Click(root, "lobby-type-all"); Click(root, "lobby-doubling"); yield return null;
            Click(root, "lobby-stake-5000"); yield return null; Assert.That(Cards(root), Is.EqualTo(2));
            Click(root, "lobby-stake-0"); Click(root, "lobby-free"); yield return null; Assert.That(Cards(root), Is.EqualTo(6), "the full table drops out");
            Click(root, "lobby-free"); yield return null;
            root.Q<TextField>("lobby-search").value = "çay"; yield return null;
            Assert.That(Cards(root), Is.EqualTo(1), "Turkish-insensitive search");
            root.Q<TextField>("lobby-search").value = "yok böyle"; yield return null;
            Assert.That(root.Q<VisualElement>("lobby-empty"), Is.Not.Null);
            Click(root, "lobby-clear-filters"); yield return null; Assert.That(Cards(root), Is.EqualTo(7));

            // Live updates: a new table arrives.
            fake.List.Add(Summary("Yeni Masa", "single", 1000, Guid.NewGuid())); fake.Push(); yield return null;
            Assert.That(Cards(root), Is.EqualTo(8));

            // A stake above the balance: the button is dimmed and says how much is missing.
            var rich = fake.List.First(t => t.Name == "Usta Masası");
            Click(root, "lobby-sit-" + rich.Id); yield return null;
            Assert.That(menu.Lobby.LastToast, Does.Contain(menu.View.N(25000)), "50.000 - 25.000 short");
            Assert.That(fake.Joined, Is.Null);

            // Back to the menu.
            Click(root, "lobby-back"); yield return null;
            Assert.That(menu.Lobby.Visible, Is.False);
            Assert.That(root.Q<VisualElement>("main-layer").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
        }

        [UnityTest]
        public IEnumerator SitConnectsThenWaitingRoomBotsCountdownAndMatchStart()
        {
            var menu = Menu(); var root = menu.View.Root; yield return null;
            menu.OpenLobby(); menu.Lobby.ConnectSeconds = .3f; yield return null;
            TableStateMessage started = null; menu.Lobby.MatchStarted += s => started = s;
            var table = fake.List.First(t => t.Name == "Hızlı Eller");
            Click(root, "lobby-sit-" + table.Id); yield return null;
            Assert.That(menu.Lobby.DialogKind, Is.EqualTo("connect").Or.Null);
            yield return Until(() => menu.Lobby.Screen == "wait" && menu.Lobby.DialogKind == null, 3, "the waiting room");
            Assert.That(fake.Joined.TableId, Is.EqualTo(table.Id));
            Assert.That(root.Q<VisualElement>("room-seat-1"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("room-bot-2"), Is.Null, "a guest cannot add bots");
            Assert.That(root.Q<Label>("room-status").text, Does.Contain("2/4"));

            // Become the host (the server passes the host on) and fill the table with bots.
            var room = fake.Room; room.Table.HostId = fake.Me; fake.Set(room); yield return null;
            Assert.That(root.Q<VisualElement>("room-host"), Is.Not.Null);
            Click(root, "room-bot-2"); yield return null;
            Click(root, "room-bot-3"); yield return null;
            Assert.That(fake.Room.Table.Seats.All(s => s != null), Is.True);
            Assert.That(menu.Lobby.Countdown, Is.InRange(4, 5));
            Assert.That(root.Q<VisualElement>("room-kick-2"), Is.Null, "no kicking during the countdown");
            yield return Shot("S13_room_full.png");

            fake.Started(); yield return null;
            Assert.That(started, Is.Not.Null); Assert.That(started.MatchId, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator PinAndCodeDialogsShakeOnWrongInputAndJoin()
        {
            var menu = Menu(); var root = menu.View.Root; yield return null;
            menu.OpenLobby(); menu.Lobby.ConnectSeconds = .05f; yield return null;
            var locked = fake.List.First(t => t.Locked);
            Click(root, "lobby-sit-" + locked.Id); yield return null;
            Assert.That(menu.Lobby.DialogKind, Is.EqualTo("pin"));
            fake.JoinAnswer = TableErrors.WrongPin;
            root.Q<TextField>("dialog-pin").value = "1111"; yield return null; yield return null;
            Assert.That(fake.Joined.Pin, Is.EqualTo("1111"));
            Assert.That(menu.Lobby.DialogKind, Is.EqualTo("pin"));
            Assert.That(root.Q<Label>("dialog-error").text, Does.Contain("Yanlış").Or.Contain("Wrong"));
            yield return Shot("S11_pin_wrong.png");
            fake.JoinAnswer = null;
            root.Q<TextField>("dialog-pin").value = "1234"; yield return null;
            yield return Until(() => menu.Lobby.Screen == "wait", 3, "joining with the right PIN");
            Assert.That(fake.Joined.Pin, Is.EqualTo("1234"));

            // Code: format check before the server, then the server's "no such table".
            Click(root, "room-leave"); yield return null; yield return null;
            Assert.That(menu.Lobby.Screen, Is.EqualTo("list"));
            Assert.That(LobbyScreen.NormalizeCode("gx7q2m"), Is.EqualTo("GX-7Q2M"));
            Assert.That(LobbyScreen.NormalizeCode("7q2m"), Is.EqualTo("GX-7Q2M"));
            Assert.That(LobbyScreen.NormalizeCode("GX-7Q2"), Is.Null);
            Assert.That(LobbyScreen.NormalizeCode("gx-12"), Is.Null);
            Click(root, "lobby-code"); yield return null;
            Assert.That(menu.Lobby.DialogKind, Is.EqualTo("code"));
            root.Q<TextField>("dialog-code").value = "gx-12"; Click(root, "dialog-submit"); yield return null;
            Assert.That(root.Q<Label>("dialog-error").text, Is.Not.Empty);
            fake.JoinAnswer = TableErrors.Closed;
            root.Q<TextField>("dialog-code").value = "gx-zz99"; Click(root, "dialog-submit");
            yield return Until(() => menu.Lobby.DialogKind == "code" && root.Q<Label>("dialog-error") != null, 3, "the code dialog back with the error");
            Assert.That(fake.Joined.Code, Is.EqualTo("GX-ZZ99"));
            Assert.That(root.Q<Label>("dialog-error").text, Does.Contain("bulunamadı").Or.Contain("No open table"));
            yield return Shot("S12_code_error.png");

            // Full while connecting: the full dialog offers a similar table.
            Click(root, "dialog-cancel"); yield return null;
            fake.JoinAnswer = TableErrors.Full;
            var open = fake.List.First(t => t.Name == "Hızlı Eller");
            Click(root, "lobby-sit-" + open.Id);
            yield return Until(() => menu.Lobby.DialogKind == "full", 3, "the full dialog");
            yield return Shot("lobby_full.png");
            Click(root, "dialog-ok"); yield return null;
            Assert.That(menu.Lobby.DialogKind, Is.Null);
        }

        [UnityTest]
        public IEnumerator FiltersAndCreateOptionsRedrawWithoutInterimFrames()
        {
            var menu = Menu(); var root = menu.View.Root; yield return null;
            menu.OpenLobby(); yield return null;
            fake.Balance(60000); yield return null;
            using (var watch = new FrameWatch(owner.GetComponent<UIDocument>(), new RectInt(0, 0, 1280, 720)))
            {
                yield return new WaitForSecondsRealtime(1f);
                foreach (var name in new[] { "lobby-type-pairs", "lobby-doubling", "lobby-stake-5000", "lobby-free", "lobby-clear-filters" })
                    yield return watch.Stable(() => Click(root, name), name);
                Click(root, "lobby-create"); yield return new WaitForSecondsRealtime(1f);
                foreach (var name in new[] { "create-type-pairs", "create-stake-10000", "create-hands-7", "create-turn-45", "create-doubling", "create-vis-private", "create-pin-toggle", "create-rules" })
                    yield return watch.Stable(() => Click(root, name), name);
            }
        }

        [UnityTest]
        public IEnumerator CreateFormValidatesAndSendsTheChosenOptions()
        {
            var menu = Menu(); var root = menu.View.Root; yield return null;
            menu.Model.Profile["name"] = "Doğukan";
            menu.OpenLobby(); yield return null;
            fake.Balance(12000); yield return null;
            Click(root, "lobby-create"); yield return null;
            Assert.That(menu.Lobby.Draft, Is.Not.Null);
            Assert.That(root.Q<TextField>("create-name").value, Is.EqualTo("Doğukan’in masası"));
            Assert.That(menu.Lobby.Draft.Stake, Is.EqualTo(5000), "the highest stake up to 5K that the balance allows");
            yield return new WaitForSeconds(.4f);
            yield return Shot("S10_create.png");

            Click(root, "create-stake-50000"); yield return null;
            Assert.That(root.Q<VisualElement>("create-warn"), Is.Not.Null, "a locked stake explains what is missing");
            Assert.That(menu.Lobby.Draft.Stake, Is.EqualTo(5000));
            Click(root, "create-stake-10000"); Click(root, "create-type-pairs"); Click(root, "create-hands-7"); Click(root, "create-turn-45"); Click(root, "create-doubling");
            Click(root, "create-vis-private"); Click(root, "create-pin-toggle"); yield return null;
            root.Q<TextField>("create-name").value = "   ";
            Click(root, "create-submit"); yield return null;
            Assert.That(fake.Created, Is.Null, "an empty name and a missing PIN block the form");
            root.Q<TextField>("create-name").value = "Gece Kuşları";
            root.Q<TextField>("create-pin").value = "a123"; yield return null;
            Assert.That(menu.Lobby.Draft.Pin, Is.EqualTo("123"), "digits only");
            root.Q<TextField>("create-pin").value = "1234"; yield return null;
            Click(root, "create-rules"); yield return null;
            Click(root, "create-rule-chat-quick"); Click(root, "create-rule-okey"); yield return null;
            Click(root, "create-submit"); yield return null; yield return null;
            Assert.That(fake.Created, Is.Not.Null);
            Assert.That(fake.Created.Name, Is.EqualTo("Gece Kuşları"));
            Assert.That(fake.Created.Type, Is.EqualTo("pairs")); Assert.That(fake.Created.Stake, Is.EqualTo(10000));
            Assert.That(fake.Created.Hands, Is.EqualTo(7)); Assert.That(fake.Created.TurnSeconds, Is.EqualTo(45)); Assert.That(fake.Created.Doubling, Is.True);
            Assert.That(fake.Created.Visibility, Is.EqualTo("private")); Assert.That(fake.Created.Pin, Is.EqualTo("1234"));
            Assert.That(fake.Created.Chat, Is.EqualTo("quick")); Assert.That(fake.Created.OkeyPenalty, Is.False); Assert.That(fake.Created.Penalties, Is.True); Assert.That(fake.Created.Processing, Is.True);
            Assert.That(menu.Lobby.Screen, Is.EqualTo("wait"));
            Assert.That(menu.Lobby.DialogKind, Is.EqualTo("share"), "a private table shows its code first");
            Assert.That(root.Q<Label>("share-code").text, Is.EqualTo("GX-AB12"));
            yield return new WaitForSeconds(.3f);
            yield return Shot("lobby_share.png");
            Click(root, "share-room"); yield return null;
            Assert.That(root.Q<Label>("room-code").text, Is.EqualTo("GX-AB12"));
            yield return Shot("S13_room.png");
        }

        [UnityTest]
        public IEnumerator InviteModalSendsInvitesAndMarksFriendsWhoSitDown()
        {
            var menu = Menu(); var root = menu.View.Root; yield return null;
            var mert = Guid.NewGuid();
            fake.Friends.Add(new FriendResponse { Player = Player(mert, "Mert K.", "#16B6A8"), Presence = "online" });
            fake.Friends.Add(new FriendResponse { Player = Player(Guid.NewGuid(), "Ece M.", "#3CCB6E"), Presence = "playing" });
            fake.Friends.Add(new FriendResponse { Player = Player(Guid.NewGuid(), "Oğuz P.", "#8B5CF6"), Presence = "offline", LastSeen = DateTimeOffset.UtcNow.AddHours(-3) });
            menu.OpenLobby(); yield return null;
            Click(root, "lobby-create"); yield return null; Click(root, "create-submit"); yield return null; yield return null;
            Assert.That(menu.Lobby.Screen, Is.EqualTo("wait"));
            Click(root, "room-invite-1"); yield return null; yield return null;
            Assert.That(menu.Lobby.InviteOpen, Is.True);
            Assert.That(root.Q<VisualElement>("invite-row-" + mert), Is.Not.Null);
            Click(root, "invite-send-" + mert); yield return null;
            Assert.That(fake.Invites.Single(), Is.EqualTo((mert, "GX-AB12")));
            Assert.That(menu.Lobby.Invite.Sent[mert].State, Is.EqualTo("pend"));
            yield return new WaitForSeconds(.4f);
            yield return Shot("S14_invite.png");

            // Mert accepts: he takes seat 1 and his row says Masada.
            var room = fake.Room; room.Table.Seats[1] = new TableSeat { Player = Player(mert, "Mert K.", "#16B6A8") }; fake.Set(room); yield return null;
            Assert.That(menu.Lobby.Invite.Sent[mert].State, Is.EqualTo("in"));
            Click(root, "invite-close"); yield return null;
            Assert.That(menu.Lobby.InviteOpen, Is.False);
            Assert.That(root.Q<VisualElement>("room-kick-1"), Is.Not.Null, "the host can remove a seated player");
        }

        [UnityTest]
        public IEnumerator OfflineShowsTheConnectionDialog()
        {
            fake.Online = false;
            var menu = Menu(); var root = menu.View.Root; yield return null;
            menu.OpenLobby(); yield return null; yield return null;
            Assert.That(menu.Lobby.DialogKind, Is.EqualTo("offline"));
            fake.Online = true; Click(root, "dialog-alt"); yield return null; yield return null;
            Assert.That(menu.Lobby.DialogKind, Is.Null);
        }

        [UnityTest]
        public IEnumerator TheDeveloperMenuListsTheSpec25Scenarios()
        {
            var menu = Menu(); var root = menu.View.Root; yield return null;
            string picked = null;
            var dev = new DevTestMenu(menu.View, key => picked = key); dev.Show(); yield return null;
            Assert.That(root.Query<VisualElement>(className: null).ToList().Count(e => e.name != null && e.name.StartsWith("dev-scenario-")), Is.EqualTo(DevTestMenu.Scenarios.Length));
            Click(root, "dev-scenario-processing"); yield return null;
            Assert.That(picked, Is.EqualTo("processing")); Assert.That(dev.Visible, Is.False);
            foreach (var name in Galaxy101.Table.TableScenarios.Names) Assert.That(Galaxy101.Table.TableScenarios.Create(name), Is.Not.Null, name);
        }

        // ---------- the real server ----------
        [UnityTest]
        public IEnumerator LiveServer()
        {
            using (var probe = UnityWebRequest.Get(GalaxyApi.ResolveServer() + "/health"))
            {
                probe.timeout = 3; yield return probe.SendWebRequest();
                if (probe.responseCode != 200) Assert.Inconclusive("No game server at " + GalaxyApi.ResolveServer() + " (start Server/ locally to run this).");
            }
            MainMenuController.LobbyBackend = null;
            var online = GalaxyOnline.Ensure();
            var signIn = online.GuestAsync("Lobi" + UnityEngine.Random.Range(1000, 9999), "#3D7BFF"); yield return Until(() => signIn.IsCompleted, 15, "guest sign-in");
            Assert.That(signIn.Exception, Is.Null);
            yield return Until(() => online.Realtime.Connected, 5, "the socket");

            var menu = Menu(); var root = menu.View.Root; yield return null;
            menu.OpenLobby(); menu.Lobby.ConnectSeconds = .1f;
            TableStateMessage started = null; menu.Lobby.MatchStarted += s => started = s;
            yield return new WaitForSeconds(.5f);
            Assert.That(menu.Lobby.DialogKind, Is.Null, "subscribed");

            // A private, PIN-locked table; a second guest joins it by code and PIN through its own socket.
            Click(root, "lobby-create"); yield return null;
            root.Q<TextField>("create-name").value = "Canlı Test " + UnityEngine.Random.Range(10, 99);
            Click(root, "create-vis-private"); Click(root, "create-pin-toggle"); yield return null;
            root.Q<TextField>("create-pin").value = "4321";
            Click(root, "create-submit");
            yield return Until(() => menu.Lobby.Screen == "wait" && menu.Lobby.DialogKind == "share", 10, "the waiting room and its code");
            string code = root.Q<Label>("share-code")?.text; Assert.That(code, Does.Match("^GX-[A-Z0-9]{4}$"));
            Click(root, "share-room"); yield return null;

            var api = new GalaxyApi(null); var realtime = new GalaxyRealtime(api);
            try
            {
                var guest = api.GuestAsync("Ikinci" + UnityEngine.Random.Range(1000, 9999), "#16B6A8"); yield return Until(() => guest.IsCompleted, 15, "second guest");
                var connect = realtime.ConnectAsync(); yield return Until(() => connect.IsCompleted, 10, "second socket");
                var wrong = realtime.RequestAsync(LobbyMessageTypes.Join, new TableJoinMessage { Code = code, Pin = "0000" }); yield return Until(() => wrong.IsCompleted, 10, "wrong PIN");
                Assert.That(wrong.Result.Code, Is.EqualTo(TableErrors.WrongPin));
                var join = realtime.RequestAsync(LobbyMessageTypes.Join, new TableJoinMessage { Code = code.Substring(3).ToLowerInvariant(), Pin = "4321" }); yield return Until(() => join.IsCompleted, 10, "joining by code");
                Assert.That(join.Result.Ok, Is.True, join.Result.Code);
                yield return Until(() => root.Q<Label>("room-status")?.text.Contains("2/4") == true, 10, "the second player in the room");

                // Fill with bots and start; the table becomes a match.
                Click(root, "room-bot-2"); yield return new WaitForSeconds(.5f);
                Click(root, "room-bot-3");
                yield return Until(() => menu.Lobby.Countdown > 0 || started != null, 10, "the countdown");
                yield return Until(() => started != null, 15, "the match start");
                Assert.That(started.MatchId, Is.Not.Null); Assert.That(started.YourSeat, Is.EqualTo(0));
            }
            finally { realtime.Dispose(); }
        }
    }
}

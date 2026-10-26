using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Galaxy101.Online;
using Galaxy101.Table;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Okey101.Core.V4;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>Live UI and authored scene transitions. No injected backend, prepared hand or accelerated game clock.</summary>
    [Category("LiveServer")]
    public sealed class ClientLiveFlowTests
    {
        private readonly List<Action> offs = new List<Action>();
        private readonly List<JToken> events = new List<JToken>();
        private GalaxyOnline online;
        private V4TableSession session;
        private JToken tableState, matchResult;
        private bool leftTable, ownsEnvironment, hadSession, oldOpen, oldReturn, oldFromRanked, oldDesignDefaults, oldIgnoreLogs;
        private bool? oldAuth;
        private string savedSession, statePath, oldStatePath, oldToast, oldScenario, oldApiOverride;
        private Func<ILobbyBackend> oldLobby;
        private Func<IRankedBackend> oldRanked;
        private RankedStart oldPendingRanked;
        private TableLaunch oldLaunch;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Inconclusive("The authored URP table scene requires a graphics device; run this bounded live flow with -Graphics.");
            oldApiOverride = GalaxyApi.Override;
            string server = GalaxyApi.ResolveServer();
            if (!Uri.TryCreate(server, UriKind.Absolute, out var serverUri) || !serverUri.IsLoopback
                || (serverUri.Scheme != Uri.UriSchemeHttp && serverUri.Scheme != Uri.UriSchemeHttps))
                Assert.Inconclusive("Live UI tests require a loopback HTTP(S) game server; no request was sent.");
            using (var probe = UnityWebRequest.Get(server.TrimEnd('/') + "/health"))
            {
                probe.timeout = 3;
                yield return probe.SendWebRequest();
                if (probe.responseCode != 200) Assert.Inconclusive("No game server at " + server + " (scripts/Start-LocalServer.ps1).");
            }
            if (V4TableSession.Current is { Closed: false }) Assert.Inconclusive("An existing table session must finish before this scene-changing test.");
            events.Clear(); tableState = null; matchResult = null; leftTable = false;
            online = null; session = null; statePath = null;
            hadSession = PlayerPrefs.HasKey(GalaxyOnline.SessionKey);
            savedSession = PlayerPrefs.GetString(GalaxyOnline.SessionKey, "");
            oldAuth = MainMenuController.AuthOnStart; oldOpen = MainMenuController.OpenMatchTable;
            oldReturn = MainMenuController.ReturnToLobby; oldFromRanked = MainMenuController.FromRanked;
            oldToast = MainMenuController.PendingToast; oldPendingRanked = MainMenuController.PendingRanked;
            oldLobby = MainMenuController.LobbyBackend; oldRanked = MainMenuController.RankedBackend;
            oldStatePath = GalaxyPreferencesBridge.OverridePath; oldDesignDefaults = TableSettings.DesignDefaultsOnly;
            oldLaunch = TableGame.Pending; oldScenario = TableGame.PendingScenario;
            oldIgnoreLogs = LogAssert.ignoreFailingMessages;
            ownsEnvironment = true;
            GalaxyApi.Override = server;
            // Disconnect the old test instance without logging out or revoking the saved user's token.
            if (GalaxyOnline.Instance != null) UnityEngine.Object.Destroy(GalaxyOnline.Instance.gameObject);
            yield return null;
            GalaxyOnline.Forget();
            statePath = Path.Combine(Path.GetTempPath(), "g101-client-flow-" + Guid.NewGuid().ToString("N") + ".json");
            GalaxyPreferencesBridge.OverridePath = statePath;
            TableSettings.DesignDefaultsOnly = true;
            MainMenuController.AuthOnStart = true; MainMenuController.OpenMatchTable = true;
            MainMenuController.LobbyBackend = null; MainMenuController.RankedBackend = null;
            MainMenuController.ReturnToLobby = false; MainMenuController.FromRanked = false;
            MainMenuController.PendingToast = null; MainMenuController.PendingRanked = null;
            TableGame.Pending = null; TableGame.PendingScenario = null;
            LogAssert.ignoreFailingMessages = false;
            Assert.That(Application.CanStreamedLevelBeLoaded("Bootstrap"), Is.True);
            Assert.That(Application.CanStreamedLevelBeLoaded(MainMenuController.GalaxyTableScene), Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!ownsEnvironment) yield break;
            string cleanupError = null;
            try
            {
                foreach (var off in offs) off();
                offs.Clear();
                // Covers a failure in the waiting room as well as a failure during the match.
                if (online != null && online.SignedIn && online.Realtime.Connected)
                {
                    var leave = online.Realtime.RequestAsync(V4Messages.TableLeave, new object());
                    float end = Time.realtimeSinceStartup + 15;
                    while (!leave.IsCompleted && Time.realtimeSinceStartup < end) yield return null;
                    if (!leave.IsCompleted) cleanupError = "Cleanup tableLeave timed out.";
                    else if (leave.IsFaulted) cleanupError = leave.Exception.GetBaseException().Message;
                    else if (!leave.Result.Ok && leave.Result.Code != V4Errors.E_NOT_SEATED) cleanupError = "Cleanup tableLeave: " + leave.Result.Code;
                }
                // matchStart owns a session before the authored table has loaded and assigned our session field.
                // A failed transition or disconnected socket must not leave Current open for the next test.
                var currentSession = V4TableSession.Current;
                if (GalaxyOnline.Instance != null) GalaxyOnline.Instance.SuspendReconnect = true;
                session?.Dispose();
                if (currentSession != null && currentSession != session) currentSession.Dispose();
                if (GalaxyOnline.Instance != null) UnityEngine.Object.Destroy(GalaxyOnline.Instance.gameObject);
                yield return null;
                var empty = SceneManager.CreateScene("After client live flow " + Guid.NewGuid().ToString("N"));
                SceneManager.SetActiveScene(empty);
                foreach (var name in new[] { MainMenuController.GalaxyTableScene, "Bootstrap" })
                {
                    var scene = SceneManager.GetSceneByName(name);
                    if (!scene.IsValid() || !scene.isLoaded) continue;
                    var unload = SceneManager.UnloadSceneAsync(scene);
                    if (unload != null) yield return Until(() => unload.isDone, 30, "unloading " + name);
                }
            }
            finally
            {
                if (hadSession) PlayerPrefs.SetString(GalaxyOnline.SessionKey, savedSession);
                else PlayerPrefs.DeleteKey(GalaxyOnline.SessionKey);
                PlayerPrefs.Save();
                GalaxyApi.Override = oldApiOverride;
                GalaxyPreferencesBridge.OverridePath = oldStatePath; TableSettings.DesignDefaultsOnly = oldDesignDefaults;
                MainMenuController.AuthOnStart = oldAuth; MainMenuController.OpenMatchTable = oldOpen;
                MainMenuController.ReturnToLobby = oldReturn; MainMenuController.FromRanked = oldFromRanked;
                MainMenuController.PendingToast = oldToast; MainMenuController.PendingRanked = oldPendingRanked;
                MainMenuController.LobbyBackend = oldLobby; MainMenuController.RankedBackend = oldRanked;
                TableGame.Pending = oldLaunch; TableGame.PendingScenario = oldScenario;
                LogAssert.ignoreFailingMessages = oldIgnoreLogs;
                if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
                if (statePath != null && File.Exists(statePath + ".tmp")) File.Delete(statePath + ".tmp");
                ownsEnvironment = false;
            }
            Assert.That(cleanupError, Is.Null);
        }

        private static IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup >= end) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        private static MainMenuController Menu => UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
        private static TableGame Game => UnityEngine.Object.FindAnyObjectByType<TableGame>();
        private static VisualElement Hud => UnityEngine.Object.FindAnyObjectByType<TableHud>()?.GetComponent<UIDocument>().rootVisualElement;

        private static void Activate(VisualElement root, string name, string container = null)
        {
            var scope = container == null ? root : root.Q<VisualElement>(container);
            var element = scope?.Q<VisualElement>(name);
            Assert.That(element, Is.Not.Null, "control " + name);
            Assert.That(element.enabledInHierarchy, Is.True, "enabled control " + name);
            if (element is Button)
            {
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = element; element.SendEvent(submit); }
            }
            else using (var click = ClickEvent.GetPooled()) { click.target = element; element.SendEvent(click); }
        }

        private IEnumerator Request(string type, object data = null)
        {
            var task = online.Realtime.RequestAsync(type, data ?? new object());
            yield return Until(() => task.IsCompleted, 15, type);
            var ack = task.GetAwaiter().GetResult();
            Assert.That(ack.Ok, Is.True, type + ": " + ack.Code);
        }

        private IEnumerator SignInThroughTheAuthUi()
        {
            var load = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            yield return Until(() => load.isDone && Menu?.View != null, 45, "the authored Bootstrap menu");
            yield return Until(() => Menu.Auth?.Screen == "choice", 30, "the sign-in choice");
            Activate(Menu.View.Root, "auth-guest");
            yield return Until(() => Menu.Auth?.Screen == "guest", 5, "guest form");
            Menu.View.Root.Q<TextField>("auth-nick").value = "Flow" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Activate(Menu.View.Root, "auth-submit");
            yield return Until(() => Menu.Auth != null && !Menu.Auth.Visible && GalaxyOnline.Instance?.SignedIn == true, 20, "guest sign-in");
            online = GalaxyOnline.Instance;
            Assert.That(online.Guest, Is.True);
            Assert.That(Menu.Model.Chips, Is.EqualTo(10000));
            Assert.That((string)Menu.Model.Settings["lang"], Is.EqualTo("tr"));
            yield return Until(() => online.Realtime.Connected, 10, "the guest socket");
            offs.Add(online.Realtime.On(V4Messages.TableState, d => tableState = d["table"]?.DeepClone()));
            offs.Add(online.Realtime.On(V4Messages.Game, d => events.Add(d["ev"].DeepClone())));
            offs.Add(online.Realtime.On(V4Messages.MatchResult, d => matchResult = d.DeepClone()));
            offs.Add(online.Realtime.On(V4Messages.LeftTable, _ => leftTable = true));
            yield return Request(V4Messages.Hello, new V4HelloRequest { Token = "" });
        }

        private IEnumerator CreateWaitingRoom(bool pairs, bool doubling, bool partnerRaises)
        {
            if (Menu.Lobby?.Visible != true)
            {
                Activate(Menu.View.Root, "menu.m.pick[0]");
                yield return null;
                Activate(Menu.View.Root, "menu.play");
            }
            yield return Until(() => Menu.Lobby?.Visible == true && Menu.Lobby.Screen == "list" && Menu.Lobby.DialogKind == null, 15, "the live lobby list");
            Activate(Menu.View.Root, "lobby.openCreate");
            yield return Until(() => Menu.View.Root.Q<TextField>("lobby.cf.onName") != null, 5, "the create form");
            if (pairs) Activate(Menu.View.Root, "lobby.o.pick[1]", "lobby-create-type");
            Activate(Menu.View.Root, "lobby.b.pick[0]", "lobby-create-tiers");
            Activate(Menu.View.Root, "lobby.o.pick[0]", "lobby-create-hands");
            if (doubling) Activate(Menu.View.Root, "lobby.cf.katT.flip");
            if (pairs && doubling && !partnerRaises) Activate(Menu.View.Root, "lobby.cf.katPT.flip");
            Activate(Menu.View.Root, "lobby.o.pick[1]", "lobby-create-vis");
            string name = "Flow-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Menu.View.Root.Q<TextField>("lobby.cf.onName").value = name;
            tableState = null;
            Activate(Menu.View.Root, "lobby.cf.create");
            yield return Until(() => Menu.Lobby.Screen == "wait" && Menu.Lobby.DialogKind == "share" && (string)tableState?["name"] == name, 15, "the server's private waiting room");
            Assert.That((string)tableState["mode"], Is.EqualTo(pairs ? "pairs" : "solo"));
            Assert.That((long)tableState["bet"], Is.EqualTo(1000));
            Assert.That((int)tableState["hands"], Is.EqualTo(3));
            Assert.That((bool)tableState["katlama"], Is.EqualTo(doubling));
            if (pairs && doubling) Assert.That((bool)tableState["katlamaPartner"], Is.EqualTo(partnerRaises));
            Assert.That((string)tableState["owner"], Is.EqualTo(online.Profile.Id.ToString()));
            Assert.That(Menu.View.Root.Q<Label>("lobby.d.code")?.text, Does.Match("^GX-[A-Z0-9]{4}$"));
            Activate(Menu.View.Root, "lobby.d.toRoom");
            yield return Until(() => Menu.Lobby.DialogKind == null && Menu.View.Root.Q<VisualElement>("lobby.w.start") != null, 5, "the waiting-room controls");
        }

        private IEnumerator StartTheWaitingRoomAndEnterTheAuthoredTable()
        {
            for (int seat = 1; seat < 4; seat++)
            {
                int target = seat;
                Activate(Menu.View.Root, "lobby.z.addBot[" + seat + "]");
                yield return Until(() => IsBotSeat(tableState, target), 10, "bot at seat " + seat);
            }
            Activate(Menu.View.Root, "lobby.w.start");
            yield return AwaitAuthoredTable();
        }

        // Empty server seats are JSON null (JValue), rather than CLR null; ?. alone cannot index them safely.
        internal static bool IsBotSeat(JToken state, int seat) => state is JObject table && table["seats"] is JArray seats
            && seat >= 0 && seat < seats.Count && seats[seat] is JObject occupant
            && occupant["bot"]?.Type == JTokenType.Boolean && occupant.Value<bool>("bot");

        private IEnumerator AwaitAuthoredTable()
        {
            yield return Until(() => Game?.Session is V4TableSession && Game.Model?.Table != null && Hud != null, 45, "the online authored table scene");
            session = (V4TableSession)Game.Session;
            Assert.That(Game.gameObject.scene.name, Is.EqualTo(MainMenuController.GalaxyTableScene));
            Assert.That(session, Is.SameAs(V4TableSession.Current));
            Assert.That(Game.Session.Online, Is.True);
        }

        private IEnumerator ExitThroughTheHudAndReturnToLobby()
        {
            leftTable = false;
            Activate(Hud, "table.askExit");
            yield return Until(() => Game?.ExitOpen == true && Hud?.Q<VisualElement>("table.leave") != null, 5, "the leave confirmation");
            Activate(Hud, "table.leave");
            yield return Until(() => Hud?.Q<VisualElement>("table-message")?.Query<TextElement>().ToList().Any(t => t.text == "Masadan kalktın · lobiye dönülüyor") == true, 3, "the leave toast on the table");
            yield return Until(() => leftTable && Game == null && Menu?.Lobby?.Visible == true && Menu.Lobby.Screen == "list", 45, "the actual lobby after leaving");
            Assert.That(Menu.gameObject.scene.name, Is.EqualTo("Bootstrap"));
            Assert.That(session.Closed, Is.True);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator GuestCreatesAWaitingRoomStartsTheOnlineSceneAndLeavesThroughTheHud()
        {
            yield return SignInThroughTheAuthUi();
            yield return CreateWaitingRoom(false, false, true);
            string tableId = (string)tableState["id"];
            yield return StartTheWaitingRoomAndEnterTheAuthoredTable();
            Assert.That(Game.Model.Table.Id, Is.EqualTo(tableId));
            Assert.That(Game.Model.Table.Mode, Is.EqualTo("solo"));
            yield return ExitThroughTheHudAndReturnToLobby();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator PairsAndBothDoublingPartnerOptionsReachTheLiveTableScene()
        {
            yield return SignInThroughTheAuthUi();
            foreach (var option in new[] { (Doubling: false, Partner: true), (Doubling: true, Partner: true), (Doubling: true, Partner: false) })
            {
                yield return CreateWaitingRoom(true, option.Doubling, option.Partner);
                yield return StartTheWaitingRoomAndEnterTheAuthoredTable();
                Assert.That(Game.Model.Table.Mode, Is.EqualTo("pairs"));
                Assert.That(Game.Model.Table.Katlama, Is.EqualTo(option.Doubling));
                Assert.That(Game.Model.Options().Katlama, Is.EqualTo(option.Doubling));
                if (option.Doubling)
                {
                    Assert.That(Game.Model.Table.KatlamaPartner, Is.EqualTo(option.Partner));
                    Assert.That(Game.Model.Options().KatlamaPartner, Is.EqualTo(option.Partner));
                }
                yield return ExitThroughTheHudAndReturnToLobby();
            }
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator OneLiveBotHandPlaysToMatchEndAndReturnsToTheLobbyWithTheResultToast()
        {
            yield return SignInThroughTheAuthUi();
            yield return Request(V4Messages.VsBotsStart, new V4VsBotsStartRequest { Mode = "solo", Hands = 1 });
            yield return AwaitAuthoredTable();
            Assert.That(Game.Model.Table.VsBots, Is.True);
            Assert.That(Game.Model.Table.Hands, Is.EqualTo(1));
            int seat = Game.Model.Seat, actions = 0, draws = 0, discards = 0;
            float end = Time.realtimeSinceStartup + 540;
            while (matchResult == null)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(end), "a natural one-hand match must finish within the test boundary");
                Assert.That(Game, Is.Not.Null, "the live table stays open until matchResult");
                var game = Game;
                if (game.HudState().Deal is { } deal && deal.Roll >= 0)
                {
                    game.RollDice();
                    int dice = game.Model.Deal?.Dice.Count ?? 0;
                    yield return Until(() => game == null || game.Model.Deal == null || game.Model.Deal.Dice.Count > dice, 10, "the human dealer's die");
                }
                else if (game.HumanTurn && (game.CanDraw || game.CanDiscard))
                {
                    // Plan from the same public projection available to the human. Opponent racks and deck order are absent.
                    var action = game.CanDraw ? GameAction.Draw(seat, "deck") : G101Bots.NextAction(game.Model.Project(), seat);
                    int before = events.Count;
                    string expected = ExpectedEvent(action.T);
                    if (action.T == "draw")
                    {
                        var draw = game.Draw(false, null);
                        yield return Until(() => draw.IsCompleted, 15, "the human draw");
                        Assert.That(draw.GetAwaiter().GetResult(), Is.True);
                        draws++;
                    }
                    else ApplyHumanAction(game, action);
                    yield return Until(() => events.Skip(before).Any(e => (int?)e["seat"] == seat
                        && ((string)e["e"] == expected || (action.T == "discard" && (string)e["e"] == "finished"))), 15, "the accepted human " + action.T);
                    if (action.T == "discard") discards++;
                    actions++;
                }
                yield return null;
            }
            Assert.That(draws, Is.GreaterThan(0));
            Assert.That(discards, Is.GreaterThan(0));
            Assert.That(events.Count(e => (string)e["e"] == "handEnd"), Is.EqualTo(1));
            Assert.That(events.Count(e => (string)e["e"] == "matchEnd"), Is.EqualTo(1));
            long won = (long)matchResult["payouts"][seat];
            int place = (int)matchResult["ranking"].First(r => r["seats"].Values<int>().Contains(seat))["rank"];
            string toast = won > 0 ? "Maçı kazandın! +" + won.ToString("N0", new CultureInfo("tr-TR")) + " çip" : "Maç bitti · " + place + ". sıradasın";
            yield return Until(() => Game == null && Menu?.Lobby?.Visible == true && Menu.Lobby.Screen == "list" && Menu.View.Root.Query<Label>(className: "toast").ToList().Any(t => t.text == toast), 45, "the lobby and server-derived match result toast");
            Assert.That(Menu.gameObject.scene.name, Is.EqualTo("Bootstrap"));
            Assert.That(session.Closed, Is.True);
            TestContext.WriteLine("Live match finished: humanActions=" + actions + " draws=" + draws + " discards=" + discards);
        }

        private static string ExpectedEvent(string action)
        {
            switch (action)
            {
                case "draw": return "drew";
                case "discard": return "discarded";
                case "open": return "opened";
                case "lay": return "laid";
                case "add": return "added";
                case "swap": return "okeySwapped";
                case "returnSide": return "sideReturned";
                default: throw new InvalidOperationException("Unsupported human action " + action);
            }
        }

        private static void ApplyHumanAction(TableGame game, GameAction action)
        {
            switch (action.T)
            {
                case "discard": game.Discard(TileIds.ToInt(action.Tile)); break;
                case "open": case "lay": game.OpenWith(action.Melds.Select(m => (IList<string>)m.Tiles)); break;
                case "add": game.LayOff(TileIds.ToInt(action.Tile), TileIds.MeldInt(action.Meld), action.Side == null ? (bool?)null : action.Side == "pre"); break;
                case "swap": game.TakeOkey(TileIds.ToInt(action.Tile), TileIds.ToInt(game.Model.MeldById(action.Meld).Tiles[action.Index])); break;
                case "returnSide": game.PutBack(); break;
                default: throw new InvalidOperationException("Unsupported human action " + action.T);
            }
        }
    }
}

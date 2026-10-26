using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Galaxy101.Contracts;
using Galaxy101.Online;
using Galaxy101.Table;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Okey101.Core.V4;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>
    /// The Unity table against the real v4 server (GALAXY101_SERVER, default localhost:5173; Inconclusive without one, like
    /// LobbyTests.LiveServer): a guest starts a match against the server's bots (vsBotsStart), the table opens on the real
    /// <see cref="V4TableSession"/>, waits through the D-24 deal to my turn, plays a draw and a discard as step messages,
    /// and its model matches the server's view; a dropped socket shows the reconnect card and hello brings the table back.
    /// Replaces the earlier engine's OnlineTableTests.
    /// </summary>
    public sealed class TableLiveServerTests
    {
        private GameObject gameOwner, sceneOwner, hudOwner;
        private string statePath;
        private readonly List<Action> offs = new List<Action>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var off in offs) off(); offs.Clear();
            MainMenuController.OpenMatchTable = true;
            var session = V4TableSession.Current;
            if (session != null && !session.Closed) { var leave = session.Leave(); yield return Until(() => leave.IsCompleted, 10, "tableLeave"); }
            foreach (var go in new[] { hudOwner, gameOwner, sceneOwner }) if (go != null) UnityEngine.Object.Destroy(go);
            yield return null;
            session?.Dispose();
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        private static IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition()) { if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what); yield return null; }
        }

        private static IEnumerator Request(GalaxyOnline online, string type, object data, Action<AckMessage> done)
        {
            var task = online.Realtime.RequestAsync(type, data ?? new object());
            yield return Until(() => task.IsCompleted, 15, type);
            done(task.Result);
        }

        /// <summary>A guest with a v4 session (hello → session + snapshot) on the local server.</summary>
        private IEnumerator SignIn(Action<GalaxyOnline> ready)
        {
            using (var probe = UnityWebRequest.Get(GalaxyApi.ResolveServer() + "/health"))
            {
                probe.timeout = 3; yield return probe.SendWebRequest();
                if (probe.responseCode != 200) Assert.Inconclusive("No game server at " + GalaxyApi.ResolveServer() + " (scripts/Start-LocalServer.ps1).");
            }
            MainMenuController.OpenMatchTable = false;   // no menu here: the test opens the table itself
            V4TableSession.Current?.Dispose();             // a session an earlier test left open (lobby tests keep the lobby on screen)
            var online = GalaxyOnline.Ensure();
            // A fresh guest per test: an account left seated by an earlier live test (lobby, auth) answers E_ALREADY_SEATED.
            if (online.SignedIn) { var signOut = online.SignOutAsync(); yield return Until(() => signOut.IsCompleted, 10, "sign-out"); }
            {
                var signIn = online.GuestAsync("Masa" + UnityEngine.Random.Range(1000, 9999), "#3D7BFF");
                yield return Until(() => signIn.IsCompleted, 15, "guest sign-in");
                Assert.That(signIn.Exception, Is.Null);
            }
            yield return Until(() => online.Realtime.Connected, 10, "the socket");
            AckMessage hello = null;
            yield return Request(online, V4Messages.Hello, new V4HelloRequest { Token = "" }, a => hello = a);
            Assert.That(hello.Ok, Is.True, "hello: " + hello.Code);
            ready(online);
        }

        /// <summary>vsBotsStart: matchStart opens a <see cref="V4TableSession"/> (GalaxyOnline watches for it).</summary>
        private IEnumerator StartAgainstBots(GalaxyOnline online, int hands, Action<V4TableSession> started)
        {
            V4TableSession session = null;
            Action<V4TableSession> onStart = s => session = s;
            V4TableSession.Started += onStart; offs.Add(() => V4TableSession.Started -= onStart);
            AckMessage ack = null;
            yield return Request(online, V4Messages.VsBotsStart, new V4VsBotsStartRequest { Mode = "solo", Hands = hands }, a => ack = a);
            Assert.That(ack.Ok, Is.True, "vsBotsStart: " + ack.Code);
            yield return Until(() => session != null, 10, "matchStart");
            Assert.That(session.Client.Seat, Is.GreaterThanOrEqualTo(0), "my seat at the table");
            started(session);
        }

        private TableGame Build(ITableSession session)
        {
            TableGame.Pending = new TableLaunch { Session = session };
            gameOwner = new GameObject("TableGame"); var game = gameOwner.AddComponent<TableGame>(); gameOwner.AddComponent<RackInput>();
            sceneOwner = new GameObject("GalaxyTable"); sceneOwner.AddComponent<TableScene>();
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-table-live-" + Guid.NewGuid().ToString("N") + ".json");
            hudOwner = new GameObject("TableHud"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
            return game;
        }

        /// <summary>The parts of a server view a turn changes: they must be the model's (TableRuntime.GameView = snapshot.game = tableView.view).</summary>
        private static readonly string[] Compared = { "counts", "discards", "deck", "melds", "rack", "opened", "openMode", "pen", "totals", "handNo" };

        private static List<string> Differences(TableClient model, IDictionary<string, object> view)
        {
            var mine = model.View(0);
            var list = new List<string>();
            foreach (var key in Compared)
            {
                view.TryGetValue(key, out var a); mine.TryGetValue(key, out var b);
                string ca = V4Tree.Canonical(a), cb = V4Tree.Canonical(b);
                if (ca != cb) list.Add(key + ": server " + ca + " · client " + cb);
            }
            return list;
        }

        /// <summary>
        /// A second model fed only by the events (never by tableView or snapshot): before each tableView is applied it must
        /// already hold what the view says, so the events alone carry the game (counts, piles, deck, melds, my rack).
        /// </summary>
        private (TableClient Shadow, Func<int> Checks, List<string> Errors) Shadow(GalaxyOnline online, int seat)
        {
            var shadow = new TableClient(seat) { TrustViews = false };
            var errors = new List<string>(); int checks = 0;
            foreach (var type in V4Wire.TableMessages)
            {
                if (type == V4Messages.Snapshot || type == V4Messages.MatchStart) continue;
                var t = type;
                offs.Add(online.Realtime.On(t, data =>
                {
                    var payload = V4Wire.Payload(t, data);
                    if (payload is V4TableView v && shadow.HasHand)
                    {
                        checks++;
                        foreach (var d in Differences(shadow, (IDictionary<string, object>)v.View)) errors.Add("tableView " + checks + " " + d);
                    }
                    shadow.Apply(t, payload);
                }));
            }
            return (shadow, () => checks, errors);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator AMatchAgainstServerBotsPlaysOnTheTable()
        {
            GalaxyOnline online = null; yield return SignIn(o => online = o);
            // The shadow needs matchStart: it subscribes first, then joins the session's start below.
            TableClient shadow = null; Func<int> checks = null; List<string> errors = null;
            Action<V4TableSession> shadowStart = s => { var sh = Shadow(online, s.Client.Seat); (shadow, checks, errors) = sh; shadow.Apply(V4Messages.MatchStart, new V4MatchStart { Table = s.Client.Table, CountdownMs = s.Client.CountdownMs, At = s.Client.MatchStartAt }); };
            V4TableSession.Started += shadowStart; offs.Add(() => V4TableSession.Started -= shadowStart);
            V4TableSession session = null; yield return StartAgainstBots(online, 3, s => session = s);
            var game = Build(session);
            yield return Until(() => game.Board != null && game.Ceremony != null, 10, "the table built");
            var model = session.Client;

            // D-24: the deal ceremony (a bot dealer ~21.7 s; when I deal, "Zar at" rolls each die).
            yield return Until(() =>
            {
                if (game.HudState().Deal is { } d && d.Roll >= 0) game.RollDice();
                return game.HumanTurn;
            }, 120, "my first turn");
            Assert.That(model.Deck, Is.InRange(17, 20), "the deck after the deal, less the bots' draws before my turn");
            Assert.That(game.Rack.Count, Is.EqualTo(model.Rack.Count));

            // The starter discards without drawing (R-06); then one full turn: gDraw, gDiscard.
            bool drew = false;
            for (int turn = 0; turn < 3 && !drew; turn++)
            {
                yield return Until(() => game.HumanTurn, 60, "my turn");
                if (model.TurnPhase == G101Rules.PhaseDraw)
                {
                    int deck = model.Deck, rack = model.Rack.Count;
                    var draw = game.Draw(false, null);
                    yield return Until(() => draw.IsCompleted, 15, "gDraw");
                    Assert.That(draw.Result, Is.True, "gDraw accepted");
                    Assert.That(model.Deck, Is.EqualTo(deck - 1)); Assert.That(model.Rack.Count, Is.EqualTo(rack + 1));
                    drew = true;
                }
                string tile = model.AutoDiscardTile();
                int pile = model.Discards[model.Seat].Count;
                game.Discard(TileIds.ToInt(tile));
                yield return Until(() => model.Discards[model.Seat].Count == pile + 1 && model.TurnSeat != model.Seat || model.Status != G101Rules.StatusPlaying, 15, "gDiscard and the turn passed");
                if (model.Status == G101Rules.StatusPlaying) Assert.That(model.Discards[model.Seat].Last(), Is.EqualTo(tile));
                Assert.That(game.Board.ViewOf(TileIds.ToInt(tile)), Is.Not.Null, "the discard lies on the table");
            }
            Assert.That(drew, Is.True, "a draw within three turns");

            // The server's view now (resync → snapshot.game) equals the table's model and the events-only shadow.
            JObject snapshot = null;
            offs.Add(online.Realtime.On(V4Messages.Snapshot, data => snapshot = (JObject)data));
            AckMessage resync = null; yield return Request(online, V4Messages.Resync, null, a => resync = a);
            Assert.That(resync.Ok, Is.True);
            yield return Until(() => snapshot != null, 10, "snapshot");
            var game0 = (IDictionary<string, object>)V4Wire.ToTree(snapshot["game"]);
            Assert.That((string)snapshot["screen"], Is.EqualTo("table"));
            Assert.That(Differences(model, game0), Is.Empty, "the table's model is the server's view");
            Assert.That(Differences(shadow, game0), Is.Empty, "the events alone give the server's view");
            Assert.That(checks(), Is.GreaterThan(3), "tableViews checked");
            Assert.That(errors, Is.Empty, string.Join("\n", errors));

            // Masadan kalk: the server lets the seat go (R-60) and the session's screen is the menu again.
            bool left = false; offs.Add(online.Realtime.On(V4Messages.LeftTable, _ => left = true));
            var leave = session.Leave(); yield return Until(() => leave.IsCompleted, 10, "tableLeave");
            Assert.That(leave.Result, Is.Null, "tableLeave: " + leave.Result);
            yield return Until(() => left, 10, "leftTable");
            // TableGame owns the session: leave before destroying it, then remove the scene before the lobby timer.
            foreach (var go in new[] { hudOwner, gameOwner, sceneOwner }) if (go != null) UnityEngine.Object.Destroy(go);
            gameOwner = sceneOwner = hudOwner = null; yield return null;
            snapshot = null; yield return Request(online, V4Messages.Resync, null, a => resync = a);
            yield return Until(() => snapshot != null, 10, "snapshot after leaving");
            Assert.That((string)snapshot["screen"], Is.EqualTo("menu"), "the lobby is given back");
            session.Dispose();
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator ADroppedSocketShowsTheReconnectCardAndHelloReturnsToTheTable()
        {
            GalaxyOnline online = null; yield return SignIn(o => online = o);
            V4TableSession session = null; yield return StartAgainstBots(online, 1, s => session = s);
            var game = Build(session);
            yield return Until(() => game.Board != null && game.Ceremony != null, 10, "the table built");
            yield return Until(() => session.Client.HasHand, 30, "the first hand");

            bool back = false;
            offs.Add(online.Realtime.On(V4Messages.Snapshot, data => back |= (string)data?["screen"] == "table"));
            online.Realtime.SimulateDrop();
            yield return Until(() => game.HudState().NetAttempt >= 1, 10, "the reconnect card");
            yield return null; yield return null;
            var canvas = hudOwner.GetComponent<UIDocument>().rootVisualElement;
            string lang = hudOwner.GetComponent<TableHud>().L("tr", "en");
            Assert.That(canvas.Q<Label>("table.t.netTitle")?.text, Is.EqualTo(TableStrings.Get("netTitle", lang)), "Bağlantı koptu");
            Assert.That(canvas.Q<Label>("table.netAttempt")?.text, Is.EqualTo(TableStrings.Get("netAttempt", lang, game.HudState().NetAttempt)), "Deneme n/5");

            // 3 s later the first attempt: a new socket, hello → session + snapshot{screen: table}; the card goes.
            yield return Until(() => !session.Dropped && back, 30, "hello back at the table");
            yield return null;
            Assert.That(game.HudState().NetAttempt, Is.EqualTo(0));
            Assert.That(canvas.Q<Label>("table.t.netTitle"), Is.Null, "the card closed");
            Assert.That(online.Realtime.Connected, Is.True);
            Assert.That(session.Client.HasHand, Is.True, "the table is rebuilt from the snapshot");
            Assert.That(session.Closed, Is.False);
        }
    }
}

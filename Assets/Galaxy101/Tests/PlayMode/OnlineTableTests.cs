using System;
using System.Collections;
using System.IO;
using System.Linq;
using Galaxy101.Contracts;
using Galaxy101.Online;
using Galaxy101.Table;
using NUnit.Framework;
using Okey101.Core.Model;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>
    /// The 3D table playing a real server match (V7e): a guest opens a table with three server bots, starts it, and the
    /// table deals from the server's deal facts, shows the bots' moves as they arrive and sends my draws and discards.
    /// Needs a running game server (GALAXY101_SERVER, default localhost:5173); Inconclusive without one.
    /// </summary>
    public sealed class OnlineTableTests
    {
        private GameObject gameOwner, sceneOwner, hudOwner;
        private string statePath;
        private LobbyClient lobby;
        private NetworkTableSession match;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (match != null) { var leave = match.LeaveAsync(); float until = Time.realtimeSinceStartup + 3; while (!leave.IsCompleted && Time.realtimeSinceStartup < until) yield return null; }
            foreach (var go in new[] { hudOwner, gameOwner, sceneOwner }) if (go != null) UnityEngine.Object.Destroy(go);
            lobby?.Dispose();
            if (GalaxyOnline.Instance != null) UnityEngine.Object.Destroy(GalaxyOnline.Instance.gameObject);
            TableGame.PendingOnline = null;
            yield return null;
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        private static IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition()) { if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what); yield return null; }
        }

        private static string Dir => Environment.GetEnvironmentVariable("GALAXY_V3_CAPTURE_DIR");

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator ATableMatchAgainstServerBotsPlaysOnTheTable()
        {
            using (var probe = UnityWebRequest.Get(GalaxyApi.ResolveServer() + "/health"))
            {
                probe.timeout = 3; yield return probe.SendWebRequest();
                if (probe.responseCode != 200) Assert.Inconclusive("No game server at " + GalaxyApi.ResolveServer());
            }
            var online = GalaxyOnline.Ensure();
            var signIn = online.GuestAsync("Masa" + UnityEngine.Random.Range(1000, 9999), "#FF8A5B"); yield return Until(() => signIn.IsCompleted, 15, "guest sign-in");
            Assert.That(signIn.Exception, Is.Null);
            yield return Until(() => online.Realtime.Connected, 5, "the socket");

            // A table with three server bots, started at once.
            lobby = new LobbyClient(online);
            TableStateMessage started = null; lobby.RoomChanged += s => { if (s?.MatchId != null) started = s; };
            var sub = lobby.SubscribeAsync(); yield return Until(() => sub.IsCompleted, 10, "lobby");
            var create = lobby.CreateAsync(new TableOptions { Name = "Online masa testi", Hands = 3, TurnSeconds = 15, Stake = 1000 }); yield return Until(() => create.IsCompleted, 10, "create");
            Assert.That(create.Result, Is.Null, create.Result);
            yield return Until(() => lobby.Room != null, 5, "the room");
            for (int s = 1; s < 4; s++) { var bot = lobby.AddBotAsync(s); yield return Until(() => bot.IsCompleted, 5, "bot " + s); }
            var go = lobby.StartAsync(); yield return Until(() => go.IsCompleted, 5, "start");
            yield return Until(() => started != null, 15, "the match");

            // The table with the network session.
            match = new NetworkTableSession(online, started.MatchId.Value);
            TableGame.PendingOnline = match;
            gameOwner = new GameObject("TableGame"); var game = gameOwner.AddComponent<TableGame>();
            sceneOwner = new GameObject("GalaxyTable"); sceneOwner.AddComponent<TableScene>();
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-online-" + Guid.NewGuid().ToString("N") + ".json");
            hudOwner = new GameObject("TableHud"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);

            yield return Until(() => game.Online != null && match.HasView, 15, "the match snapshot");
            yield return Until(() => game.Current == TableGame.Phase.Deal, 15, "the deal");
            var right = match.Start.Seats.First(x => match.Wire.FromSeat(x.Seat) == 1).Player.Name;
            Assert.That(right, Does.StartWith("Bot · ")); Assert.That(hud.SeatName(1), Is.EqualTo(right.Substring(6)), "server seat names on the table, BOT as a badge");
            if (game.HudState().Deal != null && game.HudState().Deal.Dealer == TableGame.Me) { yield return Until(() => game.HudState().Deal?.Roll >= 0, 15, "my roll"); hud.Press("roll"); yield return Until(() => game.HudState().Deal?.Roll == 1, 10, "roll again"); hud.Press("roll"); }
            if (!string.IsNullOrEmpty(Dir)) { yield return new WaitForSeconds(2); yield return TableGameRuntimeTests.Capture(hudOwner.GetComponent<UIDocument>(), Path.Combine(Dir, "online_deal.png")); }
            yield return Until(() => game.Current != TableGame.Phase.Deal, 40, "the first turn");
            Assert.That(game.Rack.Count, Is.EqualTo(match.GetView(TableGame.Me).OwnHand.Count), "my rack is the server's hand");

            // Play three of my turns: draw from the deck, throw the drawn tile; the bots' moves arrive in between.
            int myTurns = 0; long lastVersion = match.GetView(TableGame.Me).Version; int opponentsSeen = 0;
            float until = Time.realtimeSinceStartup + 200;
            while (myTurns < 3 && Time.realtimeSinceStartup < until && match.GetView(TableGame.Me).Result == null)
            {
                var view = match.GetView(TableGame.Me);
                if (view.Version != lastVersion) { if (view.ActivePlayer != TableGame.Me) opponentsSeen++; lastVersion = view.Version; }
                if (game.CanDraw) { var draw = game.Draw(DrawSource.Stock, null); yield return Until(() => draw.IsCompleted, 10, "my draw"); continue; }
                if (game.CanDiscard)
                {
                    int tile = game.LastDrawn >= 0 && game.Rack.Tiles.Contains(game.LastDrawn) ? game.LastDrawn : game.Rack.Tiles.Last();
                    int discards = match.GetView(TableGame.Me).Discards[0].Count;
                    game.Discard(tile);
                    yield return Until(() => match.GetView(TableGame.Me).Discards[0].Count > discards || match.GetView(TableGame.Me).Result != null, 10, "my discard on the server");
                    myTurns++;
                    if (myTurns == 1 && !string.IsNullOrEmpty(Dir)) yield return TableGameRuntimeTests.Capture(hudOwner.GetComponent<UIDocument>(), Path.Combine(Dir, "online_play.png"));
                    continue;
                }
                yield return null;
            }
            Assert.That(myTurns, Is.EqualTo(3).Or.Matches<int>(_ => match.GetView(TableGame.Me).Result != null), "three of my turns played online");
            yield return Until(() => game.SyncedVersion == match.GetView(TableGame.Me).Version || game.CanDiscard || game.CanDraw, 15, "the board to catch up");
            var final = match.GetView(TableGame.Me);
            Assert.That(final.Discards.Skip(1).Sum(d => d.Count), Is.GreaterThan(0), "the bots' discards reached the table");
            Assert.That(game.Rack.Count, Is.EqualTo(final.OwnHand.Count));
            for (int k = 1; k < 4; k++) Assert.That(game.Board.HiddenCount(k), Is.EqualTo(final.Players[k].HandCount), "seat " + k);
        }
    }
}

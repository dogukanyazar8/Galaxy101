using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Galaxy101.Online;
using Galaxy101.Table;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Okey101.Core.V4;
using UnityEngine;
using UnityEngine.TestTools;

namespace Galaxy101.Tests
{
    /// <summary>Production client HTTP and socket lifecycle against a controlled loopback transport.</summary>
    public sealed class ClientLifecycleTests
    {
        private TcpListener listener;
        private readonly ConcurrentBag<TcpClient> clients = new ConcurrentBag<TcpClient>();
        private readonly ConcurrentQueue<TestRequest> requests = new ConcurrentQueue<TestRequest>();
        private readonly List<TestRequest> held = new List<TestRequest>();
        private GalaxyOnline online;
        private TcpClient peer;
        private GameObject gameOwner, tableOwner;
        private V4TableSession tableSession;
        private TableLaunch previousLaunch;
        private bool previousReturnToLobby;
        private bool hadSaved;
        private string saved, path;
        private static readonly Guid A = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid B = new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(GalaxyOnline.Instance, Is.Null, "A prior client must release its session.");
            hadSaved = PlayerPrefs.HasKey(GalaxyOnline.SessionKey);
            saved = PlayerPrefs.GetString(GalaxyOnline.SessionKey, "");
            previousLaunch = TableGame.Pending; previousReturnToLobby = MainMenuController.ReturnToLobby;
            listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            int number = ((IPEndPoint)listener.LocalEndpoint).Port;
            _ = Accept(listener);
            online = GalaxyOnline.Ensure("http://localhost:" + number);
            online.SuspendReconnect = true;
            online.Api.Use(Tokens(A));
            yield return null;
        }

        private sealed class TestRequest : IDisposable
        {
            public TcpClient Client;
            public string Path, Body;
            public Dictionary<string, string> Headers;
            public void Dispose() => Client.Dispose();
        }

        private async Task Accept(TcpListener server)
        {
            try
            {
                while (true)
                {
                    var client = await server.AcceptTcpClientAsync(); clients.Add(client);
                    _ = ReadRequest(client);
                }
            }
            catch (Exception e) when (e is SocketException || e is ObjectDisposedException) { }
        }

        private async Task ReadRequest(TcpClient client)
        {
            try
            {
                var stream = client.GetStream(); var header = new List<byte>(); var one = new byte[1];
                while (header.Count < 65536)
                {
                    if (await stream.ReadAsync(one, 0, 1) == 0) return;
                    header.Add(one[0]); int count = header.Count;
                    if (count >= 4 && header[count - 4] == 13 && header[count - 3] == 10 && header[count - 2] == 13 && header[count - 1] == 10) break;
                }
                string[] lines = Encoding.ASCII.GetString(header.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.None);
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int index = 1; index < lines.Length; index++)
                {
                    int colon = lines[index].IndexOf(':');
                    if (colon > 0) headers[lines[index].Substring(0, colon)] = lines[index].Substring(colon + 1).Trim();
                }
                if (headers.TryGetValue("Expect", out var expectation) && expectation == "100-continue")
                {
                    byte[] proceed = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
                    await stream.WriteAsync(proceed, 0, proceed.Length);
                }
                int length = headers.TryGetValue("Content-Length", out string contentLength) ? int.Parse(contentLength) : 0;
                var body = new byte[length]; int offset = 0;
                while (offset < length)
                {
                    int count = await stream.ReadAsync(body, offset, length - offset); if (count == 0) return; offset += count;
                }
                requests.Enqueue(new TestRequest { Client = client, Path = lines[0].Split(' ')[1].Split('?')[0], Headers = headers, Body = Encoding.UTF8.GetString(body) });
            }
            catch (Exception e) when (e is IOException || e is SocketException || e is ObjectDisposedException) { }
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            tableSession?.Dispose(); tableSession = null;
            if (gameOwner != null) UnityEngine.Object.Destroy(gameOwner);
            if (tableOwner != null) UnityEngine.Object.Destroy(tableOwner);
            if (online != null) { online.SuspendReconnect = true; UnityEngine.Object.Destroy(online.gameObject); }
            peer?.Dispose(); peer = null;
            while (clients.TryTake(out var client)) client.Dispose();
            foreach (var context in held) { try { context.Dispose(); } catch (ObjectDisposedException) { } }
            while (requests.TryDequeue(out var queued)) { try { queued.Dispose(); } catch (ObjectDisposedException) { } }
            held.Clear(); listener?.Stop();
            yield return null;
            if (hadSaved) PlayerPrefs.SetString(GalaxyOnline.SessionKey, saved); else PlayerPrefs.DeleteKey(GalaxyOnline.SessionKey);
            PlayerPrefs.Save();
            TableGame.Pending = previousLaunch; MainMenuController.ReturnToLobby = previousReturnToLobby;
            if (path != null && File.Exists(path)) File.Delete(path);
        }

        private static TokenResponse Tokens(Guid id) => new TokenResponse { AccountId = id, AccessToken = id.ToString("N"), RefreshToken = "refresh-" + id.ToString("N") };
        private static IEnumerator Until(Func<bool> condition, string step)
        {
            float deadline = Time.realtimeSinceStartup + 8;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Timed out: " + step);
        }
        private TestRequest Next(string route)
        {
            Assert.That(requests.TryDequeue(out var context), Is.True, route);
            held.Add(context); Assert.That(context.Path, Is.EqualTo(route)); return context;
        }
        private static void Reply(TestRequest context, object body, int status = 200)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(body, GalaxyApi.Json));
            byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " " + (status == 200 ? "OK" : "Error") + "\r\nContent-Type: application/json\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n");
            var stream = context.Client.GetStream(); stream.Write(header, 0, header.Length); stream.Write(bytes, 0, bytes.Length); context.Dispose();
        }

        private IEnumerator Refresh(Guid account, string name, long chips)
        {
            var refresh = online.RefreshAsync();
            yield return Until(() => !requests.IsEmpty, "profile request");
            Reply(Next("/v1/profile"), new ProfileResponse { Id = account, Name = name });
            yield return Until(() => !requests.IsEmpty, "wallet request");
            Reply(Next("/v1/wallet"), new WalletResponse { Chips = chips });
            yield return Until(() => refresh.IsCompleted, "refresh completion");
            refresh.GetAwaiter().GetResult();
        }

        private IEnumerator ConnectSocket()
        {
            var connect = online.Realtime.ConnectAsync();
            yield return Until(() => !requests.IsEmpty, "socket ticket"); Reply(Next("/v1/realtime/ticket"), new RealtimeTicketResponse { Ticket = "test-ticket" });
            yield return Until(() => !requests.IsEmpty, "WebSocket upgrade"); var context = Next("/v1/ws"); held.Remove(context);
            string accept;
            using (var sha = SHA1.Create()) accept = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(context.Headers["Sec-WebSocket-Key"] + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            byte[] upgrade = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n");
            peer = context.Client; peer.GetStream().Write(upgrade, 0, upgrade.Length);
            yield return Until(() => connect.IsCompleted, "socket connected"); connect.GetAwaiter().GetResult();
        }

        private IEnumerator Frame(string type, object data)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(new JObject { ["type"] = type, ["data"] = JObject.FromObject(data, JsonSerializer.Create(GalaxyApi.Json)) }.ToString(Formatting.None));
            // Controlled server frames are unmasked per RFC 6455; production ClientWebSocket reads them.
            byte[] header = bytes.Length < 126 ? new[] { (byte)0x81, (byte)bytes.Length } : new[] { (byte)0x81, (byte)126, (byte)(bytes.Length >> 8), (byte)bytes.Length };
            peer.GetStream().Write(header, 0, header.Length);
            var send = peer.GetStream().WriteAsync(bytes, 0, bytes.Length);
            yield return Until(() => send.IsCompleted, "server frame " + type); send.GetAwaiter().GetResult();
        }

        private static async Task ReadExact(NetworkStream stream, byte[] bytes)
        {
            int offset = 0;
            while (offset < bytes.Length)
            {
                int count = await stream.ReadAsync(bytes, offset, bytes.Length - offset);
                if (count == 0) throw new IOException("Client socket closed before its frame completed."); offset += count;
            }
        }

        private async Task<JObject> ClientFrame()
        {
            var stream = peer.GetStream(); var header = new byte[2]; await ReadExact(stream, header);
            Assert.That(header[0], Is.EqualTo(0x81), "A complete client text frame.");
            int length = header[1] & 127;
            if (length == 126) { var extended = new byte[2]; await ReadExact(stream, extended); length = (extended[0] << 8) | extended[1]; }
            Assert.That(length, Is.LessThan(65536)); Assert.That(header[1] & 128, Is.EqualTo(128), "Client frames are masked.");
            var mask = new byte[4]; await ReadExact(stream, mask); var bytes = new byte[length]; await ReadExact(stream, bytes);
            for (int index = 0; index < bytes.Length; index++) bytes[index] ^= mask[index % 4];
            return JObject.Parse(Encoding.UTF8.GetString(bytes));
        }

        [UnityTest]
        public IEnumerator SkipDealSendsOneIntentAndAnAcceptedVoteWaitsForTheAuthoritativeSkipEvent()
        {
            yield return Refresh(A, "Player A", 1000); yield return ConnectSocket();
            var table = TableGame.DefaultTable(); table.Id = "skip-lifecycle"; table.VsBots = false; table.Seats[0].User.Id = A.ToString();
            yield return Frame(V4Messages.MatchStart, new V4MatchStart { Table = table, CountdownMs = 0 });
            yield return Until(() => V4TableSession.Current?.Client.Seat == 0, "the skip session"); tableSession = V4TableSession.Current;
            TableGame.Pending = new TableLaunch { Session = tableSession };
            tableOwner = new GameObject("Skip lifecycle table"); tableOwner.AddComponent<TableScene>();
            gameOwner = new GameObject("Skip lifecycle game"); var game = gameOwner.AddComponent<TableGame>();
            yield return Until(() => game.Session == tableSession, "table initialized");
            yield return Frame(V4Messages.DealStart, new V4DealStart { HandNo = 1, Dealer = 3, BuildMs = TableTimings.DealBuild, At = tableSession.ServerNow });
            yield return Until(() => game.CanSkipDeal, "skip affordance");
            game.SkipDeal(); game.SkipDeal();
            var request = ClientFrame(); yield return Until(() => request.IsCompleted, "one skip intent");
            var sent = request.GetAwaiter().GetResult(); Assert.That((string)sent["type"], Is.EqualTo(V4Messages.GSkipDeal));
            Assert.That(game.CanSkipDeal, Is.False); Assert.That(game.HudState().Deal?.CanSkip, Is.True);
            Assert.That(game.HudState().Deal?.SkipWait, Is.True);
            yield return Frame(MessageTypes.Ack, new AckMessage { Id = (string)sent["id"], Ok = true });
            yield return Frame(V4Messages.DealSkipVote, new V4DealSkipVote { Seat = 0, At = tableSession.ServerNow });
            float deadline = Time.realtimeSinceStartup + .25f;
            while (Time.realtimeSinceStartup < deadline)
            {
                game.SkipDeal();
                Assert.That(peer.GetStream().DataAvailable, Is.False, "The same seat must not send another vote after its ACK.");
                Assert.That(tableSession.Client.Deal.Skipped, Is.False, "An accepted vote must not bypass server authority.");
                Assert.That(tableSession.Client.Rack, Is.Null, "A vote does not reveal any hand tiles.");
                Assert.That(game.HudState().Deal?.SkipWait, Is.True);
                yield return null;
            }
            yield return Frame(V4Messages.DealSkip, new V4DealSkip
            {
                Dice = new System.Collections.Generic.List<int> { 4, 3 }, Stack = 3, Level = 2, ExtraStack = 4,
                Indicator = "Y4a", Okey = new V4OkeyFace { C = "Y", N = 5 }, At = tableSession.ServerNow,
            });
            yield return Until(() => tableSession.Client.Deal?.Skipped == true, "authoritative skip");
            Assert.That(game.HudState().Deal?.CanSkip, Is.Not.True);
            Assert.That(game.HudState().Deal?.DieA, Is.EqualTo(4), "Public skip dice must update even when the private hand payload is delayed.");
            Assert.That(game.HudState().Deal?.DieB, Is.EqualTo(3));
            Assert.That(game.HudState().Deal?.LastDie, Is.EqualTo(1));
            Assert.That(tableSession.Client.Rack, Is.Null, "The authoritative skip still waits for the private handDealt payload.");
        }

        [UnityTest]
        public IEnumerator AnnouncedDieBlocksTheRollAffordanceAndCommandUntilItsAnimationCompletes()
        {
            yield return Refresh(A, "Player A", 1000); yield return ConnectSocket();
            var table = TableGame.DefaultTable(); table.Id = "roll-lifecycle"; table.VsBots = false; table.Seats[0].User.Id = A.ToString();
            yield return Frame(V4Messages.MatchStart, new V4MatchStart { Table = table, CountdownMs = 0 });
            yield return Until(() => V4TableSession.Current?.Client.Seat == 0, "the human dealer's session"); tableSession = V4TableSession.Current;
            TableGame.Pending = new TableLaunch { Session = tableSession };
            tableOwner = new GameObject("Roll lifecycle table"); tableOwner.AddComponent<TableScene>();
            gameOwner = new GameObject("Roll lifecycle game"); var game = gameOwner.AddComponent<TableGame>();
            yield return Until(() => game.Session == tableSession, "table initialized");
            yield return Frame(V4Messages.DealStart, new V4DealStart { HandNo = 1, Dealer = 0, HumanDealer = true, BuildMs = TableTimings.DealBuild, At = tableSession.ServerNow });
            yield return Frame(V4Messages.DealStep, new V4DealStep { Step = "dice", Dealer = 0, RollWaitMs = TableTimings.HumanRollWait, At = tableSession.ServerNow });
            yield return Until(() => game.HudState().Deal?.Roll == 0, "the first roll affordance");
            game.RollDice(); game.RollDice();
            var firstRequest = ClientFrame(); yield return Until(() => firstRequest.IsCompleted, "first roll intent");
            var first = firstRequest.GetAwaiter().GetResult(); Assert.That((string)first["type"], Is.EqualTo(V4Messages.GRollDice));
            float firstDeadline = Time.realtimeSinceStartup + .25f;
            while (Time.realtimeSinceStartup < firstDeadline) { Assert.That(peer.GetStream().DataAvailable, Is.False, "A same-frame second tap must not send another first-roll request while its ack is pending."); yield return null; }
            bool? offeredWhilePending = null;
            tableSession.Client.Applied += message =>
            {
                if (message.Type != V4Messages.DealDie) return;
                // Observe the exact same callback window as a very fast second tap, before the ceremony's next poll.
                offeredWhilePending = game.HudState().Deal?.Roll >= 0;
                game.RollDice();
            };
            yield return Frame(V4Messages.DealDie, new V4DealDie { Index = 0, Value = 3, RollMs = TableTimings.Roll1, At = tableSession.ServerNow });
            yield return Frame(MessageTypes.Ack, new AckMessage { Id = (string)first["id"], Ok = true });
            yield return Until(() => offeredWhilePending.HasValue, "announced first die");
            Assert.That(offeredWhilePending.Value, Is.False, "A pending announced die must close the next-roll affordance immediately.");
            float deadline = Time.realtimeSinceStartup + .25f;
            while (Time.realtimeSinceStartup < deadline)
            {
                Assert.That(game.HudState().Deal?.Roll, Is.EqualTo(-1), "Ack completion must not reopen the affordance before the announced die finishes.");
                Assert.That(peer.GetStream().DataAvailable, Is.False, "The command must also suppress a second roll while the announced die is pending.");
                yield return null;
            }
            yield return Until(() => game.HudState().Deal?.Roll == 1, "second roll becomes available after the first animation");
            game.RollDice(); var nextRequest = ClientFrame(); yield return Until(() => nextRequest.IsCompleted, "accepted next roll intent");
            var next = nextRequest.GetAwaiter().GetResult(); Assert.That((string)next["type"], Is.EqualTo(V4Messages.GRollDice));
            yield return Frame(MessageTypes.Ack, new AckMessage { Id = (string)next["id"], Ok = true });
        }

        [Test]
        public void WaitingForABotAcceptsJsonNullSeatsUntilTheServerFillsThem()
        {
            var table = JObject.Parse("{\"seats\":[{\"bot\":false},null,null,null]}");
            Assert.That(ClientLiveFlowTests.IsBotSeat(table, 1), Is.False);
            Assert.That(ClientLiveFlowTests.IsBotSeat(JValue.CreateNull(), 1), Is.False);
            Assert.That(ClientLiveFlowTests.IsBotSeat(table, 4), Is.False);
            table["seats"][1] = new JObject { ["bot"] = false };
            Assert.That(ClientLiveFlowTests.IsBotSeat(table, 1), Is.False);
            table["seats"][1]["bot"] = true;
            Assert.That(ClientLiveFlowTests.IsBotSeat(table, 1), Is.True);
        }

        [UnityTest]
        public IEnumerator RankedRecoveryUsesHelloToRestoreTheDisconnectedHumanSeat()
        {
            yield return ConnectSocket();
            var backend = RankedOnlineBackend.For(online);
            var request = ClientFrame(); yield return Until(() => request.IsCompleted, "ranked recovery intent");
            var frame = request.GetAwaiter().GetResult();
            Assert.That((string)frame["type"], Is.EqualTo(V4Messages.Hello), "Resync only sends a snapshot; hello restores connected human control on the server.");
            var table = TableGame.DefaultTable(); table.Id = "ranked-human-recovery"; table.Ranked = true; table.VsBots = false; table.Seats[0].User.Id = A.ToString();
            var host = new LocalTableHost(0, 1000000);
            var runtime = host.Start(new TableSetup { Options = new MatchOptions { Mode = "solo", Hands = 5 }, Seed = 42, TurnMs = 60000 }, table);
            try
            {
                runtime.Disconnect(0); Assert.That(runtime.Connected[0], Is.False);
                // The real Hub.HelloAsync restores the runtime before sending session/snapshot and the ack.
                runtime.Reconnect(0); Assert.That(runtime.Connected[0], Is.True); Assert.That(runtime.Autoplay[0], Is.False);
                yield return Frame(V4Messages.Session, new V4Session { Me = new V4Me { Id = A.ToString() }, At = host.Now, ServerTime = host.Now });
                yield return Frame(V4Messages.Snapshot, new V4Snapshot { Me = new V4Me { Id = A.ToString() }, Screen = "table", Table = table, Game = runtime.GameView(0), At = host.Now });
                yield return Frame(MessageTypes.Ack, new AckMessage { Id = (string)frame["id"], Ok = true });
                yield return Until(() => V4TableSession.Current?.Client.Table?.Id == table.Id, "restored ranked table after hello");
                tableSession = V4TableSession.Current;
                Assert.That(tableSession.Client.Seat, Is.Zero);
            }
            finally { host.Stop(); }
        }

        [UnityTest]
        public IEnumerator TableSnapshotRecoversAMissedMatchStartOnceAndPreservesSpectatorProjection()
        {
            yield return ConnectSocket();
            var table = TableGame.DefaultTable(); table.Id = "ranked-recovery"; table.Ranked = true; table.VsBots = false; table.Seats[0].User.Id = A.ToString();
            var host = new LocalTableHost(0, 1000000);
            var runtime = host.Start(new TableSetup { Options = new MatchOptions { Mode = "solo", Hands = 5 }, Seed = 42, TurnMs = 60000 }, table);
            // Prepare an actual dealt hand, so own-rack and spectator privacy assertions cannot pass on a lobby projection.
            for (int step = 0; step < 256 && (runtime.Match?.Hand == null || runtime.Dealing); step++)
            {
                host.RollDice();
                Assert.That(host.NextDue.HasValue, Is.True, "The deal must have a scheduled next step after a human roll.");
                host.Advance(host.NextDue.Value);
            }
            Assert.That(runtime.Dealing, Is.False); Assert.That(runtime.Match?.Hand, Is.Not.Null);
            Assert.That(runtime.GameView(0)["rack"], Is.Not.Null); Assert.That(runtime.GameView(-1)["rack"], Is.Null);
            int starts = 0;
            Action<V4TableSession> started = session => { starts++; tableSession = session; };
            V4TableSession.Started += started;
            try
            {
                yield return Frame(V4Messages.Snapshot, new V4Snapshot { Me = new V4Me { Id = A.ToString() }, Screen = "table", Table = table, Game = runtime.GameView(0), At = host.Now });
                yield return Until(() => V4TableSession.Current?.Client.Table?.Id == table.Id, "adopted table after missed matchStart");
                var recovered = V4TableSession.Current;
                Assert.That(starts, Is.EqualTo(1)); Assert.That(recovered.Client.Seat, Is.Zero, "Snapshot me identifies the seat even before an HTTP profile refresh.");
                Assert.That(V4Tree.Canonical(recovered.Client.View(host.Now)), Is.EqualTo(V4Tree.Canonical(runtime.GameView(0))));
                int snapshots = 0; recovered.Client.Applied += message => { if (message.Type == V4Messages.Snapshot) snapshots++; };
                yield return Frame(V4Messages.Snapshot, new V4Snapshot { Me = new V4Me { Id = A.ToString() }, Screen = "table", Table = table, Game = runtime.GameView(0), At = host.Now });
                yield return Until(() => snapshots == 1, "duplicate snapshot applied to the existing session");
                Assert.That(V4TableSession.Current, Is.SameAs(recovered)); Assert.That(starts, Is.EqualTo(1));
                yield return Frame(V4Messages.Snapshot, new V4Snapshot { Me = new V4Me { Id = A.ToString() }, Screen = "table", Table = table, Game = runtime.GameView(-1), Spectator = true, At = host.Now });
                yield return Until(() => snapshots == 2, "spectator snapshot applied");
                Assert.That(recovered.Client.Seat, Is.EqualTo(-1)); Assert.That(starts, Is.EqualTo(1));
                Assert.That(V4Tree.Canonical(recovered.Client.View(host.Now)), Is.EqualTo(V4Tree.Canonical(runtime.GameView(-1))));
            }
            finally { V4TableSession.Started -= started; host.Stop(); }
        }

        [UnityTest]
        public IEnumerator CancelledOnlineLaunchSendsTableLeaveAndKeepsTheTableWhenTheAckIsRefused()
        {
            yield return Refresh(A, "Player A", 1000);
            yield return ConnectSocket();
            var table = TableGame.DefaultTable(); table.Id = "cancelled-online"; table.VsBots = false; table.Seats[0].User.Id = A.ToString();
            yield return Frame(V4Messages.MatchStart, new V4MatchStart { Table = table, CountdownMs = 0 });
            yield return Until(() => V4TableSession.Current?.Client.Seat == 0, "the server's own seat before cancelled loading");
            tableSession = V4TableSession.Current;
            MainMenuController.ReturnToLobby = false;
            TableGame.Pending = new TableLaunch { Session = tableSession, Cancelled = true };
            tableOwner = new GameObject("Cancelled launch table"); tableOwner.AddComponent<TableScene>();
            gameOwner = new GameObject("Cancelled launch game"); var game = gameOwner.AddComponent<TableGame>();
            int changes = 0; game.Changed += () => changes++;
            var request = ClientFrame(); yield return Until(() => request.IsCompleted, "cancelled launch tableLeave");
            var first = request.GetAwaiter().GetResult(); Assert.That((string)first["type"], Is.EqualTo(V4Messages.TableLeave));
            int before = changes;
            yield return Frame(MessageTypes.Ack, new AckMessage { Id = (string)first["id"], Ok = false, Code = "offline" });
            yield return Until(() => changes > before, "refused leave shown");
            Assert.That(game, Is.Not.Null); Assert.That(tableSession.Closed, Is.False); Assert.That(MainMenuController.ReturnToLobby, Is.False);
            game.RequestExit(); Assert.That(game.ExitOpen, Is.True);
            game.ConfirmExit(); var retry = ClientFrame(); yield return Until(() => retry.IsCompleted, "retry tableLeave");
            var second = retry.GetAwaiter().GetResult(); Assert.That((string)second["type"], Is.EqualTo(V4Messages.TableLeave));
            before = changes;
            yield return Frame(MessageTypes.Ack, new AckMessage { Id = (string)second["id"], Ok = false, Code = "timeout" });
            yield return Until(() => changes > before, "retry refusal shown");
            Assert.That(MainMenuController.ReturnToLobby, Is.False); Assert.That(game, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator ProfilePutsAreSerializedAcrossMenuServiceInstances()
        {
            path = Path.Combine(Path.GetTempPath(), "client-profile-" + Guid.NewGuid().ToString("N") + ".json");
            var model = MenuModel.LoadResources(path);
            var firstService = new MockProfileService(model, model.Content); var secondService = new MockProfileService(model, model.Content);
            var first = firstService.SaveAsync("First Name", "#FF8A5B", "", 0);
            yield return Until(() => !requests.IsEmpty, "first profile PUT"); var put = Next("/v1/profile");
            var second = secondService.SaveAsync("Second Name", "#FF8A5B", "", 0);
            // Observe the whole overlap window: no later PUT may reach the server while the first is held.
            float deadline = Time.realtimeSinceStartup + .25f;
            while (Time.realtimeSinceStartup < deadline) { Assert.That(requests.IsEmpty, Is.True, "The next PUT must await the first response."); yield return null; }
            Reply(put, new { });
            yield return Until(() => !requests.IsEmpty, "first profile refresh"); Reply(Next("/v1/profile"), new ProfileResponse { Id = A, Name = "First Name" });
            yield return Until(() => !requests.IsEmpty, "first wallet refresh"); Reply(Next("/v1/wallet"), new WalletResponse { Chips = 1000 });
            yield return Until(() => !requests.IsEmpty, "second profile PUT"); Reply(Next("/v1/profile"), new { });
            yield return Until(() => !requests.IsEmpty, "second profile refresh"); Reply(Next("/v1/profile"), new ProfileResponse { Id = A, Name = "Second Name" });
            yield return Until(() => !requests.IsEmpty, "second wallet refresh"); Reply(Next("/v1/wallet"), new WalletResponse { Chips = 1000 });
            yield return Until(() => first.IsCompleted && second.IsCompleted, "serialized saves complete");
            Assert.That(first.GetAwaiter().GetResult(), Is.Null); Assert.That(second.GetAwaiter().GetResult(), Is.Null);
            Assert.That((string)model.Profile["name"], Is.EqualTo("Second Name")); Assert.That(online.Profile.Name, Is.EqualTo("Second Name"));
        }

        [UnityTest]
        public IEnumerator RankedSnapshotsRestoreTheExistingSearchAndAcceptWindow()
        {
            var backend = RankedOnlineBackend.For(online);
            yield return ConnectSocket();
            yield return Frame(V4Messages.Snapshot, new V4Snapshot { Screen = "ranked.search", At = 5000, Ranked = new V4Queue { Mode = "duo", Range = 2, Since = 1000 } });
            yield return Until(() => backend.Queue?.Range == 2, "restored ranked search");
            Assert.That(backend.Queue.Mode, Is.EqualTo("duo"));
            yield return Frame(V4Messages.Snapshot, new V4Snapshot
            {
                Screen = "ranked.found", At = 5000,
                Ranked = new V4Queue { Mode = "duo", Found = new V4RankedFound { MatchId = "recovered-match", ExpiresAt = 12000, Players = new List<V4RankedPlayer> { new V4RankedPlayer { User = new V4User { Id = A.ToString(), Name = "Player A" } } }, Accepted = new List<bool> { true } } }
            });
            yield return Until(() => backend.Pending?.MatchId == "recovered-match", "restored ranked accept window");
            Assert.That(backend.Queue, Is.Null); Assert.That(backend.Pending.Seconds, Is.EqualTo(7)); Assert.That(backend.Pending.Accepted[0], Is.True);
        }

        [UnityTest]
        public IEnumerator ReplacedSocketRemainsTerminalForLobbyAndDirectConnect()
        {
            int changed = 0; online.Changed += () => changed++;
            yield return ConnectSocket();
            yield return Frame(MessageTypes.Replaced, new { });
            yield return Until(() => online.Replaced, "replaced session");
            Assert.That(changed, Is.EqualTo(1), "Menu observers must see replacement immediately, before another scene or HTTP response.");
            Assert.That(PlayerPrefs.HasKey(GalaxyOnline.SessionKey), Is.False);
            using (var lobby = new LobbyClient(online))
            {
                var subscribe = lobby.SubscribeAsync(); yield return Until(() => subscribe.IsCompleted, "terminal lobby subscribe");
                Assert.That(subscribe.GetAwaiter().GetResult(), Is.Not.Null);
            }
            var connect = online.Realtime.ConnectAsync(); yield return Until(() => connect.IsCompleted, "terminal direct connect");
            Assert.That(connect.IsFaulted, Is.True); _ = connect.Exception;
            Assert.That(requests.IsEmpty, Is.True, "Returning to the lobby must not obtain a new ticket or evict the other device.");
        }

        [UnityTest]
        public IEnumerator DestroyedOnlineSessionCannotReconnectThroughACapturedRealtimeReference()
        {
            var realtime = online.Realtime; var api = online.Api;
            UnityEngine.Object.Destroy(online.gameObject); yield return null;
            Assert.That(GalaxyOnline.Instance, Is.Null); Assert.That(api.SignedIn, Is.True, "Disposal must protect retained callers even while their API still has tokens.");
            var late = realtime.ConnectAsync();
            _ = late.ContinueWith(task => { if (task.IsFaulted) _ = task.Exception; });
            Assert.That(late.IsFaulted, Is.True, "A stale continuation must be rejected before requesting another ticket.");
            Assert.That(late.Exception.GetBaseException(), Is.TypeOf<ObjectDisposedException>());
            Assert.That(requests.IsEmpty, Is.True);
        }

        [UnityTest]
        public IEnumerator DelayedRefreshCannotReplaceTheNextAccountsProfileOrWallet()
        {
            var stale = online.RefreshAsync();
            yield return Until(() => !requests.IsEmpty, "A profile"); var oldProfile = Next("/v1/profile");
            online.Api.Use(Tokens(B));
            yield return Refresh(B, "Account B", 2000);
            Reply(oldProfile, new ProfileResponse { Id = A, Name = "Account A" });
            yield return Until(() => stale.IsCompleted || !requests.IsEmpty, "old refresh termination");
            if (!requests.IsEmpty) Reply(Next("/v1/wallet"), new WalletResponse { Chips = 1000 });
            yield return Until(() => stale.IsCompleted, "old refresh complete"); stale.GetAwaiter().GetResult();
            Assert.That(online.Profile.Id, Is.EqualTo(B)); Assert.That(online.Wallet.Chips, Is.EqualTo(2000));
        }

        [UnityTest]
        public IEnumerator DelayedLogoutCannotClearTheNextAccountsTokensOrProfile()
        {
            var logout = online.SignOutAsync();
            yield return Until(() => !requests.IsEmpty, "A logout"); var oldLogout = Next("/v1/auth/logout");
            online.Api.Use(Tokens(B)); yield return Refresh(B, "Account B", 2000);
            Reply(oldLogout, new { }); yield return Until(() => logout.IsCompleted, "logout complete"); logout.GetAwaiter().GetResult();
            Assert.That(online.Api.Tokens?.AccountId, Is.EqualTo(B));
            Assert.That(online.Profile?.Id, Is.EqualTo(B)); Assert.That(online.Wallet?.Chips, Is.EqualTo(2000));
        }

        [UnityTest]
        public IEnumerator DelayedTokenRefreshCannotOverwriteANewLogin()
        {
            var old = online.Api.ProfileAsync();
            yield return Until(() => !requests.IsEmpty, "expired profile"); Reply(Next("/v1/profile"), new { code = "unauthorized" }, 401);
            yield return Until(() => !requests.IsEmpty, "token refresh"); var refresh = Next("/v1/auth/refresh");
            online.Api.Use(Tokens(B)); Reply(refresh, Tokens(A));
            yield return Until(() => old.IsCompleted || !requests.IsEmpty, "old request termination");
            if (!requests.IsEmpty) Reply(Next("/v1/profile"), new ProfileResponse { Id = A });
            yield return Until(() => old.IsCompleted, "old request completed");
            if (old.IsFaulted) _ = old.Exception;
            Assert.That(online.Api.Tokens?.AccountId, Is.EqualTo(B));
        }

        [UnityTest]
        public IEnumerator DelayedPurchaseCannotApplyThePreviousAccountsWallet()
        {
            path = Path.Combine(Path.GetTempPath(), "client-lifecycle-" + Guid.NewGuid().ToString("N") + ".json");
            var content = Resources.Load<TextAsset>("Galaxy101/content"); var themes = Resources.Load<TextAsset>("Galaxy101/themes");
            var model = new MenuModel(content.text, themes.text, path); var menu = new OnlineMenu(model, JObject.Parse(content.text));
            var purchase = menu.PurchaseAsync("skin-mars");
            yield return Until(() => !requests.IsEmpty, "A purchase"); var oldPurchase = Next("/v1/shop/purchase");
            online.Api.Use(Tokens(B)); yield return Refresh(B, "Account B", 2000);
            Reply(oldPurchase, new PurchaseResponse { Wallet = new WalletResponse { Chips = 1000 } });
            yield return Until(() => purchase.IsCompleted || !requests.IsEmpty, "purchase termination");
            if (!requests.IsEmpty)
            {
                Reply(Next("/v1/shop/catalog"), new CatalogResponse());
                yield return Until(() => purchase.IsCompleted || !requests.IsEmpty, "shop refresh");
                if (!requests.IsEmpty)
                {
                    Reply(Next("/v1/profile"), new ProfileResponse { Id = B });
                    yield return Until(() => !requests.IsEmpty, "shop wallet"); Reply(Next("/v1/wallet"), new WalletResponse { Chips = 1000 });
                }
            }
            yield return Until(() => purchase.IsCompleted, "purchase complete"); purchase.GetAwaiter().GetResult();
            Assert.That(online.Wallet.Chips, Is.EqualTo(2000));
        }

        [Test]
        public void RankedBackendIsScopedToTheSignInRatherThanThePersistentOnlineObject()
        {
            var first = RankedOnlineBackend.For(online);
            online.Api.Use(Tokens(B));
            var second = RankedOnlineBackend.For(online);
            Assert.That(second, Is.Not.SameAs(first), "A new account cannot inherit the old ranked queue, pending match or ready table.");
            Assert.That(RankedOnlineBackend.For(online), Is.SameAs(second), "Scene changes within one sign-in keep the ranked backend.");
        }
        [UnityTest]
        public IEnumerator ANewerLoginOwnsTokensWhenAnOlderLoginAnswersLast()
        {
            var first = online.Api.LoginAsync("first@example.test", "unused");
            yield return Until(() => !requests.IsEmpty, "first login"); var oldLogin = Next("/v1/auth/login");
            var second = online.Api.LoginAsync("second@example.test", "unused");
            yield return Until(() => !requests.IsEmpty, "second login"); Reply(Next("/v1/auth/login"), Tokens(B));
            yield return Until(() => second.IsCompleted, "second login complete"); second.GetAwaiter().GetResult();
            Reply(oldLogin, Tokens(A)); yield return Until(() => first.IsCompleted, "first login complete");
            if (first.IsFaulted) _ = first.Exception;
            Assert.That(online.Api.Tokens?.AccountId, Is.EqualTo(B));
        }

        [UnityTest]
        public IEnumerator LogoutIsAcceptedOncePerSignInIncludingWhileTheRequestIsPending()
        {
            path = Path.Combine(Path.GetTempPath(), "client-logout-" + Guid.NewGuid().ToString("N") + ".json");
            var model = MenuModel.LoadResources(path); var service = new MockProfileService(model, model.Content);
            Assert.That(service.Logout(), Is.True);
            Assert.That(service.Logout(), Is.False, "A second tap cannot create a second logout while the first is pending.");
            yield return Until(() => !requests.IsEmpty, "logout request"); Reply(Next("/v1/auth/logout"), new { });
            yield return Until(() => !online.SignedIn, "signed out");
            online.Api.Use(Tokens(B));
            Assert.That(service.Logout(), Is.True, "The same menu service must permit logout after a new sign-in.");
            Assert.That(service.Logout(), Is.False);
            yield return Until(() => !requests.IsEmpty, "next logout request"); Reply(Next("/v1/auth/logout"), new { });
        }
        [UnityTest]
        public IEnumerator ConcurrentConnectsShareOneTicketAndDisposeCancelsThePendingConnection()
        {
            var first = online.Realtime.ConnectAsync(); var second = online.Realtime.ConnectAsync();
            yield return Until(() => !requests.IsEmpty, "realtime ticket"); var ticket = Next("/v1/realtime/ticket");
            online.Realtime.Dispose(); Reply(ticket, new RealtimeTicketResponse { Ticket = "cancelled" });
            // Respond to any incorrectly duplicated ticket so both production operations terminate.
            float deadline = Time.realtimeSinceStartup + 8; int count = 1, sockets = 0;
            while ((!first.IsCompleted || !second.IsCompleted) && Time.realtimeSinceStartup < deadline)
            {
                while (requests.TryDequeue(out var extra))
                {
                    held.Add(extra);
                    if (extra.Path == "/v1/realtime/ticket") { count++; Reply(extra, new RealtimeTicketResponse { Ticket = "cancelled" }); }
                    else { sockets++; Reply(extra, new { code = "cancelled" }, 409); }
                }
                yield return null;
            }
            if (first.IsFaulted) _ = first.Exception; if (second.IsFaulted) _ = second.Exception;
            Assert.That(first.IsCompleted && second.IsCompleted, Is.True);
            Assert.That(count, Is.EqualTo(1), "One connection flight obtains one ticket.");
            Assert.That(online.Realtime.Connected, Is.False); Assert.That(sockets, Is.Zero, "Disposed ticket must not open a socket.");
            Assert.That(requests.IsEmpty, Is.True, "Disposed ticket must never open a WebSocket.");
        }
    }
}

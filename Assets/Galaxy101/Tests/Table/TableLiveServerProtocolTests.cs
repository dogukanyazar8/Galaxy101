// Outside Unity only (tools/v4/nunit-table): it blocks on async socket work, which the editor's main-thread
// synchronization context would deadlock; Unity runs the same path as Tests/PlayMode/TableLiveServerTests.
#if !UNITY_5_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Okey101.Core.V4;

namespace Galaxy101.Table.Tests
{
    /// <summary>
    /// The client model and wire against the real v4 server, without Unity (GALAXY101_SERVER, default
    /// http://localhost:5173; Inconclusive when none answers): a guest's socket, hello, vsBotsStart, the D-24 deal to my
    /// turn, gDraw + gDiscard, and the model built from the frames (and an events-only shadow) equal to the server's
    /// snapshot.game; then tableLeave gives the lobby back; a dropped socket comes back with hello → snapshot{table}.
    /// The Unity side of the same path is Tests/PlayMode/TableLiveServerTests.
    /// </summary>
    [TestFixture, Category("LiveServer")]
    public sealed class TableLiveServerProtocolTests
    {
        private static string Server => Environment.GetEnvironmentVariable("GALAXY101_SERVER") ?? "http://localhost:5173";

        private sealed class Wire : IDisposable
        {
            private readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            private ClientWebSocket socket;
            private CancellationTokenSource life;
            private int nextId;
            private readonly ConcurrentDictionary<string, TaskCompletionSource<JObject>> acks = new ConcurrentDictionary<string, TaskCompletionSource<JObject>>();
            public readonly BlockingCollection<(string Type, JToken Data)> Inbox = new BlockingCollection<(string, JToken)>();
            public string Access;
            public Guid Account;

            public async Task GuestAsync()
            {
                var body = JsonConvert.SerializeObject(new GuestLoginRequest { Name = "Masa" + new Random().Next(1000, 9999), Color = "#3D7BFF", Platform = "Tests" }, V4Wire.Json);
                var r = await http.PostAsync(Server + "/v1/auth/guest", new StringContent(body, Encoding.UTF8, "application/json"));
                Assert.That((int)r.StatusCode, Is.EqualTo(200), await r.Content.ReadAsStringAsync());
                var t = JsonConvert.DeserializeObject<TokenResponse>(await r.Content.ReadAsStringAsync(), V4Wire.Json);
                Access = t.AccessToken; Account = t.AccountId;
            }

            public async Task ConnectAsync()
            {
                var req = new HttpRequestMessage(HttpMethod.Post, Server + "/v1/realtime/ticket");
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Access);
                var r = await http.SendAsync(req);
                Assert.That((int)r.StatusCode, Is.EqualTo(200), await r.Content.ReadAsStringAsync());
                var ticket = JsonConvert.DeserializeObject<RealtimeTicketResponse>(await r.Content.ReadAsStringAsync(), V4Wire.Json).Ticket;
                socket = new ClientWebSocket(); life = new CancellationTokenSource();
                await socket.ConnectAsync(new Uri(Server.Replace("http", "ws") + "/v1/ws?ticket=" + Uri.EscapeDataString(ticket)), life.Token);
                _ = Task.Run(() => Receive(socket, life.Token));
            }

            private async Task Receive(ClientWebSocket ws, CancellationToken ct)
            {
                var buffer = new byte[1 << 16]; var frame = new System.IO.MemoryStream();
                try
                {
                    while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                    {
                        var r = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (r.MessageType == WebSocketMessageType.Close) break;
                        frame.Write(buffer, 0, r.Count);
                        if (!r.EndOfMessage) continue;
                        var o = JObject.Parse(Encoding.UTF8.GetString(frame.GetBuffer(), 0, (int)frame.Length)); frame.SetLength(0);
                        var type = (string)o["type"]; var data = o["data"];
                        if (type == MessageTypes.Ack) { if (acks.TryRemove((string)data["id"], out var w)) w.TrySetResult((JObject)data); continue; }
                        Inbox.Add((type, data));
                    }
                }
                catch (Exception) { }
            }

            /// <summary>A request and its ack (<c>{id, ok, code, detail, at, …}</c>).</summary>
            public async Task<JObject> Request(string type, object data = null)
            {
                var id = "t" + Interlocked.Increment(ref nextId);
                var done = new TaskCompletionSource<JObject>(TaskCreationOptions.RunContinuationsAsynchronously);
                acks[id] = done;
                var frame = new JObject { ["type"] = type, ["id"] = id, ["data"] = data == null ? new JObject() : JToken.FromObject(data, JsonSerializer.Create(V4Wire.Json)) };
                await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(frame.ToString(Formatting.None))), WebSocketMessageType.Text, true, CancellationToken.None);
                var winner = await Task.WhenAny(done.Task, Task.Delay(10000));
                Assert.That(winner, Is.SameAs(done.Task), "ack of " + type);
                return done.Task.Result;
            }

            public void Drop() { try { socket.Abort(); } catch (Exception) { } }

            public void Dispose() { life?.Cancel(); try { socket?.Abort(); } catch (Exception) { } http.Dispose(); }
        }

        private static bool Up()
        {
            try { using (var h = new HttpClient { Timeout = TimeSpan.FromSeconds(3) }) return h.GetAsync(Server + "/health").Result.IsSuccessStatusCode; }
            catch (Exception) { return false; }
        }

        /// <summary>The table's model (views trusted) and an events-only shadow, fed from the frames; tableView checks the shadow first.</summary>
        private sealed class Models
        {
            public TableClient Table, Shadow;
            public int Checks;
            public readonly List<string> Errors = new List<string>();
            public JObject LastSnapshot;
            public bool LeftTable;

            public void Take(string type, JToken data)
            {
                if (type == V4Messages.Snapshot) LastSnapshot = (JObject)data;
                if (type == V4Messages.LeftTable) LeftTable = true;
                if (!V4Wire.Knows(type)) return;
                var payload = V4Wire.Payload(type, data);
                if (Table == null && type == V4Messages.MatchStart) return;   // seats are set by the caller
                Table?.Apply(type, payload);
                if (Shadow == null || type == V4Messages.Snapshot) return;
                if (payload is V4TableView v && Shadow.HasHand)
                {
                    Checks++;
                    foreach (var d in Differences(Shadow, (IDictionary<string, object>)v.View)) Errors.Add("tableView " + Checks + " " + d);
                }
                Shadow.Apply(type, V4Wire.Payload(type, data));
            }
        }

        private static readonly string[] Compared = { "counts", "discards", "deck", "melds", "rack", "opened", "openMode", "pen", "totals", "handNo", "status" };

        private static List<string> Differences(TableClient model, IDictionary<string, object> view)
        {
            var mine = model.View(0); var list = new List<string>();
            foreach (var key in Compared)
            {
                view.TryGetValue(key, out var a); mine.TryGetValue(key, out var b);
                string ca = V4Tree.Canonical(a), cb = V4Tree.Canonical(b);
                if (ca != cb) list.Add(key + ": server " + ca + " · client " + cb);
            }
            return list;
        }

        /// <summary>Pumps frames into the models until <paramref name="done"/> or the time is up.</summary>
        private static void Pump(Wire w, Models m, Func<bool> done, int ms, string what)
        {
            var end = DateTime.UtcNow.AddMilliseconds(ms);
            while (!done())
            {
                var left = end - DateTime.UtcNow;
                if (left <= TimeSpan.Zero) Assert.Fail("Timed out waiting for " + what);
                if (w.Inbox.TryTake(out var f, left < TimeSpan.FromMilliseconds(200) ? left : TimeSpan.FromMilliseconds(200))) m.Take(f.Type, f.Data);
            }
        }

        private static (Wire, Models) Start()
        {
            if (!Up()) Assert.Inconclusive("No game server at " + Server + " (scripts/Start-LocalServer.ps1).");
            var w = new Wire(); w.GuestAsync().Wait(); w.ConnectAsync().Wait();
            var m = new Models();
            var hello = w.Request(V4Messages.Hello, new V4HelloRequest { Token = "" }).Result;
            Assert.That((bool)hello["ok"], Is.True, hello.ToString());
            // vsBotsStart: matchStart (seat 0 is mine), the table's tableState first.
            var start = w.Request(V4Messages.VsBotsStart, new V4VsBotsStartRequest { Mode = "solo", Hands = 3 }).Result;
            Assert.That((bool)start["ok"], Is.True, start.ToString());
            var end = DateTime.UtcNow.AddSeconds(10);
            while (m.Table == null)
            {
                Assert.That(DateTime.UtcNow, Is.LessThan(end), "matchStart");
                if (!w.Inbox.TryTake(out var f, 200) || f.Type != V4Messages.MatchStart) continue;
                var ms = (V4MatchStart)V4Wire.Payload(f.Type, f.Data);
                int seat = ms.Table.Seats.FindIndex(s => s?.User?.Id == w.Account.ToString());
                Assert.That(seat, Is.GreaterThanOrEqualTo(0), "my seat in matchStart");
                m.Table = new TableClient(seat); m.Shadow = new TableClient(seat) { TrustViews = false };
                m.Table.Apply(f.Type, ms); m.Shadow.Apply(f.Type, V4Wire.Payload(f.Type, f.Data));
            }
            return (w, m);
        }

        private static void MyTurn(Wire w, Models m, int ms = 90000) =>
            Pump(w, m, () => m.Table.MyTurn && m.Table.Deal == null && m.Table.Legal().ToTree().Values.Any(v => v is bool b && b), ms, "my turn");

        [Test, Timeout(240000)]
        public void ADrawAndADiscardAgainstServerBotsKeepTheModelTheServers()
        {
            var (w, m) = Start();
            using (w)
            {
                var t = m.Table;
                // D-24: the ceremony; when I deal, gRollDice rolls each die (else the server does after 15 s).
                Pump(w, m, () =>
                {
                    if (t.Deal != null && t.Deal.Step == "dice" && t.Deal.Dealer == t.Seat && t.Deal.Dice.Count < 2) w.Request(V4Messages.GRollDice).Wait();
                    return t.MyTurn && t.Deal == null;
                }, 120000, "the deal and my first turn");
                Assert.That(t.Deck, Is.InRange(17, 20), "the deck after the deal, less the bots' draws before my turn");
                bool drew = false;
                for (int turn = 0; turn < 3 && !drew; turn++)
                {
                    MyTurn(w, m);
                    if (t.TurnPhase == G101Rules.PhaseDraw)
                    {
                        int deck = t.Deck, rack = t.Rack.Count;
                        var ack = w.Request(V4Messages.GDraw, new V4DrawRequest { Src = "deck" }).Result;
                        Assert.That((bool)ack["ok"], Is.True, ack.ToString());
                        Pump(w, m, () => t.Rack.Count == rack + 1, 5000, "drew");
                        Assert.That(t.Deck, Is.EqualTo(deck - 1));
                        drew = true;
                    }
                    var tile = t.AutoDiscardTile(); int pile = t.Discards[t.Seat].Count;
                    var ack2 = w.Request(V4Messages.GDiscard, new V4DiscardRequest { Tile = tile }).Result;
                    Assert.That((bool)ack2["ok"], Is.True, ack2.ToString());
                    Pump(w, m, () => t.Discards[t.Seat].Count == pile + 1 && t.TurnSeat != t.Seat || t.Status != G101Rules.StatusPlaying, 5000, "discarded and the turn passed");
                }
                Assert.That(drew, Is.True);
                // A wrong move: refused with the engine's code, nothing changes.
                var wrong = w.Request(V4Messages.GDiscard, new V4DiscardRequest { Tile = "Ja" }).Result;
                Assert.That((bool)wrong["ok"], Is.False);
                Assert.That(new[] { V4Errors.E_NOT_YOUR_TURN, V4Errors.E_TILE_NOT_IN_RACK, V4Errors.E_PHASE }, Does.Contain((string)wrong["code"]));

                m.LastSnapshot = null;
                Assert.That((bool)w.Request(V4Messages.Resync).Result["ok"], Is.True);
                Pump(w, m, () => m.LastSnapshot != null, 5000, "snapshot");
                var game = (IDictionary<string, object>)V4Wire.ToTree(m.LastSnapshot["game"]);
                Assert.That((string)m.LastSnapshot["screen"], Is.EqualTo("table"));
                Assert.That(Differences(m.Shadow, game), Is.Empty, "the events alone give the server's view");
                Assert.That(Differences(m.Table, game), Is.Empty);
                Assert.That(m.Checks, Is.GreaterThan(3));
                Assert.That(m.Errors, Is.Empty, string.Join("\n", m.Errors));
                TestContext.WriteLine("tableViews checked: " + m.Checks);

                // tableLeave (R-60): leftTable, and the session's screen is the menu again.
                Assert.That((bool)w.Request(V4Messages.TableLeave).Result["ok"], Is.True);
                Pump(w, m, () => m.LeftTable, 5000, "leftTable");
                m.LastSnapshot = null; w.Request(V4Messages.Resync).Wait();
                Pump(w, m, () => m.LastSnapshot != null, 5000, "snapshot after leaving");
                Assert.That((string)m.LastSnapshot["screen"], Is.EqualTo("menu"));
            }
        }

        [Test, Timeout(240000)]
        public void ADroppedSocketReturnsToTheTableWithHello()
        {
            var (w, m) = Start();
            using (w)
            {
                Pump(w, m, () => m.Table.HasHand, 20000, "the first hand");
                w.Drop();
                Thread.Sleep(TableTimings.ClientRetryMs);   // the client's first attempt, 3 s later
                w.ConnectAsync().Wait();
                m.LastSnapshot = null;
                var hello = w.Request(V4Messages.Hello, new V4HelloRequest { Token = "" }).Result;
                Assert.That((bool)hello["ok"], Is.True);
                Pump(w, m, () => m.LastSnapshot != null, 5000, "snapshot");
                Assert.That((string)m.LastSnapshot["screen"], Is.EqualTo("table"));
                Assert.That(m.Table.HasHand, Is.True);
                var game = (IDictionary<string, object>)V4Wire.ToTree(m.LastSnapshot["game"]);
                Assert.That(Differences(m.Table, game), Is.Empty);
                Assert.That((bool)w.Request(V4Messages.TableLeave).Result["ok"], Is.True);
            }
        }
    }
}
#endif

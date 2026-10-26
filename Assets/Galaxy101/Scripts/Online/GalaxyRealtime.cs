using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Galaxy101.Online
{
    /// <summary>
    /// The game server's WebSocket (<c>/v1/ws?ticket=…</c>): frames <c>{type, id, data}</c>. Requests carry an id and
    /// complete on the matching <c>ack</c>; everything else is delivered to <see cref="On"/> handlers on the Unity main
    /// thread (<see cref="Pump"/>, called by <see cref="GalaxyOnline"/> every frame).
    /// </summary>
    public sealed class GalaxyRealtime : IDisposable
    {
        private readonly GalaxyApi api;
        private ClientWebSocket socket;
        private CancellationTokenSource life;
        private readonly ConcurrentQueue<(string Type, JToken Data)> inbox = new ConcurrentQueue<(string, JToken)>();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<AckMessage>> pending = new ConcurrentDictionary<string, TaskCompletionSource<AckMessage>>();
        private readonly Dictionary<string, List<Action<JToken>>> handlers = new Dictionary<string, List<Action<JToken>>>();
        private readonly SemaphoreSlim sending = new SemaphoreSlim(1, 1);
        private int nextId;

        public bool Connected => socket != null && socket.State == WebSocketState.Open;
        /// <summary>The socket closed by itself (server gone, network lost); the reason is "replaced" when the same
        /// account connected from another device — then nothing should reconnect.</summary>
        public event Action<string> Closed;
        public const string ReplacedReason = "replaced";

        public GalaxyRealtime(GalaxyApi api) { this.api = api; }

        public async Task ConnectAsync()
        {
            if (Connected) return;
            var ticket = await api.TicketAsync();
            var url = api.BaseUrl.Replace("https://", "wss://").Replace("http://", "ws://") + "/v1/ws?ticket=" + Uri.EscapeDataString(ticket.Ticket);
            socket?.Dispose(); life?.Cancel();
            socket = new ClientWebSocket(); life = new CancellationTokenSource();
            await socket.ConnectAsync(new Uri(url), life.Token);
            _ = Task.Run(() => ReceiveAsync(socket, life.Token));
        }

        /// <summary>
        /// Tests and diagnostics: the connection drops as if the network went away (the socket is aborted, <see cref="Closed"/>
        /// fires), so the reconnect paths can be exercised against a real server.
        /// </summary>
        public void SimulateDrop() { try { socket?.Abort(); } catch (Exception e) { Debug.LogWarning("SimulateDrop: " + e.Message); } }

        /// <summary>Subscribe to a server message type; returns an unsubscribe action.</summary>
        public Action On(string type, Action<JToken> handler)
        {
            if (!handlers.TryGetValue(type, out var list)) handlers[type] = list = new List<Action<JToken>>();
            list.Add(handler);
            return () => list.Remove(handler);
        }

        public Action On<T>(string type, Action<T> handler) => On(type, data => handler(data == null ? default : data.ToObject<T>(JsonSerializer.Create(GalaxyApi.Json))));

        /// <summary>Send and wait for the server's ack (Ok, or the error Code).</summary>
        public async Task<AckMessage> RequestAsync(string type, object data, int seconds = 10)
        {
            var id = "c" + Interlocked.Increment(ref nextId);
            var done = new TaskCompletionSource<AckMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending[id] = done;
            try
            {
                try { await SendAsync(type, data, id); }
                catch (Exception e) when (e is ApiException || e is WebSocketException || e is ObjectDisposedException || e is OperationCanceledException)
                { return new AckMessage { Id = id, Ok = false, Code = "offline" }; }
                var winner = await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(seconds)));
                return winner == done.Task ? done.Task.Result : new AckMessage { Id = id, Ok = false, Code = "timeout" };
            }
            finally { pending.TryRemove(id, out _); }
        }

        public async Task SendAsync(string type, object data = null, string id = null)
        {
            if (!Connected) throw new ApiException("offline", 0);
            var frame = new JObject { ["type"] = type, ["data"] = data == null ? new JObject() : JToken.FromObject(data, JsonSerializer.Create(GalaxyApi.Json)) };
            if (id != null) frame["id"] = id;
            var bytes = Encoding.UTF8.GetBytes(frame.ToString(Formatting.None));
            await sending.WaitAsync();
            try { await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, life.Token); }
            finally { sending.Release(); }
        }

        /// <summary>Deliver queued messages on the main thread.</summary>
        public void Pump()
        {
            while (inbox.TryDequeue(out var message))
            {
                if (message.Type == "__closed") { Closed?.Invoke((string)message.Data); continue; }
                if (!handlers.TryGetValue(message.Type, out var list)) continue;
                foreach (var handler in list.ToArray())
                    try { handler(message.Data); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        private async Task ReceiveAsync(ClientWebSocket ws, CancellationToken ct)
        {
            // A frame arrives in parts that may split a multi-byte character (ş, ğ): decode the whole frame's bytes.
            var buffer = new byte[64 * 1024]; var frameBytes = new MemoryStream();
            string reason = "closed";
            try
            {
                while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close) { if (reason != ReplacedReason) reason = ws.CloseStatusDescription ?? "closed"; break; }
                    frameBytes.Write(buffer, 0, result.Count);
                    if (!result.EndOfMessage) continue;
                    var frame = JObject.Parse(Encoding.UTF8.GetString(frameBytes.GetBuffer(), 0, (int)frameBytes.Length)); frameBytes.SetLength(0);
                    var type = (string)frame["type"]; var data = frame["data"];
                    if (type == MessageTypes.Replaced) reason = ReplacedReason;
                    if (type == MessageTypes.Ack)
                    {
                        var ack = data.ToObject<AckMessage>(JsonSerializer.Create(GalaxyApi.Json));
                        if (ack?.Id != null && pending.TryRemove(ack.Id, out var waiter)) waiter.TrySetResult(ack);
                        continue;
                    }
                    inbox.Enqueue((type, data));
                }
            }
            catch (OperationCanceledException) { return; } // replaced by a new socket or disposed: its requests are not ours to fail
            catch (Exception e) { if (reason != ReplacedReason) reason = e.Message; }
            FailPending();
            if (!ct.IsCancellationRequested) inbox.Enqueue(("__closed", reason));
        }

        /// <summary>Requests waiting on a socket that is gone answer "offline" now instead of after their timeout.</summary>
        private void FailPending()
        {
            foreach (var id in pending.Keys)
                if (pending.TryRemove(id, out var waiter)) waiter.TrySetResult(new AckMessage { Id = id, Ok = false, Code = "offline" });
        }

        public void Dispose()
        {
            life?.Cancel();
            try { if (socket != null && socket.State == WebSocketState.Open) socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch (Exception) { }
            socket?.Dispose(); socket = null;
        }
    }
}

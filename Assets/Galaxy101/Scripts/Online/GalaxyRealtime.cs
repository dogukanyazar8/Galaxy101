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
    /// <summary>The authenticated socket. One connection flight owns its socket, cancellation and queued frames.</summary>
    public sealed class GalaxyRealtime : IDisposable
    {
        private readonly GalaxyApi api;
        private readonly object gate = new object();
        private ClientWebSocket socket;
        private CancellationTokenSource life;
        private TaskCompletionSource<bool> connecting;
        private int epoch, nextId;
        private bool replaced, disposed;
        private readonly ConcurrentQueue<(int Epoch, string Type, JToken Data)> inbox = new ConcurrentQueue<(int, string, JToken)>();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<AckMessage>> pending = new ConcurrentDictionary<string, TaskCompletionSource<AckMessage>>();
        private readonly Dictionary<string, List<Action<JToken>>> handlers = new Dictionary<string, List<Action<JToken>>>();
        private readonly SemaphoreSlim sending = new SemaphoreSlim(1, 1);

        public bool Connected { get { lock (gate) return !replaced && socket != null && socket.State == WebSocketState.Open; } }
        public event Action<string> Closed;
        public const string ReplacedReason = "replaced";

        public GalaxyRealtime(GalaxyApi api) { this.api = api; }

        public Task ConnectAsync()
        {
            lock (gate)
            {
                if (disposed) return Task.FromException(new ObjectDisposedException(nameof(GalaxyRealtime)));
                if (replaced) return Task.FromException(new ApiException("session_replaced", 0));
                if (!api.SignedIn) return Task.FromException(new ApiException("offline", 0));
                if (Connected) return Task.CompletedTask;
                if (connecting != null) return connecting.Task;
                Disconnect();
                var owner = new CancellationTokenSource(); life = owner;
                var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                connecting = completion;
                _ = ConnectOwnedAsync(completion, owner, epoch, api.Generation);
                return completion.Task;
            }
        }

        private bool Owns(int mine, CancellationTokenSource owner, int generation) =>
            epoch == mine && life == owner && !owner.IsCancellationRequested && !disposed && !replaced && api.SignedIn && api.Generation == generation;

        private async Task ConnectOwnedAsync(TaskCompletionSource<bool> completion, CancellationTokenSource owner, int mine, int generation)
        {
            ClientWebSocket ws = null;
            var token = owner.Token;
            try
            {
                var ticket = await api.TicketAsync();
                lock (gate)
                {
                    if (!Owns(mine, owner, generation)) throw new OperationCanceledException(token);
                    ws = new ClientWebSocket(); socket = ws;
                }
                var url = api.BaseUrl.Replace("https://", "wss://").Replace("http://", "ws://") + "/v1/ws?ticket=" + Uri.EscapeDataString(ticket.Ticket);
                await ws.ConnectAsync(new Uri(url), token);
                lock (gate)
                {
                    if (!Owns(mine, owner, generation) || socket != ws) throw new OperationCanceledException(token);
                    // Capture this exact socket and token; a later connect cannot redirect this receive loop.
                    _ = ReceiveAsync(ws, token, mine);
                    completion.TrySetResult(true);
                }
            }
            catch (OperationCanceledException) { completion.TrySetCanceled(); }
            catch (Exception e) { completion.TrySetException(e); }
            finally
            {
                lock (gate)
                {
                    if (connecting == completion) connecting = null;
                    if (!completion.Task.IsCompletedSuccessfully)
                    {
                        ws?.Dispose();
                        if (socket == ws) socket = null;
                    }
                }
            }
        }

        public void SimulateDrop() { lock (gate) { try { socket?.Abort(); } catch (Exception e) { Debug.LogWarning("SimulateDrop: " + e.Message); } } }

        public Action On(string type, Action<JToken> handler)
        {
            if (!handlers.TryGetValue(type, out var list)) handlers[type] = list = new List<Action<JToken>>();
            list.Add(handler);
            return () => list.Remove(handler);
        }
        public Action On<T>(string type, Action<T> handler) => On(type, data => handler(data == null ? default : data.ToObject<T>(JsonSerializer.Create(GalaxyApi.Json))));

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
                return winner == done.Task ? await done.Task : new AckMessage { Id = id, Ok = false, Code = "timeout" };
            }
            finally { pending.TryRemove(id, out _); }
        }

        public async Task SendAsync(string type, object data = null, string id = null)
        {
            ClientWebSocket ws; CancellationToken token; int mine;
            lock (gate)
            {
                if (!Connected || life == null) throw new ApiException("offline", 0);
                ws = socket; token = life.Token; mine = epoch;
            }
            var frame = new JObject { ["type"] = type, ["data"] = data == null ? new JObject() : JToken.FromObject(data, JsonSerializer.Create(GalaxyApi.Json)) };
            if (id != null) frame["id"] = id;
            var bytes = Encoding.UTF8.GetBytes(frame.ToString(Formatting.None));
            await sending.WaitAsync(token);
            try
            {
                lock (gate) if (epoch != mine || socket != ws || replaced) throw new ApiException("offline", 0);
                await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token);
            }
            finally { sending.Release(); }
        }

        public void Pump()
        {
            while (inbox.TryDequeue(out var message))
            {
                lock (gate) if (message.Epoch != epoch) continue;
                if (message.Type == "__closed") { Closed?.Invoke((string)message.Data); continue; }
                if (!handlers.TryGetValue(message.Type, out var list)) continue;
                foreach (var handler in list.ToArray())
                    try { handler(message.Data); } catch (Exception e) { Debug.LogException(e); }
            }
        }

        private async Task ReceiveAsync(ClientWebSocket ws, CancellationToken ct, int mine)
        {
            var buffer = new byte[64 * 1024];
            using var frameBytes = new MemoryStream();
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
                    lock (gate)
                    {
                        if (epoch != mine || socket != ws || ct.IsCancellationRequested) return;
                        if (type == MessageTypes.Replaced) { reason = ReplacedReason; replaced = true; }
                        if (type == MessageTypes.Ack)
                        {
                            var ack = data.ToObject<AckMessage>(JsonSerializer.Create(GalaxyApi.Json));
                            if (ack?.Id != null && pending.TryRemove(ack.Id, out var waiter)) waiter.TrySetResult(ack);
                        }
                        else inbox.Enqueue((mine, type, data));
                    }
                    if (reason == ReplacedReason) break;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception e) { if (reason != ReplacedReason) reason = e.Message; }
            lock (gate)
            {
                if (epoch != mine || socket != ws || ct.IsCancellationRequested) return;
                if (reason == ReplacedReason) replaced = true;
                FailPending(); socket = null; ws.Dispose();
                inbox.Enqueue((mine, "__closed", reason));
            }
        }

        private void FailPending()
        {
            foreach (var id in pending.Keys)
                if (pending.TryRemove(id, out var waiter)) waiter.TrySetResult(new AckMessage { Id = id, Ok = false, Code = "offline" });
        }

        /// <summary>A new sign-in resets the connection and replaced state while the owner is alive.</summary>
        internal void ResetSession()
        {
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(GalaxyRealtime));
                Disconnect(); replaced = false;
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true; Disconnect();
            }
        }

        // All callers hold gate. Soft resets release the connection without reviving a disposed object.
        private void Disconnect()
        {
            epoch++;
            life?.Cancel(); life?.Dispose(); life = null;
            connecting?.TrySetCanceled(); connecting = null;
            socket?.Dispose(); socket = null;
            FailPending();
            while (inbox.TryDequeue(out _)) { }
        }
    }
}

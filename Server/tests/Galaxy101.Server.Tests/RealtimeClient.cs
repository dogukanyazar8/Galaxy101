using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Galaxy101.Contracts;

namespace Galaxy101.Server.Tests;

/// <summary>Test WebSocket client: records every server frame and lets tests wait for a specific one.</summary>
public sealed class RealtimeClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly WebSocket socket;
    private readonly Channel<(string Type, JsonElement Data)> inbox = Channel.CreateUnbounded<(string, JsonElement)>();
    private readonly List<(string Type, JsonElement Data)> seen = [];
    private readonly Task reader;

    private RealtimeClient(WebSocket socket) { this.socket = socket; reader = ReadAsync(); }

    public static async Task<RealtimeClient> ConnectAsync(ServerFixture server, HttpClient authorized)
    {
        var ticket = (await (await authorized.PostAsync("/v1/realtime/ticket", null)).Content.ReadFromJsonAsync<RealtimeTicketResponse>())!;
        var ws = server.Server.CreateWebSocketClient();
        var socket = await ws.ConnectAsync(new Uri(server.Server.BaseAddress, "/v1/ws?ticket=" + ticket.Ticket), CancellationToken.None);
        return new RealtimeClient(socket);
    }

    public Task SendAsync(string type, object? data = null, string? id = null) =>
        socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(new { type, id, data = data ?? new { } }, Json), WebSocketMessageType.Text, true, CancellationToken.None);

    /// <summary>Waits for the next frame of <paramref name="type"/> matching <paramref name="match"/>; earlier unmatched frames stay available.</summary>
    public async Task<T> WaitAsync<T>(string type, Func<T, bool>? match = null, int seconds = 10)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        lock (seen)
            for (var i = 0; i < seen.Count; i++)
                if (seen[i].Type == type && seen[i].Data.Deserialize<T>(Json) is { } early && (match is null || match(early))) { seen.RemoveAt(i); return early; }
        while (true)
        {
            var frame = await inbox.Reader.ReadAsync(timeout.Token);
            if (frame.Type == type && frame.Data.Deserialize<T>(Json) is { } value && (match is null || match(value))) return value;
            lock (seen) seen.Add(frame);
        }
    }

    public async Task<AckMessage> RequestAsync(string type, object data)
    {
        var id = Guid.NewGuid().ToString("N");
        await SendAsync(type, data, id);
        return await WaitAsync<AckMessage>(MessageTypes.Ack, a => a.Id == id);
    }

    public void Forget<T>(string type) { lock (seen) seen.RemoveAll(x => x.Type == type); }

    private async Task ReadAsync()
    {
        var buffer = new byte[256 * 1024];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var length = 0;
                WebSocketReceiveResult result;
                do { result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer, length, buffer.Length - length), CancellationToken.None); length += result.Count; }
                while (!result.EndOfMessage);
                if (result.MessageType == WebSocketMessageType.Close) break;
                var root = JsonDocument.Parse(Encoding.UTF8.GetString(buffer, 0, length)).RootElement.Clone();
                await inbox.Writer.WriteAsync((root.GetProperty("type").GetString()!, root.GetProperty("data")));
            }
        }
        catch (Exception e) when (e is WebSocketException or ObjectDisposedException or IOException) { }
        finally { inbox.Writer.TryComplete(); }
    }

    public bool Closed => reader.IsCompleted;

    public async ValueTask DisposeAsync()
    {
        if (socket.State == WebSocketState.Open)
            try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); }
            catch (Exception e) when (e is WebSocketException or ObjectDisposedException or IOException) { }
        await reader;
        socket.Dispose();
    }
}

using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;

namespace Galaxy101.Server.Realtime;

/// <summary>An inbound frame: <c>{ "type": "...", "id": "...", "data": { ... } }</c>.</summary>
public readonly record struct Inbound(string Type, string? Id, JsonElement Data);

/// <summary>
/// One player's socket. Writes go through a bounded queue drained by a single loop (WebSocket allows one writer);
/// a client too slow to drain it is disconnected rather than buffered without limit.
/// </summary>
public sealed class Connection(Guid account, WebSocket socket, ILogger log, long sessionVersion = 0)
{
    public const int MaxMessageBytes = 16 * 1024;
    private const int QueueLimit = 512;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Wait mode: TryWrite answers false on a full queue (DropWrite answers true and silently loses the frame).
    private readonly Channel<byte[]> outbound = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(QueueLimit) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly CancellationTokenSource closing = new();
    // Inbound flood guard: 40-message burst, refilled at 20 per second.
    private double allowance = 40;
    private long lastRefill = Environment.TickCount64;

    public Guid Account { get; } = account;
    public long SessionVersion { get; } = sessionVersion;
    public CancellationToken Closing => closing.Token;

    public bool Send<T>(string type, T data) => SendFrame(Frame(type, data));

    /// <summary>A serialized frame, to send one message to many sockets without serializing it for each.</summary>
    public static byte[] Frame<T>(string type, T data) => JsonSerializer.SerializeToUtf8Bytes(new { type, data }, Json);

    private long queued;
    /// <summary>Frames queued for this socket so far (tests wait until the client has them all).</summary>
    internal long Queued => Interlocked.Read(ref queued);

    public bool SendFrame(byte[] bytes)
    {
        if (outbound.Writer.TryWrite(bytes)) { Interlocked.Increment(ref queued); return true; }
        log.LogWarning("Closing slow realtime client {Account}", Account);
        Close();
        return false;
    }

    public void Close() { outbound.Writer.TryComplete(); closing.Cancel(); }

    private volatile bool replaced;

    /// <summary>
    /// The same account connected again: the queued frames and the <paramref name="notice"/> still go out, then the socket
    /// closes with the reason "replaced" — the old device learns not to reconnect even if the notice did not fit a full
    /// queue. A client that does not drain is cut after five seconds, with the same reason.
    /// </summary>
    public void Replace(byte[] notice)
    {
        replaced = true;
        outbound.Writer.TryWrite(notice); // best effort: the close carries the reason anyway
        outbound.Writer.TryComplete();
        closing.CancelAfter(TimeSpan.FromSeconds(5));
    }

    /// <summary>Runs until the socket closes; calls <paramref name="handle"/> for each valid frame, one at a time.</summary>
    public async Task RunAsync(Func<Connection, Inbound, Task> handle, CancellationToken stopping)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stopping, closing.Token);
        var writer = WriteLoopAsync(linked.Token);
        var buffer = ArrayPool<byte>.Shared.Rent(MaxMessageBytes);
        try
        {
            while (socket.State == WebSocketState.Open && !linked.IsCancellationRequested)
            {
                var length = 0;
                ValueWebSocketReceiveResult result;
                do
                {
                    if (length == MaxMessageBytes) { await CloseAsync(WebSocketCloseStatus.MessageTooBig, "too big"); return; }
                    result = await socket.ReceiveAsync(buffer.AsMemory(length, MaxMessageBytes - length), linked.Token);
                    length += result.Count;
                } while (!result.EndOfMessage);
                if (result.MessageType == WebSocketMessageType.Close) break;
                if (result.MessageType != WebSocketMessageType.Text) continue;
                if (!Allow()) { await CloseAsync(WebSocketCloseStatus.PolicyViolation, "rate"); return; }
                if (Parse(buffer.AsSpan(0, length)) is { } inbound) await handle(this, inbound);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) when (e is WebSocketException or IOException) { } // the peer went away
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            Close();
            await writer;
            if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, replaced ? "replaced" : null, CancellationToken.None); }
                catch (Exception e) when (e is WebSocketException or IOException or ObjectDisposedException) { }
        }
    }

    private async Task WriteLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var frame in outbound.Reader.ReadAllAsync(ct))
                await socket.SendAsync(frame, WebSocketMessageType.Text, true, ct);
            if (replaced)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "replaced", ct);
                closing.Cancel();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception e) when (e is WebSocketException or IOException or ObjectDisposedException) { closing.Cancel(); }
    }

    private Task CloseAsync(WebSocketCloseStatus status, string reason) =>
        socket.CloseOutputAsync(status, reason, CancellationToken.None);

    private bool Allow()
    {
        var now = Environment.TickCount64;
        allowance = Math.Min(40, allowance + (now - lastRefill) * 0.02);
        lastRefill = now;
        if (allowance < 1) return false;
        allowance -= 1;
        return true;
    }

    private static Inbound? Parse(ReadOnlySpan<byte> utf8)
    {
        try
        {
            // Deserialize<JsonElement> yields a self-contained element: nothing pooled to dispose.
            var root = JsonSerializer.Deserialize<JsonElement>(utf8);
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String) return null;
            var id = root.TryGetProperty("id", out var i) && i.ValueKind == JsonValueKind.String ? i.GetString() : null;
            return new Inbound(type.GetString()!, id is { Length: <= 64 } ? id : null, root.TryGetProperty("data", out var data) ? data : default);
        }
        catch (JsonException) { return null; }
    }
}

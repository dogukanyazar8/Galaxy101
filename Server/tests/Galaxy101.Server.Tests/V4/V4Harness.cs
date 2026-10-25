using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Galaxy101.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Galaxy101.Server.Tests.V4;

/// <summary>One frame as the client got it: <c>type</c> and <c>data</c> (with <c>at</c>).</summary>
public sealed record Frame(string Type, JsonElement Data)
{
    public long At => Data.TryGetProperty("at", out var at) ? at.GetInt64() : 0;
    public JsonElement this[string key] => Data.GetProperty(key);
    public bool Has(string key) => Data.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null;
    public string? Str(string key) => Data.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    public int Int(string key) => Data.GetProperty(key).GetInt32();
    public bool Ok => Data.GetProperty("ok").GetBoolean();
    public string? Code => Str("code");
    /// <summary>A game frame's engine event.</summary>
    public JsonElement Ev => Data.GetProperty("ev");
    public string? E => Data.TryGetProperty("ev", out var ev) ? ev.GetProperty("e").GetString() : null;
}

/// <summary>
/// A v4 test client, like the package's tests/server.tests.js client(): every frame is kept in <see cref="Inbox"/>; send()
/// returns the ack. An account behind it is made through the HTTP endpoints.
/// </summary>
public sealed class V4Client : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly WebSocket socket;
    private readonly Task reader;
    private readonly List<Frame> inbox = [];
    private long received;
    private int nextId;

    public HttpClient Http { get; }
    public Guid Account { get; }
    public string? Email { get; init; }

    private V4Client(WebSocket socket, HttpClient http, Guid account)
    {
        (this.socket, Http, Account) = (socket, http, account);
        reader = ReadAsync();
    }

    public static async Task<V4Client> ConnectAsync(ServerFixture server, HttpClient http, Guid account, string? email = null)
    {
        var ticket = (await (await http.PostAsync("/v1/realtime/ticket", null)).Content.ReadFromJsonAsync<RealtimeTicketResponse>())!;
        var socket = await server.Server.CreateWebSocketClient().ConnectAsync(new Uri(server.Server.BaseAddress, "/v1/ws?ticket=" + ticket.Ticket), CancellationToken.None);
        return new V4Client(socket, http, account) { Email = email };
    }

    public List<Frame> Inbox { get { lock (inbox) return [.. inbox]; } }
    public List<Frame> Of(string type) => Inbox.Where(f => f.Type == type).ToList();
    public Frame? Last(string type) => Of(type).LastOrDefault();
    public List<Frame> Game(string e) => Of(V4Messages.Game).Where(f => f.E == e).ToList();

    /// <summary>Sends a v4 message and waits for its ack.</summary>
    public async Task<Frame> SendAsync(string type, object? data = null)
    {
        var id = "r" + Interlocked.Increment(ref nextId);
        await socket.SendAsync(JsonSerializer.SerializeToUtf8Bytes(new { type, id, data = data ?? new { } }, Json), WebSocketMessageType.Text, true, CancellationToken.None);
        var until = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < until)
        {
            lock (inbox)
                if (inbox.FirstOrDefault(f => f.Type == MessageTypes.Ack && f.Str("id") == id) is { } ack) return ack;
            await Task.Delay(2);
        }
        throw new TimeoutException($"No ack for {type}");
    }

    /// <summary>Returns once every frame the server queued for this socket so far has arrived.</summary>
    public async Task FlushAsync(Galaxy101.Server.Realtime.ConnectionRegistry registry)
    {
        var until = DateTime.UtcNow.AddSeconds(15);
        while (!reader.IsCompleted && Volatile.Read(ref received) < registry.Queued(Account) && DateTime.UtcNow < until) await Task.Delay(1);
    }

    private async Task ReadAsync()
    {
        var buffer = new byte[1024 * 1024];
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
                var type = root.GetProperty("type").GetString()!;
                if (type != MessageTypes.Pong) lock (inbox) inbox.Add(new Frame(type, root.GetProperty("data")));
                Interlocked.Increment(ref received);
            }
        }
        catch (Exception e) when (e is WebSocketException or ObjectDisposedException or IOException) { }
    }

    public async Task CloseAsync()
    {
        if (socket.State == WebSocketState.Open)
            try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None); }
            catch (Exception e) when (e is WebSocketException or ObjectDisposedException or IOException) { }
        await reader;
    }

    public async ValueTask DisposeAsync() { await CloseAsync(); socket.Dispose(); }
}

/// <summary>
/// The package's env(): the real server with a virtual clock (FakeTimeProvider). <see cref="UntilAsync"/> advances the clock
/// in 500 ms steps like E.until, letting the hub run everything due and every client receive what it was sent.
/// </summary>
public sealed class V4Env(ServerFixture server) : IAsyncDisposable
{
    private readonly List<V4Client> clients = [];
    public ServerFixture Server { get; } = server;
    public Galaxy101.Server.V4.Hub Hub => Server.Services.GetRequiredService<Galaxy101.Server.V4.Hub>();
    public long Now => Server.Clock.GetUtcNow().ToUnixTimeMilliseconds();

    /// <summary>A brand-new guest (10.000 chips) on a socket.</summary>
    public async Task<V4Client> GuestAsync(string? name = null)
    {
        var http = Server.CreateClient();
        var response = await http.PostAsJsonAsync("/v1/auth/guest", new GuestLoginRequest { Platform = "test", Name = name });
        response.EnsureSuccessStatusCode();
        var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return await Connect(http, tokens.AccountId);
    }

    /// <summary>A registered account (25.000 chips) on a socket.</summary>
    public async Task<V4Client> RegisterAsync(string? name = null)
    {
        var http = Server.CreateClient();
        var email = $"{Guid.NewGuid():N}@g.co";
        var response = await http.PostAsJsonAsync("/v1/auth/register", new RegisterRequest
        {
            Name = name ?? "P" + Guid.NewGuid().ToString("N")[..8], Email = email, Password = "123456", AcceptTerms = true,
        });
        response.EnsureSuccessStatusCode();
        var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return await Connect(http, tokens.AccountId, email);
    }

    /// <summary>The same account on a new socket (reconnect).</summary>
    public Task<V4Client> ReconnectAsync(V4Client old) => Connect(old.Http, old.Account, old.Email);

    private async Task<V4Client> Connect(HttpClient http, Guid account, string? email = null)
    {
        var c = await V4Client.ConnectAsync(Server, http, account, email);
        clients.Add(c);
        return c;
    }

    /// <summary>Everything due has run on the hub and reached the clients.</summary>
    public async Task SettleAsync()
    {
        await Hub.IdleAsync();
        var registry = Server.Services.GetRequiredService<Galaxy101.Server.Realtime.ConnectionRegistry>();
        foreach (var c in clients.ToList()) await c.FlushAsync(registry);
    }

    public async Task AdvanceAsync(long ms)
    {
        Server.Clock.Advance(TimeSpan.FromMilliseconds(ms));
        await SettleAsync();
    }

    /// <summary>E.until: 500 ms steps until <paramref name="done"/> holds (at most <paramref name="max"/> steps).</summary>
    public async Task<bool> UntilAsync(Func<bool> done, int max = 5000, long step = 500)
    {
        await SettleAsync();
        for (var i = 0; i < max && !done(); i++) await AdvanceAsync(step);
        return done();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var c in clients) await c.DisposeAsync();
    }
}

using System.Collections.Concurrent;
using Galaxy101.Contracts;
using Galaxy101.Server.Auth;
using Galaxy101.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Galaxy101.Server.Realtime;

/// <summary>A feature that owns a family of inbound message types (e.g. "match.*").</summary>
public interface IRealtimeHandler
{
    bool Handles(string type);
    /// <summary>Returns null on success or an error code; the router turns it into an ack when the client sent an id.</summary>
    Task<string?> HandleAsync(Connection connection, Inbound message, CancellationToken ct);
    Task OnDisconnectedAsync(Guid account);
}

/// <summary>
/// The answer to a v4 message (docs/v4/KARARLAR.md K-04): the error code (package <c>error</c>, sent as the ack's <c>code</c>),
/// its <c>detail</c>, and the message's return fields, which the ack carries next to <c>id</c> / <c>ok</c> / <c>code</c>.
/// </summary>
public sealed record Reply(string? Error = null, object? Detail = null, IReadOnlyDictionary<string, object?>? Data = null)
{
    public static readonly Reply Ok = new();
    public static Reply Fail(string error, object? detail = null) => new(error, detail);
    public static Reply With(params (string Key, object? Value)[] data) => new(Data: data.ToDictionary(d => d.Key, d => d.Value));
}

/// <summary>A v4 handler: every message gets exactly one ack, with the reply's detail and return fields.</summary>
public interface IRealtimeReplyHandler : IRealtimeHandler
{
    Task<Reply> ReplyAsync(Connection connection, Inbound message, CancellationToken ct);
}

public static class Presence
{
    public const string Offline = "offline", Online = "online", Playing = "playing";
}

/// <summary>
/// Live sockets by account (one per account: a new device replaces the old one) and presence.
/// In-process by design: the game server is a single instance holding the authoritative tables.
/// </summary>
public sealed class ConnectionRegistry(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ConnectionRegistry> log)
{
    private readonly ConcurrentDictionary<Guid, Connection> connections = new();
    private readonly ConcurrentDictionary<Guid, Guid> playing = new();

    public int Count => connections.Count;

    public void Register(Connection connection)
    {
        Connection? previous = null;
        connections.AddOrUpdate(connection.Account, connection, (_, old) => { previous = old; return connection; });
        if (previous is not null)
        {
            previous.Replace(Connection.Frame(MessageTypes.Replaced, new { }));
        }
        else Announce(connection.Account);
    }

    public void Unregister(Connection connection)
    {
        if (connections.TryRemove(new KeyValuePair<Guid, Connection>(connection.Account, connection))) Announce(connection.Account);
    }

    public bool IsOnline(Guid account) => connections.ContainsKey(account);

    public string PresenceOf(Guid account) => !connections.ContainsKey(account) ? Presence.Offline : playing.ContainsKey(account) ? Presence.Playing : Presence.Online;

    public bool Send<T>(Guid account, string type, T data) => connections.TryGetValue(account, out var c) && c.Send(type, data);

    public bool SendFrame(Guid account, byte[] frame) => connections.TryGetValue(account, out var c) && c.SendFrame(frame);

    public void Disconnect(Guid account)
    {
        if (connections.TryRemove(account, out var c)) c.Close();
    }

    public void SetPlaying(Guid account, Guid? match)
    {
        var changed = match is { } id ? playing.TryAdd(account, id) : playing.TryRemove(account, out _);
        if (changed && IsOnline(account)) Announce(account);
    }

    /// <summary>Tells the account's online friends about its presence. Fire-and-forget; presence is advisory.</summary>
    private void Announce(Guid account) => _ = Task.Run(async () =>
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            var friends = await db.Friendships.Where(f => f.Accepted && (f.LowId == account || f.HighId == account))
                .Select(f => f.LowId == account ? f.HighId : f.LowId).ToListAsync();
            var message = new PresenceMessage { AccountId = account, Presence = PresenceOf(account) };
            if (message.Presence == Presence.Offline)
                await db.Accounts.Where(a => a.Id == account).ExecuteUpdateAsync(s => s.SetProperty(a => a.LastSeenAt, clock.GetUtcNow()));
            foreach (var friend in friends) Send(friend, MessageTypes.Presence, message);
        }
        catch (Exception error) { log.LogWarning(error, "Presence broadcast failed for {Account}", account); }
    });
}

public static class RealtimeEndpoint
{
    public static void MapRealtime(this IEndpointRouteBuilder app) => app.MapGet("/v1/ws", async (HttpContext http, RealtimeTickets tickets,
        DeletedAccounts deleted, ConnectionRegistry registry, IEnumerable<IRealtimeHandler> handlers, IHostApplicationLifetime lifetime, TimeProvider clock, ILogger<Connection> log) =>
    {
        if (!http.WebSockets.IsWebSocketRequest) return Results.BadRequest();
        // A ticket taken before its account was deleted is worthless afterwards.
        if (tickets.Redeem(http.Request.Query["ticket"]) is not { } account || deleted.Contains(account)) return Results.Unauthorized();
        using var socket = await http.WebSockets.AcceptWebSocketAsync();
        var connection = new Connection(account, socket, log);
        registry.Register(connection);
        // Deleted while this socket was being accepted: the deletion's disconnect may have run before it was registered.
        if (deleted.Contains(account)) registry.Disconnect(account);
        try
        {
            await connection.RunAsync(async (c, message) =>
            {
                if (message.Type == MessageTypes.Ping)
                {
                    c.Send(MessageTypes.Pong, new { });
                    // v4 ping: the ack carries the server time (clock sync, protocol/timings.md).
                    if (message.Id is not null) c.SendFrame(V4Ack(message.Id, Reply.With(("serverTime", clock.GetUtcNow().ToUnixTimeMilliseconds())), clock));
                    return;
                }
                if (handlers.FirstOrDefault(h => h.Handles(message.Type)) is IRealtimeReplyHandler v4)
                {
                    Reply reply;
                    try { reply = await v4.ReplyAsync(c, message, c.Closing); }
                    catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException or ArgumentException or FormatException or KeyNotFoundException)
                    {
                        reply = Reply.Fail(V4Errors.E_BAD_MESSAGE);
                    }
                    catch (Exception e) when (e is not OperationCanceledException)
                    {
                        log.LogError(e, "Realtime {Type} failed for {Account}", message.Type, c.Account);
                        reply = Reply.Fail("server_error");
                    }
                    c.SendFrame(V4Ack(message.Id, reply, clock));
                    return;
                }
                string? error;
                try
                {
                    var handler = handlers.FirstOrDefault(h => h.Handles(message.Type));
                    error = handler is null ? ErrorCodes.NotFound : await handler.HandleAsync(c, message, c.Closing);
                }
                catch (Exception e) when (e is System.Text.Json.JsonException or InvalidOperationException or ArgumentException or FormatException)
                {
                    // Malformed payloads are the client's problem, not a reason to drop the table.
                    error = ErrorCodes.Validation;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    log.LogError(e, "Realtime {Type} failed for {Account}", message.Type, c.Account);
                    error = "server_error";
                }
                if (message.Id is not null) c.Send(MessageTypes.Ack, new AckMessage { Id = message.Id, Ok = error is null, Code = error });
            }, lifetime.ApplicationStopping);
        }
        finally
        {
            registry.Unregister(connection);
            // A socket replaced by the same player's new one is not a disconnect.
            if (!registry.IsOnline(account))
                foreach (var handler in handlers) await handler.OnDisconnectedAsync(account);
        }
        return Results.Empty;
    });

    /// <summary>A v4 ack frame: <c>{ id, ok, code, detail?, at, ...return fields }</c>.</summary>
    public static byte[] V4Ack(string? id, Reply reply, TimeProvider clock)
    {
        var data = new System.Text.Json.Nodes.JsonObject
        {
            ["id"] = id, ["ok"] = reply.Error is null, ["code"] = reply.Error, ["at"] = clock.GetUtcNow().ToUnixTimeMilliseconds(),
        };
        if (reply.Detail is not null) data["detail"] = System.Text.Json.JsonSerializer.SerializeToNode(reply.Detail, Connection.Json);
        if (reply.Data is not null)
            foreach (var (key, value) in reply.Data) data[key] = value is null ? null : System.Text.Json.JsonSerializer.SerializeToNode(value, value.GetType(), Connection.Json);
        return Connection.Frame(MessageTypes.Ack, data);
    }
}

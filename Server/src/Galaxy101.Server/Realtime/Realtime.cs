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
            previous.Send(MessageTypes.Replaced, new { });
            previous.Close();
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
        ConnectionRegistry registry, IEnumerable<IRealtimeHandler> handlers, IHostApplicationLifetime lifetime, ILogger<Connection> log) =>
    {
        if (!http.WebSockets.IsWebSocketRequest) return Results.BadRequest();
        if (tickets.Redeem(http.Request.Query["ticket"]) is not { } account) return Results.Unauthorized();
        using var socket = await http.WebSockets.AcceptWebSocketAsync();
        var connection = new Connection(account, socket, log);
        registry.Register(connection);
        try
        {
            await connection.RunAsync(async (c, message) =>
            {
                if (message.Type == MessageTypes.Ping) { c.Send(MessageTypes.Pong, new { }); return; }
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
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Infrastructure;
using Galaxy101.Server.Realtime;
using Galaxy101.Server.Social;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Galaxy101.Server.Games;

/// <summary>Shared entry checks for queues and rooms: allowed stake, not already busy, enough chips for the stake.</summary>
public sealed class Entry(Lobby lobby, IServiceScopeFactory scopes, IOptions<GameOptions> options)
{
    public async Task<string?> CheckAsync(Guid account, string mode, long stake)
    {
        if (!options.Value.Stakes.TryGetValue(mode, out var stakes) || !stakes.Contains(stake)) return ErrorCodes.Validation;
        if (lobby.Where(account) is not null) return ErrorCodes.Conflict;
        if (stake == 0) return null;
        await using var scope = scopes.CreateAsyncScope();
        var chips = await scope.ServiceProvider.GetRequiredService<GameDb>().Wallets.Where(w => w.AccountId == account).Select(w => w.Chips).FirstOrDefaultAsync();
        return chips >= stake ? null : ErrorCodes.InsufficientFunds;
    }
}

/// <summary>
/// First-come tables per (mode, stake). Four waiting players form a match; anyone who can no longer pay the stake
/// at that moment is dropped and the rest keep their place at the front of the queue. Dereceli forms tables within one
/// league of each other (two once the longest-waiting player has waited <see cref="Widen"/>).
/// </summary>
public sealed class Matchmaker : IRealtimeHandler
{
    public static readonly TimeSpan Widen = TimeSpan.FromSeconds(30);
    private readonly object gate = new();
    private readonly Dictionary<(string Mode, long Stake), List<Guid>> queues = [];
    /// <summary>Each queued player's league (0 Bronz … 4 Usta and up; 0 outside Dereceli) and when they began waiting.</summary>
    private readonly Dictionary<Guid, (int League, DateTimeOffset Since)> tickets = [];
    private readonly MatchManager matches; private readonly Lobby lobby; private readonly Entry entry; private readonly ConnectionRegistry live;
    private readonly IServiceScopeFactory scopes; private readonly GameCalendar calendar; private readonly TimeProvider clock; private readonly ILogger<Matchmaker> log;
    private readonly ITimer widening;

    public Matchmaker(MatchManager matches, Lobby lobby, Entry entry, ConnectionRegistry live, IServiceScopeFactory scopes, GameCalendar calendar,
        TimeProvider clock, ILogger<Matchmaker> log)
    {
        (this.matches, this.lobby, this.entry, this.live, this.scopes, this.calendar, this.clock, this.log) = (matches, lobby, entry, live, scopes, calendar, clock, log);
        // Waiting Dereceli players are looked at again as their window widens.
        widening = clock.CreateTimer(_ => Widened(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public bool Handles(string type) => type is MessageTypes.QueueJoin or MessageTypes.QueueLeave;

    public async Task<string?> HandleAsync(Connection connection, Inbound message, CancellationToken ct)
    {
        var account = connection.Account;
        if (message.Type == MessageTypes.QueueLeave) { Remove(account); return null; }
        var join = message.Data.Deserialize<QueueJoinMessage>(Connection.Json)!;
        if (!GameModes.Rated.Contains(join.Mode)) return ErrorCodes.Validation;
        if (await entry.CheckAsync(account, join.Mode, join.Stake) is { } error) return error;
        var league = join.Mode == GameModes.Ranked ? await LeagueAsync(account) : 0;
        if (!lobby.TryEnter(account, LobbyPlace.Queue)) return ErrorCodes.Conflict;
        List<Guid>? table = null;
        var key = (join.Mode, join.Stake);
        lock (gate)
        {
            var queue = queues.TryGetValue(key, out var q) ? q : queues[key] = [];
            queue.Add(account);
            tickets[account] = (league, clock.GetUtcNow());
            table = Pick(queue);
        }
        Notify(key);
        if (table is not null) _ = FormAsync(key, table);
        return null;
    }

    public Task OnDisconnectedAsync(Guid account) { Remove(account); return Task.CompletedTask; }

    /// <summary>The player's league this season (rolled over first, so a new season's soft reset counts).</summary>
    private async Task<int> LeagueAsync(Guid account)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        await Profile.ProfileEndpoints.SeasonAsync(db, scope.ServiceProvider.GetRequiredService<Economy.Ledger>(), calendar, account, default);
        var lp = await db.Stats.Where(x => x.AccountId == account).Select(x => x.Lp).FirstOrDefaultAsync();
        return Math.Min(Profile.Ranked.League(lp), 4);
    }

    /// <summary>
    /// Four from the queue (oldest first) whose leagues are at most one apart — two once the oldest of them has waited
    /// <see cref="Widen"/>; other modes have every league 0. Under the lock.
    /// </summary>
    private List<Guid>? Pick(List<Guid> queue)
    {
        if (queue.Count < 4) return null;
        var now = clock.GetUtcNow();
        foreach (var anchor in queue)
        {
            var (league, since) = tickets.GetValueOrDefault(anchor);
            var window = now - since >= Widen ? 2 : 1;
            for (var low = league - window; low <= league; low++)
            {
                var group = queue.Where(a => tickets.GetValueOrDefault(a).League is var l && l >= low && l <= low + window).Take(4).ToList();
                if (group.Count < 4 || !group.Contains(anchor)) continue;
                foreach (var a in group) queue.Remove(a);
                return group;
            }
        }
        return null;
    }

    private void Widened()
    {
        var formed = new List<((string, long) Key, List<Guid> Table)>();
        lock (gate)
            foreach (var (key, queue) in queues)
                if (key.Mode == GameModes.Ranked && Pick(queue) is { } table) formed.Add((key, table));
        foreach (var (key, table) in formed) { Notify(key); _ = FormAsync(key, table); }
    }

    public void Remove(Guid account)
    {
        (string, long)? changed = null;
        lock (gate)
        {
            tickets.Remove(account);
            foreach (var (queueKey, queue) in queues)
                if (queue.Remove(account)) { changed = queueKey; break; }
        }
        if (changed is { } key) { lobby.Exit(account, LobbyPlace.Queue); live.Send(account, MessageTypes.QueueStatus, Status(key, false)); Notify(key); }
    }

    private const int FormAttempts = 3;
    private static readonly TimeSpan FormRetryDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// A failed creation (nothing was charged: it is one transaction) is retried for the same four after a growing pause;
    /// after the last attempt they leave the queue instead of the server re-forming the same table in a tight loop.
    /// </summary>
    private async Task FormAsync((string Mode, long Stake) key, List<Guid> table, int attempt = 1)
    {
        try
        {
            var unpaid = await matches.CreateAsync(key.Mode, key.Stake, table);
            if (unpaid.Count == 0) { lock (gate) foreach (var account in table) tickets.Remove(account); return; }
            lock (gate) foreach (var account in unpaid) tickets.Remove(account);
            foreach (var account in unpaid) { lobby.Exit(account, LobbyPlace.Queue); live.Send(account, MessageTypes.QueueStatus, Status(key, false)); }
            Requeue(key, [.. table.Except(unpaid)]);
        }
        catch (Exception error)
        {
            var last = attempt >= FormAttempts;
            log.LogError(error, "Forming a {Mode} table failed (attempt {Attempt}); {Next}", key.Mode, attempt, last ? "players leave the queue" : "retrying");
            // Players gone meanwhile (or every player, after the last attempt) leave the queue for good.
            var staying = last ? [] : table.Where(live.IsOnline).ToList();
            lock (gate) foreach (var account in table.Except(staying)) tickets.Remove(account);
            foreach (var account in table.Except(staying)) { lobby.Exit(account, LobbyPlace.Queue); live.Send(account, MessageTypes.QueueStatus, Status(key, false)); }
            if (last) return;
            await Task.Delay(FormRetryDelay * attempt);
            if (staying.Count == table.Count) await FormAsync(key, table, attempt + 1);
            else Requeue(key, staying);
        }
    }

    private void Requeue((string, long) key, List<Guid> players)
    {
        List<Guid>? table = null;
        lock (gate)
        {
            var queue = queues.TryGetValue(key, out var q) ? q : queues[key] = [];
            queue.InsertRange(0, players.Where(p => !queue.Contains(p)));
            table = Pick(queue);
        }
        Notify(key);
        if (table is not null) _ = FormAsync(key, table);
    }

    private void Notify((string Mode, long Stake) key)
    {
        List<Guid> waiting;
        lock (gate) waiting = queues.TryGetValue(key, out var q) ? [.. q] : [];
        foreach (var account in waiting) live.Send(account, MessageTypes.QueueStatus, Status(key, true, waiting.Count));
    }

    private static QueueStatusMessage Status((string Mode, long Stake) key, bool queued, int waiting = 0) =>
        new() { Mode = key.Mode, Stake = key.Stake, Queued = queued, Waiting = waiting };
}

/// <summary>Password-protected private tables formed by invitation. The match starts when the fourth seat fills.</summary>
public sealed class PrivateRooms(MatchManager matches, Lobby lobby, Entry entry, ConnectionRegistry live, IServiceScopeFactory scopes, ILogger<PrivateRooms> log) : IRealtimeHandler
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private sealed class Room(string code, long stake, string? password)
    {
        public string Code { get; } = code;
        public long Stake { get; } = stake;
        public string? Password { get; } = password;
        public Guid?[] Seats { get; } = new Guid?[4];
        public Guid Host { get; set; }
    }

    private readonly object gate = new();
    private readonly Dictionary<string, Room> rooms = [];
    private readonly Dictionary<Guid, Room> seated = [];

    public bool Handles(string type) => type is MessageTypes.RoomCreate or MessageTypes.RoomJoin or MessageTypes.RoomLeave;

    public bool IsMember(Guid account, string code)
    {
        lock (gate) return seated.TryGetValue(account, out var room) && room.Code == code;
    }

    public long? StakeOf(string code)
    {
        lock (gate) return rooms.TryGetValue(code, out var room) ? room.Stake : null;
    }

    public async Task<string?> HandleAsync(Connection connection, Inbound message, CancellationToken ct)
    {
        var account = connection.Account;
        switch (message.Type)
        {
            case MessageTypes.RoomLeave:
                Remove(account);
                return null;
            case MessageTypes.RoomCreate:
            {
                var create = message.Data.Deserialize<RoomCreateMessage>(Connection.Json)!;
                if (create.Password is { Length: > 16 }) return ErrorCodes.Validation;
                if (await entry.CheckAsync(account, GameModes.Private, create.Stake) is { } error) return error;
                if (!lobby.TryEnter(account, LobbyPlace.Room)) return ErrorCodes.Conflict;
                Room room;
                lock (gate)
                {
                    string code;
                    do code = string.Concat(Enumerable.Range(0, 6).Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]));
                    while (rooms.ContainsKey(code));
                    room = new Room(code, create.Stake, string.IsNullOrEmpty(create.Password) ? null : create.Password) { Host = account };
                    room.Seats[0] = account;
                    rooms[code] = room;
                    seated[account] = room;
                }
                await PublishAsync(room);
                return null;
            }
            case MessageTypes.RoomJoin:
            {
                var join = message.Data.Deserialize<RoomJoinMessage>(Connection.Json)!;
                var code = (join.Code ?? "").Trim().ToUpperInvariant();
                long stake;
                lock (gate)
                {
                    if (!rooms.TryGetValue(code, out var found)) return ErrorCodes.NotFound;
                    if (found.Password is not null && !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(found.Password), Encoding.UTF8.GetBytes(join.Password ?? "")))
                        return ErrorCodes.Unauthorized;
                    stake = found.Stake;
                }
                if (await entry.CheckAsync(account, GameModes.Private, stake) is { } error) return error;
                if (!lobby.TryEnter(account, LobbyPlace.Room)) return ErrorCodes.Conflict;
                Room? room;
                Guid[]? table = null;
                lock (gate)
                {
                    // The room may have filled or closed while we checked the wallet.
                    var free = rooms.TryGetValue(code, out room) ? Array.IndexOf(room.Seats, null) : -1;
                    if (room is null || free < 0) room = null;
                    else
                    {
                        room.Seats[free] = account;
                        seated[account] = room;
                        if (room.Seats.All(s => s is not null))
                        {
                            table = [.. room.Seats.Select(s => s!.Value)];
                            rooms.Remove(room.Code);
                            foreach (var p in table) seated.Remove(p);
                        }
                    }
                }
                if (room is null) { lobby.Exit(account, LobbyPlace.Room); return ErrorCodes.Conflict; }
                await PublishAsync(room, closed: table is not null);
                if (table is not null) _ = StartAsync(room, table);
                return null;
            }
            default:
                return ErrorCodes.NotFound;
        }
    }

    public Task OnDisconnectedAsync(Guid account) { Remove(account); return Task.CompletedTask; }

    public void Remove(Guid account)
    {
        Room? room;
        lock (gate)
        {
            if (!seated.Remove(account, out room)) return;
            room.Seats[Array.IndexOf(room.Seats, account)] = null;
            if (room.Seats.All(s => s is null)) rooms.Remove(room.Code);
            else if (room.Host == account) room.Host = room.Seats.First(s => s is not null)!.Value;
        }
        lobby.Exit(account, LobbyPlace.Room);
        _ = PublishAsync(room);
        live.Send(account, MessageTypes.RoomState, new RoomStateMessage { Code = room.Code, Closed = true });
    }

    private async Task StartAsync(Room room, Guid[] table)
    {
        try
        {
            var unpaid = await matches.CreateAsync(GameModes.Private, room.Stake, table);
            if (unpaid.Count == 0) return;
            // Someone spent their chips meanwhile: reopen the room without them.
            lock (gate)
            {
                for (var s = 0; s < 4; s++) room.Seats[s] = unpaid.Contains(table[s]) ? null : table[s];
                if (unpaid.Contains(room.Host)) room.Host = room.Seats.First(x => x is not null)!.Value;
                rooms[room.Code] = room;
                foreach (var p in table.Except(unpaid)) seated[p] = room;
            }
            foreach (var account in unpaid) lobby.Exit(account, LobbyPlace.Room);
            await PublishAsync(room);
        }
        catch (Exception error)
        {
            log.LogError(error, "Private room {Code} failed to start", room.Code);
            foreach (var account in table) { lobby.Exit(account, LobbyPlace.Room); live.Send(account, MessageTypes.RoomState, new RoomStateMessage { Code = room.Code, Closed = true }); }
        }
    }

    private async Task PublishAsync(Room room, bool closed = false)
    {
        Guid?[] seats;
        Guid host;
        lock (gate) { seats = [.. room.Seats]; host = room.Host; }
        await using var scope = scopes.CreateAsyncScope();
        var cards = await Players.SummariesAsync(scope.ServiceProvider.GetRequiredService<GameDb>(), [.. seats.OfType<Guid>()], default);
        var state = new RoomStateMessage
        {
            Code = room.Code, Stake = room.Stake, HostId = host, HasPassword = room.Password is not null, Closed = closed,
            Seats = [.. seats.Select(s => s is { } id ? cards.GetValueOrDefault(id) : null)],
        };
        foreach (var account in seats.OfType<Guid>()) live.Send(account, MessageTypes.RoomState, state);
    }
}

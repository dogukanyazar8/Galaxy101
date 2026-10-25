using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Economy;
using Galaxy101.Server.Infrastructure;
using Galaxy101.Server.Realtime;
using Microsoft.Extensions.Options;

namespace Galaxy101.Server.Games;

public enum LobbyPlace { Queue, Room, Match }

/// <summary>Where each player currently is. A player is in at most one queue, room or match at a time.</summary>
public sealed class Lobby
{
    private readonly ConcurrentDictionary<Guid, LobbyPlace> places = new();

    public bool TryEnter(Guid account, LobbyPlace place) => places.TryAdd(account, place);
    public void Exit(Guid account, LobbyPlace place) => places.TryRemove(new KeyValuePair<Guid, LobbyPlace>(account, place));
    /// <summary>Hands a player from a queue or room straight to the match that formed from it.</summary>
    public void MoveToMatch(Guid account) => places[account] = LobbyPlace.Match;
    public LobbyPlace? Where(Guid account) => places.TryGetValue(account, out var p) ? p : null;
}

public sealed class MatchManager(MatchStore store, ConnectionRegistry live, Catalog catalog, Lobby lobby, IOptions<GameOptions> options,
    TimeProvider clock, ILoggerFactory logs) : IRealtimeHandler, IHostedService
{
    private readonly ConcurrentDictionary<Guid, MatchSession> byAccount = new();
    /// <summary>Who watches which match (İzle): one match at a time; a socket drop or match.leave stops it.</summary>
    private readonly ConcurrentDictionary<Guid, MatchSession> watching = new();
    private readonly ILogger log = logs.CreateLogger<MatchManager>();

    public int ActiveCount => byAccount.Values.Distinct().Count();

    public MatchSession? Current(Guid account) => byAccount.GetValueOrDefault(account);

    /// <summary>
    /// Seats four players (random seats; pairs are seats 0+2 and 1+3), takes the stakes and starts dealing.
    /// Returns the players who could not pay; then nothing was created and nobody was charged.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> CreateAsync(string mode, long stake, IReadOnlyList<Guid> accounts)
    {
        var seats = accounts.ToArray();
        RandomNumberGenerator.Shuffle(seats.AsSpan());
        return await CreateAsync(new MatchRecord
        {
            Id = Guid.CreateVersion7(), Mode = mode, Stake = stake, Status = MatchStatus.Active, Seats = seats,
            Hands = options.Value.HandsPerMatch, FirstStarter = RandomNumberGenerator.GetInt32(4), CreatedAt = clock.GetUtcNow(),
        });
    }

    /// <summary>
    /// A Klasik table's match: seats as the table had them (pairs keep their partners), bots where the table put bots,
    /// the table's hands / turn time / chat and gift rules. Returns the humans who could not pay.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> CreateAsync(MatchRecord record)
    {
        var unpaid = await store.CreateAsync(record, default);
        if (unpaid.Length > 0) return [.. unpaid.Select(s => record.Seats[s])];
        var session = Register(record);
        for (var s = 0; s < 4; s++) if (!IsBot(record, s)) lobby.MoveToMatch(record.Seats[s]);
        Started?.Invoke(session);
        try { await session.StartAsync(); } // storage hiccups are retried inside: only a fault in the session itself lands here
        catch (Exception error)
        {
            log.LogError(error, "Match {Match} failed to start; refunding the stakes", record.Id);
            Release(session);
            try { await store.AbortAsync(record); }
            catch (Exception refund)
            {
                // The stakes stay with the stored match, which the next start resumes: the lobby must not form it again.
                log.LogError(refund, "Match {Match}: refund failed; the stored match resumes on restart", record.Id);
                return [];
            }
            throw; // refunded: nothing is charged, the lobby may form the table again
        }
        return [];
    }

    public static bool IsBot(MatchRecord record, int seat) => (record.BotMask & (1 << seat)) != 0;

    /// <summary>A match began / closed (the lobby shows "Oynanıyor · N. el", then drops the table); its free seats changed.</summary>
    public event Action<MatchSession>? Started, Closed, SeatsChanged;

    public async Task<string?> WatchAsync(Guid account, MatchSession session)
    {
        if (Current(account) is not null) return ErrorCodes.Conflict;
        var error = await session.WatchAsync(account);
        if (error is not null) return error;
        if (watching.TryGetValue(account, out var before) && before != session) await Quietly(before.UnwatchAsync(account));
        watching[account] = session;
        return null;
    }

    /// <summary>"Oyuna katıl": the caller has passed the lobby's entry checks (not elsewhere, enough chips).</summary>
    public async Task<string?> TakeSeatAsync(Guid account, MatchSession session, int? seat)
    {
        if (!lobby.TryEnter(account, LobbyPlace.Match)) return ErrorCodes.Conflict;
        string? error;
        try { error = await session.TakeSeatAsync(account, seat); }
        catch (InvalidOperationException) { error = TableErrors.Closed; } // the match ended at the same moment
        if (error is not null) { lobby.Exit(account, LobbyPlace.Match); return error; }
        byAccount[account] = session;
        watching.TryRemove(account, out _);
        SeatsChanged?.Invoke(session);
        return null;
    }

    private static async Task Quietly(Task task)
    {
        try { await task; } catch (InvalidOperationException) { } // already closed
    }

    public bool Handles(string type) => type.StartsWith("match.", StringComparison.Ordinal);

    public async Task<string?> HandleAsync(Connection connection, Inbound message, CancellationToken ct)
    {
        var session = Current(connection.Account);
        var matchId = message.Data.ValueKind == JsonValueKind.Object && message.Data.TryGetProperty("matchId", out var id) ? id.GetGuid() : Guid.Empty;
        // A spectator only stops watching.
        if (session is null && watching.TryGetValue(connection.Account, out var watched) && watched.Id == matchId)
        {
            // A spectator's table opens with match.sync (the match again from the start message) and closes with match.leave.
            if (message.Type == MessageTypes.MatchSync) return await watched.WatchAsync(connection.Account);
            if (message.Type != MessageTypes.MatchLeave) return ErrorCodes.NotFound;
            if (watching.TryRemove(new KeyValuePair<Guid, MatchSession>(connection.Account, watched))) await Quietly(watched.UnwatchAsync(connection.Account));
            return null;
        }
        if (session is null || session.Id != matchId) return ErrorCodes.NotFound;
        switch (message.Type)
        {
            case MessageTypes.MatchCommand:
                return await session.SubmitAsync(connection.Account, message.Data.Deserialize<MatchCommandMessage>(Connection.Json)!);
            case MessageTypes.MatchSync:
                await session.SyncAsync(connection.Account);
                return null;
            case MessageTypes.MatchLeave:
                await LeaveAsync(connection.Account);
                return null;
            case MessageTypes.MatchEmote:
                return await session.EmoteAsync(connection.Account, message.Data.Deserialize<EmoteMessage>(Connection.Json)!.Emote ?? "");
            case MessageTypes.MatchChat:
                var chat = message.Data.Deserialize<ChatMessage>(Connection.Json)!;
                return await session.ChatAsync(connection.Account, chat.Text, chat.Quick);
            case MessageTypes.MatchGift:
                var gift = message.Data.Deserialize<GiftMessage>(Connection.Json)!;
                return await session.GiftAsync(connection.Account, gift.Gift ?? "", gift.TargetSeat);
            default:
                return ErrorCodes.NotFound;
        }
    }

    public async Task LeaveAsync(Guid account)
    {
        if (Current(account) is not { } session) return;
        try { await session.LeaveAsync(account); }
        catch (InvalidOperationException) { } // the match ended at the same moment
        // Reached only once the forfeit is stored: a failed write throws above and leaves the player seated to try again.
        if (byAccount.TryRemove(new KeyValuePair<Guid, MatchSession>(account, session))) lobby.Exit(account, LobbyPlace.Match);
        SeatsChanged?.Invoke(session); // a seat the server now plays can be taken
    }

    public Task BlockChangedAsync(Guid blocker, Guid blocked, bool on) => Current(blocker)?.BlockChangedAsync(blocker, blocked, on) ?? Task.CompletedTask;

    public Task OnDisconnectedAsync(Guid account) => watching.TryRemove(account, out var watched) ? Quietly(watched.UnwatchAsync(account)) : Seated(account);

    private Task Seated(Guid account) => Current(account)?.DisconnectedAsync(account) ?? Task.CompletedTask;

    /// <summary>Resumes every match that was in play when the process stopped.</summary>
    public async Task StartAsync(CancellationToken ct)
    {
        foreach (var stored in await store.LoadActiveAsync(ct))
        {
            var session = Register(stored.Record);
            for (var s = 0; s < 4; s++)
                if ((stored.Record.LeftMask & (1 << s)) == 0 && !IsBot(stored.Record, s)) lobby.MoveToMatch(stored.Record.Seats[s]);
            try { await session.RestoreAsync(stored); }
            catch (Exception error)
            {
                log.LogError(error, "Match {Match} could not be restored; refunding stakes", stored.Record.Id);
                Release(session);
                await store.AbortAsync(stored.Record);
            }
        }
    }

    /// <summary>Stops timers so nothing is written during shutdown; everything that matters is already stored.</summary>
    public Task StopAsync(CancellationToken ct) => Task.WhenAll(byAccount.Values.Distinct().Select(s => s.StopAsync()));

    private MatchSession Register(MatchRecord record)
    {
        var session = new MatchSession(record, store, live, catalog, options.Value, clock, logs.CreateLogger<MatchSession>(), Release);
        for (var s = 0; s < 4; s++)
            if ((record.LeftMask & (1 << s)) == 0 && !IsBot(record, s)) byAccount[record.Seats[s]] = session;
        return session;
    }

    private void Release(MatchSession session)
    {
        foreach (var (account, watched) in watching) if (watched == session) watching.TryRemove(new KeyValuePair<Guid, MatchSession>(account, session));
        foreach (var account in session.Seats)
            if (byAccount.TryRemove(new KeyValuePair<Guid, MatchSession>(account, session))) lobby.Exit(account, LobbyPlace.Match);
        Closed?.Invoke(session);
    }
}

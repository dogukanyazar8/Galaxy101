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
        var record = new MatchRecord
        {
            Id = Guid.CreateVersion7(), Mode = mode, Stake = stake, Status = MatchStatus.Active, Seats = seats,
            Hands = options.Value.HandsPerMatch, FirstStarter = RandomNumberGenerator.GetInt32(4), CreatedAt = clock.GetUtcNow(),
        };
        var unpaid = await store.CreateAsync(record, default);
        if (unpaid.Length > 0) return [.. unpaid.Select(s => seats[s])];
        var session = Register(record);
        foreach (var account in seats) lobby.MoveToMatch(account);
        await session.StartAsync();
        return [];
    }

    public bool Handles(string type) => type.StartsWith("match.", StringComparison.Ordinal);

    public async Task<string?> HandleAsync(Connection connection, Inbound message, CancellationToken ct)
    {
        var session = Current(connection.Account);
        var matchId = message.Data.ValueKind == JsonValueKind.Object && message.Data.TryGetProperty("matchId", out var id) ? id.GetGuid() : Guid.Empty;
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
            case MessageTypes.MatchGift:
                var gift = message.Data.Deserialize<GiftMessage>(Connection.Json)!;
                return await session.GiftAsync(connection.Account, gift.Gift ?? "", gift.TargetSeat);
            default:
                return ErrorCodes.NotFound;
        }
    }

    public async Task LeaveAsync(Guid account)
    {
        if (byAccount.TryRemove(account, out var session))
        {
            lobby.Exit(account, LobbyPlace.Match);
            try { await session.LeaveAsync(account); }
            catch (InvalidOperationException) { } // the match ended at the same moment
        }
    }

    public Task OnDisconnectedAsync(Guid account) => Current(account)?.DisconnectedAsync(account) ?? Task.CompletedTask;

    /// <summary>Resumes every match that was in play when the process stopped.</summary>
    public async Task StartAsync(CancellationToken ct)
    {
        foreach (var stored in await store.LoadActiveAsync(ct))
        {
            var session = Register(stored.Record);
            for (var s = 0; s < 4; s++)
                if ((stored.Record.LeftMask & (1 << s)) == 0) lobby.MoveToMatch(stored.Record.Seats[s]);
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
            if ((record.LeftMask & (1 << s)) == 0) byAccount[record.Seats[s]] = session;
        return session;
    }

    private void Release(MatchSession session)
    {
        foreach (var account in session.Seats)
            if (byAccount.TryRemove(new KeyValuePair<Guid, MatchSession>(account, session))) lobby.Exit(account, LobbyPlace.Match);
    }
}

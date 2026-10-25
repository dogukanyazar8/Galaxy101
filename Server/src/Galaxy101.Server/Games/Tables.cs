using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Infrastructure;
using Galaxy101.Server.Realtime;
using Galaxy101.Server.Social;
using Microsoft.EntityFrameworkCore;

namespace Galaxy101.Server.Games;

/// <summary>
/// Klasik lobby tables (spec/20): a player opens a table with its options, others sit by the list, by a GX-XXXX code
/// or with the 4-digit PIN; the host adds bots, removes players and starts. A full table counts down by itself
/// (5 s; the host's Başlat makes it 3 s). The match keeps the table's seats (pairs partners stay), hands, turn time,
/// chat and gift rules. Listed while playing ("Oynanıyor · N. el") until the match closes. In memory: single instance.
/// </summary>
public sealed class Tables(MatchManager matches, Lobby lobby, ConnectionRegistry live, IServiceScopeFactory scopes, TimeProvider clock, ILogger<Tables> log) : IRealtimeHandler
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    public const int AutoCountdown = 5, HostCountdown = 3;

    private sealed class Table
    {
        public Guid Id { get; } = Guid.CreateVersion7();
        public required string Code { get; init; }
        public required TableOptions Options { get; init; }
        public Guid Host { get; set; }
        /// <summary>Account, or a fresh id for a bot (BotMask).</summary>
        public Guid?[] Seats { get; } = new Guid?[4];
        public int BotMask { get; set; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? StartsAt { get; set; }
        public int Generation { get; set; }
        public MatchSession? Match { get; set; }
        /// <summary>The match is being created from a snapshot of the seats (stakes being taken): seats are frozen.</summary>
        public bool Starting { get; set; }
        public bool Closed { get; set; }
        public bool IsBot(int s) => (BotMask & (1 << s)) != 0;
        public IEnumerable<Guid> Humans => Enumerable.Range(0, 4).Where(s => Seats[s] is not null && !IsBot(s)).Select(s => Seats[s]!.Value);
        public bool Full => Seats.All(s => s is not null);
    }

    private readonly object gate = new();
    private readonly Dictionary<Guid, Table> tables = [];
    private readonly Dictionary<Guid, Table> seated = [];
    private readonly HashSet<Guid> subscribers = [];
    private bool hooked;

    public bool Handles(string type) => type is LobbyMessageTypes.Subscribe or LobbyMessageTypes.Unsubscribe or LobbyMessageTypes.Create
        or LobbyMessageTypes.Join or LobbyMessageTypes.Watch or LobbyMessageTypes.Leave or LobbyMessageTypes.AddBot or LobbyMessageTypes.Kick or LobbyMessageTypes.Start;

    /// <summary>Is <paramref name="account"/> seated at the table with <paramref name="code"/> (friend invites).</summary>
    public (bool Member, long Stake) Membership(Guid account, string code)
    {
        lock (gate) return seated.TryGetValue(account, out var t) && t.Code == code ? (true, t.Options.Stake) : (false, 0);
    }

    public static string? Validate(TableOptions? o)
    {
        if (o is null) return ErrorCodes.Validation;
        o.Name = (o.Name ?? "").Trim();
        if (o.Name.Length is 0 or > 28) return ErrorCodes.Validation;
        o.Name = Social.TextFilter.Mask(o.Name); // listed to everyone in the lobby
        if (o.Type is not ("single" or "pairs") || o.Visibility is not ("public" or "private") || o.Chat is not ("on" or "quick" or "off")) return ErrorCodes.Validation;
        if (o.Pin is { Length: > 0 } pin && (pin.Length != 4 || !pin.All(char.IsAsciiDigit))) return ErrorCodes.Validation;
        if (o.Pin is { Length: 0 }) o.Pin = null;
        if (!TableOptions.HandChoices.Contains(o.Hands) || !TableOptions.StakeChoices.Contains(o.Stake) || !TableOptions.TurnChoices.Contains(o.TurnSeconds)) return ErrorCodes.Validation;
        return null;
    }

    public async Task<string?> HandleAsync(Connection connection, Inbound message, CancellationToken ct)
    {
        Hook();
        var account = connection.Account;
        switch (message.Type)
        {
            case LobbyMessageTypes.Subscribe:
                lock (gate) subscribers.Add(account);
                live.Send(account, LobbyMessageTypes.Tables, Snapshot(account));
                return null;
            case LobbyMessageTypes.Unsubscribe:
                lock (gate) subscribers.Remove(account);
                return null;
            case LobbyMessageTypes.Create:
                return await CreateAsync(account, message.Data.Deserialize<TableCreateMessage>(Connection.Json)?.Options);
            case LobbyMessageTypes.Join:
                return await JoinAsync(account, message.Data.Deserialize<TableJoinMessage>(Connection.Json)!);
            case LobbyMessageTypes.Watch:
                return await WatchAsync(account, message.Data.Deserialize<TableJoinMessage>(Connection.Json)!);
            case LobbyMessageTypes.Leave:
                return Remove(account, null);
            case LobbyMessageTypes.AddBot:
                return HostAction(account, message.Data.Deserialize<TableSeatMessage>(Connection.Json)!.Seat, add: true);
            case LobbyMessageTypes.Kick:
                return HostAction(account, message.Data.Deserialize<TableSeatMessage>(Connection.Json)!.Seat, add: false);
            case LobbyMessageTypes.Start:
                return Start(account);
            default:
                return ErrorCodes.NotFound;
        }
    }

    public Task OnDisconnectedAsync(Guid account)
    {
        lock (gate) subscribers.Remove(account);
        Remove(account, null);
        return Task.CompletedTask;
    }

    private void Hook()
    {
        lock (gate)
        {
            if (hooked) return;
            hooked = true;
        }
        matches.SeatsChanged += session =>
        {
            bool listed; lock (gate) listed = tables.Values.Any(t => t.Match == session);
            if (listed) PublishList();
        };
        matches.Closed += session =>
        {
            Table? done = null;
            lock (gate) { done = tables.Values.FirstOrDefault(t => t.Match == session); if (done is not null) { tables.Remove(done.Id); PruneNames(); } }
            if (done is not null) PublishList();
        };
    }

    private async Task<string?> EntryAsync(Guid account, long stake)
    {
        if (lobby.Where(account) is not null) return ErrorCodes.Conflict;
        await using var scope = scopes.CreateAsyncScope();
        var chips = await scope.ServiceProvider.GetRequiredService<GameDb>().Wallets.Where(w => w.AccountId == account).Select(w => w.Chips).FirstOrDefaultAsync();
        return chips >= stake ? null : ErrorCodes.InsufficientFunds;
    }

    private async Task<string?> CreateAsync(Guid account, TableOptions? options)
    {
        if (Validate(options) is { } invalid) return invalid;
        if (await EntryAsync(account, options!.Stake) is { } error) return error;
        if (!lobby.TryEnter(account, LobbyPlace.Room)) return ErrorCodes.Conflict;
        Table table;
        lock (gate)
        {
            string code;
            do code = "GX-" + string.Concat(Enumerable.Range(0, 4).Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]));
            while (tables.Values.Any(t => t.Code == code));
            table = new Table { Code = code, Options = options, Host = account, CreatedAt = clock.GetUtcNow() };
            table.Seats[0] = account;
            tables[table.Id] = table; seated[account] = table;
        }
        await PublishAsync(table);
        return null;
    }

    /// <summary>The table a join or watch names (id from the list, or the code), checked for its PIN; or the error.</summary>
    private (Table? Table, string? Error) Find(TableJoinMessage join)
    {
        var code = (join.Code ?? "").Trim().ToUpperInvariant();
        if (code.Length == 4) code = "GX-" + code;
        var found = join.TableId is { } id ? tables.GetValueOrDefault(id) : tables.Values.FirstOrDefault(t => t.Code == code);
        if (found is null || found.Closed) return (null, TableErrors.Closed);
        // Private tables are reached by code only.
        if (join.TableId is not null && found.Options.Visibility == "private") return (null, TableErrors.Closed);
        if (found.Options.Pin is { } pin && !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(pin), Encoding.UTF8.GetBytes(join.Pin ?? "")))
            return (null, TableErrors.WrongPin);
        return (found, null);
    }

    /// <summary>İzle: a playing table that allows spectators.</summary>
    private async Task<string?> WatchAsync(Guid account, TableJoinMessage join)
    {
        MatchSession? running;
        lock (gate)
        {
            var (found, error) = Find(join);
            if (found is null) return error;
            if (!found.Options.Spectators) return ErrorCodes.Validation;
            running = found.Match;
        }
        if (running is null) return TableErrors.NotReady;
        if (await matches.WatchAsync(account, running) is { } refused) return refused;
        Handover(account, join, running, -1, "watch");
        return null;
    }

    /// <summary>The lobby hands a newcomer or a spectator over to the table (as a waiting room does when its match starts).</summary>
    private void Handover(Guid account, TableJoinMessage join, MatchSession running, int seat, string reason)
    {
        TableSummary? summary;
        lock (gate) summary = tables.Values.FirstOrDefault(t => t.Match == running) is { } t ? Summary(t) : null;
        live.Send(account, LobbyMessageTypes.State, new TableStateMessage { Table = summary, Closed = true, MatchId = running.Id, YourSeat = seat, Reason = reason });
    }

    private async Task<string?> JoinAsync(Guid account, TableJoinMessage join)
    {
        Table? found; long stake; MatchSession? running;
        lock (gate)
        {
            (found, var error) = Find(join);
            if (found is null) return error;
            running = found.Match;
            if (running is null && found.Starting) return TableErrors.Playing;
            if (running is null && found.Full) return TableErrors.Full;
            stake = found.Options.Stake;
        }
        if (await EntryAsync(account, stake) is { } refused) return refused;
        // Oyuna katıl: a playing table takes a newcomer on a seat the server plays.
        if (running is not null)
        {
            if (await matches.TakeSeatAsync(account, running, join.Seat) is { } taken) return taken;
            Handover(account, join, running, running.SeatOf(account), "joined");
            return null;
        }
        if (!lobby.TryEnter(account, LobbyPlace.Room)) return ErrorCodes.Conflict;
        lock (gate)
        {
            // The table may have filled, started or closed while the wallet was checked.
            int seat = join.Seat is >= 0 and <= 3 && found.Seats[join.Seat.Value] is null ? join.Seat.Value : Array.IndexOf(found.Seats, null);
            if (found.Closed || found.Match is not null || found.Starting || seat < 0) { lobby.Exit(account, LobbyPlace.Room); return found.Match is not null || found.Starting ? TableErrors.Playing : TableErrors.Full; }
            found.Seats[seat] = account; seated[account] = found;
            Arm(found, AutoCountdown);
        }
        await PublishAsync(found);
        return null;
    }

    private string? HostAction(Guid account, int seat, bool add)
    {
        Table? table; Guid? removed = null;
        lock (gate)
        {
            if (!seated.TryGetValue(account, out table)) return ErrorCodes.NotFound;
            if (table.Host != account) return TableErrors.NotHost;
            if (table.Starting) return TableErrors.Playing;
            if (seat is < 0 or > 3 || table.Match is not null) return ErrorCodes.Validation;
            if (add)
            {
                if (table.Seats[seat] is not null) return TableErrors.Full;
                table.Seats[seat] = Guid.NewGuid(); table.BotMask |= 1 << seat;
                Arm(table, AutoCountdown);
            }
            else
            {
                if (table.Seats[seat] is not { } who || who == account) return ErrorCodes.Validation;
                if (!table.IsBot(seat)) { removed = who; seated.Remove(who); }
                table.Seats[seat] = null; table.BotMask &= ~(1 << seat);
                table.StartsAt = null; table.Generation++;
                PruneNames();
            }
        }
        if (removed is { } kicked) { lobby.Exit(kicked, LobbyPlace.Room); live.Send(kicked, LobbyMessageTypes.State, new TableStateMessage { Table = Summary(table), Closed = true, Reason = "kicked", YourSeat = -1 }); }
        _ = PublishAsync(table);
        return null;
    }

    private string? Start(Guid account)
    {
        Table? table;
        lock (gate)
        {
            if (!seated.TryGetValue(account, out table)) return ErrorCodes.NotFound;
            if (table.Host != account) return TableErrors.NotHost;
            if (!table.Full) return TableErrors.NotReady;
            if (table.Starting) return null;
            if (table.StartsAt is { } at && at - clock.GetUtcNow() <= TimeSpan.FromSeconds(HostCountdown)) return null;
            table.StartsAt = null;
            Arm(table, HostCountdown);
        }
        _ = PublishAsync(table);
        return null;
    }

    /// <summary>Starts the countdown when the table is full (keeps a running one); call under the gate.</summary>
    private void Arm(Table table, int seconds)
    {
        if (!table.Full || table.StartsAt is not null) return;
        table.StartsAt = clock.GetUtcNow().AddSeconds(seconds);
        var generation = ++table.Generation;
        _ = Task.Delay(TimeSpan.FromSeconds(seconds), clock).ContinueWith(_ => BeginAsync(table, generation), TaskScheduler.Default);
    }

    private async Task BeginAsync(Table table, int generation)
    {
        MatchRecord record;
        lock (gate)
        {
            if (table.Generation != generation || table.Closed || table.Match is not null || table.Starting || !table.Full) return;
            table.Generation++;
            table.Starting = true;
            var o = table.Options;
            record = new MatchRecord
            {
                Id = Guid.CreateVersion7(), Mode = o.Type == "pairs" ? GameModes.Pairs : GameModes.Solo, Stake = o.Stake, Status = MatchStatus.Active,
                Seats = [.. table.Seats.Select(s => s!.Value)], BotMask = table.BotMask, Hands = o.Hands, TurnSeconds = o.TurnSeconds,
                OptionsJson = JsonSerializer.Serialize(new { chat = o.Chat, gifts = o.Gifts, spectators = o.Spectators, processing = o.Processing, penalties = o.Penalties, okeyPenalty = o.OkeyPenalty, doubling = o.Doubling, partnerRaises = o.PartnerRaises }),
                FirstStarter = RandomNumberGenerator.GetInt32(4), CreatedAt = clock.GetUtcNow(),
            };
        }
        // The match is made from the record's seats: those are who pays and plays, whatever the table shows meanwhile.
        var humans = Enumerable.Range(0, 4).Where(s => (record.BotMask & (1 << s)) == 0).Select(s => record.Seats[s]).ToArray();
        IReadOnlyList<Guid> unpaid;
        try { unpaid = await matches.CreateAsync(record); }
        catch (Exception error)
        {
            log.LogError(error, "Table {Code} failed to start", table.Code);
            lock (gate) { table.Starting = false; table.StartsAt = null; }
            await PublishAsync(table);
            return;
        }
        if (unpaid.Count > 0)
        {
            // Someone spent their chips meanwhile: they leave the table, the rest wait for a new player.
            lock (gate) { table.Starting = false; table.StartsAt = null; }
            foreach (var account in unpaid) Remove(account, "insufficient_funds");
            await PublishAsync(table);
            return;
        }
        MatchSession? session = matches.Current(humans[0]);
        lock (gate)
        {
            table.Match = session; table.Starting = false; table.StartsAt = null;
            foreach (var account in humans) seated.Remove(account);
        }
        foreach (var account in humans)
            live.Send(account, LobbyMessageTypes.State, new TableStateMessage { Table = Summary(table), Closed = true, MatchId = record.Id, Reason = "started", YourSeat = Array.IndexOf(record.Seats, account) });
        PublishList();
    }

    /// <summary>
    /// Leaving (or disconnecting from) the waiting room. The host passes to the next human; no humans closes the table.
    /// A table already starting keeps its seats (the match holds them; a disconnected player rejoins it there).
    /// </summary>
    public string? Remove(Guid account, string? reason)
    {
        Table? table;
        lock (gate)
        {
            if (!seated.TryGetValue(account, out table)) return null;
            if (table.Starting) return TableErrors.Playing;
            seated.Remove(account);
            var seat = Array.IndexOf(table.Seats, account);
            if (seat >= 0) table.Seats[seat] = null;
            table.StartsAt = null; table.Generation++;
            if (!table.Humans.Any()) { table.Closed = true; tables.Remove(table.Id); }
            else if (table.Host == account) table.Host = table.Humans.First();
            PruneNames();
        }
        lobby.Exit(account, LobbyPlace.Room);
        live.Send(account, LobbyMessageTypes.State, new TableStateMessage { Table = Summary(table), Closed = true, Reason = reason ?? "left", YourSeat = -1 });
        _ = PublishAsync(table);
        return null;
    }

    private async Task PublishAsync(Table table)
    {
        Guid?[] seats; int countdown;
        lock (gate)
        {
            seats = [.. table.Seats];
            countdown = table.StartsAt is { } at ? Math.Max(1, (int)Math.Ceiling((at - clock.GetUtcNow()).TotalSeconds)) : 0;
        }
        var summary = await SummaryAsync(table);
        foreach (var account in table.Humans.ToArray())
            live.Send(account, LobbyMessageTypes.State, new TableStateMessage { Table = summary, Code = table.Code, YourSeat = Array.IndexOf(seats, account), Countdown = countdown });
        PublishList();
    }

    /// <summary>
    /// The list goes to every subscriber: each table is summarised once and the public list serialised once; only a
    /// player sitting at a private table gets a list of their own (the public ones plus theirs).
    /// </summary>
    private void PublishList()
    {
        Guid[] targets;
        List<(Table Table, TableSummary Summary)> open;
        var privateOf = new Dictionary<Guid, List<Table>>();
        lock (gate)
        {
            targets = [.. subscribers];
            if (targets.Length == 0) return;
            open = [.. tables.Values.Where(t => !t.Closed).OrderByDescending(t => t.CreatedAt).Select(t => (t, Summary(t)))];
            foreach (var (table, _) in open.Where(x => x.Table.Options.Visibility != "public"))
                foreach (var human in table.Humans) (privateOf.TryGetValue(human, out var list) ? list : privateOf[human] = []).Add(table);
        }
        var everyone = Connection.Frame(LobbyMessageTypes.Tables, new LobbyTablesMessage { Tables = [.. open.Where(x => x.Table.Options.Visibility == "public").Select(x => x.Summary)] });
        foreach (var account in targets)
            if (privateOf.TryGetValue(account, out var mine))
                live.Send(account, LobbyMessageTypes.Tables, new LobbyTablesMessage { Tables = [.. open.Where(x => x.Table.Options.Visibility == "public" || mine.Contains(x.Table)).Select(x => x.Summary)] });
            else live.SendFrame(account, everyone);
    }

    /// <summary>Player cards live only while their player sits at a table, so a changed name or avatar shows next time.</summary>
    private void PruneNames()
    {
        foreach (var id in names.Keys.Where(id => !seated.ContainsKey(id) && !tables.Values.Any(t => t.Humans.Contains(id))).ToList()) names.Remove(id);
    }

    private LobbyTablesMessage Snapshot(Guid viewer)
    {
        lock (gate)
            return new LobbyTablesMessage
            {
                Tables = [.. tables.Values.Where(t => !t.Closed && (t.Options.Visibility == "public" || t.Humans.Contains(viewer)))
                    .OrderByDescending(t => t.CreatedAt).Select(Summary)],
            };
    }

    private readonly Dictionary<Guid, PlayerSummary> names = [];

    private async Task<TableSummary> SummaryAsync(Table table)
    {
        Guid[] humans;
        lock (gate) humans = [.. table.Humans];
        var missing = humans.Where(h => { lock (gate) return !names.ContainsKey(h); }).ToArray();
        if (missing.Length > 0)
        {
            await using var scope = scopes.CreateAsyncScope();
            var cards = await Players.SummariesAsync(scope.ServiceProvider.GetRequiredService<GameDb>(), missing, default);
            lock (gate) foreach (var (id, card) in cards) names[id] = card;
        }
        lock (gate) return Summary(table);
    }

    private static readonly string[] BotNames = ["Ayşe", "Zeynep", "Burak", "Deniz"];

    private TableSummary Summary(Table t)
    {
        var o = t.Options;
        return new TableSummary
        {
            Id = t.Id, Name = o.Name, Type = o.Type, Stake = o.Stake, Hands = o.Hands, TurnSeconds = o.TurnSeconds, Doubling = o.Doubling, PartnerRaises = o.PartnerRaises,
            Locked = o.Pin is not null, Private = o.Visibility == "private", Spectators = o.Spectators, Chat = o.Chat, Gifts = o.Gifts,
            Processing = o.Processing, Penalties = o.Penalties, OkeyPenalty = o.OkeyPenalty, HostId = t.Host, CreatedAt = t.CreatedAt,
            Status = t.Match is not null ? "playing" : t.Full ? "full" : "waiting", Hand = t.Match?.HandNumber ?? 0, OpenSeats = t.Match?.OpenSeats ?? 0,
            Seats = [.. Enumerable.Range(0, 4).Select(s => t.Seats[s] is not { } id ? null
                : t.IsBot(s) ? new TableSeat { Bot = true, Player = new PlayerSummary { Id = id, Name = "Bot · " + BotNames[s], Level = 1 } }
                : new TableSeat { Player = names.GetValueOrDefault(id) ?? new PlayerSummary { Id = id, Name = "…" } })],
        };
    }
}

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Galaxy101.Contracts;
using Galaxy101.Server.Realtime;
using Galaxy101.Server.Economy;
using Galaxy101.Server.Games;
using Galaxy101.Table;
using Okey101.Core.V4;

namespace Galaxy101.Server.V4;

public sealed partial class Hub
{
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    /// <summary>Existing server bot cards ("Bot · Ayşe" …): the package's bot names are demo data (DEMO-06).</summary>
    private static readonly string[] BotNames = ["Ayşe", "Zeynep", "Burak", "Deniz"];
    private static readonly string[] BotColors = ["#3D7BFF", "#16B6A8", "#8B5CF6", "#F5A623"];

    private sealed class Occupant
    {
        public Guid? Account { get; init; }
        public V4User? Bot { get; init; }
        /// <summary>The bot plays a seat its player left (D-31: under the player's name, no payout, no points).</summary>
        public Guid? Replaces { get; init; }
        /// <summary>The ledger key of the stake this person paid for the seat (refund key = it + ":back").</summary>
        public string? StakeKey { get; set; }
        public bool IsBot => Bot is not null;
    }

    /// <summary>Per seat accounting of the person playing it (from the hand they sat down in).</summary>
    private sealed class SeatBook
    {
        public long HandXp;
        public int WinHands, PairsOpenings, HandsPlayed, OkeyFinishes, TilesAdded;
        /// <summary>This turn's openings and lay-offs: they count once the turn ends with them, an undo (R-34) or a returned side tile drops them.</summary>
        public int TurnPairsOpenings, TurnTilesAdded;
        public List<bool> HandWins { get; } = [];
        public QuestCounters Counters => new(WinHands, PairsOpenings, HandsPlayed, OkeyFinishes, TilesAdded);
        public void KeepTurn() { PairsOpenings += TurnPairsOpenings; TilesAdded += TurnTilesAdded; DropTurn(); }
        public void DropTurn() { TurnPairsOpenings = 0; TurnTilesAdded = 0; }
    }

    private sealed class Table
    {
        public required Guid Id { get; init; }
        public required string Code { get; init; }
        public required string Name { get; init; }
        public required string Mode { get; init; }
        public bool Private { get; init; }
        public string? Pin { get; init; }
        public int Hands { get; init; }
        public long Bet { get; init; }
        public bool Katlama { get; init; }
        public bool KatlamaPartner { get; init; } = true;
        public int TurnSec { get; init; }
        public required V4TableRules Rules { get; init; }
        public Guid Owner { get; set; }
        public bool Ranked { get; init; }
        public bool VsBots { get; init; }
        public string Status { get; set; } = "waiting";
        public Occupant?[] Seats { get; } = new Occupant?[4];
        public List<Guid> Spectators { get; } = [];
        public int Cd { get; set; }
        public Dictionary<string, object> Timers { get; } = [];
        public long BetsPaid { get; set; }
        /// <summary>Stakes on their way back whose ledger write has not succeeded yet: the record stays open (restart refund) until 0.</summary>
        public int RefundsOwed { get; set; }
        public int Sequence { get; set; }
        public required RngState ChatRng { get; init; }
        public TableRuntime? Run { get; set; }
        public required TableHost Host { get; set; }
        public SeatBook[] Books { get; } = [new(), new(), new(), new()];
        public HashSet<(Guid Blocker, Guid Blocked)> Blocks { get; set; } = [];
        public bool Closed { get; set; }
        public bool Listed => !Ranked && !VsBots && !Private;
        public bool Human(int k) => Seats[k] is { IsBot: false };
        public IEnumerable<int> HumanSeats => Enumerable.Range(0, 4).Where(Human);
        public bool BotSeat(int k) => Run?.IsBotSeat(k) ?? Seats[k] is { IsBot: true };
        public string RecordMode => VsBots ? "bots" : Ranked ? GameModes.Ranked : Private ? GameModes.Private : Mode == "pairs" ? GameModes.Pairs : GameModes.Solo;
    }

    /// <summary>The hub's side of a table's runtime: its clock, its people, and the hooks for points and settlement.</summary>
    private sealed class TableHost(Hub hub, Table table) : ITableHost
    {
        public long Now => hub.now;
        public object Schedule(long atMs, Action action) => hub.Schedule(atMs, () => { if (!table.Closed) action(); });
        public void Cancel(object handle) => Hub.Cancel(handle);
        public void Send(int seat, string type, object payload)
        {
            if (seat < 0) { foreach (var s in table.Spectators) hub.Send(s, type, payload); return; }
            if (table.Seats[seat] is { IsBot: false, Account: { } account }) hub.Send(account, type, payload);
        }
        public void Applied(TableRuntime run, IReadOnlyList<GameEvent> events) => hub.Applied(table, events);
        public void MatchEnded(TableRuntime run) => hub.Defer(() => hub.MatchOverAsync(table));
    }

    private readonly Dictionary<Guid, Table> tables = [];

    private Table NewTable(Guid owner, V4TableCreateRequest m, bool ranked = false, bool vsBots = false, int? hands = null, int? turnSec = null)
    {
        string code;
        do code = "GX-" + string.Concat(Enumerable.Range(0, 4).Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]));
        while (tables.Values.Any(t => t.Code == code));
        var rules = m.Rules ?? new V4TableRules();
        Table table = null!;
        table = new Table
        {
            Id = Guid.CreateVersion7(), Code = code, Name = m.Name!, Mode = m.Mode == "pairs" ? "pairs" : "solo", Private = m.Private, Pin = m.Pin,
            Hands = hands ?? m.Hands, Bet = m.Bet, Katlama = m.Katlama, KatlamaPartner = m.KatlamaPartner != false, TurnSec = turnSec ?? m.TurnSec,
            Rules = new V4TableRules { Spectate = rules.Spectate, Chat = rules.Chat ?? "open", Gifts = rules.Gifts, Isle = rules.Isle, Penalties = rules.Penalties },
            Owner = owner, Ranked = ranked, VsBots = vsBots, ChatRng = new RngState(SeedSource()), Host = null!,
        };
        table.Host = new TableHost(this, table);
        tables[table.Id] = table;
        return table;
    }

    /// <summary>The table's durable record; without it the table is not opened (it would hold stakes no restart could find).</summary>
    private async Task RecordAsync(Table t)
    {
        try
        {
            await store.CreateRecordAsync(t.Id, t.RecordMode, t.Bet, t.Hands, t.TurnSec, new
            {
                engine = Store.Engine, ruleVersion = G101Rules.RuleVersion, shuffleVersion = G101Rules.ShuffleVersion, mode = t.Mode, @private = t.Private,
                katlama = t.Katlama, katlamaPartner = t.KatlamaPartner, rules = t.Rules, ranked = t.Ranked, vsBots = t.VsBots,
            });
        }
        catch (Exception)
        {
            t.Closed = true;
            tables.Remove(t.Id);
            throw;
        }
    }

    private V4User BotCard(int seat, int rankIdx = 0) => new()
    {
        Id = "b" + (++seq), Name = "Bot · " + BotNames[seat], Ini = "B" + BotNames[seat][0], Color = BotColors[seat], Level = 1, RankIdx = rankIdx, Online = true,
    };

    private V4Table TablePub(Table t) => new()
    {
        Id = t.Id.ToString(), Code = t.Private ? t.Code : null, Name = t.Name, Mode = t.Mode, Private = t.Private, Locked = t.Pin is not null, Hands = t.Hands,
        Bet = t.Bet, Katlama = t.Katlama, KatlamaPartner = t.Mode == "pairs" && t.Katlama ? t.KatlamaPartner : null, TurnSec = t.TurnSec,
        Rules = new V4TableRules { Spectate = t.Rules.Spectate, Chat = t.Rules.Chat, Gifts = t.Rules.Gifts, Isle = t.Rules.Isle, Penalties = t.Rules.Penalties },
        Owner = t.Owner.ToString(), Ranked = t.Ranked, VsBots = t.VsBots, Status = t.Status, HandNo = t.Run?.Match?.HandNo ?? 0,
        Seats = [.. Enumerable.Range(0, 4).Select(k => t.Seats[k] is not { } o ? null : new V4Seat
        {
            User = o.Bot ?? Pub(P(o.Account!.Value)), Bot = o.IsBot, Connected = t.Run?.Connected[k] ?? true,
        })],
        Spectators = t.Spectators.Count, Countdown = t.Cd,
    };

    /// <summary>To every person seated (not bots) and every spectator.</summary>
    private void TableEmit(Table t, string type, object payload)
    {
        foreach (var k in t.HumanSeats) Send(t.Seats[k]!.Account!.Value, type, payload);
        foreach (var s in t.Spectators) Send(s, type, payload);
    }

    private void TableState(Table t) => TableEmit(t, V4Messages.TableState, new V4TableEvent { Table = TablePub(t), At = now });

    private void LobbyEmit(Table t, bool removed = false)
    {
        if (!t.Listed) return;
        object payload = removed ? new V4IdEvent { Id = t.Id.ToString(), At = now } : new V4TableEvent { Table = TablePub(t), At = now };
        foreach (var p in subscribers) Send(p.Id, removed ? V4Messages.LobbyTableRemoved : V4Messages.LobbyTable, payload);
    }

    private Reply LobbySubscribe(Person p)
    {
        subscribers.Add(p);
        Send(p.Id, V4Messages.Lobby, new V4LobbyEvent { Tables = [.. tables.Values.Where(t => t.Listed && !t.Closed).Select(TablePub)], At = now });
        return Reply.Ok;
    }

    private Reply LobbyUnsubscribe(Person p) { subscribers.Remove(p); return Reply.Ok; }

    /// <summary>Somewhere already: a table, the ranked queue, or a quick match / room of the earlier protocol.</summary>
    private bool Busy(Person p) => p.Table is not null || p.Queue is not null || lobby.Where(p.Id) is not null;

    // ---- Opening and joining ---------------------------------------------------------------------------------------

    private static readonly Regex PinPattern = new("^[0-9]{4}$");
    private static readonly Regex CodePattern = new("^GX-[A-Z0-9]{4}$");

    /// <summary>S10 Masa aç (protocol.json tableCreate): the stake is taken when the owner sits (R-90).</summary>
    private async Task<Reply> TableCreateAsync(Person p, V4TableCreateRequest m)
    {
        if (Busy(p)) return Reply.Fail(V4Errors.E_ALREADY_SEATED);
        var name = (m.Name ?? "").Trim();
        if (name.Length is 0 or > 28) return Reply.Fail(V4Errors.E_TABLE_NAME);
        if (m.Pin is not null && !PinPattern.IsMatch(m.Pin)) return Reply.Fail(V4Errors.E_PIN_FORMAT);
        var chat = m.Rules?.Chat ?? "open";
        if (!TableEconomy.Bets.Contains(m.Bet) || !TableEconomy.Hands.Contains(m.Hands) || !TableEconomy.TurnSec.Contains(m.TurnSec) ||
            m.Mode is not ("solo" or "pairs") || chat is not ("open" or "quick" or "off"))
            return Reply.Fail(V4Errors.E_TABLE_OPTIONS);
        // The wallet, not the cached card: chips may have come in over HTTP (daily reward, shop) since the card was read.
        var chips = await store.ChipsAsync(p.Id);
        if (chips < m.Bet) return Reply.Fail(V4Errors.E_NO_CHIPS, new { missing = m.Bet - chips });
        m.Name = Social.TextFilter.Mask(name); // listed to everyone in the lobby (existing rule)
        var t = NewTable(p.Id, m);
        await RecordAsync(t);
        Reply? error;
        try { error = await SeatAsync(t, p, 0); }
        catch (Exception) { await CloseTableAsync(t, null); throw; }
        if (error is not null) { await CloseTableAsync(t, null); return error; }
        return Reply.With(("table", TablePub(t)));
    }

    /// <summary>Sits a person: stake (R-90), seat, tableState, lobby. A reply on failure (the stake could not be taken).</summary>
    private async Task<Reply?> SeatAsync(Table t, Person p, int seat)
    {
        if (!lobby.TryEnter(p.Id, LobbyPlace.Room)) return Reply.Fail(V4Errors.E_ALREADY_SEATED);
        var occupant = new Occupant { Account = p.Id };
        if (t.Bet > 0 && !t.Ranked && !t.VsBots)
        {
            var key = $"v4:{t.Id}:bet:{p.Id:N}:{++t.Sequence}";
            var paid = await StakeAsync(t, p, key);
            if (paid.Outcome != LedgerOutcome.Applied)
            {
                lobby.Exit(p.Id, LobbyPlace.Room);
                return Reply.Fail(V4Errors.E_NO_CHIPS, new { missing = Math.Max(0, t.Bet - paid.Chips) });
            }
            occupant.StakeKey = key;
            t.BetsPaid += t.Bet;
            Balance(p, paid.Chips, paid.Gems, -t.Bet, "bet", t.Id.ToString());
        }
        t.Seats[seat] = occupant;
        p.Table = t; p.Seat = seat;
        TableState(t);
        LobbyEmit(t);
        MaybeAutoStart(t);
        return null;
    }

    /// <summary>
    /// Takes the stake (R-90). A failed write releases the room the person reserved and may still have landed, so it is
    /// given back if the ledger holds it (<see cref="ReturnStakeAsync"/>) before the error goes on.
    /// </summary>
    private async Task<LedgerResult> StakeAsync(Table t, Person p, string key)
    {
        try { return await store.ApplyAsync(p.Id, -t.Bet, "stake", key); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            lobby.Exit(p.Id, LobbyPlace.Room);
            t.RefundsOwed++;
            Defer(() => ReturnStakeAsync(t, p.Id, key, 0));
            throw;
        }
    }

    /// <summary>The stake back to a person leaving before the match (R-90, D-11) or kicked.</summary>
    private async Task RefundAsync(Table t, Person p, Occupant o)
    {
        if (o.StakeKey is not { } key) return;
        // The seat no longer holds it; the return below is retried until the ledger takes it.
        o.StakeKey = null;
        t.BetsPaid -= t.Bet;
        t.RefundsOwed++;
        await ReturnStakeAsync(t, p.Id, key, 0);
    }

    private const int RefundRetryMs = 5000;

    /// <summary>
    /// One owed stake (counted in <see cref="Table.RefundsOwed"/>) goes back under its idempotent key; a storage failure
    /// retries later. When the last one is back and the table has closed before its match, its record closes too.
    /// </summary>
    private async Task ReturnStakeAsync(Table t, Guid account, string key, int attempt)
    {
        LedgerResult? back;
        try { back = await store.RefundStakeAsync(account, t.Bet, key); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            log.LogWarning(error, "v4 table {Table}: stake of {Account} not returned yet (attempt {Attempt})", t.Id, account, attempt + 1);
            Schedule(now + RefundRetryMs * Math.Min(attempt + 1, 12), () => ReturnStakeAsync(t, account, key, attempt + 1));
            return;
        }
        if (back is { Outcome: LedgerOutcome.Applied } b) Balance(P(account), b.Chips, b.Gems, t.Bet, "betRefund", t.Id.ToString());
        if (--t.RefundsOwed == 0 && t.Closed && t.Status == "over" && t.Run is null) await CloseRecordAsync(t);
    }

    private async Task CloseRecordAsync(Table t)
    {
        try { await store.CloseRecordAsync(t.Id); }
        catch (Exception error) when (error is not OperationCanceledException) { log.LogWarning(error, "v4 table {Table}: record not closed", t.Id); }
    }

    private Reply? JoinCheck(Table? t, Person p, string? pin, out int seat)
    {
        seat = -1;
        if (t is null || t.Closed) return Reply.Fail(V4Errors.E_TABLE_CLOSED);
        if (Busy(p)) return Reply.Fail(V4Errors.E_ALREADY_SEATED);
        if (t.Pin is not null && pin != t.Pin) return Reply.Fail(V4Errors.E_PIN_WRONG);
        seat = Array.IndexOf(t.Seats, null);
        if (seat < 0 || t.Status != "waiting") return Reply.Fail(V4Errors.E_TABLE_FULL);
        // Chips are checked by the stake itself (SeatAsync): the cached card can be behind the wallet.
        return null;
    }

    private Table? Find(string? id) => Guid.TryParse(id, out var g) && tables.TryGetValue(g, out var t) && !t.Closed ? t : null;

    private async Task<Reply> TableJoinAsync(Person p, V4TableJoinRequest m)
    {
        var t = Find(m.TableId);
        if (t is { Status: "playing" }) return await TakeOverAsync(t, p, m);
        if (JoinCheck(t, p, m.Pin, out var free) is { } refused) return refused;
        var seat = m.Seat is >= 0 and <= 3 && t!.Seats[m.Seat.Value] is null ? m.Seat.Value : free;
        if (await SeatAsync(t!, p, seat) is { } error) return error;
        return Reply.With(("table", TablePub(t!)), ("seat", seat));
    }

    private async Task<Reply> TableJoinCodeAsync(Person p, V4TableJoinCodeRequest m)
    {
        var code = string.Concat((m.Code ?? "").ToUpperInvariant().Where(c => !char.IsWhiteSpace(c)));
        if (!CodePattern.IsMatch(code)) return Reply.Fail(V4Errors.E_CODE_FORMAT);
        var t = tables.Values.FirstOrDefault(x => x.Code == code && !x.Closed && x.Status != "over");
        if (t is null) return Reply.Fail(V4Errors.E_CODE_UNKNOWN);
        if (JoinCheck(t, p, m.Pin, out var seat) is { } refused) return refused;
        if (await SeatAsync(t, p, seat) is { } error) return error;
        return Reply.With(("table", TablePub(t)), ("seat", seat));
    }

    /// <summary>
    /// R-65 "Oyuna katıl": a bot's or a leaver's seat of a running match goes to the newcomer, who pays the stake now (the pot
    /// grows, D-34) and plays on from the seat's total. Not at ranked or against-bots tables.
    /// </summary>
    private async Task<Reply> TakeOverAsync(Table t, Person p, V4TableJoinRequest m)
    {
        if (Busy(p)) return Reply.Fail(V4Errors.E_ALREADY_SEATED);
        if (t.Ranked || t.VsBots) return Reply.Fail(V4Errors.E_TABLE_PLAYING);
        if (t.Pin is not null && m.Pin != t.Pin) return Reply.Fail(V4Errors.E_PIN_WRONG);
        var k = m.Seat is >= 0 and <= 3 ? m.Seat.Value : Enumerable.Range(0, 4).Where(i => t.Seats[i] is { IsBot: true }).DefaultIfEmpty(-1).First();
        if (k < 0 || t.Seats[k] is not { IsBot: true } || t.Run is null || t.Run.Over) return Reply.Fail(V4Errors.E_TABLE_FULL);
        if (!lobby.TryEnter(p.Id, LobbyPlace.Room)) return Reply.Fail(V4Errors.E_ALREADY_SEATED);
        var occupant = new Occupant { Account = p.Id };
        if (t.Bet > 0)
        {
            var key = $"v4:{t.Id}:bet:{p.Id:N}:{++t.Sequence}";
            var paid = await StakeAsync(t, p, key);
            if (paid.Outcome != LedgerOutcome.Applied) { lobby.Exit(p.Id, LobbyPlace.Room); return Reply.Fail(V4Errors.E_NO_CHIPS, new { missing = Math.Max(0, t.Bet - paid.Chips) }); }
            occupant.StakeKey = key;
            t.BetsPaid += t.Bet;
            Balance(p, paid.Chips, paid.Gems, -t.Bet, "bet", t.Id.ToString());
        }
        var before = t.Seats[k];
        t.Seats[k] = occupant;
        if (t.Run.TakeSeat(k) is { } engine)
        {
            t.Seats[k] = before;
            await RefundAsync(t, p, occupant);
            lobby.Exit(p.Id, LobbyPlace.Room);
            return Reply.Fail(engine);
        }
        p.Table = t; p.Seat = k;
        t.Books[k] = new SeatBook();
        live.SetPlaying(p.Id, t.Id);
        t.Blocks = await store.BlocksAsync([.. t.HumanSeats.Select(i => t.Seats[i]!.Account!.Value)]);
        TableState(t);
        LobbyEmit(t);
        Send(p.Id, V4Messages.Snapshot, new V4Snapshot { Me = Me(p), Screen = "table", Table = TablePub(t), Game = t.Run.GameView(k), At = now });
        return Reply.With(("table", TablePub(t)), ("seat", k));
    }

    private Reply TableSpectate(Person p, V4TableSpectateRequest m)
    {
        var t = Find(m.TableId);
        if (t is null) return Reply.Fail(V4Errors.E_TABLE_CLOSED);
        if (!t.Rules.Spectate) return Reply.Fail(V4Errors.E_SPECTATE_OFF);
        if (Busy(p)) return Reply.Fail(V4Errors.E_ALREADY_SEATED);
        t.Spectators.Add(p.Id);
        p.Table = t; p.Seat = -1;
        Send(p.Id, V4Messages.Snapshot, new V4Snapshot
        {
            Me = Me(p), Screen = t.Status == "playing" ? "table" : "waitroom", Table = TablePub(t), Game = t.Run?.GameView(-1), Spectator = true, At = now,
        });
        return Reply.Ok;
    }

    // ---- Leaving -----------------------------------------------------------------------------------------------------

    private async Task<Reply> TableLeaveAsync(Person p)
    {
        if (p.Table is not { } t) return Reply.Fail(V4Errors.E_NOT_SEATED);
        if (p.Seat < 0) { t.Spectators.Remove(p.Id); p.Table = null; return Reply.Ok; }
        if (t.Status is "waiting" or "countdown") { await LeaveWaitRoomAsync(t, p, "leave"); return Reply.Ok; }
        var seat = p.Seat;
        // R-60 in the match: the stake stays in the pot, last place (solo), a bot plays the seat under the player's name (D-31).
        if (t.Run is null) return Reply.Fail(V4Errors.E_LEAVE_INVALID);
        if (t.Run.Leave(seat) is { } error) return Reply.Fail(error);
        var bot = BotCard(seat);
        bot.Name = p.Card?.Name ?? bot.Name;
        bot.Ini = p.Card?.Ini ?? bot.Ini;
        bot.Color = p.Card?.Color ?? bot.Color;
        t.Seats[seat] = new Occupant { Bot = bot, Replaces = p.Id };
        p.Table = null; p.Seat = -1;
        lobby.Exit(p.Id, LobbyPlace.Room);
        live.SetPlaying(p.Id, null);
        int? lp = null;
        if (t.Ranked)
        {
            // Ranked: leaving the match is fourth place (mockServer tableLeave → rankApply(u, 3)).
            var (before, shield) = await store.RankAsync(p.Id);
            lp = Profile.Ranked.Table(before)[3];
            RankChange(p, before, shield, lp.Value, 4);
        }
        var book = t.Books[seat];
        t.Books[seat] = new SeatBook();
        Defer(() => LeaverAsync(t.Id, t.RecordMode, seat, p.Id, t.Run.Match.Totals[seat], t.Bet, book.Counters, [.. book.HandWins], lp, 0));
        TableState(t);
        Send(p.Id, V4Messages.LeftTable, new V4LeftTable { Reason = "leave", ToLobbyIn = TableTimings.LeaveToLobby, At = now });
        LobbyEmit(t);
        return Reply.Ok;
    }

    private async Task LeaveWaitRoomAsync(Table t, Person p, string reason)
    {
        var seat = p.Seat;
        if (t.Seats[seat] is { } o) await RefundAsync(t, p, o);
        ClearCountdown(t);
        t.Seats[seat] = null;
        p.Table = null; p.Seat = -1;
        lobby.Exit(p.Id, LobbyPlace.Room);
        Send(p.Id, V4Messages.LeftTable, new V4LeftTable { Reason = reason, ToLobbyIn = 0, At = now });
        // K-12: transfer ownership to the first remaining human in seat order; bots and spectators cannot own it.
        if (t.Owner == p.Id)
        {
            var successor = t.Seats.FirstOrDefault(o => o is { IsBot: false });
            if (successor?.Account is not { } owner) { await CloseTableAsync(t, "ownerLeft"); return; }
            t.Owner = owner;
        }
        TableState(t);
        LobbyEmit(t);
    }

    /// <summary>Closes a table before its match: everyone seated gets their stake back and tableClosed.</summary>
    private async Task CloseTableAsync(Table t, string? reason)
    {
        foreach (var timer in t.Timers.Values) Cancel(timer);
        t.Timers.Clear();
        t.Run?.Stop();
        foreach (var k in t.HumanSeats.ToList())
        {
            var p = P(t.Seats[k]!.Account!.Value);
            await RefundAsync(t, p, t.Seats[k]!);
            p.Table = null; p.Seat = -1;
            lobby.Exit(p.Id, LobbyPlace.Room);
        }
        if (reason is not null) TableEmit(t, V4Messages.TableClosed, new V4TableClosed { Id = t.Id.ToString(), Reason = reason, At = now });
        foreach (var s in t.Spectators) if (people.TryGetValue(s, out var w)) w.Table = null;
        t.Spectators.Clear();
        t.Status = "over";
        t.Closed = true;
        LobbyEmit(t, removed: true);
        tables.Remove(t.Id);
        // A stake still on its way back keeps the record open: a restart then returns it (Store.AbortOpenAsync).
        if (t.RefundsOwed == 0) await CloseRecordAsync(t);
    }

    // ---- Waiting room ------------------------------------------------------------------------------------------------

    private Reply? OwnerOnly(Person p, out Table t)
    {
        t = p.Table!;
        if (p.Table is null || p.Seat < 0) return Reply.Fail(V4Errors.E_NOT_SEATED);
        if (t.Owner != p.Id) return Reply.Fail(V4Errors.E_NOT_OWNER);
        if (t.Status != "waiting") return Reply.Fail(V4Errors.E_STATUS);
        return null;
    }

    private Reply SeatAddBot(Person p, V4SeatRequest m)
    {
        if (OwnerOnly(p, out var t) is { } refused) return refused;
        if (m.Seat is < 0 or > 3) return Reply.Fail(V4Errors.E_BAD_MESSAGE);
        if (t.Seats[m.Seat] is not null) return Reply.Fail(V4Errors.E_SEAT_TAKEN);
        t.Seats[m.Seat] = new Occupant { Bot = BotCard(m.Seat) };
        TableState(t);
        LobbyEmit(t);
        MaybeAutoStart(t);
        return Reply.Ok;
    }

    private async Task<Reply> SeatKickAsync(Person p, V4SeatRequest m)
    {
        if (OwnerOnly(p, out var t) is { } refused) return refused;
        if (m.Seat is < 0 or > 3 || t.Seats[m.Seat] is not { } o || o.Account == p.Id) return Reply.Fail(V4Errors.E_SEAT_EMPTY);
        if (!o.IsBot)
        {
            var v = P(o.Account!.Value);
            await RefundAsync(t, v, o);
            v.Table = null; v.Seat = -1;
            lobby.Exit(v.Id, LobbyPlace.Room);
            Send(v.Id, V4Messages.Kicked, new V4Kicked { TableId = t.Id.ToString(), At = now });
        }
        t.Seats[m.Seat] = null;
        TableState(t);
        LobbyEmit(t);
        return Reply.Ok;
    }

    private Reply TableStart(Person p)
    {
        if (OwnerOnly(p, out var t) is { } refused) return refused;
        if (t.Seats.Contains(null)) return Reply.Fail(V4Errors.E_TABLE_NOT_FULL);
        StartCountdown(t);
        return Reply.Ok;
    }

    /// <summary>seatedToCountdown: a full table whose owner is not a seated person counts down by itself (mockServer maybeAutoStart).</summary>
    private void MaybeAutoStart(Table t)
    {
        if (t.Status != "waiting" || t.Seats.Contains(null)) return;
        if (t.Seats.Any(o => o is { IsBot: false } && o.Account == t.Owner)) return;
        t.Timers["auto"] = Schedule(now + TableTimings.SeatedToCountdown, () => StartCountdown(t));
    }

    /// <summary>waitRoomCountdown: 3-2-1, one per second, then matchStart.</summary>
    private void StartCountdown(Table t)
    {
        if (t.Status != "waiting" || t.Closed) return;
        t.Status = "countdown";
        t.Cd = TableTimings.WaitRoomCountdown / TableTimings.CountdownTick;
        TableEmit(t, V4Messages.Countdown, new V4Countdown { N = t.Cd, StartsIn = TableTimings.WaitRoomCountdown, At = now });
        LobbyEmit(t);
        void Tick()
        {
            t.Cd -= 1;
            if (t.Cd <= 0) { t.Cd = 0; t.Timers["cd"] = Schedule(now, () => StartMatchAsync(t)); return; }
            TableEmit(t, V4Messages.Countdown, new V4Countdown { N = t.Cd, StartsIn = t.Cd * TableTimings.CountdownTick, At = now });
            t.Timers["cd"] = Schedule(now + TableTimings.CountdownTick, Tick);
        }
        t.Timers["cd"] = Schedule(now + TableTimings.CountdownTick, Tick);
    }

    private void ClearCountdown(Table t)
    {
        if (t.Status == "countdown")
        {
            if (t.Timers.Remove("cd", out var cd)) Cancel(cd);
            t.Status = "waiting";
            t.Cd = 0;
            TableEmit(t, V4Messages.Countdown, new V4Countdown { N = 0, Cancelled = true, At = now });
        }
        if (t.Timers.Remove("auto", out var auto)) Cancel(auto);
    }

    // ---- Against bots ------------------------------------------------------------------------------------------------

    /// <summary>Botlara karşı: three bots, no chips, the match starts at once (no waiting room).</summary>
    private async Task<Reply> VsBotsStartAsync(Person p, V4VsBotsStartRequest m)
    {
        if (Busy(p)) return Reply.Fail(V4Errors.E_ALREADY_SEATED);
        var hands = m.Hands ?? 5;
        if (m.Mode is not ("solo" or "pairs") || hands is < 1 or > 7) return Reply.Fail(V4Errors.E_TABLE_OPTIONS);
        var t = NewTable(p.Id, new V4TableCreateRequest
        {
            Name = "Botlara karşı", Mode = m.Mode, Bet = 0, Rules = new V4TableRules { Spectate = false, Chat = "open", Gifts = false },
        }, vsBots: true, hands: hands, turnSec: TableTimings.DefaultTurnMs / 1000);
        await RecordAsync(t);
        if (!lobby.TryEnter(p.Id, LobbyPlace.Room)) { await CloseTableAsync(t, null); return Reply.Fail(V4Errors.E_ALREADY_SEATED); }
        t.Seats[0] = new Occupant { Account = p.Id };
        p.Table = t; p.Seat = 0;
        for (var k = 1; k < 4; k++) t.Seats[k] = new Occupant { Bot = BotCard(k) };
        Send(p.Id, V4Messages.TableState, new V4TableEvent { Table = TablePub(t), At = now });
        await StartMatchAsync(t);
        return Reply.With(("table", TablePub(t)));
    }

    // ---- Chat and gifts (R-63, D-22, D-26, D-32) ---------------------------------------------------------------------

    private Reply ChatSend(Person p, V4ChatSendRequest m)
    {
        if (p.Table is not { } t) return Reply.Fail(V4Errors.E_NOT_SEATED);
        if (t.Rules.Chat == "off") return Reply.Fail(V4Errors.E_CHAT_OFF);
        if (m.Text is not null && t.Rules.Chat == "quick") return Reply.Fail(V4Errors.E_CHAT_QUICK_ONLY);
        if (m.Text is not null && (m.Text.Trim().Length == 0 || m.Text.Length > TableEconomy.ChatMax)) return Reply.Fail(V4Errors.E_CHAT_LENGTH);
        if (m.Quick is not null && !TableEconomy.IsQuickKey(m.Quick)) return Reply.Fail(V4Errors.E_CHAT_QUICK_UNKNOWN);
        if (m.Text is null && m.Quick is null) return Reply.Fail(V4Errors.E_CHAT_LENGTH);
        var line = new V4Chat
        {
            Seat = p.Seat, User = p.Id.ToString(), Text = m.Text is null ? null : Social.TextFilter.Mask(m.Text), Quick = m.Quick,
            Spectator = p.Seat < 0, At = now,
        };
        // Existing rule: a blocked player's words do not reach the blocker.
        foreach (var k in t.HumanSeats)
            if (t.Seats[k]!.Account is { } to && !t.Blocks.Contains((to, p.Id))) Send(to, V4Messages.Chat, line);
        foreach (var s in t.Spectators) Send(s, V4Messages.Chat, line);
        if (t.Rules.Chat != "off") BotChat.Reply(t.Host, t.ChatRng, p.Seat, t.BotSeat, () => !t.Closed);
        return Reply.Ok;
    }

    private async Task<Reply> GiftSendAsync(Person p, Galaxy101.Server.Realtime.Inbound m)
    {
        if (p.Table is not { } t) return Reply.Fail(V4Errors.E_NOT_SEATED);
        if (!t.Rules.Gifts) return Reply.Fail(V4Errors.E_GIFTS_OFF);
        var gift = m.Data.ValueKind == JsonValueKind.Object && m.Data.TryGetProperty("gift", out var g) && g.ValueKind == JsonValueKind.String ? g.GetString() : null;
        var item = catalog.Gifts.FirstOrDefault(x => x.Id == gift);
        if (item is null) return Reply.Fail(V4Errors.E_GIFT_UNKNOWN);
        List<int> to;
        var target = m.Data.TryGetProperty("to", out var tv) ? tv : default;
        if (target.ValueKind == JsonValueKind.String && target.GetString() == "all") to = [.. Enumerable.Range(0, 4).Where(k => k != p.Seat && t.Seats[k] is not null)];
        else if (target.ValueKind == JsonValueKind.Number && target.TryGetInt32(out var one)) to = [one];
        else return Reply.Fail(V4Errors.E_GIFT_TARGET);
        if (to.Count == 0 || to.Any(k => k is < 0 or > 3 || k == p.Seat || t.Seats[k] is null)) return Reply.Fail(V4Errors.E_GIFT_TARGET);
        // D-26: "everyone" costs the price once per recipient.
        var cost = item.Chips * to.Count;
        var paid = await store.ApplyAsync(p.Id, -cost, "gift", $"v4:{t.Id}:gift:{p.Id:N}:{++t.Sequence}");
        if (paid.Outcome != LedgerOutcome.Applied) return Reply.Fail(V4Errors.E_NO_CHIPS, new { missing = Math.Max(0, cost - paid.Chips) });
        Balance(p, paid.Chips, paid.Gems, -cost, "gift", item.Id);
        TableEmit(t, V4Messages.Gift, new V4Gift { From = p.Seat, To = to, Gift = item.Id, Cost = cost, At = now });
        foreach (var k in to) if (t.BotSeat(k)) BotChat.Thanks(t.Host, k, item.Id, () => !t.Closed);
        return Reply.Ok;
    }
}

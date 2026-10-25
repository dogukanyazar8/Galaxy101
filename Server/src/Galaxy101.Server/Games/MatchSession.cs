using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Economy;
using Galaxy101.Server.Infrastructure;
using Galaxy101.Server.Realtime;
using Okey101.Bots;
using Okey101.Core.Model;
using Okey101.Core.Turns;

namespace Galaxy101.Server.Games;

public enum SeatStatus { Connected, Disconnected, Auto, Left }

/// <summary>
/// One online table: the authoritative <see cref="RoundState"/> plus everything around it (timers, seats, scores).
/// All state is touched only by the session's own loop, which runs queued work items one at a time, so there are no
/// locks and every move is applied in a single total order. A move becomes visible only after it is durably stored;
/// replaying the stored moves over the seeded deal rebuilds the table after a restart.
/// </summary>
public sealed class MatchSession
{
    private static readonly TimeSpan AutoPlayDelay = TimeSpan.FromSeconds(1);
    private DateTimeOffset? dealUntil;
    private static readonly TimeSpan BotThinkLimit = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan EmoteInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ChatInterval = TimeSpan.FromSeconds(1);
    /// <summary>Ready chat phrases the client offers (quick.0 … quick.5).</summary>
    private const int QuickPhrases = 6;

    private readonly MatchRecord record;
    private readonly MatchStore store;
    private readonly ConnectionRegistry live;
    private readonly Catalog catalog;
    private readonly GameOptions options;
    private readonly TimeProvider clock;
    private readonly ILogger log;
    private readonly Action<MatchSession> onClosed;
    private readonly Channel<Func<Task>> inbox = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions { SingleReader = true });

    private Dictionary<Guid, PlayerSummary> players = [];
    /// <summary>(blocker, blocked) among the seats: a blocked seat's chat and emotes do not reach its blocker.</summary>
    private HashSet<(Guid Blocker, Guid Blocked)> blocks = [];
    /// <summary>İzle: accounts watching (public view only, never anyone's tiles).</summary>
    private readonly HashSet<Guid> spectators = [];
    public const int MaxSpectators = 20;
    /// <summary>The hand from which a seat's current player counts (a player who took the seat mid-match).</summary>
    private readonly int[] joinedHand = new int[4];
    private RoundState state = null!;
    private int handIndex;
    private readonly List<GameCommand> handCommands = [];
    private readonly int[] totals = new int[4];
    private readonly long[] xp = new long[4];
    private readonly List<QuestCounters[]> counters = [];
    private readonly int[] bestOpening = new int[4];
    private readonly SeatStatus[] status = new SeatStatus[4];
    private readonly int[] timeouts = new int[4];
    private readonly DateTimeOffset[] lastEmote = new DateTimeOffset[4];
    private readonly DateTimeOffset[] lastChat = new DateTimeOffset[4];
    private (int Hand, int Turn, int Seat) turnKey = (-1, -1, -1);
    private DateTimeOffset? deadline;
    private DateTimeOffset? nextHandAt;
    private MatchEventDto? lastEvent;
    private long timerGeneration;
    private bool closed;

    public MatchSession(MatchRecord record, MatchStore store, ConnectionRegistry live, Catalog catalog, GameOptions options,
        TimeProvider clock, ILogger log, Action<MatchSession> onClosed)
    {
        (this.record, this.store, this.live, this.catalog, this.options, this.clock, this.log, this.onClosed) = (record, store, live, catalog, options, clock, log, onClosed);
        for (var s = 0; s < 4; s++) if (Left(s)) status[s] = SeatStatus.Left; else if (Bot(s)) status[s] = SeatStatus.Auto;
        try { rules = JsonDocument.Parse(string.IsNullOrEmpty(record.OptionsJson) ? "{}" : record.OptionsJson).RootElement.Clone(); }
        catch (JsonException) { rules = JsonDocument.Parse("{}").RootElement.Clone(); }
        _ = Task.Run(LoopAsync);
    }

    public Guid Id => record.Id;
    /// <summary>1-based hand in play (lobby list: "Oynanıyor · N. el").</summary>
    public int HandNumber => handIndex + 1;
    private readonly JsonElement rules;
    private bool Bot(int seat) => MatchManager.IsBot(record, seat);
    private bool SpectatorsAllowed => !rules.TryGetProperty("spectators", out var v) || v.ValueKind != JsonValueKind.False;
    /// <summary>Seats the server plays (a bot's, or left) that a player may take while the match runs.</summary>
    public int OpenSeats => closed ? 0 : Enumerable.Range(0, 4).Count(s => Bot(s) || Left(s));
    private int TurnSeconds => record.TurnSeconds > 0 ? record.TurnSeconds : options.TurnSeconds;
    private string ChatRule => rules.TryGetProperty("chat", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString()! : "on";
    /// <summary>
    /// The room's rules for the engine: penalty and işleme switches (missing = on), katlamalı (the Katlamalı queue or a
    /// table's option) and, at an Eşli table, whether the partner's opening raises the bar (missing = on).
    /// </summary>
    private RuleOptions Rules => new(!rules.TryGetProperty("okeyPenalty", out var o) || o.ValueKind != JsonValueKind.False,
        !rules.TryGetProperty("penalties", out var p) || p.ValueKind != JsonValueKind.False,
        !rules.TryGetProperty("processing", out var k) || k.ValueKind != JsonValueKind.False,
        record.Mode == GameModes.Doubling || (rules.TryGetProperty("doubling", out var d) && d.ValueKind == JsonValueKind.True),
        record.Mode != GameModes.Pairs || !rules.TryGetProperty("partnerRaises", out var r) || r.ValueKind != JsonValueKind.False);
    private bool GiftsAllowed => !rules.TryGetProperty("gifts", out var g) || g.ValueKind != JsonValueKind.False;

    /// <summary>Bots of a Klasik table show as "Bot · Ayşe" etc. (no account behind them).</summary>
    private static readonly string[] BotNames = ["Ayşe", "Zeynep", "Burak", "Deniz"];
    private static readonly string[] BotColors = ["#3D7BFF", "#16B6A8", "#8B5CF6", "#F5A623"];
    private void AddBotSummaries()
    {
        for (var s = 0; s < 4; s++)
            if (Bot(s)) players[record.Seats[s]] = new PlayerSummary { Id = record.Seats[s], Name = "Bot · " + BotNames[s], Color = BotColors[s], Level = 1 };
    }
    public IReadOnlyList<Guid> Seats => record.Seats;
    public int SeatOf(Guid account) => Array.IndexOf(record.Seats, account);

    // ---- Public API: every call is queued onto the session loop -------------------------------------------------

    /// <summary>
    /// The match and its stakes are already stored: from here it must go on. A storage hiccup is retried on the session's
    /// timer, never thrown back to the lobby, whose retry would charge the stakes again under a new match.
    /// </summary>
    public Task StartAsync() => Run(async () =>
    {
        try { players = await store.SummariesAsync(record.Seats); blocks = await store.BlocksAsync(record.Seats); }
        catch (Exception error) when (error is not OperationCanceledException) { log.LogWarning(error, "Match {Match}: player cards unavailable", Id); }
        AddBotSummaries();
        for (var s = 0; s < 4; s++) if (!Bot(s)) live.SetPlaying(record.Seats[s], Id);
        async Task Deal()
        {
            await StartHandAsync(0);
            foreach (var s in Enumerable.Range(0, 4)) SendStart(s);
            Broadcast();
        }
        try { await Deal(); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            log.LogWarning(error, "Match {Match}: first hand not stored; retrying", Id);
            Arm(TimeSpan.FromSeconds(2), Deal);
        }
        return true;
    });

    /// <summary>Rebuilds the table from storage after a restart. Seats start disconnected until their players sync.</summary>
    public Task RestoreAsync(StoredMatch stored) => Run(async () =>
    {
        players = await store.SummariesAsync(record.Seats);
        blocks = await store.BlocksAsync(record.Seats);
        AddBotSummaries();
        foreach (var hand in stored.Hands)
        {
            handIndex = hand.HandIndex;
            handCommands.Clear();
            state = RoundFactory.Create((uint)hand.Seed, hand.RoundId, Starter(hand.HandIndex), Rules);
            foreach (var move in hand.Moves)
            {
                var message = JsonSerializer.Deserialize<MatchCommandMessage>(move.Payload, Connection.Json)!;
                var command = Wire.ToCommand(message, Meta(move.Seat, state.Version));
                var result = GameEngine.Apply(state, command, CancellationToken.None);
                if (!result.Success || state.Version != move.Version) throw new InvalidOperationException($"Replay diverged at hand {hand.HandIndex} v{move.Version}: {result.Error}.");
                state = result.Value;
                handCommands.Add(command);
            }
            if (hand.Scores.Length == 4) Tally(hand.Scores);
        }
        for (var s = 0; s < 4; s++) if (!Left(s)) status[s] = Bot(s) ? SeatStatus.Auto : SeatStatus.Disconnected;
        for (var s = 0; s < 4; s++) if (!Left(s) && !Bot(s)) live.SetPlaying(record.Seats[s], Id);
        if (stored.Hands.Count == 0) await StartHandAsync(0);                       // stopped right after the stakes were taken
        else if (InPlay) { BeginTurnIfNew(); ScheduleTurn(); }
        else if (stored.Hands[^1].Scores.Length == 4) await AfterHandAsync(false);  // stopped between hands
        else await EndHandAsync();                                                  // stopped before the hand's scores were stored
        return true;
    });

    public Task<string?> SubmitAsync(Guid account, MatchCommandMessage message) => Run<string?>(async () =>
    {
        var seat = SeatOf(account);
        if (seat < 0 || status[seat] == SeatStatus.Left) return ErrorCodes.NotFound;
        if (!InPlay) return "rule.wrongPhase";
        if (state.ActivePlayer != seat) return "rule.wrongPlayer";
        var command = Wire.ToCommand(message, Meta(seat, message.ExpectedVersion));
        var error = await AcceptAsync(seat, command, byServer: false);
        if (error is null && status[seat] != SeatStatus.Connected) { status[seat] = SeatStatus.Connected; Broadcast(); }
        return error;
    });

    /// <summary>The player (re)opened the table: back in control, full snapshot sent.</summary>
    public Task SyncAsync(Guid account) => Run(() =>
    {
        var seat = SeatOf(account);
        if (seat < 0 || status[seat] == SeatStatus.Left) return Task.FromResult(false);
        status[seat] = SeatStatus.Connected;
        timeouts[seat] = 0;
        SendStart(seat);
        Broadcast();
        if (InPlay && state.ActivePlayer == seat) ScheduleTurn();
        return Task.FromResult(true);
    });

    /// <summary>İzle: the spectator gets the match (seen from seat 0) and every public update until it stops watching.</summary>
    public Task<string?> WatchAsync(Guid account) => Run<string?>(() =>
    {
        if (closed || state is null) return Task.FromResult<string?>(TableErrors.Closed);
        if (!SpectatorsAllowed) return Task.FromResult<string?>(ErrorCodes.Validation);
        if (SeatOf(account) is var seat && seat >= 0 && !Left(seat)) return Task.FromResult<string?>(ErrorCodes.Conflict);
        if (!spectators.Contains(account) && spectators.Count >= MaxSpectators) return Task.FromResult<string?>(TableErrors.Full);
        spectators.Add(account);
        live.Send(account, MessageTypes.MatchStart, new MatchStartMessage
        {
            MatchId = Id, Mode = record.Mode, Stake = record.Stake, Hands = record.Hands, YourSeat = 0, TurnSeconds = TurnSeconds, Seats = SeatList(), Spectator = true,
        });
        live.Send(account, MessageTypes.MatchState, SpectatorState(SeatList()));
        return Task.FromResult<string?>(null);
    });

    public Task UnwatchAsync(Guid account) => Run(() => Task.FromResult(spectators.Remove(account)));

    /// <summary>
    /// "Oyuna katıl": a person takes a seat the server plays — a bot's, or one its player left (that player's forfeit is
    /// recorded) — pays the stake and plays on from the seat's running total. Null when seated, else the error code.
    /// </summary>
    public Task<string?> TakeSeatAsync(Guid account, int? wanted) => Run<string?>(async () =>
    {
        if (!InPlay) return TableErrors.Playing;
        if (SeatOf(account) is var mine && mine >= 0 && !Left(mine)) return ErrorCodes.Conflict;
        bool Open(int s) => Bot(s) || Left(s);
        int seat = wanted is >= 0 and <= 3 && Open(wanted.Value) ? wanted.Value : Enumerable.Range(0, 4).Where(Open).DefaultIfEmpty(-1).First();
        if (seat < 0) return TableErrors.Full;
        Guid? leaver = Left(seat) && !Bot(seat) ? record.Seats[seat] : null;
        if (await store.TakeSeatAsync(record, seat, account, leaver, totals[seat], handIndex) is { } error) return error;
        var keep = ~(1 << seat);
        record.Seats[seat] = account; record.BotMask &= keep; record.LeftMask &= keep;
        status[seat] = SeatStatus.Connected; timeouts[seat] = 0; xp[seat] = 0; bestOpening[seat] = 0; joinedHand[seat] = handIndex;
        spectators.Remove(account);
        try
        {
            foreach (var (id, card) in await store.SummariesAsync([account])) players[id] = card;
            blocks = await store.BlocksAsync(record.Seats);
        }
        catch (Exception error2) when (error2 is not OperationCanceledException) { log.LogWarning(error2, "Match {Match}: new player's card unavailable", Id); }
        live.SetPlaying(account, Id);
        SendStart(seat);
        ToAll(MessageTypes.MatchSeats, new MatchSeatsMessage { MatchId = Id, Seats = SeatList() });
        Broadcast();
        // A turn the server was about to play becomes the newcomer's, with a full turn time.
        if (InPlay && state.ActivePlayer == seat) { deadline = clock.GetUtcNow().AddSeconds(TurnSeconds); ScheduleTurn(); }
        return null;
    });

    public Task DisconnectedAsync(Guid account) => Run(() =>
    {
        var seat = SeatOf(account);
        if (seat >= 0 && status[seat] is SeatStatus.Connected) { status[seat] = SeatStatus.Disconnected; Broadcast(); }
        return Task.FromResult(true);
    });

    /// <summary>Leaving forfeits: the stake stays in the pot and the server plays the seat to the end.</summary>
    public Task LeaveAsync(Guid account) => Run(async () =>
    {
        var seat = SeatOf(account);
        if (seat < 0 || status[seat] == SeatStatus.Left) return false;
        // Stored first: a failed write throws with the seat untouched, so memory and storage never disagree.
        await store.SetLeftAsync(Id, record.LeftMask | 1 << seat);
        status[seat] = SeatStatus.Left;
        record.LeftMask |= 1 << seat;
        live.SetPlaying(account, null);
        if (record.LeftMask == 0b1111) { await FinishAsync(); return true; }
        Broadcast();
        if (InPlay && state.ActivePlayer == seat) ScheduleTurn();
        return true;
    });

    public Task<string?> EmoteAsync(Guid account, string emote) => Run<string?>(() =>
    {
        var seat = SeatOf(account);
        if (seat < 0 || status[seat] == SeatStatus.Left || !catalog.Emotes.Contains(emote)) return Task.FromResult<string?>(ErrorCodes.Validation);
        if (ChatRule == "off") return Task.FromResult<string?>(ErrorCodes.Validation);
        var now = clock.GetUtcNow();
        if (now - lastEmote[seat] < EmoteInterval) return Task.FromResult<string?>(ErrorCodes.RateLimited);
        lastEmote[seat] = now;
        ToAllFrom(seat, MessageTypes.MatchEmote, new EmoteMessage { MatchId = Id, Seat = seat, Emote = emote });
        return Task.FromResult<string?>(null);
    });

    /// <summary>
    /// Table chat by the room's rule: "off" none, "quick" ready phrases only (by index), "on" also text up to 120 (D-32)
    /// characters. Everyone at the table gets it, the sender too (that is their confirmation).
    /// </summary>
    public Task<string?> ChatAsync(Guid account, string? text, int? quick) => Run<string?>(() =>
    {
        var seat = SeatOf(account);
        if (seat < 0 || status[seat] == SeatStatus.Left || ChatRule == "off") return Task.FromResult<string?>(ErrorCodes.Validation);
        text = text?.Trim();
        if (quick is { } q) { if (q is < 0 or >= QuickPhrases) return Task.FromResult<string?>(ErrorCodes.Validation); text = null; }
        else if (ChatRule == "quick" || string.IsNullOrEmpty(text) || text.Length > Galaxy101.Table.TableEconomy.ChatMax) return Task.FromResult<string?>(ErrorCodes.Validation);
        var now = clock.GetUtcNow();
        if (now - lastChat[seat] < ChatInterval) return Task.FromResult<string?>(ErrorCodes.RateLimited);
        lastChat[seat] = now;
        if (text is not null) text = Social.TextFilter.Mask(text);
        ToAllFrom(seat, MessageTypes.MatchChat, new ChatMessage { MatchId = Id, Seat = seat, Text = text, Quick = quick });
        return Task.FromResult<string?>(null);
    });

    public Task<string?> GiftAsync(Guid account, string gift, int? target) => Run<string?>(async () =>
    {
        var seat = SeatOf(account);
        var item = catalog.Gifts.FirstOrDefault(g => g.Id == gift);
        if (seat < 0 || status[seat] == SeatStatus.Left || item is null || target is < 0 or > 3 || target == seat) return ErrorCodes.Validation;
        if (!GiftsAllowed) return ErrorCodes.Validation;
        // Treating the whole table costs one item per other player.
        var cost = item.Chips * (target is null ? 3 : 1);
        if (await store.ChargeAsync(account, cost, "gift") is null) return ErrorCodes.InsufficientFunds;
        ToAll(MessageTypes.MatchGift, new GiftMessage { MatchId = Id, Seat = seat, TargetSeat = target, Gift = gift });
        live.Send(account, MessageTypes.Wallet, await store.WalletAsync(account));
        return null;
    });

    /// <summary>Host shutdown: stop timers and the loop without settling; the stored moves resume the table on the next start.</summary>
    public async Task StopAsync()
    {
        try { await Run(() => { closed = true; timerGeneration++; inbox.Writer.TryComplete(); return Task.FromResult(true); }); }
        catch (InvalidOperationException) { } // already over
    }

    /// <summary>Test probe: current hand, round version and a seat's hand.</summary>
    internal Task<(int Hand, long Version, int[] Tiles)> ProbeAsync(int seat) =>
        Run(() => Task.FromResult((handIndex, state.Version, state.Players[seat].Hand.Select(t => t.Value).ToArray())));

    // ---- Turn handling (loop only) -------------------------------------------------------------------------------

    private bool InPlay => !closed && state is not null && state.Phase is RoundPhase.AwaitingDraw or RoundPhase.PlanningTurn;
    private bool Left(int seat) => (record.LeftMask & (1 << seat)) != 0;
    private int Starter(int hand) => (record.FirstStarter + hand) % 4;
    private CommandMeta Meta(int seat, long expectedVersion) => new(state.RoundId, seat, state.LastAcceptedSequences[seat] + 1, expectedVersion);

    private async Task<string?> AcceptAsync(int seat, GameCommand command, bool byServer)
    {
        var result = GameEngine.Apply(state, command, CancellationToken.None);
        if (!result.Success) return "rule." + JsonNamingPolicy.CamelCase.ConvertName(result.Error.ToString());
        await store.AppendMoveAsync(Id, handIndex, state.Version, seat, JsonSerializer.Serialize(Wire.ToMessage(command), Connection.Json));
        var before = state;
        state = result.Value;
        handCommands.Add(command);
        lastEvent = Event(seat, command, before, byServer);
        if (!byServer) timeouts[seat] = 0;
        if (!InPlay) { await EndHandAsync(); return null; }
        BeginTurnIfNew();
        Broadcast();
        ScheduleTurn();
        return null;
    }

    private void BeginTurnIfNew()
    {
        var key = (handIndex, state.TurnNumber, state.ActivePlayer);
        if (key == turnKey) return;
        turnKey = key;
        var from = clock.GetUtcNow();
        if (dealUntil is { } dealt && dealt > from) from = dealt;
        deadline = from.AddSeconds(TurnSeconds);
    }

    /// <summary>Arms the single active timer: the turn deadline, or a short pause before the server plays a seat.</summary>
    private void ScheduleTurn()
    {
        var seat = state.ActivePlayer;
        var auto = status[seat] is SeatStatus.Auto or SeatStatus.Left;
        var due = auto ? AutoPlayDelay : (deadline!.Value - clock.GetUtcNow());
        // Nobody moves while the clients deal (the ceremony): bots wait for it too.
        if (dealUntil is { } dealt && dealt - clock.GetUtcNow() > due) due = dealt - clock.GetUtcNow();
        Arm(due < TimeSpan.Zero ? TimeSpan.Zero : due, async () =>
        {
            if (!InPlay || state.ActivePlayer != seat) return;
            if (status[seat] is SeatStatus.Connected or SeatStatus.Disconnected)
            {
                if (clock.GetUtcNow() < deadline) { ScheduleTurn(); return; }
                if (++timeouts[seat] >= options.TimeoutsBeforeAuto) status[seat] = SeatStatus.Auto;
            }
            await PlayForAsync(seat);
        });
    }

    /// <summary>Arms the session's single timer; arming again (or any hand/match transition) supersedes the previous one.</summary>
    private void Arm(TimeSpan due, Func<Task> work)
    {
        var generation = ++timerGeneration;
        _ = Task.Delay(due, clock).ContinueWith(_ => inbox.Writer.TryWrite(async () =>
        {
            if (generation != timerGeneration) return;
            try { await work(); }
            catch (Exception error)
            {
                // Typically a storage hiccup. Nothing was applied (moves are stored before they count), so try again.
                log.LogWarning(error, "Match {Match} timer work failed; retrying", Id);
                if (generation == timerGeneration) Arm(TimeSpan.FromSeconds(2), work);
            }
        }), TaskScheduler.Default);
    }

    /// <summary>The server plays one turn for a seat (timeout, auto or left) with the same bot the offline table uses.</summary>
    private async Task PlayForAsync(int seat)
    {
        var planner = new BotPlanner();
        using var think = new CancellationTokenSource(BotThinkLimit);
        TurnPlan? witness = null;
        if (state.Phase == RoundPhase.AwaitingDraw)
        {
            DrawDecision decision;
            try { decision = planner.ChooseDraw(PlayerViewProjector.ForPlayer(state, seat), think.Token); }
            catch (OperationCanceledException) { decision = new DrawDecision(DrawSource.Stock); }
            catch (Exception error)
            {
                // A planner fault must not stall the table: the stock is always a legal draw.
                log.LogError(error, "Match {Match}: bot draw failed for seat {Seat}", Id, seat);
                decision = new DrawDecision(DrawSource.Stock);
            }
            if (await AcceptAsync(seat, new DrawCommand(Meta(seat, state.Version), decision.Source), true) is null) witness = decision.Witness;
            // Refused (the pile tile): the stock instead, and the pile's witness no longer fits the hand.
            else await AcceptAsync(seat, new DrawCommand(Meta(seat, state.Version), DrawSource.Stock), true);
        }
        if (!Planning(seat)) return;
        var plan = Plan(planner, seat, witness, think.Token);
        // The player took a pile tile and the time ran out before it was used: it goes back, the stock is drawn and
        // the turn is planned again from there.
        if (plan is null && await ReturnPileTileAsync(seat)) plan = Plan(planner, seat, null, think.Token);
        if (plan is not null && await AcceptAsync(seat, new CommitTurnCommand(Meta(seat, state.Version), plan), true) is null) return;
        await FallbackTurnAsync(seat);
    }

    private bool Planning(int seat) => InPlay && state.ActivePlayer == seat && state.Phase == RoundPhase.PlanningTurn;

    /// <summary>The bot's turn, or null when it has none in time. A pile tile with no legal use (taken by the player) is not a fault.</summary>
    private TurnPlan? Plan(BotPlanner planner, int seat, TurnPlan? witness, CancellationToken token)
    {
        try { return planner.PlanTurn(PlayerViewProjector.ForPlayer(state, seat), witness, token); }
        catch (OperationCanceledException) { return null; }
        catch (InvalidOperationException) when (state.PendingDraw?.Source == DrawSource.PreviousDiscard && witness is null) { return null; }
        catch (Exception error)
        {
            log.LogError(error, "Match {Match}: bot turn failed for seat {Seat}", Id, seat);
            return null;
        }
    }

    /// <summary>Puts a taken pile tile back and draws the stock; true when the seat is planning again with the stock tile.</summary>
    private async Task<bool> ReturnPileTileAsync(int seat)
    {
        if (state.PendingDraw?.Source != DrawSource.PreviousDiscard) return false;
        if (await AcceptAsync(seat, new ReturnDiscardCommand(Meta(seat, state.Version)), true) is not null) return false;
        if (InPlay && state.ActivePlayer == seat && state.Phase == RoundPhase.AwaitingDraw)
            await AcceptAsync(seat, new DrawCommand(Meta(seat, state.Version), DrawSource.Stock), true);
        return Planning(seat);
    }

    /// <summary>Last resort when the planner gives nothing usable: put an unusable discard back, then discard any legal tile.</summary>
    private async Task FallbackTurnAsync(int seat)
    {
        await ReturnPileTileAsync(seat);
        if (!Planning(seat)) return;
        var hand = state.Players[seat].Hand;
        var candidates = state.PendingDraw is { } drawn ? hand.OrderByDescending(t => t == drawn.Tile) : hand.AsEnumerable();
        foreach (var tile in candidates.ToList())
            foreach (var penalty in new[] { false, true })
                if (await AcceptAsync(seat, new CommitTurnCommand(Meta(seat, state.Version), new TurnPlan([], tile, penalty)), true) is null) return;
        log.LogError("Match {Match}: no legal turn found for seat {Seat}; aborting with refunds", Id, seat);
        await AbortAsync();
    }

    // ---- Hands and the end of the match --------------------------------------------------------------------------

    private async Task StartHandAsync(int index)
    {
        var seed = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4));
        var roundId = Guid.CreateVersion7();
        var dealt = RoundFactory.Create(seed, roundId, Starter(index), Rules);
        // Stored first: a failed write leaves the previous hand in place, so the retried timer deals this hand again.
        await store.StartHandAsync(Id, index, seed, roundId);
        handIndex = index;
        handCommands.Clear();
        nextHandAt = null;
        lastEvent = null;
        state = dealt;
        dealUntil = clock.GetUtcNow().AddMilliseconds(options.DealMilliseconds);
        BeginTurnIfNew();
        ScheduleTurn();
    }

    private async Task EndHandAsync()
    {
        timerGeneration++;
        deadline = null;
        var scores = Settlement.HandScores(state);
        try { await store.FinishHandAsync(Id, handIndex, scores); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // The last move is stored, the scores are not: show the finished hand and keep trying instead of leaving
            // the table without a timer.
            log.LogWarning(error, "Match {Match} hand {Hand} scores not stored; retrying", Id, handIndex);
            Broadcast();
            Arm(TimeSpan.FromSeconds(2), EndHandAsync);
            return;
        }
        Tally(scores);
        await AfterHandAsync(true);
    }

    private void Tally(int[] scores)
    {
        for (var s = 0; s < 4; s++) totals[s] += scores[s];
        var hand = Settlement.HandCounters(state, handCommands, record.LeftMask);
        counters.Add(hand);
        var winner = state.Result?.FinishInfo.Winner;
        for (var s = 0; s < 4; s++)
            if (!Left(s)) xp[s] += winner == s ? options.XpHandWin : options.XpHandPlayed;
        foreach (var c in handCommands.OfType<CommitTurnCommand>())
            foreach (var open in c.Plan.Actions.OfType<OpenGroups>().Where(o => o.Opening == OpeningKind.Series))
                bestOpening[c.Meta.Actor] = Math.Max(bestOpening[c.Meta.Actor], open.Groups.Sum(g => g.Tiles.Sum(t => t.RepresentedFace.Number)));
    }

    private async Task AfterHandAsync(bool handScored)
    {
        var more = handIndex + 1 < record.Hands && record.LeftMask != 0b1111;
        nextHandAt = more ? clock.GetUtcNow().AddSeconds(options.NextHandSeconds) : null;
        if (handScored)
        {
            Broadcast();
            ToAll(MessageTypes.MatchHandEnd, new HandEndMessage
            {
                MatchId = Id, HandIndex = handIndex, Result = state.Result is { } r ? Wire.ToDto(r) : null,
                Totals = [.. totals], Xp = [.. xp], NextHandAt = nextHandAt,
            });
        }
        if (!more) { await FinishAsync(); return; }
        var next = handIndex + 1;
        Arm(TimeSpan.FromSeconds(options.NextHandSeconds), async () => { await StartHandAsync(next); Broadcast(); });
    }

    /// <summary>
    /// Settles and closes. The table counts as closed only once the settlement is stored: a failed write (all or
    /// nothing) is retried, so the players are paid and released instead of staying "playing" forever.
    /// </summary>
    private async Task FinishAsync()
    {
        if (closed) return;
        timerGeneration++;
        int[]? seasonLp = null;
        if (record.Mode == GameModes.Ranked)
            try { seasonLp = await store.SeasonLpAsync(record.Seats); }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                log.LogWarning(error, "Match {Match}: league points unavailable; retrying", Id);
                Arm(TimeSpan.FromSeconds(5), FinishAsync);
                return;
            }
        var outcomes = Settlement.Settle(record.Mode, record.Stake, totals, record.LeftMask, options, record.BotMask, seasonLp);
        // The hands' XP plus the place bonus (computed afresh: a retried settlement must not add it twice).
        var matchXp = Enumerable.Range(0, 4).Select(s => xp[s] + (Left(s) ? 0 : outcomes[s].XpBonus)).ToArray();
        var tallies = Enumerable.Range(0, 4).Select(s => new SeatTally(
            new QuestCounters(Since(s).Sum(h => h[s].WinHands), Since(s).Sum(h => h[s].PairsOpenings), Since(s).Sum(h => h[s].HandsPlayed),
                Since(s).Sum(h => h[s].OkeyFinishes), Since(s).Sum(h => h[s].TilesAdded)),
            [.. Since(s).Where(h => h[s].HandsPlayed > 0).Select(h => h[s].WinHands > 0)], matchXp[s], bestOpening[s])).ToArray();
        try { await store.SettleAsync(record, outcomes, tallies, totals); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            log.LogWarning(error, "Match {Match} settlement not stored; retrying", Id);
            Arm(TimeSpan.FromSeconds(5), FinishAsync);
            return;
        }
        closed = true;
        ToAll(MessageTypes.MatchEnd, new MatchEndMessage
        {
            MatchId = Id,
            Placements = [.. outcomes.Select(o => new PlacementDto { Seat = o.Seat, Place = o.Place, Total = totals[o.Seat], ChipDelta = o.ChipDelta, LpDelta = o.LpDelta, Xp = matchXp[o.Seat], Left = Left(o.Seat) })],
        }, includeLeft: true);
        try
        {
            foreach (var account in record.Seats)
            {
                live.SetPlaying(account, null);
                if (!live.IsOnline(account)) continue;
                // A fresh balance is a courtesy (the wallet endpoint has it too): a failed read must not keep the table open.
                try { live.Send(account, MessageTypes.Wallet, await store.WalletAsync(account)); }
                catch (Exception error) when (error is not OperationCanceledException) { log.LogWarning(error, "Match {Match}: wallet push failed", Id); }
            }
        }
        finally { Close(); }
    }

    private async Task AbortAsync()
    {
        if (closed) return;
        timerGeneration++;
        try { await store.AbortAsync(record); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            log.LogWarning(error, "Match {Match} refunds not stored; retrying", Id);
            Arm(TimeSpan.FromSeconds(5), AbortAsync);
            return;
        }
        closed = true;
        ToAll(MessageTypes.MatchEnd, new MatchEndMessage { MatchId = Id, Aborted = true }, includeLeft: true);
        foreach (var account in record.Seats) live.SetPlaying(account, null);
        Close();
    }

    private void Close()
    {
        inbox.Writer.TryComplete();
        onClosed(this);
    }

    // ---- Messaging -------------------------------------------------------------------------------------------------

    private void SendStart(int seat) => live.Send(record.Seats[seat], MessageTypes.MatchStart, new MatchStartMessage
    {
        MatchId = Id, Mode = record.Mode, Stake = record.Stake, Hands = record.Hands, YourSeat = seat, TurnSeconds = TurnSeconds, Seats = SeatList(),
    });

    private void Broadcast()
    {
        if (state is null) return;
        var seats = SeatList();
        for (var s = 0; s < 4; s++)
        {
            if (status[s] == SeatStatus.Left || !live.IsOnline(record.Seats[s])) continue;
            live.Send(record.Seats[s], MessageTypes.MatchState, new MatchStateMessage
            {
                MatchId = Id, HandIndex = handIndex, View = Wire.ToDto(PlayerViewProjector.ForPlayer(state, s)),
                LastEvent = lastEvent, TurnDeadline = InPlay ? deadline : nextHandAt, Seats = seats, Totals = [.. totals],
            });
        }
        if (spectators.Count > 0)
        {
            var watched = SpectatorState(seats);
            foreach (var watcher in spectators) live.Send(watcher, MessageTypes.MatchState, watched);
        }
    }

    /// <summary>The hands that count for a seat's current player (from the hand it took the seat in).</summary>
    private IEnumerable<QuestCounters[]> Since(int seat) => counters.Skip(joinedHand[seat]);

    /// <summary>What spectators see: seat 0's view without any tiles (its hand and its private draw stay with it).</summary>
    private MatchStateMessage SpectatorState(List<SeatResponse> seats)
    {
        var view = Wire.ToDto(PlayerViewProjector.ForPlayer(state, 0));
        view.Hand = []; view.PendingDraw = null;
        return new MatchStateMessage
        {
            MatchId = Id, HandIndex = handIndex, View = view, LastEvent = lastEvent,
            TurnDeadline = InPlay ? deadline : nextHandAt, Seats = seats, Totals = [.. totals],
        };
    }

    /// <summary>A seat's own words (chat, emotes): every seat still playing except those who blocked it, and the spectators.</summary>
    private void ToAllFrom<T>(int from, string type, T message)
    {
        for (var s = 0; s < 4; s++)
            if (status[s] != SeatStatus.Left && !blocks.Contains((record.Seats[s], record.Seats[from]))) live.Send(record.Seats[s], type, message);
        foreach (var watcher in spectators) live.Send(watcher, type, message);
    }

    /// <summary>A block placed or lifted while both sit at this table takes effect at once.</summary>
    public Task BlockChangedAsync(Guid blocker, Guid blocked, bool on) => Run(() =>
    {
        if (on) blocks.Add((blocker, blocked)); else blocks.Remove((blocker, blocked));
        return Task.FromResult(true);
    });

    private void ToAll<T>(string type, T message, bool includeLeft = false)
    {
        for (var s = 0; s < 4; s++)
            if (includeLeft || status[s] != SeatStatus.Left) live.Send(record.Seats[s], type, message);
        foreach (var watcher in spectators) live.Send(watcher, type, message);
    }

    private List<SeatResponse> SeatList() => [.. Enumerable.Range(0, 4).Select(s => new SeatResponse
    {
        Seat = s, Team = record.Mode == GameModes.Pairs ? s % 2 : s, Player = players.GetValueOrDefault(record.Seats[s]),
        Status = JsonNamingPolicy.CamelCase.ConvertName(status[s].ToString()),
    })];

    private static MatchEventDto Event(int seat, GameCommand command, RoundState before, bool byServer) => command switch
    {
        // A stock draw stays private; a draw from the discard pile shows which tile was taken.
        DrawCommand d => new MatchEventDto { Kind = "draw", Seat = seat, Auto = byServer, Tile = d.Source == DrawSource.PreviousDiscard ? before.Discards[(seat + 3) % 4][^1].Value : null },
        ReturnDiscardCommand => new MatchEventDto { Kind = "return", Seat = seat, Auto = byServer },
        ShortOpeningCommand => new MatchEventDto { Kind = "shortOpening", Seat = seat, Auto = byServer },
        CommitTurnCommand c => new MatchEventDto { Kind = "commit", Seat = seat, Auto = byServer, Tile = c.Plan.Discard.Value },
        _ => new MatchEventDto { Kind = "unknown", Seat = seat },
    };

    // ---- Loop ------------------------------------------------------------------------------------------------------

    private async Task LoopAsync()
    {
        await foreach (var work in inbox.Reader.ReadAllAsync())
        {
            try { await work(); }
            catch (Exception error) { log.LogError(error, "Match {Match} work item failed", Id); }
        }
    }

    private Task<T> Run<T>(Func<Task<T>> work)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = inbox.Writer.TryWrite(async () =>
        {
            try { done.SetResult(await work()); }
            catch (Exception error) { done.SetException(error); }
        });
        if (!queued) done.SetException(new InvalidOperationException("The match is over."));
        return done.Task;
    }
}

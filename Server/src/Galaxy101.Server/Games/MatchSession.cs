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
    private static readonly TimeSpan BotThinkLimit = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan EmoteInterval = TimeSpan.FromSeconds(2);

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
        for (var s = 0; s < 4; s++) if (Left(s)) status[s] = SeatStatus.Left;
        _ = Task.Run(LoopAsync);
    }

    public Guid Id => record.Id;
    public IReadOnlyList<Guid> Seats => record.Seats;
    public int SeatOf(Guid account) => Array.IndexOf(record.Seats, account);

    // ---- Public API: every call is queued onto the session loop -------------------------------------------------

    public Task StartAsync() => Run(async () =>
    {
        players = await store.SummariesAsync(record.Seats);
        for (var s = 0; s < 4; s++) live.SetPlaying(record.Seats[s], Id);
        await StartHandAsync(0);
        foreach (var s in Enumerable.Range(0, 4)) SendStart(s);
        Broadcast();
        return true;
    });

    /// <summary>Rebuilds the table from storage after a restart. Seats start disconnected until their players sync.</summary>
    public Task RestoreAsync(StoredMatch stored) => Run(async () =>
    {
        players = await store.SummariesAsync(record.Seats);
        foreach (var hand in stored.Hands)
        {
            handIndex = hand.HandIndex;
            handCommands.Clear();
            state = RoundFactory.Create((uint)hand.Seed, hand.RoundId, Starter(hand.HandIndex));
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
        for (var s = 0; s < 4; s++) if (!Left(s)) status[s] = SeatStatus.Disconnected;
        for (var s = 0; s < 4; s++) if (!Left(s)) live.SetPlaying(record.Seats[s], Id);
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
        status[seat] = SeatStatus.Left;
        record.LeftMask |= 1 << seat;
        await store.SetLeftAsync(Id, record.LeftMask);
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
        var now = clock.GetUtcNow();
        if (now - lastEmote[seat] < EmoteInterval) return Task.FromResult<string?>(ErrorCodes.RateLimited);
        lastEmote[seat] = now;
        ToAll(MessageTypes.MatchEmote, new EmoteMessage { MatchId = Id, Seat = seat, Emote = emote });
        return Task.FromResult<string?>(null);
    });

    public Task<string?> GiftAsync(Guid account, string gift, int? target) => Run<string?>(async () =>
    {
        var seat = SeatOf(account);
        var item = catalog.Gifts.FirstOrDefault(g => g.Id == gift);
        if (seat < 0 || status[seat] == SeatStatus.Left || item is null || target is < 0 or > 3 || target == seat) return ErrorCodes.Validation;
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
        deadline = clock.GetUtcNow().AddSeconds(options.TurnSeconds);
    }

    /// <summary>Arms the single active timer: the turn deadline, or a short pause before the server plays a seat.</summary>
    private void ScheduleTurn()
    {
        var seat = state.ActivePlayer;
        var auto = status[seat] is SeatStatus.Auto or SeatStatus.Left;
        var due = auto ? AutoPlayDelay : (deadline!.Value - clock.GetUtcNow());
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
            if (await AcceptAsync(seat, new DrawCommand(Meta(seat, state.Version), decision.Source), true) is not null)
                await AcceptAsync(seat, new DrawCommand(Meta(seat, state.Version), DrawSource.Stock), true);
            witness = decision.Witness;
        }
        if (!InPlay || state.ActivePlayer != seat || state.Phase != RoundPhase.PlanningTurn) return;
        TurnPlan? plan = null;
        try { plan = planner.PlanTurn(PlayerViewProjector.ForPlayer(state, seat), witness, think.Token); }
        catch (OperationCanceledException) { }
        if (plan is not null && await AcceptAsync(seat, new CommitTurnCommand(Meta(seat, state.Version), plan), true) is null) return;
        await FallbackTurnAsync(seat);
    }

    /// <summary>Last resort when the planner gives nothing usable: put an unusable discard back, then discard any legal tile.</summary>
    private async Task FallbackTurnAsync(int seat)
    {
        if (state.PendingDraw?.Source == DrawSource.PreviousDiscard)
        {
            await AcceptAsync(seat, new ReturnDiscardCommand(Meta(seat, state.Version)), true);
            if (InPlay && state.Phase == RoundPhase.AwaitingDraw)
                await AcceptAsync(seat, new DrawCommand(Meta(seat, state.Version), DrawSource.Stock), true);
        }
        if (!InPlay || state.ActivePlayer != seat || state.Phase != RoundPhase.PlanningTurn) return;
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
        handIndex = index;
        handCommands.Clear();
        nextHandAt = null;
        lastEvent = null;
        var seed = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4));
        var roundId = Guid.CreateVersion7();
        state = RoundFactory.Create(seed, roundId, Starter(index));
        await store.StartHandAsync(Id, index, seed, roundId);
        BeginTurnIfNew();
        ScheduleTurn();
    }

    private async Task EndHandAsync()
    {
        timerGeneration++;
        deadline = null;
        var scores = Settlement.HandScores(state);
        await store.FinishHandAsync(Id, handIndex, scores);
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
        Arm(TimeSpan.FromSeconds(options.NextHandSeconds), async () => { await StartHandAsync(handIndex + 1); Broadcast(); });
    }

    private async Task FinishAsync()
    {
        if (closed) return;
        closed = true;
        timerGeneration++;
        var outcomes = Settlement.Settle(record.Mode, record.Stake, totals, record.LeftMask, options);
        var tallies = Enumerable.Range(0, 4).Select(s => new SeatTally(
            new QuestCounters(counters.Sum(h => h[s].WinHands), counters.Sum(h => h[s].PairsOpenings), counters.Sum(h => h[s].HandsPlayed),
                counters.Sum(h => h[s].OkeyFinishes), counters.Sum(h => h[s].TilesAdded)),
            [.. counters.Where(h => h[s].HandsPlayed > 0).Select(h => h[s].WinHands > 0)], xp[s], bestOpening[s])).ToArray();
        await store.SettleAsync(record, outcomes, tallies, totals);
        ToAll(MessageTypes.MatchEnd, new MatchEndMessage
        {
            MatchId = Id,
            Placements = [.. outcomes.Select(o => new PlacementDto { Seat = o.Seat, Place = o.Place, Total = totals[o.Seat], ChipDelta = o.ChipDelta, LpDelta = o.LpDelta, Xp = xp[o.Seat], Left = Left(o.Seat) })],
        }, includeLeft: true);
        foreach (var account in record.Seats)
        {
            live.SetPlaying(account, null);
            if (live.IsOnline(account)) live.Send(account, MessageTypes.Wallet, await store.WalletAsync(account));
        }
        Close();
    }

    private async Task AbortAsync()
    {
        if (closed) return;
        closed = true;
        timerGeneration++;
        await store.AbortAsync(record);
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
        MatchId = Id, Mode = record.Mode, Stake = record.Stake, Hands = record.Hands, YourSeat = seat, TurnSeconds = options.TurnSeconds, Seats = SeatList(),
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
    }

    private void ToAll<T>(string type, T message, bool includeLeft = false)
    {
        for (var s = 0; s < 4; s++)
            if (includeLeft || status[s] != SeatStatus.Left) live.Send(record.Seats[s], type, message);
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

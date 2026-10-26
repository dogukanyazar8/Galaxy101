using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using Galaxy101.Contracts;
using Galaxy101.Server.Economy;
using Galaxy101.Server.Games;
using Galaxy101.Server.Infrastructure;
using Galaxy101.Server.Realtime;
using Galaxy101.Table;
using Microsoft.Extensions.Options;
using Okey101.Core.V4;

namespace Galaxy101.Server.V4;

/// <summary>
/// The v4 protocol over the existing WebSocket (docs/v4/KARARLAR.md K-04): lobby tables, waiting room, invites, against
/// bots, ranked queue, chat and gifts, and the matches (each a <see cref="TableRuntime"/> on the v4 rules engine). One
/// actor: every message and every timer runs on a single loop, so the whole hub needs no locks. Time is logical: a timer
/// runs with "now" = its due time, so event times follow the package's schedule exactly however late the loop wakes
/// (protocol/timings.md: the server does not wait for animations, it sends events by its own clock).
/// </summary>
public sealed partial class Hub : IRealtimeReplyHandler, IHostedService
{
    /// <summary>protocol.json version (session.serverVersion).</summary>
    public const string ProtocolVersion = "0.4.0";

    private static readonly HashSet<string> Types =
    [
        V4Messages.Hello, V4Messages.Resync, V4Messages.LobbySubscribe, V4Messages.LobbyUnsubscribe, V4Messages.TableCreate, V4Messages.TableJoin,
        V4Messages.TableJoinCode, V4Messages.TableSpectate, V4Messages.TableLeave, V4Messages.SeatAddBot, V4Messages.SeatKick, V4Messages.TableStart,
        V4Messages.InviteSend, V4Messages.InviteRespond, V4Messages.InviteCancel, V4Messages.VsBotsStart, V4Messages.RankedQueue,
        V4Messages.RankedAccept, V4Messages.RankedCancel, V4Messages.GRollDice, V4Messages.GDraw, V4Messages.GReturnSide, V4Messages.GOpen,
        V4Messages.GLay, V4Messages.GAdd, V4Messages.GSwap, V4Messages.GUndo, V4Messages.GDiscard, V4Messages.GReady, V4Messages.GRackOrder,
        V4Messages.ChatSend, V4Messages.GiftSend,
    ];

    private readonly ConnectionRegistry live;
    private readonly Store store;
    private readonly Lobby lobby;
    private readonly Catalog catalog;
    private readonly GameOptions options;
    private readonly TimeProvider clock;
    private readonly ILogger<Hub> log;
    private readonly Channel<Func<Task>> inbox = Channel.CreateUnbounded<Func<Task>>(new UnboundedChannelOptions { SingleReader = true });
    private readonly PriorityQueue<Timer, (long Due, long Seq)> due = new();
    private readonly ITimer wake;
    private Task loop = Task.CompletedTask;
    private long now;
    private long seq;

    public Hub(ConnectionRegistry live, Store store, Lobby lobby, Catalog catalog, IOptions<GameOptions> options, TimeProvider clock, ILogger<Hub> log)
    {
        (this.live, this.store, this.lobby, this.catalog, this.options, this.clock, this.log) = (live, store, lobby, catalog, options.Value, clock, log);
        wake = clock.CreateTimer(_ => inbox.Writer.TryWrite(RunDueAsync), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Test seam: the seed of the next match (real play: a random one).</summary>
    internal Func<uint> SeedSource { get; set; } = () => BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4));
    /// <summary>Test seam (package DEMO-17, scenarios only): the next match's first deal.</summary>
    internal DealRig? NextRig { get; set; }

    private long ClockMs => clock.GetUtcNow().ToUnixTimeMilliseconds();
    /// <summary>Logical time of the work being run (ms since the Unix epoch).</summary>
    internal long Now => now;

    // ---- Hosting -------------------------------------------------------------------------------------------------

    public async Task StartAsync(CancellationToken ct)
    {
        loop = Task.Run(LoopAsync, CancellationToken.None);
        try
        {
            var aborted = await store.AbortOpenAsync(ct);
            if (aborted > 0) log.LogWarning("v4: {Count} tables open at the last stop were aborted and their stakes returned", aborted);
        }
        catch (Exception error) when (error is not OperationCanceledException) { log.LogError(error, "v4: open tables could not be aborted at start"); }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        inbox.Writer.TryComplete();
        await wake.DisposeAsync();
        await Task.WhenAny(loop, Task.Delay(Timeout.Infinite, ct));
    }

    private async Task LoopAsync()
    {
        await foreach (var work in inbox.Reader.ReadAllAsync())
        {
            try { await work(); }
            catch (Exception error) { log.LogError(error, "v4 hub work item failed"); }
        }
    }

    private Task<T> Run<T>(Func<Task<T>> work)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = inbox.Writer.TryWrite(async () =>
        {
            try
            {
                await RunDueAsync();
                now = Math.Max(now, ClockMs);
                var result = await work();
                await RunDueAsync();
                done.SetResult(result);
            }
            catch (Exception error)
            {
                done.SetException(error);
                await RunDueAsync(); // work the failed item deferred (a stake to give back) runs now, not at the next message
            }
        });
        if (!queued) done.SetException(new InvalidOperationException("The hub is stopped."));
        return done.Task;
    }

    /// <summary>Test seam: returns once everything due by now has run.</summary>
    internal Task IdleAsync() => Run(() => Task.FromResult(true));

    // ---- Logical timers ------------------------------------------------------------------------------------------

    private sealed class Timer(Func<Task> work)
    {
        public Func<Task> Work { get; } = work;
        public bool Cancelled { get; set; }
    }

    /// <summary>Runs <paramref name="work"/> on the loop at <paramref name="atMs"/> (logical time; never before now).</summary>
    internal object Schedule(long atMs, Func<Task> work)
    {
        var timer = new Timer(work);
        due.Enqueue(timer, (Math.Max(atMs, now), ++seq));
        return timer;
    }

    internal object Schedule(long atMs, Action action) => Schedule(atMs, () => { action(); return Task.CompletedTask; });

    internal static void Cancel(object? handle)
    {
        if (handle is Timer t) t.Cancelled = true;
    }

    /// <summary>Async work right after the current item (a sync callback that needs storage).</summary>
    private void Defer(Func<Task> work) => Schedule(now, work);

    private async Task RunDueAsync()
    {
        while (due.TryPeek(out var timer, out var key))
        {
            if (timer.Cancelled) { due.Dequeue(); continue; }
            if (key.Due > ClockMs) break;
            due.Dequeue();
            now = Math.Max(now, key.Due);
            try { await timer.Work(); }
            catch (Exception error) { log.LogError(error, "v4 timer failed"); }
        }
        while (due.TryPeek(out var t, out _) && t.Cancelled) due.Dequeue();
        if (due.TryPeek(out _, out var next)) wake.Change(TimeSpan.FromMilliseconds(Math.Max(1, next.Due - ClockMs)), Timeout.InfiniteTimeSpan);
    }

    // ---- People --------------------------------------------------------------------------------------------------

    private sealed class Person(Guid id)
    {
        public Guid Id { get; } = id;
        public Card? Card { get; set; }
        public Table? Table { get; set; }
        /// <summary>0–3 seated, -1 spectating (with <see cref="Table"/> set) or nowhere.</summary>
        public int Seat { get; set; } = -1;
        public RankedEntry? Queue { get; set; }
    }

    private readonly Dictionary<Guid, Person> people = [];
    /// <summary>Who follows the lobby list (lobbySubscribe): the lobby broadcasts go to them, not through everyone ever seen.</summary>
    private readonly HashSet<Person> subscribers = [];

    /// <summary>An offline account the hub holds nothing for is let go (its card is read again when it comes back).</summary>
    private void ForgetIfIdle(Person p)
    {
        if (p.Table is null && p.Queue is null && !live.IsOnline(p.Id) && lobby.Where(p.Id) is null) { subscribers.Remove(p); people.Remove(p.Id); }
    }

    private Person P(Guid id) => people.TryGetValue(id, out var p) ? p : people[id] = new Person(id);

    private async Task<Card?> CardAsync(Person p, bool refresh = false)
    {
        if (p.Card is null || refresh) p.Card = await store.CardAsync(p.Id) ?? p.Card;
        return p.Card;
    }

    private V4User Pub(Person p) => Pub(p.Card, p.Id);

    private V4User Pub(Card? c, Guid id) => new()
    {
        Id = id.ToString(), Name = c?.Name ?? "…", Ini = c?.Ini ?? "?", Color = c?.Color ?? "#3D7BFF", Level = c?.Level ?? 1, Frame = c?.Frame ?? 0,
        Status = c?.Status ?? "", RankIdx = c?.RankIdx ?? 0, Online = live.IsOnline(id), InGame = people.TryGetValue(id, out var p) && p.Table is not null,
    };

    private V4Me Me(Person p)
    {
        var c = p.Card;
        var u = Pub(p);
        return new V4Me
        {
            Id = u.Id, Name = u.Name, Ini = u.Ini, Color = u.Color, Level = u.Level, Frame = u.Frame, Status = u.Status, RankIdx = u.RankIdx,
            Online = u.Online, InGame = u.InGame, Xp = c?.Xp ?? 0, XpNext = c?.XpNext ?? 375, Chips = c?.Chips ?? 0, Gems = c?.Gems ?? 0,
            Code = c?.Code ?? "", Skin = c?.Skin ?? "classic", Skins = c?.Skins ?? [], Rank = c?.Rank ?? new V4Rank(),
        };
    }

    private void Send(Guid account, string type, object payload) => live.Send(account, type, payload);

    /// <summary>A balance change of the hub (bet, betRefund, win, gift): the cached card follows the ledger.</summary>
    private void Balance(Person p, long chipsAfter, long gemsAfter, long dChips, string reason, string? reference)
    {
        if (p.Card is { } c) p.Card = c with { Chips = chipsAfter, Gems = gemsAfter };
        Send(p.Id, V4Messages.Balance, new V4Balance { Chips = chipsAfter, Gems = gemsAfter, DChips = dChips, Reason = reason, Ref = reference, At = now });
    }

    // ---- Messages ------------------------------------------------------------------------------------------------

    public bool Handles(string type) => Types.Contains(type);

    public Task<string?> HandleAsync(Connection connection, Inbound message, CancellationToken ct) => throw new NotSupportedException("v4 messages are answered by ReplyAsync.");

    public Task<Reply> ReplyAsync(Connection connection, Inbound message, CancellationToken ct) => Run(() => DispatchAsync(P(connection.Account), message));

    public Task OnDisconnectedAsync(Guid account) => Run(async () => { await DisconnectedAsync(account); return true; });

    private static T Body<T>(Inbound m) where T : new() =>
        m.Data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? new T() : m.Data.Deserialize<T>(Connection.Json) ?? new T();

    private async Task<Reply> DispatchAsync(Person p, Inbound m)
    {
        if (p.Card is null) await CardAsync(p);
        return m.Type switch
        {
            V4Messages.Hello => await HelloAsync(p, Body<V4HelloRequest>(m)),
            V4Messages.Resync => Resync(p),
            V4Messages.LobbySubscribe => LobbySubscribe(p),
            V4Messages.LobbyUnsubscribe => LobbyUnsubscribe(p),
            V4Messages.TableCreate => await TableCreateAsync(p, Body<V4TableCreateRequest>(m)),
            V4Messages.TableJoin => await TableJoinAsync(p, Body<V4TableJoinRequest>(m)),
            V4Messages.TableJoinCode => await TableJoinCodeAsync(p, Body<V4TableJoinCodeRequest>(m)),
            V4Messages.TableSpectate => TableSpectate(p, Body<V4TableSpectateRequest>(m)),
            V4Messages.TableLeave => await TableLeaveAsync(p),
            V4Messages.SeatAddBot => SeatAddBot(p, Body<V4SeatRequest>(m)),
            V4Messages.SeatKick => await SeatKickAsync(p, Body<V4SeatRequest>(m)),
            V4Messages.TableStart => TableStart(p),
            V4Messages.InviteSend => await InviteSendAsync(p, Body<V4InviteSendRequest>(m)),
            V4Messages.InviteRespond => await InviteRespondAsync(p, Body<V4InviteRespondRequest>(m)),
            V4Messages.InviteCancel => InviteCancel(p, Body<V4InviteCancelRequest>(m)),
            V4Messages.VsBotsStart => await VsBotsStartAsync(p, Body<V4VsBotsStartRequest>(m)),
            V4Messages.RankedQueue => await RankedQueueAsync(p, Body<V4RankedQueueRequest>(m)),
            V4Messages.RankedAccept => await RankedAcceptAsync(p, Body<V4RankedAcceptRequest>(m)),
            V4Messages.RankedCancel => RankedCancel(p),
            V4Messages.ChatSend => ChatSend(p, Body<V4ChatSendRequest>(m)),
            V4Messages.GiftSend => await GiftSendAsync(p, m),
            _ => GameMessage(p, m),
        };
    }

    // ---- Session -------------------------------------------------------------------------------------------------

    /// <summary>
    /// hello: the socket is signed in by its ticket (the package's token is accepted as is). The player is back: the server
    /// stops playing their seat and a running turn starts over (D-36); session + snapshot say where to return.
    /// </summary>
    private async Task<Reply> HelloAsync(Person p, V4HelloRequest request)
    {
        if (await CardAsync(p, refresh: true) is null) return Reply.Fail(V4Errors.E_SESSION_EXPIRED);
        var t = p.Table;
        if (t is not null && p.Seat >= 0) t.Run?.Reconnect(p.Seat);
        Send(p.Id, V4Messages.Session, new V4Session { Me = Me(p), ServerVersion = ProtocolVersion, ServerTime = now, At = now });
        Snapshot(p);
        if (t is not null && p.Seat >= 0) t.Run?.AnnounceSeat(p.Seat);
        return Reply.With(("token", request.Token));
    }

    private Reply Resync(Person p) { Snapshot(p); return Reply.Ok; }

    /// <summary>The full state and the screen a returning client goes to (protocol.json reconnect.screens).</summary>
    private void Snapshot(Person p)
    {
        var s = new V4Snapshot { Me = Me(p), Screen = "menu", At = now };
        if (p.Table is { } t)
        {
            s.Table = TablePub(t);
            if (t.Status is "waiting" or "countdown") s.Screen = t.Ranked ? "ranked.loading" : "waitroom";
            else { s.Screen = "table"; s.Game = t.Run?.GameView(p.Seat); }
            if (p.Seat < 0) s.Spectator = true;
        }
        else if (p.Queue is { } q) { s.Screen = q.Found is null ? "ranked.search" : "ranked.found"; s.Ranked = QueuePub(q); }
        Send(p.Id, V4Messages.Snapshot, s);
    }

    private async Task DisconnectedAsync(Guid account)
    {
        if (!people.TryGetValue(account, out var p)) return;
        subscribers.Remove(p);
        if (p.Table is { } t)
        {
            if (p.Seat < 0) { t.Spectators.Remove(p.Id); p.Table = null; }
            // Waiting room: out at once, stake back (R-66, reconnectWaitRoom 0). In the match the seat is kept (R-66); a ranked
            // table being prepared is already the match (its seat stays, the server plays it when its turns time out).
            else if (t.Status is "waiting" or "countdown" && !t.Ranked) await LeaveWaitRoomAsync(t, p, "disconnect");
            else t.Run?.Disconnect(p.Seat);
        }
        ForgetIfIdle(p);
    }

    /// <summary>The account is deleted or banned: out of the queue, the waiting room or the match (a forfeit, R-60).</summary>
    public Task RemoveAsync(Guid account) => Run(async () =>
    {
        if (!people.TryGetValue(account, out var p)) return true;
        if (p.Queue is { } q) { var f = q.Found; Unqueue(q, "cancel"); q.Found = null; if (f is not null) { Release(f, q); TryMatch(); } }
        if (p.Table is not null) await TableLeaveAsync(p);
        return true;
    });

    /// <summary>A block set or lifted over HTTP: the blocker's table chat filter follows at once (it was read when they sat).</summary>
    public Task BlockChangedAsync(Guid blocker, Guid blocked, bool on) => Run(() =>
    {
        if (people.TryGetValue(blocker, out var p) && p.Table is { Closed: false } t)
        {
            if (on) t.Blocks.Add((blocker, blocked));
            else t.Blocks.Remove((blocker, blocked));
        }
        return Task.FromResult(true);
    });

    // ---- Game intents ----------------------------------------------------------------------------------------------

    private Reply GameMessage(Person p, Inbound m)
    {
        if (p.Table is not { } t || t.Run is null) return Reply.Fail(V4Errors.E_NOT_SEATED);
        if (p.Seat < 0) return Reply.Fail(V4Errors.E_SPECTATOR);
        var run = t.Run;
        switch (m.Type)
        {
            case V4Messages.GRollDice:
                return run.RollDice(p.Seat) is { } rollError ? Reply.Fail(rollError) : Reply.Ok;
            case V4Messages.GReady:
                return run.ReadyUp(p.Seat) is { } readyError ? Reply.Fail(readyError) : Reply.Ok;
            case V4Messages.GRackOrder:
                return run.RackOrder(p.Seat, Body<V4RackOrderRequest>(m).Tiles) is { } rackError ? Reply.Fail(rackError) : Reply.Ok;
        }
        GameAction action = m.Type switch
        {
            V4Messages.GDraw => new GameAction { T = "draw", Src = Body<V4DrawRequest>(m).Src },
            V4Messages.GReturnSide => new GameAction { T = "returnSide" },
            V4Messages.GOpen => new GameAction { T = "open", Melds = Melds(Body<V4MeldsRequest>(m)) },
            V4Messages.GLay => new GameAction { T = "lay", Melds = Melds(Body<V4MeldsRequest>(m)) },
            V4Messages.GAdd => Add(Body<V4AddRequest>(m)),
            V4Messages.GSwap => Swap(Body<V4SwapRequest>(m)),
            V4Messages.GUndo => new GameAction { T = "undo" },
            V4Messages.GDiscard => new GameAction { T = "discard", Tile = Body<V4DiscardRequest>(m).Tile },
            _ => throw new InvalidOperationException("Unknown v4 game message."),
        };
        var error = run.Move(p.Seat, action, out var detail);
        return error is null ? Reply.Ok : Reply.Fail(error, detail);
    }

    private static List<MeldSpec> Melds(V4MeldsRequest r) => [.. (r.Melds ?? []).Select(x => new MeldSpec(x?.Kind, x?.Tiles ?? []))];

    /// <summary>
    /// gAdd: the protocol names the side "L" / "R" (or null for a group); the engine "pre" / "post" (logic/rules.js addSides).
    /// Both are accepted (docs/v4/hat-b/EKSIKLER.md).
    /// </summary>
    private static GameAction Add(V4AddRequest r) => new()
    {
        T = "add", Tile = r.Tile, Meld = r.Meld, Side = r.Side switch { "L" => "pre", "R" => "post", _ => r.Side },
    };

    private static GameAction Swap(V4SwapRequest r) => new() { T = "swap", Tile = r.Tile, Meld = r.Meld, Index = r.Index };
}

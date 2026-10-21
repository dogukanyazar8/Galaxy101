using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Okey101.Core.Model;
using Okey101.Core.Turns;

namespace Okey101.Application
{
    public sealed class LocalSession : IDisposable
    {
        private readonly object gate = new object();
        private readonly SemaphoreSlim serial = new SemaphoreSlim(1, 1);
        private CancellationTokenSource epoch = new CancellationTokenSource();
        private RoundState state;
        private IReadOnlyList<VisibleEvent> lastEvents;
        private int lastActor;
        private bool lastDrawWasPrivate;
        private bool paused;
        private bool disposed;
        private int pendingCommands;
        private bool serialDisposed;
        private RoundState replayInitial;
        private readonly List<GameCommand> replayCommands = new List<GameCommand>();
        private bool replayAvailable;

        public bool IsPaused { get { lock (gate) return paused; } }
        public bool IsDisposed { get { lock (gate) return disposed; } }
        public void StartNextRound(uint seed)
        {
            CancellationTokenSource previous;
            lock(gate)
            {
                EnsureOpen();
                if(state?.Result==null)throw new InvalidOperationException("Finish the current round before dealing again.");
                var starter=(state.StartingPlayer+(state.Result.FinishInfo.Reason==FinishReason.AllPairs?0:1))%4;
                var initial=RoundFactory.Create(seed,Guid.NewGuid(),starter);
                previous=epoch;epoch=new CancellationTokenSource();state=initial;paused=false;lastEvents=null;
                replayInitial=initial;replayCommands.Clear();replayAvailable=true;
            }
            previous.Cancel();previous.Dispose();
        }
        public ReplayRecord CaptureReplay()
        {
            lock (gate)
            {
                EnsureOpen();
                if (!replayAvailable) throw new InvalidOperationException("Replay is unavailable: no round or the recording limit was reached.");
                return new ReplayRecord(replayInitial, replayCommands, ReplayCodec.Hash(state));
            }
        }

        public void Start(uint seed, int? starter)
            => Start(RoundFactory.Create(seed, Guid.NewGuid(), starter));

        public void Start(RoundState initial)
        {
            if (!StateInvariant.ValidateAuthority(initial).Success)
                throw new ArgumentException("A session requires a valid authority snapshot.", nameof(initial));
            CancellationTokenSource previous;
            lock (gate)
            {
                EnsureOpen();
                previous = epoch; epoch = new CancellationTokenSource();
                state = initial; paused = false; lastEvents = null;
                replayInitial = initial; replayCommands.Clear(); replayAvailable = true;
            }
            previous.Cancel(); previous.Dispose();
        }

        public PlayerView GetView(int actor)
            => GetView(actor, out _);

        public Task<RuleResult<bool>> RestoreReplayAsync(ReplayRecord record, CancellationToken token)
        {
            lock (gate)
            {
                EnsureOpen();
                if (paused || state == null || token.IsCancellationRequested) return Task.FromResult(RuleResult<bool>.Fail(ErrorCode.Cancelled));
                var work = CancellationTokenSource.CreateLinkedTokenSource(token, epoch.Token);
                return RestoreReplayAsync(record, state, epoch, work);
            }
        }
        private async Task<RuleResult<bool>> RestoreReplayAsync(ReplayRecord record, RoundState expected,
            CancellationTokenSource expectedEpoch, CancellationTokenSource work)
        {
            using (work)
            {
                try
                {
                    var restored = await Task.Run(() => ReplayCodec.Restore(record, work.Token)).ConfigureAwait(false);
                    if (!restored.Success) return RuleResult<bool>.Fail(restored.Error);
                    CancellationTokenSource previous;
                    lock (gate)
                    {
                        if (WorkStatus(expectedEpoch, work.Token) != ErrorCode.None || !ReferenceEquals(state, expected))
                            return RuleResult<bool>.Fail(ErrorCode.Cancelled);
                        previous = epoch; epoch = new CancellationTokenSource(); state = restored.Value; paused = false; lastEvents = null;
                        replayInitial = record.InitialState; replayCommands.Clear(); replayCommands.AddRange(record.Commands); replayAvailable = true;
                    }
                    previous.Cancel(); previous.Dispose(); return RuleResult<bool>.Ok(true);
                }
                catch (OperationCanceledException) when (work.IsCancellationRequested) { return RuleResult<bool>.Fail(ErrorCode.Cancelled); }
            }
        }

        public PlayerView GetView(int actor, out CancellationToken workToken)
        {
            lock (gate)
            {
                EnsureOpen();
                if (state == null) throw new InvalidOperationException("Start the session before reading a view.");
                workToken = epoch.Token;
                return PlayerViewProjector.ForPlayer(state, actor);
            }
        }

        public Task<CommandReceipt> SubmitAsync(GameCommand command, CancellationToken token)
        {
            lock (gate)
            {
                if (disposed) return Task.FromResult(Failure(ErrorCode.Cancelled));
                if (paused) return Task.FromResult(Failure(ErrorCode.WrongPhase));
                if (token.IsCancellationRequested) return Task.FromResult(Failure(ErrorCode.Cancelled));
                if (state == null || command == null) return Task.FromResult(Failure(ErrorCode.InvalidState));
                var linked = CancellationTokenSource.CreateLinkedTokenSource(token, epoch.Token);
                pendingCommands++;
                return ExecuteAsync(command, epoch, linked);
            }
        }

        private async Task<CommandReceipt> ExecuteAsync(GameCommand command, CancellationTokenSource expectedEpoch,
            CancellationTokenSource linked)
        {
            var acquired = false;
            try
            {
                await serial.WaitAsync(linked.Token).ConfigureAwait(false);
                acquired = true;
                RoundState before;
                lock (gate)
                {
                    var status = WorkStatus(expectedEpoch, linked.Token);
                    if (status != ErrorCode.None) return Failure(status);
                    before = state;
                }
                var result = await Task.Run(() => GameEngine.Apply(before, command, linked.Token)).ConfigureAwait(false);
                var events = result.Success ? Events(before, result.Value, command) : null;
                lock (gate)
                {
                    var status = WorkStatus(expectedEpoch, linked.Token);
                    if (status != ErrorCode.None) return Failure(status);
                    if (!result.Success) return Failure(result.Error);
                    state = result.Value;
                    if (replayAvailable)
                    {
                        if (replayCommands.Count == ReplayCodec.MaxCommands) { replayAvailable = false; replayCommands.Clear(); }
                        else replayCommands.Add(command);
                    }
                    lastEvents = events;
                    lastActor = command.Meta.Actor;
                    lastDrawWasPrivate = command is DrawCommand draw && draw.Source == DrawSource.Stock;
                    return ReceiptFor(lastActor);
                }
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested)
            {
                lock (gate) return Failure(WorkStatus(expectedEpoch, linked.Token));
            }
            finally
            {
                if (acquired) serial.Release();
                linked.Dispose();
                lock (gate)
                {
                    pendingCommands--;
                    DisposeSerialWhenIdle();
                }
            }
        }

        public CommandReceipt GetLastAcceptedReceipt(int viewer)
        {
            if (viewer < 0 || viewer > 3) throw new ArgumentOutOfRangeException(nameof(viewer));
            lock (gate)
            {
                EnsureOpen();
                return lastEvents == null ? null : ReceiptFor(viewer);
            }
        }

        private CommandReceipt ReceiptFor(int viewer)
        {
            var visible = new VisibleEvent[lastEvents.Count];
            for (var i = 0; i < visible.Length; i++)
            {
                var item = lastEvents[i];
                visible[i] = lastDrawWasPrivate && viewer != lastActor && item.Kind == VisibleEventKind.Draw
                    ? new VisibleEvent(item.Kind, item.Actor, null, item.MeldId) : item;
            }
            return new CommandReceipt(true, ErrorCode.None, state.Version, visible);
        }

        public void SetPaused(bool value)
        {
            CancellationTokenSource previous;
            lock (gate)
            {
                EnsureOpen();
                if (paused == value) return;
                paused = value;
                previous = epoch; epoch = new CancellationTokenSource();
            }
            previous.Cancel(); previous.Dispose();
        }

        private ErrorCode WorkStatus(CancellationTokenSource expectedEpoch, CancellationToken token)
        {
            if (disposed) return ErrorCode.Cancelled;
            if (paused) return ErrorCode.WrongPhase;
            return !ReferenceEquals(epoch, expectedEpoch) || token.IsCancellationRequested ? ErrorCode.Cancelled : ErrorCode.None;
        }

        private CommandReceipt Failure(ErrorCode error)
            => new CommandReceipt(false, error, state == null ? 0 : state.Version, Array.Empty<VisibleEvent>());

        private void EnsureOpen()
        {
            if (disposed) throw new ObjectDisposedException(nameof(LocalSession));
        }

        public void Dispose()
        {
            CancellationTokenSource previous;
            lock (gate)
            {
                if (disposed) return;
                disposed = true; paused = true; previous = epoch;
                DisposeSerialWhenIdle();
            }
            previous.Cancel(); previous.Dispose();
        }

        private void DisposeSerialWhenIdle()
        {
            if (disposed && pendingCommands == 0 && !serialDisposed)
            {
                serialDisposed = true; serial.Dispose();
            }
        }

        private static IReadOnlyList<VisibleEvent> Events(RoundState before, RoundState after, GameCommand command)
        {
            var events = new List<VisibleEvent>();
            var actor = command.Meta.Actor;
            if (command is DrawCommand)
                events.Add(new VisibleEvent(VisibleEventKind.Draw, actor, after.PendingDraw.Tile));
            else if (command is CommitTurnCommand commit)
            {
                long nextId = 0;
                foreach (var meld in before.Table) if (meld.Id >= nextId) nextId = (long)meld.Id + 1;
                foreach (var action in commit.Plan.Actions)
                    switch (action)
                    {
                        case OpenGroups open:
                            foreach (var group in open.Groups)
                                events.Add(new VisibleEvent(VisibleEventKind.GroupsOpened, actor, null, (int)nextId++));
                            break;
                        case AddGroup _:
                            events.Add(new VisibleEvent(VisibleEventKind.GroupAdded, actor, null, (int)nextId++));
                            break;
                        case ExtendMeld extend:
                            foreach (var tile in extend.Tiles)
                                events.Add(new VisibleEvent(VisibleEventKind.GroupExtended, actor, tile.Id, extend.MeldId));
                            break;
                        case SwapJoker swap:
                            events.Add(new VisibleEvent(VisibleEventKind.JokerSwapped, actor, swap.ReplacementId, swap.MeldId));
                            break;
                    }
                events.Add(new VisibleEvent(VisibleEventKind.Discard, actor, commit.Plan.Discard));
                if (after.Result != null) events.Add(new VisibleEvent(after.Phase == RoundPhase.Cancelled
                    ? VisibleEventKind.RoundCancelled : VisibleEventKind.RoundFinished, actor));
            }
            return events.AsReadOnly();
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Okey101.Application;
using Okey101.Core.Model;

namespace Okey101.Bots
{
    public sealed class BotRunner : IDisposable
    {
        private readonly LocalSession session;
        private readonly BotPlanner planner = new BotPlanner();
        private readonly SemaphoreSlim serial = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly object gate = new object();
        private int pending;
        private bool disposed;
        private bool cleaned;
        private bool cancellationIssued;
        public BotRunner(LocalSession session) { this.session = session ?? throw new ArgumentNullException(nameof(session)); }
        public Task RunOneTurnAsync(int actor, CancellationToken token)
        {
            lock (gate)
            {
                if (disposed) throw new ObjectDisposedException(nameof(BotRunner));
                var view = session.GetView(actor, out var epoch);
                if (session.IsPaused || view.Actor != view.ActivePlayer || view.Result != null || token.IsCancellationRequested) return Task.CompletedTask;
                // Capture the observation and its session epoch before waiting. Duplicate
                // requests cannot be relabeled with a later turn's version after the queue.
                var linked = CancellationTokenSource.CreateLinkedTokenSource(token, epoch, lifetime.Token);
                pending++; return Run(view, linked);
            }
        }
        private async Task Run(PlayerView initial, CancellationTokenSource linked)
        {
            var acquired = false;
            try
            {
                await serial.WaitAsync(linked.Token).ConfigureAwait(false); acquired = true;
                if (!Fresh(initial, linked.Token)) return;
                var view = initial; TurnPlan witness = null;
                if (view.Phase == RoundPhase.AwaitingDraw)
                {
                    var draw = await Task.Run(() => planner.ChooseDraw(view, linked.Token)).ConfigureAwait(false);
                    if (!Fresh(view, linked.Token)) return;
                    var receipt = await session.SubmitAsync(new DrawCommand(Meta(view), draw.Source), linked.Token).ConfigureAwait(false);
                    if (!receipt.Accepted) { CheckRejection(receipt); return; }
                    linked.Token.ThrowIfCancellationRequested();
                    var drawn = session.GetView(view.Actor);
                    if (drawn.RoundId != view.RoundId || drawn.Version != view.Version + 1 || drawn.ActivePlayer != view.Actor || drawn.Phase != RoundPhase.PlanningTurn) return;
                    view = drawn; witness = draw.Witness;
                }
                if (view.Phase != RoundPhase.PlanningTurn || !Fresh(view, linked.Token)) return;
                var plan = await Task.Run(() => planner.PlanTurn(view, witness, linked.Token)).ConfigureAwait(false);
                if (!Fresh(view, linked.Token)) return;
                var committed = await session.SubmitAsync(new CommitTurnCommand(Meta(view), plan), linked.Token).ConfigureAwait(false);
                if (!committed.Accepted) CheckRejection(committed);
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (linked.IsCancellationRequested) { }
            finally
            {
                if (acquired) serial.Release();
                linked.Dispose();
                lock (gate) { pending--; CleanupWhenIdle(); }
            }
        }
        private bool Fresh(PlayerView expected, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); if (session.IsPaused) return false;
            var current = session.GetView(expected.Actor);
            return current.RoundId == expected.RoundId && current.Version == expected.Version && current.ActivePlayer == expected.Actor
                && current.Phase == expected.Phase && current.Result == null;
        }
        private static CommandMeta Meta(PlayerView view) => new CommandMeta(view.RoundId, view.Actor, view.OwnLastSequence + 1, view.Version);
        private static void CheckRejection(CommandReceipt receipt)
        {
            switch (receipt.Error)
            {
                case ErrorCode.Cancelled: case ErrorCode.WrongRound: case ErrorCode.StaleVersion:
                case ErrorCode.WrongPlayer: case ErrorCode.WrongPhase: case ErrorCode.DuplicateCommand: return;
                default: throw new InvalidOperationException("A bot-generated command was rejected: " + receipt.Error);
            }
        }
        public void Dispose()
        {
            lock (gate) { if (disposed) return; disposed = true; }
            lifetime.Cancel();
            lock (gate) { cancellationIssued = true; CleanupWhenIdle(); }
        }
        private void CleanupWhenIdle()
        {
            if (!disposed || !cancellationIssued || pending != 0 || cleaned) return;
            cleaned = true; serial.Dispose(); lifetime.Dispose();
        }
    }
}

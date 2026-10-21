using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Okey101.Application;
using Okey101.Core.Model;

namespace Okey101.Application.Tests
{
    public sealed class PlayerViewTests
    {
        [Test]
        public void OpponentFacesAndStockOrderAreAbsent()
        {
            using (var session = new LocalSession())
            {
                session.Start(71, 0);
                Assert.That(session.GetView(0).OwnHand.Count, Is.EqualTo(22));
                Assert.That(session.GetView(1).OwnHand.Count, Is.EqualTo(21));
                Assert.That(typeof(PlayerView).GetProperty("Stock"), Is.Null);
                Assert.That(typeof(PublicPlayerInfo).GetProperty("Hand"), Is.Null);
                Assert.That(session.GetView(1).PendingDraw, Is.Null);
            }
        }

        [Test]
        public async Task PublicDrawEventHidesTileFromOthers()
        {
            using (var session = await WaitingSession())
            {
                var view = session.GetView(1);
                var receipt = await session.SubmitAsync(new DrawCommand(Meta(view), DrawSource.Stock), CancellationToken.None);
                Assert.That(receipt.Accepted, Is.True, receipt.Error.ToString());
                Assert.That(receipt.Events.Single(e => e.Kind == VisibleEventKind.Draw).TileId, Is.Not.Null);
                Assert.That(session.GetLastAcceptedReceipt(0).Events.Single(e => e.Kind == VisibleEventKind.Draw).TileId, Is.Null);
                Assert.That(session.GetLastAcceptedReceipt(1).Events.Single(e => e.Kind == VisibleEventKind.Draw).TileId, Is.Not.Null);
                Assert.That(session.GetView(0).PendingDraw, Is.Null);
                Assert.That(session.GetView(1).PendingDraw, Is.Not.Null);
            }
        }

        [Test]
        public async Task ConcurrentDuplicateDrawAcceptsExactlyOnce()
        {
            using (var session = await WaitingSession())
            {
                var view = session.GetView(1);
                var command = new DrawCommand(Meta(view), DrawSource.Stock);
                var receipts = await Task.WhenAll(session.SubmitAsync(command, CancellationToken.None),
                    session.SubmitAsync(command, CancellationToken.None));
                Assert.That(receipts.Count(r => r.Accepted), Is.EqualTo(1));
                Assert.That(receipts.Single(r => !r.Accepted).Error, Is.EqualTo(ErrorCode.DuplicateCommand));
                Assert.That(session.GetView(1).Version, Is.EqualTo(view.Version + 1));
            }
        }

        [Test]
        public async Task PausedCommandsAreRejectedAndResumeKeepsState()
        {
            using (var session = new LocalSession())
            {
                session.Start(71, 0);
                var before = session.GetView(0);
                session.SetPaused(true);
                Assert.That(session.IsPaused, Is.True);
                var receipt = await session.SubmitAsync(new CommitTurnCommand(Meta(before), Plan(before)), CancellationToken.None);
                Assert.That(receipt.Error, Is.EqualTo(ErrorCode.WrongPhase));
                Assert.That(session.GetView(0).Version, Is.EqualTo(before.Version));
                session.SetPaused(false);
                Assert.That((await session.SubmitAsync(new CommitTurnCommand(Meta(before), Plan(before)), CancellationToken.None)).Accepted, Is.True);
            }
        }

        [Test]
        public async Task PauseInvalidatesQueuedWorkAtSnapshotBoundary()
        {
            using (var session = await WaitingSession())
            {
                var view = session.GetView(1);
                var command = new DrawCommand(Meta(view), DrawSource.Stock);
                var pending = Enumerable.Range(0, 32).Select(_ => session.SubmitAsync(command, CancellationToken.None)).ToArray();
                session.SetPaused(true);
                var boundary = session.GetView(1).Version;
                session.SetPaused(false);
                await Task.WhenAll(pending);
                Assert.That(session.GetView(1).Version, Is.EqualTo(boundary));
            }
        }

        [Test]
        public async Task RestartInvalidatesOldRoundWork()
        {
            using (var session = new LocalSession())
            {
                session.Start(71, 0);
                var old = session.GetView(0);
                var command = new CommitTurnCommand(Meta(old), Plan(old));
                var pending = Enumerable.Range(0, 16).Select(_ => session.SubmitAsync(command, CancellationToken.None)).ToArray();
                session.Start(72, 0);
                var fresh = session.GetView(0);
                await Task.WhenAll(pending);
                Assert.That(session.GetView(0).RoundId, Is.EqualTo(fresh.RoundId).And.Not.EqualTo(old.RoundId));
                Assert.That(session.GetView(0).Version, Is.Zero);
            }
        }

        [Test]
        public async Task CancelledAndDisposedCommandsCannotChangeAuthority()
        {
            var session = new LocalSession();
            session.Start(71, 0);
            var view = session.GetView(0);
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var rejected = await session.SubmitAsync(new CommitTurnCommand(Meta(view), Plan(view)), cancellation.Token);
                Assert.That(rejected.Error, Is.EqualTo(ErrorCode.Cancelled));
                Assert.That(session.GetView(0).Version, Is.Zero);
            }
            var command = new CommitTurnCommand(Meta(view), Plan(view));
            var pending = Enumerable.Range(0, 16).Select(_ => session.SubmitAsync(command, CancellationToken.None)).ToArray();
            session.Dispose();
            await Task.WhenAll(pending);
            Assert.That((await session.SubmitAsync(command, CancellationToken.None)).Error, Is.EqualTo(ErrorCode.Cancelled));
            Assert.Throws<ObjectDisposedException>(() => session.GetView(0));
            session.Dispose();
        }

        [Test]
        public void DraftDoesNotChangeSnapshotAndRequiresExplicitDiscard()
        {
            using (var session = new LocalSession())
            {
                session.Start(71, 0);
                var view = session.GetView(0);
                var draft = new TurnDraft(); draft.Reset(view);
                Assert.Throws<InvalidOperationException>(() => draft.Build());
                draft.SelectDiscard(view.OwnHand[0]);
                Assert.That(draft.Build().Discard, Is.EqualTo(view.OwnHand[0]));
                Assert.That(session.GetView(0).Version, Is.Zero);
                draft.ClearDiscard();
                Assert.Throws<InvalidOperationException>(() => draft.Build());
                draft.SetPlan(Plan(view));
                Assert.That(draft.Build().Discard, Is.EqualTo(view.OwnHand[0]));
            }
        }

        private static async Task<LocalSession> WaitingSession()
        {
            var session = new LocalSession(); session.Start(71, 0);
            var view = session.GetView(0);
            var receipt = await session.SubmitAsync(new CommitTurnCommand(Meta(view), Plan(view)), CancellationToken.None);
            Assert.That(receipt.Accepted, Is.True, receipt.Error.ToString());
            return session;
        }
        private static CommandMeta Meta(PlayerView view) => new CommandMeta(view.RoundId, view.Actor, view.OwnLastSequence + 1, view.Version);
        private static TurnPlan Plan(PlayerView view) => new TurnPlan(Array.Empty<TableAction>(), view.OwnHand[0], true);
    }
}

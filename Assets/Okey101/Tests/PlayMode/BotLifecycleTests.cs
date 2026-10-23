using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Okey101.Application;
using Okey101.Bots;
using Okey101.Core.Model;
using Okey101.Presentation;
using Okey101.TestSupport;
using UnityEngine;
using UnityEngine.TestTools;

namespace Okey101.PlayMode.Tests
{
    public sealed class BotLifecycleTests
    {
        [UnityTest]
        public IEnumerator RunnerOpensAndWinsThroughSession()
        {
            using (var session = new LocalSession()) using (var runner = new BotRunner(session))
            {
                session.Start(PracticeDeals.Create("Finish"));
                yield return Await(runner.RunOneTurnAsync(0, CancellationToken.None));
                Assert.That(session.GetView(0).Result, Is.Not.Null);
                Assert.That(session.GetView(0).Result.FinishInfo.Winner, Is.EqualTo(0));
            }
        }

        [UnityTest]
        public IEnumerator NewRoundRejectsOldBotResult()
        {
            using (var session = new LocalSession()) using (var runner = new BotRunner(session))
            {
                session.Start(PracticeDeals.Create("Finish"));
                var old = runner.RunOneTurnAsync(0, CancellationToken.None);
                session.Start(72, 0); var fresh = session.GetView(0);
                yield return Await(old);
                Assert.That(session.GetView(0).RoundId, Is.EqualTo(fresh.RoundId));
                Assert.That(session.GetView(0).Version, Is.Zero);
                Assert.That(session.GetView(0).OwnHand, Is.EqualTo(fresh.OwnHand));
            }
        }

        [UnityTest]
        public IEnumerator PauseAndResumeHasExactlyOneAction()
        {
            using (var session = new LocalSession()) using (var runner = new BotRunner(session))
            {
                var ready = PrepareWaiting(session); yield return Await(ready); Assert.That(ready.Result.Accepted, Is.True);
                session.SetPaused(true); var version = session.GetView(1).Version;
                yield return Await(runner.RunOneTurnAsync(1, CancellationToken.None));
                Assert.That(session.GetView(1).Version, Is.EqualTo(version));
                session.SetPaused(false);
                yield return Await(Task.WhenAll(runner.RunOneTurnAsync(1, CancellationToken.None), runner.RunOneTurnAsync(1, CancellationToken.None)));
                Assert.That(session.GetView(1).Version, Is.EqualTo(version + 2), "Exactly one draw and one commit.");
                // One draw: from the stock (19 left) or the previous player's discard (20 left), depending on the deal.
                Assert.That(session.GetView(1).StockCount, Is.InRange(19, 20));
                Assert.That(session.GetView(1).ActivePlayer, Is.EqualTo(2));
            }
        }

        [UnityTest]
        public IEnumerator PauseInvalidatesRunningWorkAcrossResume()
        {
            using (var session = new LocalSession()) using (var runner = new BotRunner(session))
            {
                yield return Await(PrepareWaiting(session));
                var pending = runner.RunOneTurnAsync(1, CancellationToken.None);
                session.SetPaused(true); var boundary = session.GetView(1);
                session.SetPaused(false); yield return Await(pending);
                Assert.That(session.GetView(1).Version, Is.EqualTo(boundary.Version));
                Assert.That(session.GetView(1).OwnHand, Is.EqualTo(boundary.OwnHand));
                var commands = boundary.ActivePlayer != 1 ? 0 : boundary.Phase == RoundPhase.AwaitingDraw ? 2 : 1;
                yield return Await(runner.RunOneTurnAsync(1, CancellationToken.None));
                Assert.That(session.GetView(1).Version, Is.EqualTo(boundary.Version + commands));
            }
        }
        private static Task<CommandReceipt> PrepareWaiting(LocalSession session)
        {
            session.Start(71, 0); var view = session.GetView(0);
            return session.SubmitAsync(new CommitTurnCommand(new CommandMeta(view.RoundId, 0, 1, view.Version),
                new TurnPlan(Array.Empty<TableAction>(), view.OwnHand[0], true)), CancellationToken.None);
        }

        [UnityTest]
        public IEnumerator ResumeAfterAcceptedPreviousDrawUsesWitness()
        {
            using (var session = new LocalSession()) using (var runner = new BotRunner(session))
            {
                TileId Id(TileColor color, int number) => new TileId(2 * (13 * (int)color + number - 1));
                var initial = new RoundTestBuilder().WithIndicator(Id(TileColor.Black, 13)).WithOpening(0, OpeningKind.Series)
                    .WithTurn(0, RoundPhase.AwaitingDraw).WithHand(0, Id(TileColor.Red, 2), Id(TileColor.Red, 3), Id(TileColor.Black, 12))
                    .WithHand(1, Id(TileColor.Yellow, 10)).WithHand(2, Id(TileColor.Blue, 10)).WithHand(3, Id(TileColor.Black, 10))
                    .WithDiscard(3, Id(TileColor.Red, 1)).Build();
                session.Start(initial); var view = session.GetView(0);
                var draw = session.SubmitAsync(new DrawCommand(new CommandMeta(view.RoundId, 0, 1, view.Version), DrawSource.PreviousDiscard), CancellationToken.None);
                yield return Await(draw); Assert.That(draw.Result.Accepted, Is.True);
                session.SetPaused(true); session.SetPaused(false);
                yield return Await(runner.RunOneTurnAsync(0, CancellationToken.None));
                Assert.That(session.GetView(0).Version, Is.EqualTo(initial.Version + 2));
                Assert.That(session.GetView(0).StockCount, Is.EqualTo(initial.Stock.Count));
                Assert.That(session.GetView(0).Result.FinishInfo.Winner, Is.EqualTo(0));
            }
        }

        [UnityTest]
        public IEnumerator DisposeCancelsPlanningAndRejectsNewWork()
        {
            using (var session = new LocalSession())
            {
                var runner = new BotRunner(session); session.Start(PracticeDeals.Create("Finish"));
                var pending = runner.RunOneTurnAsync(0, CancellationToken.None); runner.Dispose(); var boundary = session.GetView(0).Version;
                yield return Await(pending);
                Assert.That(session.GetView(0).Version, Is.EqualTo(boundary));
                Assert.Throws<ObjectDisposedException>(() => runner.RunOneTurnAsync(0, CancellationToken.None));
                runner.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator ThreeBotsReturnControlToHuman()
        {
            var root = new GameObject("Three-bot table");
            try
            {
                var game = root.AddComponent<GameBootstrap>(); game.Initialize(71, 0); game.SetBotsEnabled(true);
                Assert.That(game.BotsEnabled, Is.True);
                game.SelectTile(game.Session.GetView(0).OwnHand[0]);
                var commit = game.DiscardSelectedAsync(true); yield return Await(commit);
                Assert.That(commit.Result.Accepted, Is.True);
                yield return Until(() => game.Session.GetView(0).ActivePlayer == 0 || game.Session.GetView(0).Result != null);
                var view = game.Session.GetView(0);
                Assert.That(view.Version, Is.EqualTo(7), "Human discard, then three complete bot turns.");
                Assert.That(view.ActivePlayer, Is.Zero); Assert.That(game.Presenter.VisibleOwnTileIds, Is.EquivalentTo(view.OwnHand));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator HumanAndThreeBotsCompleteARealDeal()
        {
            var root = new GameObject("Complete bot table");
            try
            {
                var game = root.AddComponent<GameBootstrap>(); game.Initialize(71, 0); game.SetBotsEnabled(true);
                Assert.That(game.BotsEnabled, Is.True);
                var human = new BotPlanner(); var humanTurns = 0;
                for (var turns = 0; turns < 200; turns++)
                {
                    yield return Until(() => game.Session.GetView(0).Result != null || game.Session.GetView(0).ActivePlayer == 0 && !game.BotThinking);
                    var view = game.Session.GetView(0); if (view.Result != null) break;
                    if (view.Phase == RoundPhase.AwaitingDraw)
                    {
                        var draw = game.DrawAsync(DrawSource.Stock); yield return Await(draw); Assert.That(draw.Result.Accepted, Is.True);
                    }
                    view = game.Session.GetView(0);
                    var planned = Task.Run(() => human.PlanTurn(view, CancellationToken.None)); yield return Await(planned);
                    game.Draft.SetPlan(planned.Result); game.SelectTile(planned.Result.Discard);
                    var commit = game.DiscardSelectedAsync(planned.Result.AcceptPenalty); yield return Await(commit); Assert.That(commit.Result.Accepted, Is.True);
                    humanTurns++;
                }
                Assert.That(humanTurns, Is.GreaterThan(0)); Assert.That(game.Session.GetView(0).Result, Is.Not.Null);
                Assert.That(game.Session.GetView(0).Version, Is.GreaterThan(7));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static IEnumerator Until(Func<bool> condition)
        { var deadline = Time.realtimeSinceStartup + 20; while (!condition()) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; } }

        [UnityTest]
        public IEnumerator BotStartingSeatCommitsWithoutInitialDraw()
        {
            var root = new GameObject("Bot starting seat");
            try
            {
                var game = root.AddComponent<GameBootstrap>(); game.Initialize(71, 1, true);
                yield return Until(() => game.Session.GetView(0).ActivePlayer == 0 && !game.BotThinking || game.Session.GetView(0).Result != null);
                Assert.That(game.Session.GetView(0).Version, Is.EqualTo(5), "Starting bot commits once, then two draw/commit turns.");
                Assert.That(game.Session.GetView(0).ActivePlayer, Is.Zero);
                Assert.That(game.Session.GetView(0).Phase, Is.EqualTo(RoundPhase.AwaitingDraw));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        internal static IEnumerator Await(Task task)
        {
            var deadline = Time.realtimeSinceStartup + 45;
            while (!task.IsCompleted) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Bot work exceeded the test deadline."); yield return null; }
            task.GetAwaiter().GetResult();
        }
    }
}

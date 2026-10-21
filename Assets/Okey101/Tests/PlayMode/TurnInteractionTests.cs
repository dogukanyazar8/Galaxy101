using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Okey101.Application;
using Okey101.Core.Model;
using Okey101.Presentation;
using Okey101.TestSupport;
using UnityEngine;
using UnityEngine.TestTools;

namespace Okey101.PlayMode.Tests
{
    public sealed class TurnInteractionTests
    {
        private GameObject root;
        private static readonly TileId Spare = Id(TileColor.Black, 12);
        [SetUp] public void Setup() { root = new GameObject("Turn UI test"); }
        [TearDown] public void Cleanup() { UnityEngine.Object.DestroyImmediate(root); }

        [UnityTest]
        public IEnumerator Opening101AndFivePairsShowExactTotals()
        {
            foreach (var total in new[] { 100, 101 })
            {
                var groups = Opening(total);
                var state = Builder(Ids(groups).Concat(new[] { Spare }).ToArray()).Build();
                var draft = Draft(state, new OpenGroups(OpeningKind.Series, groups));
                draft.SelectDiscard(Spare);
                var task = draft.PreviewAsync(CancellationToken.None); yield return Await(task);
                Assert.That(task.Result.OpeningTotal, Is.EqualTo(total));
                Assert.That(task.Result.Valid, Is.EqualTo(total == 101));
                var panel = root.AddComponent<OpeningPanel>(); panel.Show(task.Result);
                Assert.That(panel.CanConfirm, Is.EqualTo(total == 101));
                Assert.That(state.Table, Is.Empty);
            }
            var pairs = Enumerable.Range(1, 5).Select(n => Pair(TileColor.Red, n)).ToArray();
            var pairState = Builder(Ids(pairs).Concat(new[] { Spare }).ToArray()).Build();
            var pairDraft = Draft(pairState, new OpenGroups(OpeningKind.Pairs, pairs)); pairDraft.SelectDiscard(Spare);
            var preview = pairDraft.PreviewAsync(CancellationToken.None); yield return Await(preview);
            Assert.That(preview.Result.PairCount, Is.EqualTo(5));
            Assert.That(preview.Result.Valid, Is.True);
        }

        [UnityTest]
        public IEnumerator CanProcessAfterDraftOpening()
        {
            var groups = Opening(101); var added = Normal(TileColor.Red, 10);
            var state = Builder(Ids(groups).Concat(new[] { added.Id, Spare }).ToArray()).Build();
            var draft = Draft(state, new OpenGroups(OpeningKind.Series, groups), new ExtendMeld(0, true, new[] { added }));
            draft.SelectDiscard(Spare);
            var preview = draft.PreviewAsync(CancellationToken.None); yield return Await(preview);
            Assert.That(preview.Result.Valid, Is.True);
            Assert.That(preview.Result.ProjectedTable.Single(m => m.Id == 0).Group.Tiles.Count, Is.EqualTo(4));
            Assert.That(preview.Result.ProjectedOwnHand, Is.EqualTo(new[] { Spare }));
            Assert.That(state.Players[0].Opening, Is.EqualTo(OpeningKind.None));
        }

        [UnityTest]
        public IEnumerator DiscardWitnessSwapLeavesJokerInRack()
        {
            var picked = Id(TileColor.Red, 2); var joker = Id(TileColor.Black, 1);
            var target = new Meld(0, 1, new MeldProposal(MeldKind.Run, new[]
            { Normal(TileColor.Red, 1), new PlacedTile(joker, new TileFace(TileColor.Red, 2)), Normal(TileColor.Red, 3) }));
            var state = Context(Builder(new[] { picked, Spare }, OpeningKind.Series).WithOpening(1, OpeningKind.Series).WithTable(target).Build(),
                new DrawReceipt(DrawSource.PreviousDiscard, picked, false));
            var draft = Draft(state, new SwapJoker(0, joker, picked)); draft.SelectDiscard(Spare);
            var preview = draft.PreviewAsync(CancellationToken.None); yield return Await(preview);
            Assert.That(preview.Result.Valid, Is.True);
            Assert.That(preview.Result.ProjectedOwnHand, Has.Member(joker));
            Assert.That(preview.Result.ProjectedOwnHand, Has.No.Member(picked));
            Assert.That(preview.Result.ProjectedTable[0].Group.Tiles[1].Id, Is.EqualTo(picked));
            Assert.That(state.Players[0].Hand, Has.Member(picked));
            Assert.That(state.Players[0].Hand, Has.No.Member(joker));
        }

        [UnityTest]
        public IEnumerator ChangingJokerChoiceRecomputesPreview()
        {
            var joker = Id(TileColor.Black, 1);
            var groups = new[] { Run(TileColor.Red, 11, 12, 13), Run(TileColor.Blue, 9, 10, 11),
                new MeldProposal(MeldKind.Run, new[] { new PlacedTile(joker, new TileFace(TileColor.Yellow, 9)), Normal(TileColor.Yellow, 10), Normal(TileColor.Yellow, 11) }),
                Run(TileColor.Black, 2, 3, 4) };
            var state = Builder(Ids(groups).Concat(new[] { Spare }).ToArray()).Build();
            var draft = Draft(state, new OpenGroups(OpeningKind.Series, groups)); draft.SelectDiscard(Spare);
            var first = draft.PreviewAsync(CancellationToken.None); yield return Await(first);
            Assert.That(first.Result.OpeningTotal, Is.EqualTo(105));
            var picker = root.AddComponent<JokerBindingPicker>(); picker.Initialize(draft.ChooseBinding);
            picker.Choose(joker, new TileFace(TileColor.Yellow, 12));
            var next = draft.PreviewAsync(CancellationToken.None); yield return Await(next);
            Assert.That(next.Result.Valid, Is.True);
            Assert.That(next.Result.OpeningTotal, Is.EqualTo(108));
            Assert.That(next.Result.BindingChoices[joker], Is.EqualTo(new TileFace(TileColor.Yellow, 12)));
            Assert.That(first.Result.OpeningTotal, Is.EqualTo(105), "Earlier preview must stay immutable.");
        }

        [UnityTest]
        public IEnumerator RejectThirdProcessingAndUndoDraft()
        {
            var drawn = Id(TileColor.Red, 4);
            var state = Context(Builder(new[] { drawn, Id(TileColor.Red, 5), Id(TileColor.Red, 6), Spare }, OpeningKind.Pairs)
                .WithOpening(1, OpeningKind.Series).WithTable(new Meld(0, 1, Run(TileColor.Red, 1, 2, 3))).Build(),
                new DrawReceipt(DrawSource.Stock, drawn, false));
            var actions = new TableAction[] { new ExtendMeld(0, false, new[] { Normal(TileColor.Red, 4) }),
                new ExtendMeld(0, false, new[] { Normal(TileColor.Red, 5) }), new ExtendMeld(0, false, new[] { Normal(TileColor.Red, 6) }) };
            var draft = Draft(state, actions); draft.SelectDiscard(Spare);
            var rejected = draft.PreviewAsync(CancellationToken.None); yield return Await(rejected);
            Assert.That(rejected.Result.Error, Is.EqualTo(ErrorCode.ProcessingLimit));
            draft.UndoLast();
            var accepted = draft.PreviewAsync(CancellationToken.None); yield return Await(accepted);
            Assert.That(accepted.Result.Valid, Is.True); Assert.That(accepted.Result.SeriesTilesUsed, Is.EqualTo(2));
            draft.Reset(PlayerViewProjector.ForPlayer(state, 0));
            var reset = draft.PreviewAsync(CancellationToken.None); yield return Await(reset);
            Assert.That(reset.Result.ProjectedOwnHand, Does.Contain(drawn));
            Assert.That(state.PendingDraw.Tile, Is.EqualTo(drawn));
            Assert.That(reset.Result.ProjectedTable[0].Group.Tiles.Count, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator PenaltyCancelDoesNotCommit()
        {
            using (var session = new LocalSession())
            {
                session.Start(71, 0); var before = session.GetView(0);
                var dialog = root.AddComponent<PenaltyDialog>();
                var confirmation = dialog.ConfirmAsync(101, CancellationToken.None);
                Assert.That(dialog.IsOpen, Is.True); Assert.That(confirmation.IsCompleted, Is.False);
                dialog.Resolve(false); yield return Await(confirmation);
                Assert.That(confirmation.Result, Is.False);
                Assert.That(session.GetView(0).Version, Is.EqualTo(before.Version));
                Assert.That(session.GetView(0).OwnPenalties, Is.EqualTo(before.OwnPenalties));
            }
        }

        [UnityTest]
        public IEnumerator CommitRequiresDiscard()
        {
            var groups = Opening(101); var state = Builder(Ids(groups).Concat(new[] { Spare }).ToArray()).Build();
            var draft = Draft(state, new OpenGroups(OpeningKind.Series, groups));
            var missing = draft.PreviewAsync(CancellationToken.None); yield return Await(missing);
            Assert.That(missing.Result.Error, Is.EqualTo(ErrorCode.LastTileRequired));
            var panel = root.AddComponent<OpeningPanel>(); panel.Show(missing.Result); Assert.That(panel.CanConfirm, Is.False);
            draft.SelectDiscard(Spare);
            var ready = draft.PreviewAsync(CancellationToken.None); yield return Await(ready);
            panel.Show(ready.Result); Assert.That(panel.CanConfirm, Is.True);
        }

        [UnityTest]
        public IEnumerator PracticeOpeningsCommitAtomicallyAndFinishUsesExactMultiplier()
        {
            foreach (var name in new[] { "Series", "Pairs", "Finish" })
            {
                var host = new GameObject(name); host.transform.SetParent(root.transform);
                var game = host.AddComponent<GameBootstrap>(); game.Initialize(PracticeDeals.Create(name));
                var before = game.Session.GetView(0);
                var groups = name == "Pairs" ? Enumerable.Range(1, 5).Select(n => Pair(TileColor.Red, n)).ToArray() : Opening(101);
                foreach (var group in groups)
                {
                    foreach (var tile in group.Tiles) game.ToggleGroupTile(tile.Id);
                    game.StageSelectedGroup(group.Kind);
                    yield return Await(game.RecomputePreviewAsync());
                    Assert.That(game.Session.GetView(0).Version, Is.EqualTo(before.Version));
                }
                var discard = name == "Finish" ? Id(TileColor.Black, 1) : Spare;
                game.SelectTile(discard); yield return Await(game.RecomputePreviewAsync());
                Assert.That(game.CanCommit, Is.True);
                Assert.That(game.Preview.OpeningTotal, Is.EqualTo(name == "Pairs" ? 30 : 101));
                Assert.That(game.Preview.DiscardPenalty, Is.Zero, "A winning joker discard is not penalized.");
                var committed = game.DiscardSelectedAsync(); yield return Await(committed);
                Assert.That(committed.Result.Accepted, Is.True, committed.Result.Error.ToString());
                var after = game.Session.GetView(0);
                Assert.That(after.Version, Is.EqualTo(before.Version + 1));
                Assert.That(after.Table.Count, Is.EqualTo(groups.Length));
                Assert.That(after.OwnOpening, Is.EqualTo(name == "Pairs" ? OpeningKind.Pairs : OpeningKind.Series));
                Assert.That(after.Result.FinishInfo.Winner, Is.EqualTo(0));
                Assert.That(after.Result.Scores[0].M, Is.EqualTo(name == "Series" ? 2 : 4));
                Assert.That(after.Result.Scores[0].Total, Is.EqualTo(name == "Series" ? -202 : -404));
                Assert.That(after.Result.Scores[1].Total, Is.EqualTo(name == "Series" ? 404 : 808));
                UnityEngine.Object.DestroyImmediate(host);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator RealSwapReturnsJokerWithoutChangingAuthorityUntilDiscard()
        {
            var game = root.AddComponent<GameBootstrap>(); game.Initialize(PracticeDeals.Create("Swap"));
            yield return Await(game.RecomputePreviewAsync());
            var before = game.Session.GetView(0);
            game.SelectTile(Id(TileColor.Red, 2)); game.Presenter.Melds.Select(0, Id(TileColor.Black, 1));
            game.StageSelectedSwap(); yield return Await(game.RecomputePreviewAsync());
            Assert.That(game.Preview.ProjectedOwnHand, Has.Member(Id(TileColor.Black, 1)));
            Assert.That(game.Session.GetView(0).OwnHand, Has.No.Member(Id(TileColor.Black, 1)));
            game.SelectTile(Spare); yield return Await(game.RecomputePreviewAsync());
            Assert.That(game.CanCommit, Is.True);
            var commit = game.DiscardSelectedAsync(); yield return Await(commit);
            Assert.That(commit.Result.Accepted, Is.True);
            Assert.That(game.Session.GetView(0).OwnHand, Has.Member(Id(TileColor.Black, 1)));
            Assert.That(game.Session.GetView(0).Version, Is.EqualTo(before.Version + 1));
        }

        [UnityTest]
        public IEnumerator PenaltyDialogCancelAndAcceptUseSameUncommittedDraft()
        {
            var game = root.AddComponent<GameBootstrap>(); game.Initialize(PracticeDeals.Create("Penalty"));
            yield return Await(game.RecomputePreviewAsync());
            game.SelectTile(Id(TileColor.Red, 4)); yield return Await(game.RecomputePreviewAsync());
            Assert.That(game.Preview.DiscardPenalty, Is.EqualTo(101));
            var before = game.Session.GetView(0); var dialog = root.GetComponent<PenaltyDialog>();
            game.RequestDiscard();
            yield return Until(() => dialog.IsOpen);
            Assert.That(game.CanControl, Is.False);
            dialog.Resolve(false); yield return Until(() => game.CanControl);
            Assert.That(game.Session.GetView(0).Version, Is.EqualTo(before.Version));
            Assert.That(game.Session.GetView(0).OwnPenalties, Is.Zero);
            Assert.That(game.SelectedTiles, Is.EqualTo(new[] { Id(TileColor.Red, 4) }));
            game.RequestDiscard(); yield return Until(() => dialog.IsOpen);
            dialog.Resolve(true); yield return Until(() => game.Session.GetView(0).Version > before.Version);
            Assert.That(game.Session.GetView(0).OwnPenalties, Is.EqualTo(101));
        }

        [UnityTest]
        public IEnumerator CancelledSearchCanChooseStockAndUndoPreservesDraw()
        {
            var original = PracticeDeals.Create("Swap"); var picked = original.PendingDraw.Tile;
            var players = original.Players.ToArray();
            players[0] = new PlayerState(players[0].Hand.Where(t => t != picked).ToArray(), OpeningKind.Series, 0);
            var piles = original.Discards.ToArray(); piles[3] = new[] { picked };
            var waiting = new RoundState(original.RoundId, original.Version, 71, 0, 0, 1, RoundPhase.AwaitingDraw,
                original.Indicator, original.Stock, players, original.Table, piles, null, OpeningKind.Series, 0, false, new long[4]);
            var game = root.AddComponent<GameBootstrap>(); game.Initialize(waiting);
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                var attempt = game.DrawAsync(DrawSource.PreviousDiscard, cancelled.Token); yield return Await(attempt);
                Assert.That(attempt.Result.Error, Is.EqualTo(ErrorCode.Cancelled));
                Assert.That(game.Session.GetView(0).Version, Is.EqualTo(waiting.Version));
            }
            var draw = game.DrawAsync(DrawSource.Stock); yield return Await(draw);
            Assert.That(draw.Result.Accepted, Is.True);
            var drawn = game.Session.GetView(0); game.ResetDraft(); yield return Await(game.RecomputePreviewAsync());
            Assert.That(game.Session.GetView(0).PendingDraw.Tile, Is.EqualTo(drawn.PendingDraw.Tile));
            Assert.That(game.Presenter.VisibleOwnTileIds, Is.EquivalentTo(drawn.OwnHand));
        }

        [UnityTest]
        public IEnumerator CommittedJokerBindingCannotBeEditedAndCancellationIsDistinct()
        {
            var state = PracticeDeals.Create("Swap");
            var draft = new TurnDraft(); draft.Reset(PlayerViewProjector.ForPlayer(state, 0));
            Assert.Throws<InvalidOperationException>(() => draft.ChooseBinding(Id(TileColor.Black, 1), new TileFace(TileColor.Red, 5)));
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel(); var task = draft.PreviewAsync(cancellation.Token); yield return Await(task);
                Assert.That(task.Result.Error, Is.EqualTo(ErrorCode.Cancelled)); Assert.That(task.Result.Valid, Is.False);
            }
            using (var session = new LocalSession())
            {
                session.Start(71, 0); var before = session.GetView(0);
                var invalid = Builder(new[] { Spare }).Build();
                Assert.Throws<ArgumentException>(() => session.Start(invalid));
                Assert.That(session.GetView(0).RoundId, Is.EqualTo(before.RoundId));
            }
        }

        private static IEnumerator Until(Func<bool> condition)
        { var deadline = Time.realtimeSinceStartup + 15; while (!condition()) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; } }

        [UnityTest]
        public IEnumerator PairAfterSeriesOpeningRemainsAfterOpening()
        {
            var groups = Opening(101); var pair = Pair(TileColor.Black, 2);
            var state = Builder(Ids(groups).Concat(Ids(pair)).Concat(new[] { Spare }).ToArray()).WithOpening(2, OpeningKind.Pairs).Build();
            var draft = new TurnDraft(); draft.Reset(PlayerViewProjector.ForPlayer(state, 0));
            foreach (var group in groups) draft.StageGroup(group);
            draft.StageGroup(pair); draft.SelectDiscard(Spare);
            var preview = draft.PreviewAsync(CancellationToken.None); yield return Await(preview);
            Assert.That(preview.Result.Valid, Is.True);
            Assert.That(draft.Build().Actions.Last(), Is.TypeOf<AddGroup>());
            Assert.That(preview.Result.OpeningTotal, Is.EqualTo(101), "Later groups do not count toward the opening threshold.");
        }

        [UnityTest]
        public IEnumerator SwapRecoveredJokerGroupKeepsActionOrderAndUndo()
        {
            var opening = Opening(101); var joker = Id(TileColor.Black, 1); var replacement = Id(TileColor.Red, 2);
            var table = new Meld(0, 1, new MeldProposal(MeldKind.Run, new[] { Normal(TileColor.Red, 1),
                new PlacedTile(joker, new TileFace(TileColor.Red, 2)), Normal(TileColor.Red, 3) }));
            var state = Builder(Ids(opening).Concat(new[] { replacement, Id(TileColor.Blue, 2), Id(TileColor.Blue, 3), Spare }).ToArray())
                .WithOpening(1, OpeningKind.Series).WithTable(table).Build();
            var draft = new TurnDraft(); draft.Reset(PlayerViewProjector.ForPlayer(state, 0));
            foreach (var group in opening) draft.StageGroup(group);
            draft.StageAction(new SwapJoker(0, joker, replacement)); draft.ChooseBinding(joker, new TileFace(TileColor.Blue, 1));
            draft.StageGroup(new MeldProposal(MeldKind.Run, new[] { new PlacedTile(joker, new TileFace(TileColor.Blue, 1)), Normal(TileColor.Blue, 2), Normal(TileColor.Blue, 3) }));
            draft.SelectDiscard(Spare);
            var preview = draft.PreviewAsync(CancellationToken.None); yield return Await(preview);
            Assert.That(preview.Result.Valid, Is.True);
            Assert.That(draft.Build().Actions.Select(a => a.GetType()), Is.EqualTo(new[] { typeof(OpenGroups), typeof(SwapJoker), typeof(AddGroup) }));
            draft.UndoLast(); var undo = draft.PreviewAsync(CancellationToken.None); yield return Await(undo);
            Assert.That(undo.Result.ProjectedOwnHand, Has.Member(joker));
        }

        [UnityTest]
        public IEnumerator EitherStagedJokerCanBeTargetedForEditing()
        {
            var first = Id(TileColor.Black, 1); var second = Id(TileColor.Black, 1, 1);
            var groups = new[] { Run(TileColor.Red, 11, 12, 13), Run(TileColor.Blue, 9, 10, 11),
                new MeldProposal(MeldKind.Run, new[] { new PlacedTile(first, new TileFace(TileColor.Yellow, 9)), Normal(TileColor.Yellow, 10), Normal(TileColor.Yellow, 11) }),
                Run(TileColor.Black, 2, 3, 4),
                new MeldProposal(MeldKind.Run, new[] { new PlacedTile(second, new TileFace(TileColor.Blue, 1)), Normal(TileColor.Blue, 2), Normal(TileColor.Blue, 3) }) };
            var state = Context(Builder(Ids(groups).Concat(new[] { Spare }).ToArray())
                .WithHand(1, Id(TileColor.Red, 5, 1)).WithHand(2, Id(TileColor.Black, 10, 1)).WithHand(3, Id(TileColor.Yellow, 2, 1)).Build(),
                new DrawReceipt(DrawSource.Stock, Spare, false));
            var game = root.AddComponent<GameBootstrap>(); game.Initialize(state);
            game.ChooseBinding(first, new TileFace(TileColor.Yellow, 9)); game.ChooseBinding(second, new TileFace(TileColor.Blue, 1));
            foreach (var group in groups)
            {
                foreach (var tile in group.Tiles) game.ToggleGroupTile(tile.Id);
                game.StageSelectedGroup(group.Kind); yield return Await(game.RecomputePreviewAsync());
            }
            game.SelectMeldTile(4, second); game.RequestJokerPicker();
            var picker = root.GetComponent<JokerBindingPicker>();
            Assert.That(picker.CurrentJoker, Is.EqualTo(second));
            picker.Choose(picker.CurrentJoker, new TileFace(TileColor.Blue, 4));
            yield return Await(game.RecomputePreviewAsync());
            Assert.That(game.Preview.BindingChoices[first], Is.EqualTo(new TileFace(TileColor.Yellow, 9)));
            Assert.That(game.Preview.BindingChoices[second], Is.EqualTo(new TileFace(TileColor.Blue, 4)));
        }

        [UnityTest]
        public IEnumerator ReusedJokerKeepsEarlierBindingWhenAssignedAgain()
        {
            var joker = Id(TileColor.Black, 1);
            var target = new Meld(0, 1, new MeldProposal(MeldKind.Run, new[] { Normal(TileColor.Red, 1),
                new PlacedTile(joker, new TileFace(TileColor.Red, 2)), Normal(TileColor.Red, 3) }));
            var state = Builder(new[] { Id(TileColor.Red, 2), Id(TileColor.Blue, 2), Id(TileColor.Blue, 3), Id(TileColor.Blue, 4),
                Id(TileColor.Yellow, 2), Id(TileColor.Yellow, 3), Spare }, OpeningKind.Series).WithOpening(1, OpeningKind.Series).WithTable(target).Build();
            var draft = new TurnDraft(); draft.Reset(PlayerViewProjector.ForPlayer(state, 0));
            draft.StageAction(new SwapJoker(0, joker, Id(TileColor.Red, 2)));
            draft.ChooseBinding(joker, new TileFace(TileColor.Blue, 4));
            draft.StageGroup(new MeldProposal(MeldKind.Run, new[] { Normal(TileColor.Blue, 2), Normal(TileColor.Blue, 3), new PlacedTile(joker, new TileFace(TileColor.Blue, 4)) }));
            draft.StageAction(new SwapJoker(1, joker, Id(TileColor.Blue, 4)));
            draft.ChooseBinding(joker, new TileFace(TileColor.Yellow, 1));
            draft.StageGroup(new MeldProposal(MeldKind.Run, new[] { new PlacedTile(joker, new TileFace(TileColor.Yellow, 1)), Normal(TileColor.Yellow, 2), Normal(TileColor.Yellow, 3) }));
            draft.SelectDiscard(Spare);
            var preview = draft.PreviewAsync(CancellationToken.None); yield return Await(preview);
            Assert.That(preview.Result.Valid, Is.True, preview.Result.Error.ToString());
            Assert.That(((AddGroup)draft.Build().Actions[1]).Group.Tiles.Single(t => t.Id == joker).RepresentedFace,
                Is.EqualTo(new TileFace(TileColor.Blue, 4)));
        }

        private static IEnumerator Await(Task task)
        { var deadline = Time.realtimeSinceStartup + 15; while (!task.IsCompleted) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline)); yield return null; } task.GetAwaiter().GetResult(); }
        private static TurnDraft Draft(RoundState state, params TableAction[] actions)
        { var draft = new TurnDraft(); draft.Reset(PlayerViewProjector.ForPlayer(state, 0)); draft.SetPlan(new TurnPlan(actions, Spare, false)); draft.ClearDiscard(); return draft; }
        internal static TileId Id(TileColor color, int number, int copy = 0) => new TileId(2 * (13 * (int)color + number - 1) + copy);
        internal static PlacedTile Normal(TileColor color, int number, int copy = 0) => new PlacedTile(Id(color, number, copy), new TileFace(color, number));
        internal static MeldProposal Run(TileColor color, params int[] numbers) => new MeldProposal(MeldKind.Run, numbers.Select(n => Normal(color, n)).ToArray());
        internal static MeldProposal Pair(TileColor color, int number) => new MeldProposal(MeldKind.Pair, new[] { Normal(color, number), Normal(color, number, 1) });
        internal static TileId[] Ids(params MeldProposal[] groups) => groups.SelectMany(g => g.Tiles.Select(t => t.Id)).ToArray();
        internal static MeldProposal[] Opening(int total) => new[] { Run(TileColor.Red, 11, 12, 13), Run(TileColor.Blue, 9, 10, 11),
            total == 101 ? Run(TileColor.Yellow, 5, 6, 7, 8, 9) : Run(TileColor.Yellow, 7, 8, 9, 10) };
        internal static RoundTestBuilder Builder(TileId[] hand, OpeningKind opening = OpeningKind.None)
            => new RoundTestBuilder().WithIndicator(Id(TileColor.Black, 13)).WithHand(0, hand).WithOpening(0, opening);
        internal static RoundState Context(RoundState s, DrawReceipt receipt) => new RoundState(s.RoundId, 2, s.Seed, 0, 0, 1, s.Phase,
            s.Indicator, s.Stock, s.Players, s.Table, s.Discards, receipt, s.Players[0].Opening, 0, false, s.LastAcceptedSequences);
    }
}

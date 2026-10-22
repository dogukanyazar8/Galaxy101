using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Okey101.Core.Model;
using Okey101.Core.Turns;
using static Okey101.Core.Tests.TableTest;
using static Okey101.Core.Tests.TurnTest;
using static Okey101.Core.Tests.EngineTest;

namespace Okey101.Core.Tests
{
    public sealed class RoundEndTests
    {
        [Test]
        public void Initial22TileOpeningCanWinWithoutDrawing()
        {
            var groups = new[] { Run(TileColor.Red, Enumerable.Range(1, 10).ToArray()),
                Run(TileColor.Blue, Enumerable.Range(1, 11).ToArray()) };
            var fixture = Builder(Ids(groups).Concat(new[] { Spare }).ToArray()).Build();
            var players = fixture.Players.ToArray();
            for (var p = 1; p < 4; p++)
                players[p] = new PlayerState(fixture.Stock.Skip((p - 1) * 21).Take(21).ToArray(), OpeningKind.None, 0);
            var state = new RoundState(fixture.RoundId, 0, fixture.Seed, 0, 0, 0, RoundPhase.PlanningTurn,
                fixture.Indicator, fixture.Stock.Skip(63).ToArray(), players, fixture.Table, fixture.Discards,
                null, OpeningKind.None, 0, true, new long[4]);
            Assert.That(StateInvariant.ValidateAuthority(state).Success, Is.True);
            var result = Apply(state, new CommitTurnCommand(Meta(state), new TurnPlan(new TableAction[]
                { new OpenGroups(OpeningKind.Series, groups) }, Spare, false)));
            Assert.That(result.Result.FinishInfo.WinnerOpening, Is.EqualTo(OpeningKind.Series));
            Assert.That(result.Result.FinishInfo.WinnerOpenedThisTurn, Is.True);
            Assert.That(result.Result.Scores[0].Total, Is.EqualTo(-202));
            Assert.That(result.Version, Is.EqualTo(1));
            Assert.That(result.TurnNumber, Is.Zero);
        }

        [Test]
        public void LastStockDrawCanStillWin()
        {
            var joker = Id(TileColor.Black, 1);
            var before = Authority(Waiting(new[] { Spare }, Id(TileColor.Red, 4), OpeningKind.Series)
                .WithTable(new Meld(5, 1, Run(TileColor.Black, 9, 10, 11))).Build(), lastStock: joker);
            var drawn = Apply(before, new DrawCommand(Meta(before), DrawSource.Stock));
            Assert.That(drawn.PendingDraw.WasLastStock, Is.True);
            Assert.That(drawn.Result, Is.Null);
            Assert.That(drawn.Phase, Is.EqualTo(RoundPhase.PlanningTurn));
            var result = Apply(drawn, new CommitTurnCommand(Meta(drawn), new TurnPlan(new TableAction[]
                { new ExtendMeld(5, false, new[] { Normal(TileColor.Black, 12) }) }, joker, false)));
            Assert.That(result.Phase, Is.EqualTo(RoundPhase.Finished));
            Assert.That(result.Result.FinishInfo.Reason, Is.EqualTo(FinishReason.DiscardWin));
            Assert.That(result.Result.FinishInfo.Winner, Is.EqualTo(0));
            Assert.That(result.Result.FinishInfo.DiscardedRealJoker, Is.True);
            Assert.That(result.Result.FinishInfo.WinnerOpenedThisTurn, Is.False);
            Assert.That(result.Result.Scores[0].Total, Is.EqualTo(-202));
            Assert.That(result.Result.Scores.All(s => s.M == 2), Is.True);
            Assert.That(result.Players[0].Penalties, Is.Zero);
            Assert.That(result.ActivePlayer, Is.EqualTo(0));
            Assert.That(result.TurnNumber, Is.EqualTo(drawn.TurnNumber));
            Assert.That(result.PendingDraw, Is.Null);
        }

        [Test]
        public void LastStockNoFinishScoresWithoutWinner()
        {
            var draw = Id(TileColor.Blue, 2);
            var before = Authority(Waiting(new[] { Id(TileColor.Red, 1) }, Id(TileColor.Red, 4))
                .WithHand(1, new TileId(104)).WithOpening(1, OpeningKind.Series)
                .WithHand(2, Id(TileColor.Black, 1)).WithOpening(2, OpeningKind.Pairs)
                .WithHand(3, Id(TileColor.Black, 1, 1)).Build(), lastStock: draw, penalties: new[] { 0, 101, 0, 0 });
            var drawn = Apply(before, new DrawCommand(Meta(before), DrawSource.Stock));
            var terminal = Apply(drawn, new CommitTurnCommand(Meta(drawn), Plan(draw)));
            var finish = terminal.Result.FinishInfo;
            Assert.That(finish.Reason, Is.EqualTo(FinishReason.StockExhausted));
            Assert.That(finish.Winner, Is.Null);
            Assert.That(finish.WinnerOpening, Is.EqualTo(OpeningKind.None));
            Assert.That(finish.DiscardedRealJoker || finish.WinnerOpenedThisTurn, Is.False);
            Assert.That(terminal.Result.Scores.Select(s => s.Total), Is.EqualTo(new int?[] { 202, 102, 202, 202 }));
            Assert.That(terminal.Result.Scores.All(s => s.M == 1 && s.HasScore), Is.True);
            Assert.That(terminal.Result.Scores[2].N, Is.Zero);
            Assert.That(terminal.Result.Scores[2].J, Is.EqualTo(1));
        }

        [Test]
        public void FakeJokerScoresAssignedNumber()
        {
            var state = Authority(Builder(new[] { Id(TileColor.Black, 11), Spare }, OpeningKind.Series)
                .WithIndicator(Id(TileColor.Red, 7))
                .WithHand(1, new TileId(104), Id(TileColor.Red, 8)).WithOpening(1, OpeningKind.Series)
                .WithHand(2, new TileId(105), Id(TileColor.Red, 8, 1)).WithOpening(2, OpeningKind.Pairs)
                .WithTable(new Meld(0, 0, Run(TileColor.Black, 8, 9, 10))).Build());
            var next = Apply(state, new CommitTurnCommand(Meta(state), new TurnPlan(new TableAction[]
                { new ExtendMeld(0, false, new[] { Normal(TileColor.Black, 11) }) }, Spare, false)));
            var series = next.Result.Scores[1];
            var pairs = next.Result.Scores[2];
            Assert.That(series.N, Is.EqualTo(8));
            Assert.That(series.J, Is.EqualTo(1));
            Assert.That(series.B, Is.EqualTo(109));
            Assert.That(series.Total, Is.EqualTo(109));
            Assert.That(pairs.N, Is.EqualTo(8));
            Assert.That(pairs.J, Is.EqualTo(1));
            Assert.That(pairs.Total, Is.EqualTo(218));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FourPairsCancelAfterCompletedTurn(bool lastStock)
        {
            var groups = Enumerable.Range(1, 5).Select(n => Pair(TileColor.Red, n)).ToArray();
            var state = Authority(PairOpeningState(groups, new[] { Spare, Id(TileColor.Black, 11) }),
                lastDraw: lastStock, penalties: new[] { 101, 0, 0, 0 });
            Assert.That(state.Result, Is.Null);
            var result = Apply(state, new CommitTurnCommand(Meta(state),
                new TurnPlan(new TableAction[] { new OpenGroups(OpeningKind.Pairs, groups) }, Spare, false)));
            Assert.That(result.Phase, Is.EqualTo(RoundPhase.Cancelled));
            Assert.That(result.Result.FinishInfo.Reason, Is.EqualTo(FinishReason.AllPairs));
            Assert.That(result.Result.Scores.All(s => !s.HasScore && s.Total == null), Is.True);
            Assert.That(result.Result.Scores[0].P, Is.EqualTo(101));
            Assert.That(result.Players[0].Penalties, Is.EqualTo(101));
            Assert.That(result.SeriesTilesUsed, Is.Zero);
            var redeal = RoundFactory.Create(24, Guid.NewGuid(), result.StartingPlayer);
            Assert.That(redeal.StartingPlayer, Is.EqualTo(state.StartingPlayer));
            Assert.That(redeal.RoundId, Is.Not.EqualTo(state.RoundId));
            Assert.That(StateInvariant.ValidateAuthority(redeal).Success, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WinningDiscardHasPriority(bool lastStock)
        {
            var groups = Enumerable.Range(1, 5).Select(n => Pair(TileColor.Red, n)).ToArray();
            var joker = Id(TileColor.Black, 1);
            var state = Authority(PairOpeningState(groups, new[] { joker }), lastDraw: lastStock);
            var result = Apply(state, new CommitTurnCommand(Meta(state),
                new TurnPlan(new TableAction[] { new OpenGroups(OpeningKind.Pairs, groups) }, joker, false)));
            Assert.That(result.Phase, Is.EqualTo(RoundPhase.Finished));
            Assert.That(result.Result.FinishInfo.Reason, Is.EqualTo(FinishReason.DiscardWin));
            Assert.That(result.Result.FinishInfo.WinnerOpenedThisTurn, Is.True);
            Assert.That(result.Result.FinishInfo.WinnerOpening, Is.EqualTo(OpeningKind.Pairs));
            Assert.That(result.Result.Scores.All(s => s.M == 8), Is.True);
            Assert.That(result.Result.Scores[0].Total, Is.EqualTo(-808));
            Assert.That(result.Players[0].Penalties, Is.Zero);
        }

        [Test]
        public void WinningFakeJokerDoesNotDoubleFinishMultiplier()
        {
            var groups = Enumerable.Range(1, 5).Select(n => Pair(TileColor.Red, n)).ToArray();
            var state = Authority(PairOpeningState(groups, new[] { new TileId(104) }));
            var result = Apply(state, new CommitTurnCommand(Meta(state),
                new TurnPlan(new TableAction[] { new OpenGroups(OpeningKind.Pairs, groups) }, new TileId(104), false)));
            Assert.That(result.Result.FinishInfo.DiscardedRealJoker, Is.False);
            Assert.That(result.Result.Scores.All(s => s.M == 4), Is.True);
            Assert.That(result.Result.Scores[0].Total, Is.EqualTo(-404));
        }

        [Test]
        public void WinnerAccumulatedPenaltiesAreAddedUnmultiplied()
        {
            var groups = Enumerable.Range(1, 5).Select(n => Pair(TileColor.Red, n)).ToArray();
            var joker = Id(TileColor.Black, 1);
            var state = Authority(PairOpeningState(groups, new[] { joker }), penalties: new[] { 202, 0, 0, 0 });
            var result = Apply(state, new CommitTurnCommand(Meta(state),
                new TurnPlan(new TableAction[] { new OpenGroups(OpeningKind.Pairs, groups) }, joker, false)));
            Assert.That(result.Result.Scores[0].B, Is.EqualTo(-101));
            Assert.That(result.Result.Scores[0].P, Is.EqualTo(202));
            Assert.That(result.Result.Scores[0].Total, Is.EqualTo(-606)); // −101 × 8 + 202
        }

        [Test]
        public void CancelledRoundDoesNotWriteItsLastDiscardPenalty()
        {
            var groups = Enumerable.Range(1, 5).Select(n => Pair(TileColor.Red, n)).ToArray();
            var joker = Id(TileColor.Black, 1);
            var state = Authority(PairOpeningState(groups, new[] { joker, Spare }));
            var result = Apply(state, new CommitTurnCommand(Meta(state),
                new TurnPlan(new TableAction[] { new OpenGroups(OpeningKind.Pairs, groups) }, joker, true)));
            Assert.That(result.Result.FinishInfo.Reason, Is.EqualTo(FinishReason.AllPairs));
            Assert.That(result.Result.Scores[0].P, Is.EqualTo(101));
            Assert.That(result.Result.Scores.All(s => !s.HasScore && s.Total == null), Is.True);
        }

        [Test]
        public void TerminalCommitDoesNotIncrementMaximumTurnNumber()
        {
            var groups = Enumerable.Range(1, 5).Select(n => Pair(TileColor.Red, n)).ToArray();
            var state = Authority(PairOpeningState(groups, new[] { Spare }), turnNumber: int.MaxValue);
            var result = Apply(state, new CommitTurnCommand(Meta(state),
                new TurnPlan(new TableAction[] { new OpenGroups(OpeningKind.Pairs, groups) }, Spare, false)));
            Assert.That(result.TurnNumber, Is.EqualTo(int.MaxValue));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TerminalRoundRejectsAllFurtherCommands(bool cancelled)
        {
            var groups = Enumerable.Range(1, 5).Select(n => Pair(TileColor.Red, n)).ToArray();
            var state = Authority(PairOpeningState(groups, cancelled ? new[] { Spare, Id(TileColor.Black, 11) } : new[] { Spare }));
            var terminal = Apply(state, new CommitTurnCommand(Meta(state),
                new TurnPlan(new TableAction[] { new OpenGroups(OpeningKind.Pairs, groups) }, Spare, false)));
            Reject(terminal, new DrawCommand(Meta(terminal), DrawSource.Stock), ErrorCode.WrongPhase);
            Reject(terminal, new CommitTurnCommand(Meta(terminal), Plan(Spare)), ErrorCode.WrongPhase);
        }

        [Test]
        public void ResultOverflowCannotPartiallyFinalizeRound()
        {
            var groups = Enumerable.Range(1, 5).Select(n => Pair(TileColor.Red, n)).ToArray();
            var state = Authority(PairOpeningState(groups, new[] { Spare }), penalties: new[] { 0, int.MaxValue, 0, 0 });
            Reject(state, new CommitTurnCommand(Meta(state),
                new TurnPlan(new TableAction[] { new OpenGroups(OpeningKind.Pairs, groups) }, Spare, false)), ErrorCode.InvalidState);
        }

        [Test]
        public void InvalidScoredTerminalSnapshotIsRejectedByAuthorityValidation()
        {
            var groups = Enumerable.Range(1, 5).Select(n => Pair(TileColor.Red, n)).ToArray();
            var state = Authority(PairOpeningState(groups, new[] { Spare }));
            var terminal = Apply(state, new CommitTurnCommand(Meta(state),
                new TurnPlan(new TableAction[] { new OpenGroups(OpeningKind.Pairs, groups) }, Spare, false)));
            var scores = terminal.Result.Scores.ToArray();
            var original = scores[0];
            scores[0] = new ScoreBreakdown(true, original.N, original.J, original.P, original.B, original.M, 0);
            var forged = new RoundState(terminal.RoundId, terminal.Version, terminal.Seed, terminal.StartingPlayer,
                terminal.ActivePlayer, terminal.TurnNumber, terminal.Phase, terminal.Indicator, terminal.Stock,
                terminal.Players, terminal.Table, terminal.Discards, terminal.PendingDraw, terminal.OpeningAtTurnStart,
                terminal.SeriesTilesUsed, terminal.IsInitialTurn, terminal.LastAcceptedSequences,
                new RoundResult(terminal.Result.FinishInfo, scores));
            Assert.That(StateInvariant.Validate(forged).Success, Is.True);
            Assert.That(StateInvariant.ValidateAuthority(forged).Error, Is.EqualTo(ErrorCode.InvalidState));
        }

        [Test]
        public void CancelledSnapshotMustContainCompletedTurnDiscard()
        {
            var groups = Enumerable.Range(1, 5).Select(n => Pair(TileColor.Red, n)).ToArray();
            var state = Authority(PairOpeningState(groups, new[] { Spare, Id(TileColor.Black, 11) }));
            var terminal = Apply(state, new CommitTurnCommand(Meta(state),
                new TurnPlan(new TableAction[] { new OpenGroups(OpeningKind.Pairs, groups) }, Spare, false)));
            var discards = terminal.Discards.ToArray();
            var stock = terminal.Stock.Concat(discards[terminal.ActivePlayer]).ToArray();
            discards[terminal.ActivePlayer] = Array.Empty<TileId>();
            var forged = new RoundState(terminal.RoundId, terminal.Version, terminal.Seed, terminal.StartingPlayer,
                terminal.ActivePlayer, terminal.TurnNumber, terminal.Phase, terminal.Indicator, stock,
                terminal.Players, terminal.Table, discards, null, terminal.OpeningAtTurnStart, 0, false,
                terminal.LastAcceptedSequences, terminal.Result);
            Assert.That(StateInvariant.Validate(forged).Success, Is.True);
            Assert.That(StateInvariant.ValidateAuthority(forged).Error, Is.EqualTo(ErrorCode.InvalidState));
        }

        private static RoundState PairOpeningState(MeldProposal[] groups, TileId[] remaining)
            => Builder(Ids(groups).Concat(remaining).ToArray())
                .WithOpening(1, OpeningKind.Pairs).WithHand(1, Id(TileColor.Yellow, 1))
                .WithOpening(2, OpeningKind.Pairs).WithHand(2, Id(TileColor.Blue, 1))
                .WithOpening(3, OpeningKind.Pairs).WithHand(3, Id(TileColor.Black, 10)).Build();
    }
}

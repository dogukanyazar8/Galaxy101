using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Okey101.Core.Model;
using Okey101.Core.Turns;
using Okey101.TestSupport;
using static Okey101.Core.Tests.TableTest;
using static Okey101.Core.Tests.TurnTest;

namespace Okey101.Core.Tests
{
    public sealed class TurnEvaluatorTests
    {
        [Test]
        public void DiscardIsMandatory()
        {
            var group = Run(TileColor.Red, 1, 2, 3);
            Fail(Builder(Ids(group), OpeningKind.Series).Build(),
                new TurnPlan(new TableAction[] { new AddGroup(group) }, group.Tiles[2].Id, true), ErrorCode.LastTileRequired);
            var state = Builder(Ids(group).Concat(new[] { Spare }).ToArray(), OpeningKind.Series).Build();
            var next = Pass(state, new TurnPlan(new TableAction[] { new AddGroup(group) }, Spare, false));
            Assert.That(next.Players[0].Hand, Is.Empty);
            Assert.That(next.Discards[0], Is.EqualTo(new[] { Spare }));
        }

        [Test]
        public void PreviousDiscardUsesExactPhysicalTile()
        {
            var picked = Id(TileColor.Red, 4);
            var twin = Id(TileColor.Red, 4, 1);
            var before = Waiting(new[] { twin, Spare }, picked, OpeningKind.Series)
                .WithTable(new Meld(0, 1, Run(TileColor.Red, 1, 2, 3))).Build();
            var drawn = Draw(before);
            Fail(drawn, new TurnPlan(new TableAction[] { new ExtendMeld(0, false, new[] { Normal(TileColor.Red, 4, 1) }) },
                Spare, true), ErrorCode.DiscardMustBeUsed);
            var next = Pass(drawn, new TurnPlan(new TableAction[] { new ExtendMeld(0, false, new[] { Normal(TileColor.Red, 4) }) },
                Spare, true));
            Assert.That(next.Players[0].Hand, Is.EqualTo(new[] { twin }));
        }

        [Test]
        public void UnopenedDiscardMustBeInFirstOpeningNotLaterProcessing()
        {
            var groups = OpeningGroups(101);
            var picked = Id(TileColor.Red, 2);
            var target = new Meld(0, 1, new MeldProposal(MeldKind.Run,
                new[] { Normal(TileColor.Red, 1), Joker(0, TileColor.Red, 2), Normal(TileColor.Red, 3) }));
            var drawn = Draw(Waiting(Ids(groups).Concat(new[] { Spare }).ToArray(), picked)
                .WithTable(target).Build());
            Fail(drawn, new TurnPlan(new TableAction[] { new OpenGroups(OpeningKind.Series, groups),
                new SwapJoker(0, Id(TileColor.Black, 1), picked) }, Spare, true), ErrorCode.DiscardMustBeUsed);
        }

        [Test]
        public void PreviousDiscardIncludedInOpeningIsValid()
        {
            var groups = OpeningGroups(101);
            var picked = groups[0].Tiles[0].Id;
            var drawn = Draw(Waiting(Ids(groups).Where(id => id != picked).Concat(new[] { Spare }).ToArray(), picked).Build());
            var next = Pass(drawn, new TurnPlan(new TableAction[] { new OpenGroups(OpeningKind.Series, groups) }, Spare, false));
            Assert.That(next.Players[0].Hand, Is.Empty);
            Assert.That(next.OpeningAtTurnStart, Is.EqualTo(OpeningKind.None));
        }

        [Test]
        public void RecoveredPickedJokerCannotRemainInHand()
        {
            var picked = Id(TileColor.Black, 1);
            var drawn = Draw(Waiting(new[] { Id(TileColor.Red, 2), Spare }, picked, OpeningKind.Series)
                .WithTable(new Meld(0, 1, Run(TileColor.Red, 3, 4, 5))).Build());
            Fail(drawn, new TurnPlan(new TableAction[]
            {
                new ExtendMeld(0, true, new[] { Joker(0, TileColor.Red, 2) }),
                new SwapJoker(0, picked, Id(TileColor.Red, 2))
            }, Spare, true), ErrorCode.DiscardMustBeUsed);
        }

        [Test]
        public void PickedTileCannotBeDiscardedBack()
        {
            var picked = Id(TileColor.Red, 4);
            Fail(Draw(Waiting(new[] { Spare }, picked, OpeningKind.Series).Build()), Plan(picked), ErrorCode.DiscardMustBeUsed);
        }

        [Test]
        public void PenaltyNeedsExplicitAcceptance()
        {
            var tile = Id(TileColor.Red, 4);
            var state = Context(Builder(new[] { tile, Spare }, OpeningKind.Series)
                .WithTable(new Meld(0, 0, Run(TileColor.Red, 1, 2, 3))).Build(), null, penalties: 101);
            Fail(state, Plan(tile), ErrorCode.PenaltyConfirmationRequired);
            Assert.That(Pass(state, Plan(tile, true)).Players[0].Penalties, Is.EqualTo(202));
        }

        [Test]
        public void WinningRealJokerDiscardHasNoPenalty()
        {
            var joker = Id(TileColor.Black, 1);
            var next = Pass(Builder(new[] { joker }, OpeningKind.Series).Build(), Plan(joker));
            Assert.That(next.Players[0].Penalties, Is.Zero);
            Assert.That(next.Players[0].Hand, Is.Empty);
        }

        [Test]
        public void InvalidTablePlanDoesNotWritePenaltyOrDiscard()
        {
            var state = Builder(new[] { Id(TileColor.Red, 4), Spare }, OpeningKind.Series)
                .WithTable(new Meld(0, 0, Run(TileColor.Red, 1, 2, 3))).Build();
            Fail(state, new TurnPlan(new TableAction[] { new ExtendMeld(0, false, new[] { Normal(TileColor.Red, 6) }) },
                Id(TileColor.Red, 4), true), ErrorCode.InvalidTile);
        }

        [Test]
        public void DiscardMustBeOwnedAndWinnerMustHaveOpened()
        {
            var state = Builder(new[] { Spare, Id(TileColor.Red, 4) }).Build();
            Fail(state, Plan(Id(TileColor.Blue, 4)), ErrorCode.InvalidTile);
            Fail(Builder(new[] { Spare }).Build(), Plan(Spare), ErrorCode.OpeningRequired);
        }

        [Test]
        public void PenaltyOverflowIsRejectedAtomically()
        {
            var joker = Id(TileColor.Black, 1);
            var state = Context(Builder(new[] { joker, Spare }, OpeningKind.Series).Build(), null, penalties: int.MaxValue);
            Fail(state, Plan(joker, true), ErrorCode.InvalidState);
        }

        [Test]
        public void DrawReceiptAndAuthorityMetadataSurviveCommitCandidate()
        {
            var before = Waiting(new[] { Spare }, Id(TileColor.Red, 4), OpeningKind.Series).Build();
            var drawn = Draw(before, DrawSource.Stock);
            var next = Pass(drawn, Plan(drawn.PendingDraw.Tile, true));
            Assert.That(next.PendingDraw, Is.SameAs(drawn.PendingDraw));
        }

        [Test]
        public void LastStockReceiptSurvivesUntilRoundFinalization()
        {
            var state = Context(Builder(new[] { Id(TileColor.Red, 4), Spare }, OpeningKind.Series).Build(),
                new DrawReceipt(DrawSource.Stock, Id(TileColor.Red, 4), true));
            var next = Pass(state, Plan(Id(TileColor.Red, 4)));
            Assert.That(next.PendingDraw.WasLastStock, Is.True);
        }

        [Test]
        public void MissingPlanOrDrawTileAndWrongPhaseAreRejected()
        {
            Assert.That(TurnEvaluator.Evaluate(null, Plan(Spare)).Error, Is.EqualTo(ErrorCode.InvalidState));
            var state = Builder(new[] { Spare, Id(TileColor.Red, 4) }).Build();
            Assert.That(TurnEvaluator.Evaluate(state, null).Error, Is.EqualTo(ErrorCode.InvalidState));
            Fail(Context(state, new DrawReceipt(DrawSource.PreviousDiscard, Id(TileColor.Blue, 4), false)),
                Plan(Spare), ErrorCode.InvalidState);
            Fail(Waiting(new[] { Spare }, Id(TileColor.Red, 4)).Build(), Plan(Spare), ErrorCode.WrongPhase);
        }
    }

    internal static class TurnTest
    {
        internal static TurnPlan Plan(TileId discard, bool acceptPenalty = false)
            => new TurnPlan(Array.Empty<TableAction>(), discard, acceptPenalty);
        internal static RoundTestBuilder Waiting(TileId[] hand, TileId picked, OpeningKind opening = OpeningKind.None)
            => Builder(hand, opening).WithOpening(1, OpeningKind.Series)
                .WithTurn(0, RoundPhase.AwaitingDraw).WithDiscard(3, picked);

        internal static RoundState Context(RoundState state, DrawReceipt pending, int used = 0, int penalties = 0)
        {
            var players = state.Players.ToArray();
            var actor = state.ActivePlayer;
            players[actor] = new PlayerState(players[actor].Hand, players[actor].Opening, penalties);
            return new RoundState(state.RoundId, 9, state.Seed, state.StartingPlayer, actor, 4, state.Phase,
                state.Indicator, state.Stock, players, state.Table, state.Discards, pending,
                state.OpeningAtTurnStart, used, false, new long[] { 2, 3, 4, 5 });
        }

        // Independent fixture draw; the finder must never create or inspect this private state.
        internal static RoundState Draw(RoundState before, DrawSource source = DrawSource.PreviousDiscard)
        {
            var actor = before.ActivePlayer;
            var previous = (actor + 3) % 4;
            var discards = before.Discards.Select(d => d.ToList()).ToArray();
            var stock = before.Stock.ToList();
            var picked = source == DrawSource.Stock ? stock[0] : discards[previous].Last();
            if (source == DrawSource.Stock) stock.RemoveAt(0);
            else discards[previous].RemoveAt(discards[previous].Count - 1);
            var players = before.Players.ToArray();
            var player = players[actor];
            players[actor] = new PlayerState(player.Hand.Concat(new[] { picked }).ToArray(), player.Opening, player.Penalties);
            var drawn = new RoundState(before.RoundId, before.Version, before.Seed, before.StartingPlayer, actor,
                before.TurnNumber, RoundPhase.PlanningTurn, before.Indicator, stock, players, before.Table,
                discards.Cast<IReadOnlyList<TileId>>().ToArray(),
                new DrawReceipt(source, picked, source == DrawSource.Stock && stock.Count == 0),
                player.Opening, before.SeriesTilesUsed, false, before.LastAcceptedSequences);
            Assert.That(StateInvariant.Validate(drawn).Success, Is.True);
            return drawn;
        }

        internal static RoundState Pass(RoundState state, TurnPlan plan)
        {
            var snapshot = Fingerprint(state);
            var result = TurnEvaluator.Evaluate(state, plan);
            Assert.That(Fingerprint(state), Is.EqualTo(snapshot));
            Assert.That(result.Success, Is.True, result.Error.ToString());
            var next = result.Value;
            Assert.That(StateInvariant.Validate(next).Success, Is.True);
            Assert.That(next.RoundId, Is.EqualTo(state.RoundId));
            Assert.That(next.Version, Is.EqualTo(state.Version));
            Assert.That(next.Seed, Is.EqualTo(state.Seed));
            Assert.That(next.StartingPlayer, Is.EqualTo(state.StartingPlayer));
            Assert.That(next.ActivePlayer, Is.EqualTo(state.ActivePlayer));
            Assert.That(next.TurnNumber, Is.EqualTo(state.TurnNumber));
            Assert.That(next.Phase, Is.EqualTo(RoundPhase.PlanningTurn));
            Assert.That(next.Stock, Is.EqualTo(state.Stock));
            Assert.That(next.PendingDraw, Is.SameAs(state.PendingDraw));
            Assert.That(next.OpeningAtTurnStart, Is.EqualTo(state.OpeningAtTurnStart));
            Assert.That(next.IsInitialTurn, Is.EqualTo(state.IsInitialTurn));
            Assert.That(next.LastAcceptedSequences, Is.EqualTo(state.LastAcceptedSequences));
            Assert.That(next.Result, Is.Null);
            return next;
        }

        internal static void Fail(RoundState state, TurnPlan plan, ErrorCode expected)
        {
            var snapshot = Fingerprint(state);
            var result = TurnEvaluator.Evaluate(state, plan);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(expected));
            Assert.That(Fingerprint(state), Is.EqualTo(snapshot));
            Assert.That(StateInvariant.Validate(state).Success, Is.True);
        }
    }
}

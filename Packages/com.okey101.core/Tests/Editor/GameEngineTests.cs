using System;
using System.Collections.Generic;
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
    public sealed class GameEngineTests
    {
        [Test]
        public void Initial22TurnCannotDrawAgain()
        {
            var state = RoundFactory.Create(71, Guid.NewGuid(), 0);
            Reject(state, new DrawCommand(Meta(state), DrawSource.Stock), ErrorCode.WrongPhase);
            var next = Apply(state, new CommitTurnCommand(Meta(state), Plan(state.Players[0].Hand[0], true)));
            Assert.That(next.Players[0].Hand.Count, Is.EqualTo(21));
            Assert.That(next.ActivePlayer, Is.EqualTo(1));
            Assert.That(next.Phase, Is.EqualTo(RoundPhase.AwaitingDraw));
            Assert.That(next.TurnNumber, Is.EqualTo(1));
            Assert.That(next.IsInitialTurn, Is.False);
            Assert.That(next.PendingDraw, Is.Null);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void NormalTurnAndDirection(int starter)
        {
            var state = RoundFactory.Create(17, Guid.NewGuid(), starter);
            var waiting = Apply(state, new CommitTurnCommand(Meta(state), Plan(state.Players[starter].Hand[0], true)));
            var actor = (starter + 1) % 4;
            var tile = waiting.Stock[0];
            var drawn = Apply(waiting, new DrawCommand(Meta(waiting), DrawSource.Stock));
            Assert.That(drawn.Players[actor].Hand.Count, Is.EqualTo(22));
            Assert.That(drawn.Players[actor].Hand.Last(), Is.EqualTo(tile));
            Assert.That(drawn.Stock, Is.EqualTo(waiting.Stock.Skip(1)));
            Assert.That(drawn.PendingDraw.Source, Is.EqualTo(DrawSource.Stock));
            Assert.That(drawn.PendingDraw.WasLastStock, Is.False);
            Reject(drawn, new DrawCommand(Meta(drawn), DrawSource.PreviousDiscard), ErrorCode.WrongPhase);
            var next = Apply(drawn, new CommitTurnCommand(Meta(drawn), Plan(tile, true)));
            Assert.That(next.Players[actor].Hand.Count, Is.EqualTo(21));
            Assert.That(next.ActivePlayer, Is.EqualTo((actor + 1) % 4));
            Assert.That(next.PendingDraw, Is.Null);
            Assert.That(next.SeriesTilesUsed, Is.Zero);
            Assert.That(next.OpeningAtTurnStart, Is.EqualTo(next.Players[next.ActivePlayer].Opening));
            Assert.That(next.TurnNumber, Is.EqualTo(2));
        }

        [TestCase(0, ErrorCode.StaleVersion)]
        [TestCase(1, ErrorCode.DuplicateCommand)]
        [TestCase(2, ErrorCode.WrongPlayer)]
        [TestCase(3, ErrorCode.WrongRound)]
        public void StaleDuplicateWrongActorAndWrongRound(int mistake, ErrorCode expected)
        {
            var state = Authority(Waiting(new[] { Spare }, Id(TileColor.Red, 4), OpeningKind.Series).Build());
            var meta = new CommandMeta(mistake == 3 ? Guid.NewGuid() : state.RoundId,
                mistake == 2 ? 1 : 0, mistake == 1 ? state.LastAcceptedSequences[0] : state.LastAcceptedSequences[0] + 1,
                mistake == 0 ? state.Version - 1 : state.Version);
            Reject(state, new DrawCommand(meta, DrawSource.Stock), expected);
        }

        [Test]
        public void ReplayIsDuplicateEvenWhenItsVersionIsStale()
        {
            var state = Authority(Waiting(new[] { Spare }, Id(TileColor.Red, 4), OpeningKind.Series).Build());
            var command = new DrawCommand(Meta(state), DrawSource.Stock);
            Reject(Apply(state, command), command, ErrorCode.DuplicateCommand);
        }

        [Test]
        public void OnlyAcceptedIncreasingSequenceIsRecorded()
        {
            var state = Authority(Waiting(new[] { Spare }, Id(TileColor.Red, 4), OpeningKind.Series).Build());
            var command = new DrawCommand(new CommandMeta(state.RoundId, 0, 42, state.Version), DrawSource.Stock);
            var next = Apply(state, command);
            Assert.That(next.LastAcceptedSequences[0], Is.EqualTo(42));
            Reject(next, new CommitTurnCommand(new CommandMeta(next.RoundId, 0, 41, next.Version),
                Plan(next.Players[0].Hand[0], true)), ErrorCode.DuplicateCommand);
        }

        [Test]
        public void CancelledCommitCannotDiscardOrWritePenalty()
        {
            var tile = Id(TileColor.Red, 4);
            var state = Authority(Builder(new[] { tile, Spare }, OpeningKind.Series)
                .WithTable(new Meld(0, 0, Run(TileColor.Red, 1, 2, 3))).Build());
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Reject(state, new CommitTurnCommand(Meta(state), Plan(tile, true)), ErrorCode.Cancelled, cancellation.Token);
            }
        }

        [Test]
        public void DiscardDrawRequiresWitness()
        {
            var picked = Id(TileColor.Red, 4);
            var builder = Waiting(new[] { Spare }, picked).WithTable(new Meld(0, 1, Run(TileColor.Red, 1, 2, 3)));
            var unopened = Authority(builder.Build());
            Reject(unopened, new DrawCommand(Meta(unopened), DrawSource.PreviousDiscard), ErrorCode.NoLegalUse);
            var opened = Authority(builder.WithOpening(0, OpeningKind.Series).Build());
            var drawn = Apply(opened, new DrawCommand(Meta(opened), DrawSource.PreviousDiscard));
            Assert.That(drawn.Players[0].Hand.Last(), Is.EqualTo(picked));
            Assert.That(drawn.Stock, Is.EqualTo(opened.Stock));
            Assert.That(drawn.Discards[3].Count, Is.EqualTo(opened.Discards[3].Count - 1));
            Assert.That(drawn.PendingDraw.Source, Is.EqualTo(DrawSource.PreviousDiscard));
            Reject(drawn, new CommitTurnCommand(Meta(drawn), Plan(Spare)), ErrorCode.DiscardMustBeUsed);
            Apply(drawn, new CommitTurnCommand(Meta(drawn), new TurnPlan(new TableAction[]
                { new ExtendMeld(0, false, new[] { Normal(TileColor.Red, 4) }) }, Spare, false)));
        }

        [Test]
        public void CancelledDrawCannotConsumeDiscardOrSequence()
        {
            var state = Authority(Waiting(new[] { Spare }, Id(TileColor.Red, 4), OpeningKind.Series)
                .WithTable(new Meld(0, 1, Run(TileColor.Red, 1, 2, 3))).Build());
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Reject(state, new DrawCommand(Meta(state), DrawSource.PreviousDiscard), ErrorCode.Cancelled, cancellation.Token);
            }
            Apply(state, new DrawCommand(Meta(state), DrawSource.PreviousDiscard));
        }

        [Test]
        public void FailAfterSeveralActionsRollsBackAll()
        {
            var pickedJoker = Joker(0, TileColor.Red, 2);
            var state = Authority(Builder(new[] { Id(TileColor.Red, 2), Id(TileColor.Blue, 6), Id(TileColor.Blue, 8), Spare }, OpeningKind.Series)
                .WithOpening(1, OpeningKind.Series).WithTable(new Meld(0, 1, new MeldProposal(MeldKind.Run,
                    new[] { Normal(TileColor.Red, 1), pickedJoker, Normal(TileColor.Red, 3) }))).Build());
            var actions = new TableAction[]
            {
                new SwapJoker(0, pickedJoker.Id, Id(TileColor.Red, 2)),
                new AddGroup(new MeldProposal(MeldKind.Run, new[] { Normal(TileColor.Blue, 6), Joker(0, TileColor.Blue, 7), Normal(TileColor.Blue, 8) }))
            };
            Reject(state, new CommitTurnCommand(Meta(state), new TurnPlan(actions.Concat(new TableAction[]
                { new ExtendMeld(999, false, new[] { Normal(TileColor.Black, 12) }) }).ToArray(), Spare, true)), ErrorCode.InvalidGroup);
            Apply(state, new CommitTurnCommand(Meta(state), new TurnPlan(actions, Spare, false)));
        }

        [Test]
        public void RejectedPenaltyCanBeRetriedWithSameSequence()
        {
            var tile = Id(TileColor.Red, 4);
            var state = Authority(Builder(new[] { tile, Spare }, OpeningKind.Series)
                .WithTable(new Meld(0, 0, Run(TileColor.Red, 1, 2, 3))).Build());
            var meta = Meta(state);
            Reject(state, new CommitTurnCommand(meta, Plan(tile)), ErrorCode.PenaltyConfirmationRequired);
            var next = Apply(state, new CommitTurnCommand(meta, Plan(tile, true)));
            Assert.That(next.Players[0].Penalties, Is.EqualTo(101));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ConservationAfterEveryAcceptedCommand(int starter)
        {
            var state = RoundFactory.Create(81, Guid.NewGuid(), starter);
            while (state.Phase != RoundPhase.Finished)
            {
                if (state.Phase == RoundPhase.AwaitingDraw)
                    state = Apply(state, new DrawCommand(Meta(state), DrawSource.Stock));
                else
                    state = Apply(state, new CommitTurnCommand(Meta(state), Plan(state.Players[state.ActivePlayer].Hand[0], true)));
                Assert.That(state.Version, Is.LessThanOrEqualTo(41));
            }
            Assert.That(state.Version, Is.EqualTo(41));
            Assert.That(state.LastAcceptedSequences.Sum(), Is.EqualTo(41));
            Assert.That(state.Stock, Is.Empty);
            Assert.That(state.Result.FinishInfo.Reason, Is.EqualTo(FinishReason.StockExhausted));
        }

        [Test]
        public void MissingDiscardAndWrongPhaseAreRejected()
        {
            var state = Authority(Waiting(new[] { Spare }, Id(TileColor.Red, 4), OpeningKind.Series).WithDiscard(3).Build());
            Reject(state, new DrawCommand(Meta(state), DrawSource.PreviousDiscard), ErrorCode.PreviousDiscardUnavailable);
            Reject(state, new CommitTurnCommand(Meta(state), Plan(Spare)), ErrorCode.WrongPhase);
            Assert.That(GameEngine.Apply(null, new DrawCommand(Meta(state), DrawSource.Stock), CancellationToken.None).Error,
                Is.EqualTo(ErrorCode.InvalidState));
            Assert.That(GameEngine.Apply(state, null, CancellationToken.None).Error, Is.EqualTo(ErrorCode.InvalidState));
        }

        [Test]
        public void AuthorityCountersCannotWrap()
        {
            var state = Authority(Waiting(new[] { Spare }, Id(TileColor.Red, 4), OpeningKind.Series).Build(), version: long.MaxValue);
            Reject(state, new DrawCommand(Meta(state), DrawSource.Stock), ErrorCode.InvalidState);
            state = Authority(Builder(new[] { Spare, Id(TileColor.Red, 4) }, OpeningKind.Series).Build(), turnNumber: int.MaxValue);
            Reject(state, new CommitTurnCommand(Meta(state), Plan(Id(TileColor.Red, 4))), ErrorCode.InvalidState);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void AuthorityValidationRejectsDraftAndInvalidLifecycle(int mistake)
        {
            var state = Authority(Builder(new[] { Spare, Id(TileColor.Red, 4) }, OpeningKind.Series).Build());
            var broken = new RoundState(state.RoundId, state.Version, state.Seed, state.StartingPlayer, 0, state.TurnNumber,
                mistake == 0 ? RoundPhase.AwaitingDraw : mistake == 5 ? RoundPhase.Finished : state.Phase,
                state.Indicator, state.Stock, state.Players, state.Table, state.Discards,
                mistake == 1 || mistake == 5 ? null : mistake == 2 ? new DrawReceipt(DrawSource.Stock, Id(TileColor.Blue, 4), false) : state.PendingDraw,
                mistake == 3 ? OpeningKind.Pairs : state.OpeningAtTurnStart, mistake == 4 ? 1 : 0,
                mistake == 6, state.LastAcceptedSequences);
            Assert.That(StateInvariant.Validate(broken).Success, Is.True);
            Assert.That(StateInvariant.ValidateAuthority(broken).Error, Is.EqualTo(ErrorCode.InvalidState));
            Reject(broken, mistake == 0 ? (GameCommand)new DrawCommand(Meta(broken), DrawSource.Stock)
                : new CommitTurnCommand(Meta(broken), Plan(Id(TileColor.Red, 4))),
                mistake == 5 ? ErrorCode.WrongPhase : ErrorCode.InvalidState);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void AuthorityRejectsInvalidGroupShapeOwnerAndPairArea(int mistake)
        {
            var group = mistake == 2 ? Pair(TileColor.Red, 1) :
                mistake == 0 ? Run(TileColor.Red, 1, 2, 4) : Run(TileColor.Red, 1, 2, 3);
            var state = Authority(Builder(new[] { Id(TileColor.Black, 11), Spare }, OpeningKind.Series)
                .WithOpening(1, mistake == 1 ? OpeningKind.None : OpeningKind.Series)
                .WithTable(new Meld(0, 1, group)).Build());
            Assert.That(StateInvariant.Validate(state).Success, Is.True);
            Assert.That(StateInvariant.ValidateAuthority(state).Success, Is.False);
            Reject(state, new CommitTurnCommand(Meta(state), Plan(Spare)), ErrorCode.InvalidState);
        }

        [Test]
        public void AuthorityRejectsOversizedHand()
        {
            var hand = Enumerable.Range(0, 23).Select(value => new TileId(value)).ToArray();
            var state = Authority(Builder(hand).Build());
            Assert.That(StateInvariant.Validate(state).Success, Is.True);
            Assert.That(StateInvariant.ValidateAuthority(state).Success, Is.False);
            Reject(state, new CommitTurnCommand(Meta(state), Plan(hand[0])), ErrorCode.InvalidState);
        }

        [Test]
        public void EmptyStockRequiresAccurateLastDrawReceipt()
        {
            var state = Authority(Builder(new[] { Id(TileColor.Red, 4), Spare }, OpeningKind.Series).Build(), lastDraw: true);
            var wrong = new RoundState(state.RoundId, state.Version, state.Seed, state.StartingPlayer, state.ActivePlayer,
                state.TurnNumber, state.Phase, state.Indicator, state.Stock, state.Players, state.Table, state.Discards,
                new DrawReceipt(DrawSource.Stock, state.PendingDraw.Tile, false), state.OpeningAtTurnStart,
                0, false, state.LastAcceptedSequences);
            Reject(wrong, new CommitTurnCommand(Meta(wrong), Plan(Spare)), ErrorCode.InvalidState);
        }
    }

    internal static class EngineTest
    {
        internal static CommandMeta Meta(RoundState state)
            => new CommandMeta(state.RoundId, state.ActivePlayer,
                state.LastAcceptedSequences[state.ActivePlayer] + 1, state.Version);

        internal static RoundState Authority(RoundState state, TileId? lastStock = null, bool lastDraw = false,
            int[] penalties = null, long version = 9, int turnNumber = 4)
        {
            var stock = state.Stock.ToList();
            var players = state.Players.ToArray();
            var actor = state.ActivePlayer;
            for (var p = 0; p < 4; p++)
            {
                var hand = players[p].Hand;
                if (p != actor && hand.Count == 0)
                {
                    var tile = stock.First(id => !lastStock.HasValue || id != lastStock.Value);
                    stock.Remove(tile);
                    hand = new[] { tile };
                }
                players[p] = new PlayerState(hand, players[p].Opening, penalties == null ? players[p].Penalties : penalties[p]);
            }
            var discards = state.Discards.Select(d => d.ToList()).ToArray();
            if (lastDraw || lastStock.HasValue)
            {
                discards[(actor + 1) % 4].AddRange(stock.Where(id => !lastStock.HasValue || id != lastStock.Value));
                stock = lastStock.HasValue ? new List<TileId> { lastStock.Value } : new List<TileId>();
            }
            return new RoundState(state.RoundId, version, state.Seed, state.StartingPlayer, actor, turnNumber,
                state.Phase, state.Indicator, stock, players, state.Table,
                discards.Cast<IReadOnlyList<TileId>>().ToArray(),
                state.Phase == RoundPhase.PlanningTurn ? new DrawReceipt(DrawSource.Stock, players[actor].Hand[0], lastDraw) : null,
                players[actor].Opening, 0, false, new long[] { 2, 3, 4, 5 });
        }

        internal static string Snapshot(RoundState state)
            => Fingerprint(state) + (state.Result == null ? "|no-result" :
                $"|{state.Result.FinishInfo.Reason}/{state.Result.FinishInfo.Winner}/{state.Result.FinishInfo.WinnerOpening}/" +
                $"{state.Result.FinishInfo.WinnerOpenedThisTurn}/{state.Result.FinishInfo.DiscardedRealJoker}|" +
                string.Join(";", state.Result.Scores.Select(s => $"{s.HasScore}/{s.N}/{s.J}/{s.P}/{s.B}/{s.M}/{s.Total}")));

        internal static RoundState Apply(RoundState state, GameCommand command)
        {
            var snapshot = Snapshot(state);
            var result = GameEngine.Apply(state, command, CancellationToken.None);
            Assert.That(Snapshot(state), Is.EqualTo(snapshot), "Command mutated its source.");
            Assert.That(result.Success, Is.True, result.Error.ToString());
            var next = result.Value;
            Assert.That(StateInvariant.Validate(next).Success, Is.True);
            Assert.That(StateInvariant.ValidateAuthority(next).Success, Is.True, "Invalid authority snapshot.");
            Assert.That(next.Version, Is.EqualTo(state.Version + 1));
            Assert.That(next.RoundId, Is.EqualTo(state.RoundId));
            Assert.That(next.Seed, Is.EqualTo(state.Seed));
            Assert.That(next.StartingPlayer, Is.EqualTo(state.StartingPlayer));
            for (var p = 0; p < 4; p++)
                Assert.That(next.LastAcceptedSequences[p], Is.EqualTo(p == command.Meta.Actor ? command.Meta.Sequence : state.LastAcceptedSequences[p]));
            return next;
        }

        internal static void Reject(RoundState state, GameCommand command, ErrorCode expected,
            CancellationToken token = default)
        {
            var snapshot = Snapshot(state);
            var result = GameEngine.Apply(state, command, token);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(expected));
            Assert.That(Snapshot(state), Is.EqualTo(snapshot), "Rejected command changed authority or result.");
        }
    }
}

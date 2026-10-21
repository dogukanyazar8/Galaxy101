using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Okey101.Core.Model;
using Okey101.Core.Rules;
using Okey101.Core.Turns;
using Okey101.TestSupport;
using static Okey101.Core.Tests.TableTest;

namespace Okey101.Core.Tests
{
    public sealed class OpeningTests
    {
        [TestCase(100, false)]
        [TestCase(101, true)]
        public void Series100Fails101Succeeds(int total, bool valid)
        {
            var groups = OpeningGroups(total);
            var state = Builder(Ids(groups).Concat(new[] { Spare }).ToArray()).Build();
            var action = new OpenGroups(OpeningKind.Series, groups);
            if (!valid) Reject(state, ErrorCode.InvalidOpening, action);
            else
            {
                var next = Accept(state, action);
                Assert.That(next.Players[0].Opening, Is.EqualTo(OpeningKind.Series));
                Assert.That(next.Players[0].Hand, Is.EqualTo(new[] { Spare }));
                Assert.That(next.Table.Select(m => m.Id), Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(next.Table.All(m => m.Owner == 0), Is.True);
                Assert.That(next.SeriesTilesUsed, Is.Zero);
            }
        }

        [TestCase(4, false)]
        [TestCase(5, true)]
        public void FourPairsFailFiveSucceed(int count, bool valid)
        {
            var groups = Enumerable.Range(1, count).Select(n => Pair(TileColor.Red, n)).ToArray();
            var state = Builder(Ids(groups).Concat(new[] { Spare }).ToArray()).Build();
            var action = new OpenGroups(OpeningKind.Pairs, groups);
            if (!valid) Reject(state, ErrorCode.InvalidOpening, action);
            else
            {
                var next = Accept(state, action);
                Assert.That(next.Players[0].Opening, Is.EqualTo(OpeningKind.Pairs));
                Assert.That(next.Table.Count, Is.EqualTo(5));
            }
        }

        [Test]
        public void ProcessedTilesNeverCountTowardOpening()
        {
            var groups = OpeningGroups(100);
            var target = new Meld(7, 1, Run(TileColor.Black, 2, 3, 4));
            var fake = new PlacedTile(new TileId(104), new TileFace(TileColor.Black, 1));
            var state = Builder(Ids(groups).Concat(new[] { fake.Id, Spare }).ToArray())
                .WithOpening(1, OpeningKind.Series).WithTable(target).Build();
            Reject(state, ErrorCode.InvalidOpening, new OpenGroups(OpeningKind.Series, groups),
                new ExtendMeld(7, true, new[] { fake }));
        }

        [TestCase(OpeningKind.Series, OpeningKind.Pairs)]
        [TestCase(OpeningKind.Pairs, OpeningKind.Series)]
        public void OpeningKindCannotChange(OpeningKind initial, OpeningKind requested)
        {
            var group = requested == OpeningKind.Pairs ? Pair(TileColor.Red, 2) : Run(TileColor.Red, 1, 2, 3);
            var state = Builder(Ids(group).Concat(new[] { Spare }).ToArray(), initial).Build();
            Reject(state, ErrorCode.OpeningTypeLocked, new OpenGroups(requested, new[] { group }));
        }

        [TestCase(OpeningKind.Series)]
        [TestCase(OpeningKind.Pairs)]
        public void ExistingOpeningDoesNotRequireThresholdAgain(OpeningKind opening)
        {
            var group = opening == OpeningKind.Series ? Run(TileColor.Red, 1, 2, 3) : Pair(TileColor.Red, 1);
            var state = Builder(Ids(group).Concat(new[] { Spare }).ToArray(), opening).Build();
            var next = Accept(state, new OpenGroups(opening, new[] { group }));
            Assert.That(next.Players[0].Opening, Is.EqualTo(opening));
            Assert.That(next.Table.Count, Is.EqualTo(1));
        }

        [TestCase(OpeningKind.None)]
        [TestCase(OpeningKind.Series)]
        [TestCase(OpeningKind.Pairs)]
        public void EmptyOpeningIsRejected(OpeningKind opening)
        {
            Reject(Builder(new[] { Spare }).Build(), ErrorCode.InvalidOpening,
                new OpenGroups(opening, Array.Empty<MeldProposal>()));
        }

        [TestCase(OpeningKind.Series)]
        [TestCase(OpeningKind.Pairs)]
        public void MixedOpeningGroupsAreRejected(OpeningKind opening)
        {
            var groups = new[] { Run(TileColor.Red, 1, 2, 3), Pair(TileColor.Blue, 2) };
            Reject(Builder(Ids(groups).Concat(new[] { Spare }).ToArray()).Build(), ErrorCode.InvalidGroup,
                new OpenGroups(opening, groups));
        }

        [Test]
        public void RepeatedPhysicalTileAcrossOpeningGroupsIsRejected()
        {
            var group = Run(TileColor.Red, 11, 12, 13);
            Reject(Builder(Ids(group).Concat(new[] { Spare }).ToArray()).Build(), ErrorCode.InvalidTile,
                new OpenGroups(OpeningKind.Series, new[] { group, group, group }));
        }

        [Test]
        public void MissingPhysicalCopyCannotBeSubstituted()
        {
            var groups = OpeningGroups(101);
            var hand = Ids(groups).ToArray();
            hand[0] = new TileId(hand[0].Value + 1);
            Reject(Builder(hand.Concat(new[] { Spare }).ToArray()).Build(), ErrorCode.InvalidTile,
                new OpenGroups(OpeningKind.Series, groups));
        }

        [Test]
        public void DeclaredJokerFaceCountsTowardOpening()
        {
            var groups = OpeningGroups(101);
            var first = groups[0].Tiles.ToArray();
            first[1] = Joker(0, TileColor.Red, 12);
            groups[0] = new MeldProposal(MeldKind.Run, first);
            var next = Accept(Builder(Ids(groups).Concat(new[] { Spare }).ToArray()).Build(),
                new OpenGroups(OpeningKind.Series, groups));
            Assert.That(next.Table[0].Group.Tiles[1].RepresentedFace, Is.EqualTo(new TileFace(TileColor.Red, 12)));
        }

        [Test]
        public void GroupIdentityStartsAboveExistingMaximumAndUsesActiveOwner()
        {
            var group = Run(TileColor.Red, 1, 2, 3);
            var state = Builder(Array.Empty<TileId>()).WithHand(2, Ids(group).Concat(new[] { Spare }).ToArray())
                .WithOpening(2, OpeningKind.Series).WithTurn(2, RoundPhase.PlanningTurn)
                .WithOpening(1, OpeningKind.Series)
                .WithTable(new Meld(3, 1, Run(TileColor.Blue, 1, 2, 3)),
                    new Meld(12, 1, Run(TileColor.Yellow, 1, 2, 3))).Build();
            var next = Accept(state, new AddGroup(group));
            Assert.That(next.Table[2].Id, Is.EqualTo(13));
            Assert.That(next.Table[2].Owner, Is.EqualTo(2));
            Assert.That(next.Players[2].Hand, Is.EqualTo(new[] { Spare }));
        }

        [TestCase(RoundPhase.AwaitingDraw)]
        [TestCase(RoundPhase.Finished)]
        [TestCase(RoundPhase.Cancelled)]
        public void OnlyPlanningPhaseAllowsActions(RoundPhase phase)
        {
            Reject(Builder(new[] { Spare }).WithTurn(0, phase).Build(), ErrorCode.WrongPhase);
        }

        [Test]
        public void EmptyPlanPreservesAllMetadataAndDrawContext()
        {
            var state = Context(Builder(new[] { Spare }, OpeningKind.Series).Build(), 1);
            var next = Accept(state);
            Assert.That(Fingerprint(next), Is.EqualTo(Fingerprint(state)));
        }

        [Test]
        public void InvalidStateAndMissingActionsAreRejected()
        {
            Assert.That(TableEvaluator.Evaluate(null, Array.Empty<TableAction>()).Error, Is.EqualTo(ErrorCode.InvalidState));
            var state = Builder(new[] { Spare }).Build();
            Assert.That(TableEvaluator.Evaluate(state, null).Error, Is.EqualTo(ErrorCode.InvalidState));
            Reject(state, ErrorCode.InvalidState, new TableAction[] { null });
            var corrupt = new RoundState(state.RoundId, 0, 1, 0, 0, 0, state.Phase, state.Indicator,
                state.Stock.Skip(1).ToArray(), state.Players, state.Table, state.Discards, null,
                OpeningKind.None, 0, true, new long[4]);
            Assert.That(TableEvaluator.Evaluate(corrupt, Array.Empty<TableAction>()).Error, Is.EqualTo(ErrorCode.InvalidState));
        }
    }

    internal static class TableTest
    {
        internal static readonly TileId Spare = Id(TileColor.Black, 12);
        internal static TileId Id(TileColor color, int number, int copy = 0)
            => new TileId(2 * (13 * (int)color + number - 1) + copy);
        internal static PlacedTile Normal(TileColor color, int number, int copy = 0)
            => new PlacedTile(Id(color, number, copy), new TileFace(color, number));
        internal static PlacedTile Joker(int copy, TileColor color, int number)
            => new PlacedTile(Id(TileColor.Black, 1, copy), new TileFace(color, number));
        internal static MeldProposal Run(TileColor color, params int[] numbers)
            => new MeldProposal(MeldKind.Run, numbers.Select(n => Normal(color, n)).ToArray());
        internal static MeldProposal Pair(TileColor color, int number)
            => new MeldProposal(MeldKind.Pair, new[] { Normal(color, number), Normal(color, number, 1) });
        internal static TileId[] Ids(params MeldProposal[] groups)
            => groups.SelectMany(g => g.Tiles.Select(t => t.Id)).ToArray();
        internal static RoundTestBuilder Builder(TileId[] hand, OpeningKind opening = OpeningKind.None)
            => new RoundTestBuilder().WithIndicator(Id(TileColor.Black, 13)).WithHand(0, hand).WithOpening(0, opening);

        // 36 + 30 + (34 or 35), without sharing physical tiles between groups.
        internal static MeldProposal[] OpeningGroups(int total) => new[]
        {
            Run(TileColor.Red, 11, 12, 13), Run(TileColor.Blue, 9, 10, 11),
            total == 101 ? Run(TileColor.Yellow, 5, 6, 7, 8, 9) : Run(TileColor.Yellow, 7, 8, 9, 10)
        };

        internal static RoundState Context(RoundState state, int used)
            => new RoundState(state.RoundId, 7, state.Seed, state.StartingPlayer, state.ActivePlayer, 3,
                state.Phase, state.Indicator, state.Stock, state.Players, state.Table, state.Discards,
                new DrawReceipt(DrawSource.Stock, state.Players[state.ActivePlayer].Hand[0], true),
                state.Players[state.ActivePlayer].Opening, used, false, new long[] { 1, 2, 3, 4 });

        internal static RoundState Accept(RoundState state, params TableAction[] actions)
        {
            var before = Fingerprint(state);
            var result = TableEvaluator.Evaluate(state, actions);
            Assert.That(Fingerprint(state), Is.EqualTo(before), "Source state changed.");
            Assert.That(result.Success, Is.True, result.Error.ToString());
            var next = result.Value;
            Assert.That(StateInvariant.Validate(next).Success, Is.True, "106 physical tiles must remain singly owned.");
            Assert.That(next.RoundId, Is.EqualTo(state.RoundId));
            Assert.That(next.Version, Is.EqualTo(state.Version));
            Assert.That(next.Seed, Is.EqualTo(state.Seed));
            Assert.That(next.StartingPlayer, Is.EqualTo(state.StartingPlayer));
            Assert.That(next.ActivePlayer, Is.EqualTo(state.ActivePlayer));
            Assert.That(next.TurnNumber, Is.EqualTo(state.TurnNumber));
            Assert.That(next.Phase, Is.EqualTo(state.Phase));
            Assert.That(next.Indicator, Is.EqualTo(state.Indicator));
            Assert.That(next.Stock, Is.EqualTo(state.Stock));
            for (var p = 0; p < 4; p++)
            {
                Assert.That(next.Discards[p], Is.EqualTo(state.Discards[p]));
                Assert.That(next.Players[p].Penalties, Is.EqualTo(state.Players[p].Penalties));
                if (p != state.ActivePlayer) Assert.That(next.Players[p], Is.SameAs(state.Players[p]));
            }
            Assert.That(next.PendingDraw, Is.SameAs(state.PendingDraw));
            Assert.That(next.OpeningAtTurnStart, Is.EqualTo(state.OpeningAtTurnStart));
            Assert.That(next.IsInitialTurn, Is.EqualTo(state.IsInitialTurn));
            Assert.That(next.LastAcceptedSequences, Is.EqualTo(state.LastAcceptedSequences));
            Assert.That(next.Result, Is.SameAs(state.Result));
            return next;
        }

        internal static void Reject(RoundState state, ErrorCode error, params TableAction[] actions)
        {
            var before = Fingerprint(state);
            var result = TableEvaluator.Evaluate(state, actions);
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(error));
            Assert.That(Fingerprint(state), Is.EqualTo(before), "Rejected plan changed physical locations, bindings or metadata.");
            Assert.That(StateInvariant.Validate(state).Success, Is.True);
        }

        internal static string Fingerprint(RoundState state)
            => string.Join("|", state.RoundId, state.Version, state.Seed, state.StartingPlayer, state.ActivePlayer,
                state.TurnNumber, state.Phase, state.Indicator, state.OpeningAtTurnStart, state.SeriesTilesUsed,
                state.IsInitialTurn, string.Join(",", state.LastAcceptedSequences),
                state.PendingDraw == null ? "no-draw" : $"{state.PendingDraw.Source}/{state.PendingDraw.Tile}/{state.PendingDraw.WasLastStock}",
                string.Join(",", state.Stock),
                string.Join(";", state.Players.Select(p => $"{p.Opening}/{p.Penalties}/" + string.Join(",", p.Hand))),
                string.Join(";", state.Discards.Select(d => string.Join(",", d))),
                string.Join(";", state.Table.Select(m => $"{m.Id}/{m.Owner}/{m.Group.Kind}/" +
                    string.Join(",", m.Group.Tiles.Select(t => $"{t.Id}:{t.RepresentedFace}")))));
    }
}

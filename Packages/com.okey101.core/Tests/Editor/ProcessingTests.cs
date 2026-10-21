using System;
using System.Linq;
using NUnit.Framework;
using Okey101.Core.Model;
using static Okey101.Core.Tests.TableTest;

namespace Okey101.Core.Tests
{
    public sealed class ProcessingTests
    {
        [Test]
        public void OpenThenProcessInSamePlan()
        {
            var groups = OpeningGroups(101);
            var fake = new PlacedTile(new TileId(104), new TileFace(TileColor.Black, 1));
            var state = Builder(Ids(groups).Concat(new[] { fake.Id, Spare }).ToArray())
                .WithOpening(1, OpeningKind.Series)
                .WithTable(new Meld(8, 1, Run(TileColor.Black, 2, 3, 4))).Build();
            var open = new OpenGroups(OpeningKind.Series, groups);
            var extend = new ExtendMeld(8, true, new[] { fake });
            var next = Accept(state, open, extend);
            Assert.That(next.Players[0].Opening, Is.EqualTo(OpeningKind.Series));
            Assert.That(next.Table[0].Group.Tiles.Select(t => t.RepresentedFace.Number), Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(next.OpeningAtTurnStart, Is.EqualTo(OpeningKind.None));
            Reject(state, ErrorCode.OpeningRequired, extend, open);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PairAreaRequiresSomeoneOpenedPairs(bool pairsOpened)
        {
            var pair = Pair(TileColor.Red, 1);
            var state = Builder(Ids(pair).Concat(new[] { Spare }).ToArray(), OpeningKind.Series)
                .WithOpening(1, pairsOpened ? OpeningKind.Pairs : OpeningKind.Series).Build();
            if (!pairsOpened) Reject(state, ErrorCode.OpeningRequired, new AddGroup(pair));
            else
            {
                var next = Accept(state, new AddGroup(pair));
                Assert.That(next.Players[0].Opening, Is.EqualTo(OpeningKind.Series));
                Assert.That(next.Table[0].Owner, Is.Zero);
            }
        }

        [Test]
        public void PairPlayerBudgetIsPerTurn()
        {
            var pair = Pair(TileColor.Yellow, 1);
            var state = Builder(new[] { Id(TileColor.Red, 4), Id(TileColor.Red, 5), Id(TileColor.Blue, 4), Spare }
                    .Concat(Ids(pair)).ToArray(), OpeningKind.Pairs)
                .WithOpening(1, OpeningKind.Series)
                .WithTable(new Meld(1, 1, Run(TileColor.Red, 1, 2, 3)),
                    new Meld(9, 1, Run(TileColor.Blue, 1, 2, 3))).Build();
            var first = new ExtendMeld(1, false, new[] { Normal(TileColor.Red, 4) });
            var second = new ExtendMeld(9, false, new[] { Normal(TileColor.Blue, 4) });
            var third = new ExtendMeld(1, false, new[] { Normal(TileColor.Red, 5) });
            var next = Accept(state, first, new AddGroup(pair), second);
            Assert.That(next.SeriesTilesUsed, Is.EqualTo(2));
            Assert.That(next.Table[2].Group.Kind, Is.EqualTo(MeldKind.Pair));
            Reject(state, ErrorCode.ProcessingLimit, first, new AddGroup(pair), second, third);
            Reject(next, ErrorCode.ProcessingLimit, third);
        }

        [TestCase(1, true)]
        [TestCase(2, false)]
        public void BudgetContinuesFromEarlierCandidate(int alreadyUsed, bool valid)
        {
            var state = Context(Builder(new[] { Id(TileColor.Red, 4), Spare }, OpeningKind.Pairs)
                .WithOpening(1, OpeningKind.Series).WithTable(new Meld(0, 1, Run(TileColor.Red, 1, 2, 3))).Build(), alreadyUsed);
            var action = new ExtendMeld(0, false, new[] { Normal(TileColor.Red, 4) });
            if (!valid) Reject(state, ErrorCode.ProcessingLimit, action);
            else Assert.That(Accept(state, action).SeriesTilesUsed, Is.EqualTo(2));
        }

        [Test]
        public void MultipleTilesConsumeMultipleBudgetUnits()
        {
            var state = Builder(new[] { Id(TileColor.Red, 4), Id(TileColor.Red, 5), Id(TileColor.Red, 6), Spare }, OpeningKind.Pairs)
                .WithOpening(1, OpeningKind.Series).WithTable(new Meld(0, 1, Run(TileColor.Red, 1, 2, 3))).Build();
            Assert.That(Accept(state, new ExtendMeld(0, false,
                new[] { Normal(TileColor.Red, 4), Normal(TileColor.Red, 5) })).SeriesTilesUsed, Is.EqualTo(2));
            Reject(state, ErrorCode.ProcessingLimit, new ExtendMeld(0, false,
                new[] { Normal(TileColor.Red, 4), Normal(TileColor.Red, 5), Normal(TileColor.Red, 6) }));
        }

        [Test]
        public void SeriesPlayerHasNoNumericProcessingLimit()
        {
            var state = Builder(new[] { Id(TileColor.Red, 4), Id(TileColor.Red, 5), Id(TileColor.Red, 6), Id(TileColor.Blue, 4), Spare }, OpeningKind.Series)
                .WithOpening(1, OpeningKind.Series).WithTable(new Meld(0, 1, Run(TileColor.Red, 1, 2, 3)),
                    new Meld(1, 0, Run(TileColor.Blue, 1, 2, 3))).Build();
            var next = Accept(state, new ExtendMeld(0, false,
                new[] { Normal(TileColor.Red, 4), Normal(TileColor.Red, 5), Normal(TileColor.Red, 6) }),
                new ExtendMeld(1, false, new[] { Normal(TileColor.Blue, 4) }));
            Assert.That(next.SeriesTilesUsed, Is.EqualTo(4));
            Assert.That(next.Players[0].Hand, Is.EqualTo(new[] { Spare }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RunEndpointsIgnoreSelectionOrder(bool prepend)
        {
            var group = prepend ? Run(TileColor.Red, 5, 3, 4) : Run(TileColor.Red, 3, 1, 2);
            var tiles = prepend ? new[] { Normal(TileColor.Red, 2), Normal(TileColor.Red, 1) }
                : new[] { Normal(TileColor.Red, 5), Normal(TileColor.Red, 4) };
            var state = Builder(tiles.Select(t => t.Id).Concat(new[] { Spare }).ToArray(), OpeningKind.Series)
                .WithTable(new Meld(0, 0, group)).Build();
            var next = Accept(state, new ExtendMeld(0, prepend, tiles));
            Assert.That(next.Table[0].Group.Tiles.Select(t => t.RepresentedFace.Number), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
        }

        [TestCase(true, 6)]
        [TestCase(false, 2)]
        [TestCase(false, 7)]
        public void WrongSideAndGapsAreRejected(bool prepend, int number)
        {
            var state = Builder(new[] { Id(TileColor.Red, number), Spare }, OpeningKind.Series)
                .WithTable(new Meld(0, 0, Run(TileColor.Red, 3, 4, 5))).Build();
            Reject(state, ErrorCode.InvalidGroup, new ExtendMeld(0, prepend, new[] { Normal(TileColor.Red, number) }));
        }

        [Test]
        public void SetCanGainOnlyMissingColor()
        {
            var group = new MeldProposal(MeldKind.Set, new[] { Normal(TileColor.Red, 7), Normal(TileColor.Blue, 7), Normal(TileColor.Yellow, 7) });
            var state = Builder(new[] { Id(TileColor.Black, 7), Id(TileColor.Red, 7, 1), Spare }, OpeningKind.Series)
                .WithTable(new Meld(0, 0, group)).Build();
            var next = Accept(state, new ExtendMeld(0, false, new[] { Normal(TileColor.Black, 7) }));
            Assert.That(next.Table[0].Group.Tiles.Count, Is.EqualTo(4));
            Reject(state, ErrorCode.InvalidGroup, new ExtendMeld(0, false, new[] { Normal(TileColor.Red, 7, 1) }));
        }

        [Test]
        public void PairCannotBeExtendedAndEmptyExtensionIsRejected()
        {
            var state = Builder(new[] { Spare }, OpeningKind.Pairs)
                .WithTable(new Meld(0, 0, Pair(TileColor.Red, 1))).Build();
            Reject(state, ErrorCode.InvalidGroup, new ExtendMeld(0, false, new[] { Normal(TileColor.Black, 12) }));
            Reject(state, ErrorCode.InvalidGroup, new ExtendMeld(0, false, Array.Empty<PlacedTile>()));
        }

        [Test]
        public void AddingNewGroupsRespectsOpeningType()
        {
            var run = Run(TileColor.Red, 1, 2, 3);
            var pair = Pair(TileColor.Blue, 1);
            var hand = Ids(run, pair).Concat(new[] { Spare }).ToArray();
            var unopened = Builder(hand).Build();
            Reject(unopened, ErrorCode.OpeningRequired, new AddGroup(run));
            var pairs = Builder(hand, OpeningKind.Pairs).Build();
            Reject(pairs, ErrorCode.OpeningTypeLocked, new AddGroup(run));
            Assert.That(Accept(pairs, new AddGroup(pair)).SeriesTilesUsed, Is.Zero);
            Assert.That(Accept(Builder(hand, OpeningKind.Series).Build(), new AddGroup(run)).Table.Count, Is.EqualTo(1));
        }

        [Test]
        public void ExtensionCannotUseForeignOrRepeatedPhysicalTile()
        {
            var state = Builder(new[] { Id(TileColor.Red, 4), Spare }, OpeningKind.Series)
                .WithHand(1, Id(TileColor.Red, 5)).WithTable(new Meld(0, 0, Run(TileColor.Red, 1, 2, 3))).Build();
            Reject(state, ErrorCode.InvalidTile, new ExtendMeld(0, false, new[] { Normal(TileColor.Red, 5) }));
            Reject(state, ErrorCode.InvalidTile, new ExtendMeld(0, false, new[] { Normal(TileColor.Red, 4), Normal(TileColor.Red, 4) }));
        }

        [Test]
        public void LaterFailureRollsBackOpeningAndAllocatedGroupIds()
        {
            var groups = OpeningGroups(101);
            var state = Builder(Ids(groups).Concat(new[] { Spare }).ToArray()).Build();
            Reject(state, ErrorCode.InvalidGroup, new OpenGroups(OpeningKind.Series, groups),
                new ExtendMeld(99, false, new[] { Normal(TileColor.Black, 12) }));
            Assert.That(Accept(state, new OpenGroups(OpeningKind.Series, groups)).Table[0].Id, Is.Zero);
        }

        [Test]
        public void GroupIdExhaustionIsRejectedWithoutOverflow()
        {
            var group = Run(TileColor.Red, 1, 2, 3);
            var state = Builder(Ids(group).Concat(new[] { Spare }).ToArray(), OpeningKind.Series)
                .WithTable(new Meld(int.MaxValue, 0, Run(TileColor.Blue, 1, 2, 3))).Build();
            Reject(state, ErrorCode.InvalidState, new AddGroup(group));
        }

        [TestCase(OpeningKind.Series)]
        [TestCase(OpeningKind.Pairs)]
        public void NewPairsDoNotConsumeSeriesBudget(OpeningKind opening)
        {
            var groups = new[] { Pair(TileColor.Red, 1), Pair(TileColor.Blue, 2), Pair(TileColor.Yellow, 3) };
            var state = Context(Builder(Ids(groups).Concat(new[] { Spare }).ToArray(), opening)
                .WithOpening(1, OpeningKind.Pairs).Build(), 2);
            var next = Accept(state, groups.Select(g => (TableAction)new AddGroup(g)).ToArray());
            Assert.That(next.SeriesTilesUsed, Is.EqualTo(2));
            Assert.That(next.Players[0].Opening, Is.EqualTo(opening));
            Assert.That(next.Table.Select(m => m.Id), Is.EqualTo(new[] { 0, 1, 2 }));
        }

        [Test]
        public void InvalidRepresentedFaceCannotEnterTable()
        {
            var forged = new PlacedTile(Id(TileColor.Blue, 4), new TileFace(TileColor.Red, 4));
            var state = Builder(new[] { forged.Id, Spare }, OpeningKind.Series)
                .WithTable(new Meld(0, 0, Run(TileColor.Red, 1, 2, 3))).Build();
            Reject(state, ErrorCode.InvalidTile, new ExtendMeld(0, false, new[] { forged }));
        }

        [Test]
        public void InvalidExistingGroupCannotBeUsedAsBase()
        {
            var state = Builder(new[] { Spare }, OpeningKind.Series)
                .WithTable(new Meld(0, 0, Run(TileColor.Red, 1, 2, 4))).Build();
            Reject(state, ErrorCode.InvalidState);
        }
    }
}
